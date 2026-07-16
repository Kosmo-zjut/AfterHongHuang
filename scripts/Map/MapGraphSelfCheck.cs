using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>Debug 构建的 B1 RNG/MapGraph 批量自检，不修改生产地图或 GameManager 运行态。</summary>
public static class MapGraphSelfCheck
{
    public static void Run()
    {
        const int seedCount = 500;
        var act = ActDefinition.Act1;
        var generator = new MapGraphGenerator();
        var firstSeed = 0xA17C_2026_0713_0001UL;
        int failures = 0;
        int minPaths = int.MaxValue;
        int maxPaths = int.MinValue;
        int minShops = int.MaxValue;
        int maxShops = int.MinValue;
        int minAttempts = int.MaxValue;
        int maxAttempts = int.MinValue;
        long totalAttempts = 0;
        int crossingCount = 0;
        int minMixedDistinct = int.MaxValue;
        int minMixedCombat = int.MaxValue;
        var fingerprints = new HashSet<string>();
        var validator = new MapGraphValidator();

        for (int i = 0; i < seedCount; i++)
        {
            ulong seed = firstSeed + (ulong)i * 0x9E3779B97F4A7C15UL;
            if (!generator.TryGenerate(seed, act, out var graph, out var report, out var error))
            {
                failures++;
                throw new System.InvalidOperationException($"B1 MapGraph 生成失败：seed={seed}，{error}");
            }

            if (!generator.TryGenerate(seed, act, out var repeatGraph, out var repeatReport, out error))
                throw new System.InvalidOperationException($"B1 同 seed 重生成失败：seed={seed}，{error}");
            Ensure(graph.Fingerprint == repeatGraph.Fingerprint, "同 seed MapGraph fingerprint 不一致");
            Ensure(report.Attempts == repeatReport.Attempts, "同 seed 生成尝试次数不一致");
            var validation = validator.Validate(graph, act);
            Ensure(validation.IsValid, string.Join("；", validation.Errors));
            crossingCount += validation.CrossingCount;

            foreach (var layer in act.Layers)
            {
                var window = act.NodeTypeWindows.FirstOrDefault(item => layer.WindowIds.Contains(item.WindowId));
                if (window == null || window.MinDistinctTypesPerLayer <= 1)
                    continue;
                int distinct = graph.Layers[layer.LayerIndex].Select(node => node.NodeType).Distinct().Count();
                int combat = graph.Layers[layer.LayerIndex].Count(node =>
                    node.NodeType == MapGraphNodeType.Battle || node.NodeType == MapGraphNodeType.Boss);
                minMixedDistinct = System.Math.Min(minMixedDistinct, distinct);
                minMixedCombat = System.Math.Min(minMixedCombat, combat);
            }

            minPaths = System.Math.Min(minPaths, report.PathCount);
            maxPaths = System.Math.Max(maxPaths, report.PathCount);
            minShops = System.Math.Min(minShops, report.ReachableShopCount);
            maxShops = System.Math.Max(maxShops, report.ReachableShopCount);
            minAttempts = System.Math.Min(minAttempts, report.Attempts);
            maxAttempts = System.Math.Max(maxAttempts, report.Attempts);
            totalAttempts += report.Attempts;
            fingerprints.Add(graph.Fingerprint);
        }

        Ensure(fingerprints.Count > 1, "500 个不同 RunSeed 没有产生图变化");
        Ensure(crossingCount == 0, "生产图存在稳定顺序交叉边");
        Ensure(minMixedDistinct >= 2 && minMixedCombat >= 1, "混合层未满足类型多样性/战斗节点下限");
        VerifyRandomStreams(firstSeed, act);
        VerifyValidatorRejectsInvalidFixtures(generator, act);

        double averageAttempts = (double)totalAttempts / seedCount;
        GD.Print($"[MapGraphRngSelfCheck] PASS seeds={seedCount} failures={failures} " +
            $"paths={minPaths}..{maxPaths} shops={minShops}..{maxShops} " +
            $"attempts={minAttempts}/{averageAttempts:F2}/{maxAttempts} fingerprints={fingerprints.Count} " +
            $"crossings={crossingCount} mixedDistinctMin={minMixedDistinct} mixedCombatMin={minMixedCombat}");
    }

    private static void VerifyRandomStreams(ulong runSeed, ActDefinition act)
    {
        var streams = new StableRandomStreams(runSeed, act.ActId, act.RuleVersion);
        var sequenceA = ReadSequence(streams.CreateMapStream(0));
        var sequenceB = ReadSequence(streams.CreateMapStream(0));
        for (int i = 0; i < sequenceA.Length; i++)
            Ensure(sequenceA[i] == sequenceB[i], "同输入稳定随机序列不一致");

        var streamSeeds = new HashSet<ulong>();
        foreach (RandomStreamKey key in System.Enum.GetValues(typeof(RandomStreamKey)))
            streamSeeds.Add(streams.DeriveSeed(key, "root", 0));
        Ensure(streamSeeds.Count == 6, "命名随机流之间出现 seed 碰撞");

        var generator = new MapGraphGenerator();
        Ensure(generator.TryGenerate(runSeed, act, out var mapBeforeReward, out _, out var mapError), mapError);
        var rewardStream = streams.CreateRewardStream("reward_node", 0);
        for (int i = 0; i < 32; i++)
            rewardStream.NextUInt64();
        Ensure(generator.TryGenerate(runSeed, act, out var mapAfterReward, out _, out mapError), mapError);
        Ensure(mapBeforeReward.Fingerprint == mapAfterReward.Fingerprint,
            "额外消耗 Reward 流改变了 Map fingerprint");

        var combatA = ReadSequence(streams.CreateCombatStream("act1_l1_n0", 0));
        var combatB = ReadSequence(streams.CreateCombatStream("act1_l1_n0", 0));
        for (int i = 0; i < combatA.Length; i++)
            Ensure(combatA[i] == combatB[i], "Reward 流调用改变了 Combat 局部流");
    }

    private static ulong[] ReadSequence(StableRandom random)
    {
        var values = new ulong[8];
        for (int i = 0; i < values.Length; i++)
            values[i] = random.NextUInt64();
        return values;
    }

    private static void VerifyValidatorRejectsInvalidFixtures(MapGraphGenerator generator, ActDefinition act)
    {
        Ensure(generator.TryGenerate(0xC1F1_A7E1UL, act, out var graph, out _, out var error), error);
        var validator = new MapGraphValidator();

        var crossingEdges = CopyEdges(graph);
        bool swapped = false;
        for (int i = 0; i < crossingEdges.Count && !swapped; i++)
        for (int j = i + 1; j < crossingEdges.Count && !swapped; j++)
        {
            var firstFrom = graph.GetNode(crossingEdges[i].FromNodeId);
            var secondFrom = graph.GetNode(crossingEdges[j].FromNodeId);
            var firstTo = graph.GetNode(crossingEdges[i].ToNodeId);
            var secondTo = graph.GetNode(crossingEdges[j].ToNodeId);
            if (firstFrom?.LayerIndex != secondFrom?.LayerIndex || firstFrom?.NodeId == secondFrom?.NodeId ||
                firstFrom.StableOrder >= secondFrom.StableOrder || firstTo.StableOrder >= secondTo.StableOrder)
                continue;
            crossingEdges[i] = new MapGraphEdge { FromNodeId = firstFrom.NodeId, ToNodeId = secondTo.NodeId };
            crossingEdges[j] = new MapGraphEdge { FromNodeId = secondFrom.NodeId, ToNodeId = firstTo.NodeId };
            swapped = true;
        }
        Ensure(swapped, "非法 fixture 未找到可构造交叉边");
        var crossingGraph = new MapGraph(CopyLayers(graph), crossingEdges, act.ActId, act.RuleVersion);
        var crossingReport = validator.Validate(crossingGraph, act);
        Ensure(!crossingReport.IsValid && crossingReport.CrossingCount > 0, "Validator 未拒绝交叉边");

        var wrongOrderLayers = CopyLayers(graph);
        var first = wrongOrderLayers[1][0];
        wrongOrderLayers[1][0] = CloneNode(first, stableOrder: wrongOrderLayers[1][1].StableOrder);
        var wrongOrderReport = validator.Validate(
            new MapGraph(wrongOrderLayers, CopyEdges(graph), act.ActId, act.RuleVersion), act);
        Ensure(!wrongOrderReport.IsValid, "Validator 未拒绝错误 StableOrder");

        int mixedLayer = act.Layers.First(layer => layer.AllowedNodeTypes.Contains(MapGraphNodeType.Lingmai)).LayerIndex;
        var singleTypeLayers = CopyLayers(graph);
        for (int i = 0; i < singleTypeLayers[mixedLayer].Count; i++)
            singleTypeLayers[mixedLayer][i] = CloneNode(singleTypeLayers[mixedLayer][i], MapGraphNodeType.Lingmai);
        var singleTypeReport = validator.Validate(
            new MapGraph(singleTypeLayers, CopyEdges(graph), act.ActId, act.RuleVersion), act);
        Ensure(!singleTypeReport.IsValid, "Validator 未拒绝整层单一非战斗节点");

        var consecutiveLayers = CopyLayers(graph);
        bool foundConsecutive = false;
        foreach (var edge in graph.Edges)
        {
            var from = graph.GetNode(edge.FromNodeId);
            var to = graph.GetNode(edge.ToNodeId);
            if (from == null || to == null || from.LayerIndex < 4 ||
                !act.Layers[to.LayerIndex].AllowedNodeTypes.Contains(MapGraphNodeType.Lingmai))
                continue;
            consecutiveLayers[from.LayerIndex][from.LayerOrder] = CloneNode(from, MapGraphNodeType.Lingmai);
            consecutiveLayers[to.LayerIndex][to.LayerOrder] = CloneNode(to, MapGraphNodeType.Lingmai);
            foundConsecutive = true;
            break;
        }
        Ensure(foundConsecutive, "非法 fixture 未找到可构造连续非战斗节点边");
        var consecutiveReport = validator.Validate(
            new MapGraph(consecutiveLayers, CopyEdges(graph), act.ActId, act.RuleVersion), act);
        Ensure(!consecutiveReport.IsValid, "Validator 未拒绝连续同类非战斗节点");
    }

    private static List<List<MapNodeDefinition>> CopyLayers(MapGraph graph)
    {
        var result = new List<List<MapNodeDefinition>>();
        foreach (var layer in graph.Layers)
            result.Add(new List<MapNodeDefinition>(layer));
        return result;
    }

    private static List<MapGraphEdge> CopyEdges(MapGraph graph) => new(graph.Edges);

    private static MapNodeDefinition CloneNode(MapNodeDefinition source,
        MapGraphNodeType? type = null, int? stableOrder = null)
    {
        return new MapNodeDefinition
        {
            NodeId = source.NodeId,
            LayerIndex = source.LayerIndex,
            IndexInLayer = source.IndexInLayer,
            LayerOrder = source.LayerOrder,
            StableOrder = stableOrder ?? source.StableOrder,
            NodeType = type ?? source.NodeType,
            Tier = source.Tier,
            PoolId = source.PoolId,
            NodeSeed = source.NodeSeed,
            ContentId = source.ContentId,
            DisplayName = source.DisplayName,
        };
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[MapGraphRngSelfCheck] {error}");
    }
}
