using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ACT MapGraph 生成器。
/// 先生成每层稳定递增的 lane 映射，再按 ActDefinition 抽取节点类型，最后由独立 Validator
/// 复验所有拓扑、配额和内容约束。生成算法不认识具体事件、敌人或层号语义。
/// </summary>
public sealed class MapGraphGenerator
{
    public const int MaxGenerationAttempts = 128;

    private readonly MapGraphValidator _validator = new();

    public bool TryGenerate(ulong runSeed, ActDefinition act, out MapGraph graph,
        out MapGraphGenerationReport report, out string error)
    {
        graph = null;
        report = null;
        error = "";
        if (act == null || act.Layers == null || act.LayerNodeCounts == null ||
            act.Layers.Count != act.LayerNodeCounts.Count)
        {
            error = "MapGraph 缺少完整 ActDefinition 层规则。";
            return false;
        }

        var streams = new StableRandomStreams(runSeed, act.ActId, act.RuleVersion);
        MapGraphValidationReport lastValidation = null;
        string lastBuildError = "";
        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            if (!TryBuildCandidate(act, streams.CreateMapStream(attempt), streams,
                    out var candidate, out lastBuildError))
                continue;

            var validation = _validator.Validate(candidate, act);
            if (validation.IsValid)
            {
                graph = candidate;
                report = new MapGraphGenerationReport
                {
                    Attempts = attempt + 1,
                    PathCount = validation.PathCount,
                    ReachableShopCount = validation.ReachableShopCount,
                    Fingerprint = candidate.Fingerprint,
                    ValidationErrors = validation.Errors,
                };
                return true;
            }

            lastValidation = validation;
        }

        error = $"MapGraph 生成达到 {MaxGenerationAttempts} 次上限，runSeed={runSeed}，规则={act.RuleVersion}，" +
            $"构建失败={lastBuildError}，最后校验失败：{string.Join("；", lastValidation?.Errors ?? new List<string>())}";
        report = new MapGraphGenerationReport
        {
            Attempts = MaxGenerationAttempts,
            PathCount = lastValidation?.PathCount ?? 0,
            ReachableShopCount = lastValidation?.ReachableShopCount ?? 0,
            Fingerprint = "",
            ValidationErrors = lastValidation?.Errors ?? new List<string>(),
        };
        return false;
    }

    private static bool TryBuildCandidate(ActDefinition act, StableRandom rng,
        StableRandomStreams streams, out MapGraph graph, out string error)
    {
        graph = null;
        error = "";
        if (act.RouteLaneCount < 1)
        {
            error = "ActDefinition.RouteLaneCount 必须大于0。";
            return false;
        }

        var laneAssignments = BuildMonotonicLaneAssignments(act, rng);
        if (laneAssignments == null)
        {
            error = "无法为全部层生成稳定单调 lane 分配。";
            return false;
        }

        if (!TryAssignNodeTypes(act, rng, laneAssignments, out var typeAssignments, out error))
            return false;

        var layers = new List<List<MapNodeDefinition>>();
        for (int layerIndex = 0; layerIndex < act.Layers.Count; layerIndex++)
        {
            var layerDefinition = act.Layers[layerIndex];
            var layer = new List<MapNodeDefinition>();
            for (int stableOrder = 0; stableOrder < layerDefinition.NodeCount; stableOrder++)
            {
                string nodeId = $"{act.ActId}_l{layerIndex}_n{stableOrder}";
                var nodeType = typeAssignments[layerIndex][stableOrder];
                var nodeRng = streams.CreateNodeStream(RandomStreamKey.Map, nodeId, 1);
                if (!MapNodeContentCatalog.TryResolve(nodeType, layerDefinition.BattleTier,
                        nodeRng, out var content, out error))
                    return false;

                layer.Add(new MapNodeDefinition
                {
                    NodeId = nodeId,
                    LayerIndex = layerIndex,
                    IndexInLayer = stableOrder,
                    LayerOrder = stableOrder,
                    StableOrder = stableOrder,
                    NodeType = nodeType,
                    Tier = nodeType == MapGraphNodeType.Battle || nodeType == MapGraphNodeType.Boss
                        ? layerDefinition.BattleTier
                        : EncounterTier.Weak,
                    PoolId = content.PoolId,
                    NodeSeed = streams.DeriveSeed(RandomStreamKey.Map, nodeId, 0),
                    ContentId = content.ContentId,
                    DisplayName = content.DisplayName,
                });
            }
            layers.Add(layer);
        }

        var edges = new List<MapGraphEdge>();
        var edgeKeys = new HashSet<string>();
        for (int layerIndex = 0; layerIndex < layers.Count - 1; layerIndex++)
        {
            for (int lane = 0; lane < act.RouteLaneCount; lane++)
            {
                var from = layers[layerIndex][laneAssignments[layerIndex][lane]];
                var to = layers[layerIndex + 1][laneAssignments[layerIndex + 1][lane]];
                string key = $"{from.NodeId}>{to.NodeId}";
                if (edgeKeys.Add(key))
                    edges.Add(new MapGraphEdge { FromNodeId = from.NodeId, ToNodeId = to.NodeId });
            }
        }

        graph = new MapGraph(layers, edges, act.ActId, act.RuleVersion);
        return true;
    }

    private static List<int[]> BuildMonotonicLaneAssignments(ActDefinition act, StableRandom rng)
    {
        var result = new List<int[]>();
        foreach (var layer in act.Layers)
        {
            if (layer.NodeCount < 1 || layer.NodeCount > act.RouteLaneCount)
                return null;

            var bucketSizes = new int[layer.NodeCount];
            for (int i = 0; i < bucketSizes.Length; i++)
                bucketSizes[i] = 1;
            for (int remaining = act.RouteLaneCount - layer.NodeCount; remaining > 0; remaining--)
                bucketSizes[rng.NextInt(0, bucketSizes.Length)]++;

            var assignment = new int[act.RouteLaneCount];
            int lane = 0;
            for (int nodeIndex = 0; nodeIndex < bucketSizes.Length; nodeIndex++)
            for (int count = 0; count < bucketSizes[nodeIndex]; count++)
                assignment[lane++] = nodeIndex;
            result.Add(assignment);
        }
        return result;
    }

    private static bool TryAssignNodeTypes(ActDefinition act, StableRandom rng,
        List<int[]> laneAssignments,
        out List<List<MapGraphNodeType>> assignments, out string error)
    {
        assignments = new List<List<MapGraphNodeType>>();
        error = "";
        var graphCounts = new Dictionary<MapGraphNodeType, int>();

        foreach (var layer in act.Layers)
        {
            var window = GetWindow(act, layer);
            if (window == null)
            {
                error = $"L{layer.LayerIndex} 没有匹配 NodeTypeWindow。";
                return false;
            }

            var allowed = new List<MapGraphNodeType>(layer.AllowedNodeTypes ?? window.AllowedNodeTypes);
            if (allowed.Count == 0)
            {
                error = $"L{layer.LayerIndex} 没有允许的节点类型。";
                return false;
            }

            var types = new List<MapGraphNodeType>();
            var layerCounts = new Dictionary<MapGraphNodeType, int>();
            foreach (var required in layer.RequiredNodeTypes ?? new List<MapGraphNodeType>())
            {
                if (!allowed.Contains(required))
                {
                    error = $"L{layer.LayerIndex} 的 RequiredNodeType 不在允许窗口中：{required}";
                    return false;
                }
                types.Add(required);
                layerCounts[required] = layerCounts.GetValueOrDefault(required) + 1;
            }

            while (types.Count < layer.NodeCount)
            {
                var type = PickWeightedType(allowed, layer.NodeTypeWeights, graphCounts, layerCounts, window, rng);
                if (type == null)
                {
                    error = $"L{layer.LayerIndex} 在上限约束下无法抽取节点类型。";
                    return false;
                }
                types.Add(type.Value);
                layerCounts[type.Value] = layerCounts.GetValueOrDefault(type.Value) + 1;
            }

            int minimumDistinct = window.MinDistinctTypesPerLayer;
            while (DistinctCount(types) < minimumDistinct)
            {
                MapGraphNodeType? missing = FindMissingType(allowed, types);
                if (missing == null)
                {
                    error = $"L{layer.LayerIndex} 无法满足最少节点类型数：{minimumDistinct}";
                    return false;
                }
                int replacement = FindReplaceableIndex(types, missing.Value, window);
                if (replacement < 0)
                {
                    error = $"L{layer.LayerIndex} 无法修复节点类型多样性。";
                    return false;
                }
                types[replacement] = missing.Value;
            }

            int combatCount = 0;
            foreach (var type in types)
                if (type == MapGraphNodeType.Battle || type == MapGraphNodeType.Boss)
                    combatCount++;
            while (combatCount < window.MinCombatNodesPerLayer)
            {
                int replacement = FindNonCombatIndex(types);
                if (replacement < 0)
                {
                    error = $"L{layer.LayerIndex} 无法满足最少战斗节点数：{window.MinCombatNodesPerLayer}";
                    return false;
                }
                types[replacement] = MapGraphNodeType.Battle;
                combatCount++;
            }

            if (!WithinPerLayerCaps(types, window))
            {
                error = $"L{layer.LayerIndex} 超出 NodeTypeWindow 层内上限。";
                return false;
            }

            foreach (var type in types)
                graphCounts[type] = graphCounts.GetValueOrDefault(type) + 1;
            assignments.Add(types);
        }

        if (!RepairPathCoverage(act, laneAssignments, assignments, out error))
            return false;

        foreach (var window in act.NodeTypeWindows)
        foreach (var cap in window.GraphCaps)
        {
            if (graphCounts.GetValueOrDefault(cap.Key) > cap.Value)
            {
                error = $"节点类型超出图级上限：{cap.Key}";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 用实际 lane 拓扑修复逐路径 Strong/Lingmai 覆盖。
    /// 只有在不破坏窗口最少类型、战斗数和层上限时才替换节点，失败则让外层受控重试。
    /// </summary>
    private static bool RepairPathCoverage(ActDefinition act, List<int[]> laneAssignments,
        List<List<MapGraphNodeType>> assignments, out string error)
    {
        error = "";
        var paths = EnumeratePaths(laneAssignments, act.RouteLaneCount);
        foreach (var path in paths)
        {
            if (act.MinStrongPerPath > 0 && !PathHasStrong(act, assignments, path))
            {
                if (!TryReplacePathNode(act, assignments, path, MapGraphNodeType.Battle))
                {
                    error = "无法在稳定拓扑上补齐逐路径 Strong 覆盖。";
                    return false;
                }
            }
        }

        foreach (var path in paths)
        {
            if (act.MinLingmaiPerPath > 0 && !PathHasType(assignments, path, MapGraphNodeType.Lingmai))
            {
                if (!TryReplacePathNode(act, assignments, path, MapGraphNodeType.Lingmai))
                {
                    error = "无法在稳定拓扑上补齐逐路径 Lingmai 覆盖。";
                    return false;
                }
            }
        }

        if (act.RequiredShopPathFromLayer >= 0)
        {
            var requiredLayerNodes = new HashSet<int>();
            foreach (var path in paths)
                requiredLayerNodes.Add(path[act.RequiredShopPathFromLayer]);

            foreach (int requiredNode in requiredLayerNodes)
            {
                bool hasShopPath = paths.Any(path => path[act.RequiredShopPathFromLayer] == requiredNode &&
                    PathHasType(assignments, path, MapGraphNodeType.Shop));
                if (hasShopPath)
                    continue;

                var repairPath = paths.First(path => path[act.RequiredShopPathFromLayer] == requiredNode);
                if (!TryReplacePathNode(act, assignments, repairPath, MapGraphNodeType.Shop))
                {
                    error = "无法为每个要求层节点补齐商店可达路径。";
                    return false;
                }
            }
        }
        return true;
    }

    private static List<List<int>> EnumeratePaths(List<int[]> laneAssignments, int laneCount)
    {
        var paths = new List<List<int>>();
        var current = new List<int> { 0 };
        EnumeratePathsAtLayer(laneAssignments, laneCount, 0, current, paths);
        return paths;
    }

    private static void EnumeratePathsAtLayer(List<int[]> laneAssignments, int laneCount,
        int layerIndex, List<int> current, List<List<int>> paths)
    {
        if (layerIndex >= laneAssignments.Count - 1)
        {
            paths.Add(new List<int>(current));
            return;
        }

        int source = current[^1];
        var targets = new HashSet<int>();
        for (int lane = 0; lane < laneCount; lane++)
            if (laneAssignments[layerIndex][lane] == source)
                targets.Add(laneAssignments[layerIndex + 1][lane]);

        foreach (int target in targets.OrderBy(value => value))
        {
            current.Add(target);
            EnumeratePathsAtLayer(laneAssignments, laneCount, layerIndex + 1, current, paths);
            current.RemoveAt(current.Count - 1);
        }
    }

    private static bool PathHasStrong(ActDefinition act, List<List<MapGraphNodeType>> assignments,
        IReadOnlyList<int> path)
    {
        for (int layer = 0; layer < path.Count; layer++)
            if (assignments[layer][path[layer]] == MapGraphNodeType.Battle &&
                act.Layers[layer].BattleTier == EncounterTier.Strong)
                return true;
        return false;
    }

    private static bool PathHasType(List<List<MapGraphNodeType>> assignments,
        IReadOnlyList<int> path, MapGraphNodeType target)
    {
        for (int layer = 0; layer < path.Count; layer++)
            if (assignments[layer][path[layer]] == target)
                return true;
        return false;
    }

    private static bool TryReplacePathNode(ActDefinition act, List<List<MapGraphNodeType>> assignments,
        IReadOnlyList<int> path, MapGraphNodeType replacement)
    {
        for (int layerIndex = path.Count - 1; layerIndex >= 0; layerIndex--)
        {
            var layer = act.Layers[layerIndex];
            if (!layer.AllowedNodeTypes.Contains(replacement))
                continue;

            int nodeIndex = path[layerIndex];
            var previous = assignments[layerIndex][nodeIndex];
            if (previous == replacement)
                return true;

            assignments[layerIndex][nodeIndex] = replacement;
            var window = GetWindow(act, layer);
            bool valid = WithinPerLayerCaps(assignments[layerIndex], window) &&
                DistinctCount(assignments[layerIndex]) >= window.MinDistinctTypesPerLayer &&
                CountCombat(assignments[layerIndex]) >= window.MinCombatNodesPerLayer;
            if (valid)
                return true;

            assignments[layerIndex][nodeIndex] = previous;
        }
        return false;
    }

    private static int CountCombat(IReadOnlyList<MapGraphNodeType> types)
    {
        return types.Count(type => type == MapGraphNodeType.Battle || type == MapGraphNodeType.Boss);
    }

    private static NodeTypeWindowDefinition GetWindow(ActDefinition act, ActLayerDefinition layer)
    {
        foreach (var windowId in layer.WindowIds ?? new List<string>())
        foreach (var window in act.NodeTypeWindows)
            if (window.WindowId == windowId && layer.LayerIndex >= window.MinLayer && layer.LayerIndex <= window.MaxLayer)
                return window;
        return null;
    }

    private static MapGraphNodeType? PickWeightedType(IReadOnlyList<MapGraphNodeType> allowed,
        IReadOnlyDictionary<MapGraphNodeType, int> weights,
        IReadOnlyDictionary<MapGraphNodeType, int> graphCounts,
        IReadOnlyDictionary<MapGraphNodeType, int> layerCounts,
        NodeTypeWindowDefinition window, StableRandom rng)
    {
        int total = 0;
        var available = new List<MapGraphNodeType>();
        foreach (var type in allowed)
        {
            int cap = window.GraphCaps.GetValueOrDefault(type, int.MaxValue);
            if (graphCounts.GetValueOrDefault(type) >= cap)
                continue;
            int layerCap = window.PerLayerCaps.GetValueOrDefault(type, int.MaxValue);
            if (layerCounts.GetValueOrDefault(type) >= layerCap)
                continue;
            int weight = weights?.GetValueOrDefault(type) ?? 1;
            if (weight > 0)
            {
                available.Add(type);
                total += weight;
            }
        }
        if (available.Count == 0 || total <= 0)
            return null;

        int roll = rng.NextInt(0, total);
        foreach (var type in available)
        {
            roll -= weights?.GetValueOrDefault(type) ?? 1;
            if (roll < 0)
                return type;
        }
        return null;
    }

    private static int DistinctCount(List<MapGraphNodeType> types)
    {
        var values = new HashSet<MapGraphNodeType>(types);
        return values.Count;
    }

    private static MapGraphNodeType? FindMissingType(IReadOnlyList<MapGraphNodeType> allowed,
        IReadOnlyList<MapGraphNodeType> types)
    {
        foreach (var type in allowed)
            if (!types.Contains(type))
                return type;
        return null;
    }

    private static int FindReplaceableIndex(IReadOnlyList<MapGraphNodeType> types,
        MapGraphNodeType replacement, NodeTypeWindowDefinition window)
    {
        for (int i = types.Count - 1; i >= 0; i--)
        {
            if (types[i] == replacement)
                continue;
            int sameCount = 0;
            foreach (var type in types)
                if (type == types[i]) sameCount++;
            if (sameCount > 1)
                return i;
        }
        return -1;
    }

    private static int FindNonCombatIndex(IReadOnlyList<MapGraphNodeType> types)
    {
        for (int i = types.Count - 1; i >= 0; i--)
            if (types[i] != MapGraphNodeType.Battle && types[i] != MapGraphNodeType.Boss)
                return i;
        return -1;
    }

    private static bool WithinPerLayerCaps(IReadOnlyList<MapGraphNodeType> types, NodeTypeWindowDefinition window)
    {
        foreach (var cap in window.PerLayerCaps)
        {
            int count = 0;
            foreach (var type in types)
                if (type == cap.Key) count++;
            if (count > cap.Value)
                return false;
        }
        return true;
    }
}
