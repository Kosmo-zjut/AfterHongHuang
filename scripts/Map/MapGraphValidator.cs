using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 独立验证 MapGraph 的结构、稳定顺序、层窗口、无交叉和路线配额。
/// Validator 不信任生成器内部状态，所有生产图和非法 fixture 都从同一入口复验。
/// </summary>
public sealed class MapGraphValidator
{
    public MapGraphValidationReport Validate(MapGraph graph, ActDefinition act)
    {
        var report = new MapGraphValidationReport();
        if (graph == null || act == null)
        {
            report.Errors.Add("MapGraph 或 ActDefinition 为空。");
            return report;
        }

        if (graph.ActId != act.ActId || graph.RuleVersion != act.RuleVersion)
            report.Errors.Add("MapGraph 的 ActId/RuleVersion 与 ActDefinition 不一致。");
        if (graph.Layers.Count != act.Layers.Count)
        {
            report.Errors.Add("MapGraph 层数不符合 ActDefinition。");
            return report;
        }

        var nodes = graph.AllNodes().ToList();
        var duplicateNodeGroups = nodes.GroupBy(node => node.NodeId)
            .Where(group => group.Count() > 1).ToList();
        foreach (var duplicate in duplicateNodeGroups)
            report.Errors.Add($"节点 ID 重复：{duplicate.Key}（{duplicate.Count()} 个）");
        if (duplicateNodeGroups.Count > 0)
            return report;

        var nodeById = nodes.ToDictionary(node => node.NodeId);
        for (int layerIndex = 0; layerIndex < graph.Layers.Count; layerIndex++)
        {
            var layer = graph.Layers[layerIndex];
            var layerDefinition = act.Layers[layerIndex];
            if (layer.Count != layerDefinition.NodeCount)
                report.Errors.Add($"L{layerIndex} 节点数错误：{layer.Count}");

            var previousStableOrder = int.MinValue;
            for (int index = 0; index < layer.Count; index++)
            {
                var node = layer[index];
                if (node == null)
                {
                    report.Errors.Add($"节点缺少玩家显示名：{node?.NodeId ?? "<missing-node>"}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.DisplayName))
                    report.Errors.Add($"节点缺少玩家显示名：{node.NodeId}");
                if (node.LayerIndex != layerIndex || node.LayerOrder != node.IndexInLayer)
                    report.Errors.Add($"节点层内顺序字段不一致：{node.NodeId}");
                if (node.LayerOrder != index)
                    report.Errors.Add($"节点 LayerOrder 不稳定：{node.NodeId}");
                if (node.StableOrder <= previousStableOrder)
                    report.Errors.Add($"节点 StableOrder 未严格递增：{node.NodeId}");
                previousStableOrder = node.StableOrder;
            }

            ValidateLayerTypes(layer, layerDefinition, act, layerIndex, report);
        }

        foreach (var cap in GetGraphCaps(act))
        {
            int count = nodes.Count(node => node.NodeType == cap.Key);
            if (count > cap.Value)
                report.Errors.Add($"节点类型超出图级上限：{cap.Key}={count}>{cap.Value}");
        }

        var duplicateEdges = graph.Edges
            .GroupBy(edge => $"{edge.FromNodeId}>{edge.ToNodeId}")
            .Where(group => group.Count() > 1).ToList();
        foreach (var duplicate in duplicateEdges)
            report.Errors.Add($"地图边重复：{duplicate.Key}（{duplicate.Count()} 条）");

        var outgoing = graph.Edges.GroupBy(edge => edge.FromNodeId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var incoming = graph.Edges.GroupBy(edge => edge.ToNodeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var edge in graph.Edges)
        {
            if (!nodeById.TryGetValue(edge.FromNodeId, out var from) ||
                !nodeById.TryGetValue(edge.ToNodeId, out var to))
            {
                report.Errors.Add("边引用了不存在的节点。");
                continue;
            }

            if (to.LayerIndex != from.LayerIndex + 1)
                report.Errors.Add($"存在非相邻层边：{edge.FromNodeId}->{edge.ToNodeId}");
            if (IsForbiddenSameTypeEdge(from.NodeType, to.NodeType, act.AdjacencyRules))
                report.Errors.Add($"同类非战斗节点沿边连续：{edge.FromNodeId}->{edge.ToNodeId}");
        }

        ValidateStableOrderEdges(graph, nodeById, report);
        if (graph.Layers.Count == 0 || graph.Layers[0].Count == 0)
        {
            report.Errors.Add("MapGraph 缺少起点层。");
            return report;
        }

        var start = graph.Layers[0][0];
        var boss = graph.Layers[^1].FirstOrDefault();
        if (boss == null)
        {
            report.Errors.Add("MapGraph 缺少终点节点。");
            return report;
        }

        var reachableFromStart = Traverse(start.NodeId, outgoing);
        var canReachBoss = Traverse(boss.NodeId, incoming, reverse: true);
        foreach (var node in nodes)
        {
            if (!reachableFromStart.Contains(node.NodeId) || !canReachBoss.Contains(node.NodeId))
                report.Errors.Add($"节点不在起点到 Boss 的有效路径上：{node.NodeId}");
            if (node.NodeType != MapGraphNodeType.Boss && !outgoing.ContainsKey(node.NodeId))
                report.Errors.Add($"非 Boss 节点没有出边：{node.NodeId}");
            if (node.NodeType != MapGraphNodeType.Start && !incoming.ContainsKey(node.NodeId))
                report.Errors.Add($"非起点节点没有入边：{node.NodeId}");
        }

        report.PathCount = CountPaths(start.NodeId, boss.NodeId, graph, outgoing);
        if (report.PathCount < act.MinCompletePaths || report.PathCount > act.MaxCompletePaths)
            report.Errors.Add($"完整路径数不在 {act.MinCompletePaths}~{act.MaxCompletePaths}：{report.PathCount}");

        report.ReachableShopCount = nodes.Count(node => node.NodeType == MapGraphNodeType.Shop &&
            reachableFromStart.Contains(node.NodeId) && canReachBoss.Contains(node.NodeId));
        if (report.ReachableShopCount < act.MinReachableShops || report.ReachableShopCount > act.MaxReachableShops)
            report.Errors.Add($"可达商店数不在 {act.MinReachableShops}~{act.MaxReachableShops}：{report.ReachableShopCount}");

        var quota = BuildPathQuotaProfiles(start.NodeId, boss.NodeId, graph, outgoing, act);
        report.NoShopPathCount = quota.NoShopPathCount;
        if (quota.InvalidPathCount > 0)
            report.Errors.Add($"存在不满足逐路径配额的路径：{quota.InvalidPathCount} 条");
        if (act.RequireNoShopPath && report.NoShopPathCount <= 0)
            report.Errors.Add("不存在绕开全部商店的有效路径。");

        if (act.RequiredShopPathFromLayer >= 0)
        {
            foreach (var node in graph.Layers[act.RequiredShopPathFromLayer])
            {
                if (!HasShopPathToBoss(node.NodeId, graph, outgoing, canReachBoss))
                    report.Errors.Add($"L{act.RequiredShopPathFromLayer} 节点不存在经过商店到 Boss 的路径：{node.NodeId}");
            }
        }
        return report;
    }

    private static void ValidateLayerTypes(IReadOnlyList<MapNodeDefinition> layer,
        ActLayerDefinition layerDefinition, ActDefinition act, int layerIndex,
        MapGraphValidationReport report)
    {
        var window = GetWindow(act, layerDefinition);
        if (window == null)
        {
            report.Errors.Add($"L{layerIndex} 没有匹配 NodeTypeWindow。");
            return;
        }

        var allowed = new HashSet<MapGraphNodeType>(layerDefinition.AllowedNodeTypes ?? window.AllowedNodeTypes);
        foreach (var node in layer)
        {
            if (!allowed.Contains(node.NodeType))
                report.Errors.Add($"节点类型不在 L{layerIndex} 窗口中：{node.NodeId}/{node.NodeType}");
            if ((node.NodeType == MapGraphNodeType.Battle || node.NodeType == MapGraphNodeType.Boss) &&
                node.NodeType != MapGraphNodeType.Boss && node.Tier != layerDefinition.BattleTier)
                report.Errors.Add($"战斗节点层级不符合 ActDefinition：{node.NodeId}");
        }

        int distinct = layer.Select(node => node.NodeType).Distinct().Count();
        if (distinct < window.MinDistinctTypesPerLayer)
            report.Errors.Add($"L{layerIndex} 节点类型不足：{distinct}<{window.MinDistinctTypesPerLayer}");

        int combatCount = layer.Count(node => node.NodeType == MapGraphNodeType.Battle ||
            node.NodeType == MapGraphNodeType.Boss);
        if (combatCount < window.MinCombatNodesPerLayer)
            report.Errors.Add($"L{layerIndex} 战斗节点不足：{combatCount}<{window.MinCombatNodesPerLayer}");

        foreach (var required in layerDefinition.RequiredNodeTypes ?? new List<MapGraphNodeType>())
            if (!layer.Any(node => node.NodeType == required))
                report.Errors.Add($"L{layerIndex} 缺少 RequiredNodeType：{required}");

        foreach (var cap in window.PerLayerCaps)
        {
            int count = layer.Count(node => node.NodeType == cap.Key);
            if (count > cap.Value)
                report.Errors.Add($"L{layerIndex} 节点类型超出层上限：{cap.Key}={count}>{cap.Value}");
        }
    }

    private static NodeTypeWindowDefinition GetWindow(ActDefinition act, ActLayerDefinition layer)
    {
        foreach (var windowId in layer.WindowIds ?? new List<string>())
        foreach (var window in act.NodeTypeWindows)
            if (window.WindowId == windowId && layer.LayerIndex >= window.MinLayer && layer.LayerIndex <= window.MaxLayer)
                return window;
        return null;
    }

    private static Dictionary<MapGraphNodeType, int> GetGraphCaps(ActDefinition act)
    {
        var caps = new Dictionary<MapGraphNodeType, int>();
        foreach (var window in act.NodeTypeWindows)
        foreach (var cap in window.GraphCaps)
        {
            if (!caps.TryGetValue(cap.Key, out var current) || cap.Value < current)
                caps[cap.Key] = cap.Value;
        }
        return caps;
    }

    private static void ValidateStableOrderEdges(MapGraph graph,
        IReadOnlyDictionary<string, MapNodeDefinition> nodeById, MapGraphValidationReport report)
    {
        for (int i = 0; i < graph.Edges.Count; i++)
        {
            var first = graph.Edges[i];
            if (!nodeById.TryGetValue(first.FromNodeId, out var firstFrom) ||
                !nodeById.TryGetValue(first.ToNodeId, out var firstTo))
                continue;
            for (int j = i + 1; j < graph.Edges.Count; j++)
            {
                var second = graph.Edges[j];
                if (!nodeById.TryGetValue(second.FromNodeId, out var secondFrom) ||
                    !nodeById.TryGetValue(second.ToNodeId, out var secondTo) ||
                    firstFrom.LayerIndex != secondFrom.LayerIndex ||
                    firstFrom.NodeId == secondFrom.NodeId)
                    continue;

                if (firstFrom.StableOrder < secondFrom.StableOrder &&
                    firstTo.StableOrder > secondTo.StableOrder)
                {
                    report.CrossingCount++;
                    report.Errors.Add($"稳定顺序边反转/交叉：{first.FromNodeId}->{first.ToNodeId} 与 {second.FromNodeId}->{second.ToNodeId}");
                }
                if (secondFrom.StableOrder < firstFrom.StableOrder &&
                    secondTo.StableOrder > firstTo.StableOrder)
                {
                    report.CrossingCount++;
                    report.Errors.Add($"稳定顺序边反转/交叉：{second.FromNodeId}->{second.ToNodeId} 与 {first.FromNodeId}->{first.ToNodeId}");
                }
            }
        }
    }

    private static bool IsForbiddenSameTypeEdge(MapGraphNodeType from, MapGraphNodeType to,
        IReadOnlyList<NodeAdjacencyRule> rules)
    {
        if (from != to || rules == null)
            return false;
        foreach (var rule in rules)
            if (rule.AppliesTo.Contains(from))
                return true;
        return false;
    }

    private static HashSet<string> Traverse(string start, Dictionary<string, List<MapGraphEdge>> adjacency,
        bool reverse = false)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!visited.Add(current) || !adjacency.TryGetValue(current, out var edges))
                continue;
            foreach (var edge in edges)
                queue.Enqueue(reverse ? edge.FromNodeId : edge.ToNodeId);
        }
        return visited;
    }

    private static int CountPaths(string start, string boss, MapGraph graph,
        Dictionary<string, List<MapGraphEdge>> outgoing)
    {
        var counts = new Dictionary<string, long> { [start] = 1 };
        foreach (var layer in graph.Layers)
        foreach (var node in layer)
        {
            string source = node.NodeId;
            if (!outgoing.TryGetValue(source, out var edges))
                continue;
            foreach (var edge in edges)
                counts[edge.ToNodeId] = counts.GetValueOrDefault(edge.ToNodeId) + counts[source];
        }
        long result = counts.GetValueOrDefault(boss);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }

    private readonly record struct PathProfile(int Strong, int Lingmai, int Events, int Shops);

    private sealed class PathQuotaReport
    {
        public int InvalidPathCount { get; set; }
        public int NoShopPathCount { get; set; }
    }

    private static PathQuotaReport BuildPathQuotaProfiles(string start, string boss, MapGraph graph,
        Dictionary<string, List<MapGraphEdge>> outgoing, ActDefinition act)
    {
        var states = new Dictionary<string, Dictionary<PathProfile, long>>();
        foreach (var layer in graph.Layers)
        foreach (var node in layer)
        {
            if (node.NodeId == start)
            {
                var initial = IncrementProfile(new PathProfile(), node);
                states[node.NodeId] = new Dictionary<PathProfile, long> { [initial] = 1 };
                continue;
            }

            var current = new Dictionary<PathProfile, long>();
            foreach (var edge in graph.Edges.Where(edge => edge.ToNodeId == node.NodeId))
            {
                if (!states.TryGetValue(edge.FromNodeId, out var sourceStates))
                    continue;
                foreach (var source in sourceStates)
                {
                    var next = IncrementProfile(source.Key, node);
                    current[next] = current.GetValueOrDefault(next) + source.Value;
                }
            }
            states[node.NodeId] = current;
        }

        var result = new PathQuotaReport();
        if (!states.TryGetValue(boss, out var bossStates))
            return result;
        foreach (var state in bossStates)
        {
            if (state.Key.Shops == 0)
                result.NoShopPathCount += ClampPathCount(state.Value);
            if (state.Key.Strong < act.MinStrongPerPath ||
                state.Key.Lingmai < act.MinLingmaiPerPath ||
                state.Key.Events > act.MaxEventPerPath ||
                state.Key.Shops > act.MaxShopPerPath)
                result.InvalidPathCount += ClampPathCount(state.Value);
        }
        return result;
    }

    private static PathProfile IncrementProfile(PathProfile profile, MapNodeDefinition node)
    {
        return new PathProfile(
            profile.Strong + (node.NodeType == MapGraphNodeType.Battle && node.Tier == EncounterTier.Strong ? 1 : 0),
            profile.Lingmai + (node.NodeType == MapGraphNodeType.Lingmai ? 1 : 0),
            profile.Events + (node.NodeType == MapGraphNodeType.Event ? 1 : 0),
            profile.Shops + (node.NodeType == MapGraphNodeType.Shop ? 1 : 0));
    }

    private static int ClampPathCount(long count) => count > int.MaxValue ? int.MaxValue : (int)count;

    private static bool HasShopPathToBoss(string start, MapGraph graph,
        Dictionary<string, List<MapGraphEdge>> outgoing, HashSet<string> canReachBoss)
    {
        var nodes = graph.AllNodes().ToDictionary(node => node.NodeId);
        var visited = new HashSet<string>();
        var queue = new Queue<(string NodeId, bool HasShop)>();
        queue.Enqueue((start, false));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add($"{current.NodeId}:{current.HasShop}"))
                continue;
            bool hasShop = current.HasShop || nodes[current.NodeId].NodeType == MapGraphNodeType.Shop;
            if (nodes[current.NodeId].NodeType == MapGraphNodeType.Shop && canReachBoss.Contains(current.NodeId))
                return true;
            if (outgoing.TryGetValue(current.NodeId, out var edges))
                foreach (var edge in edges)
                    queue.Enqueue((edge.ToNodeId, hasShop));
        }
        return false;
    }
}
