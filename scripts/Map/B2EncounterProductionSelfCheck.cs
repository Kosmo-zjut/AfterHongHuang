using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Debug 构建的 B2 自检：确认遭遇池、生产节点入口和图结构防御边界已接通。
/// 该自检只使用纯数据/资源存在性检查，不修改玩家运行态。
/// </summary>
public static class B2EncounterProductionSelfCheck
{
    public static void Run()
    {
        const int seedCount = 500;
        var generator = new MapGraphGenerator();
        var act = ActDefinition.Act1;
        var required = new[]
        {
            "act1_weak_monkey", "act1_weak_stone", "act1_weak_scavenger",
            "act1_strong_guard", "act1_strong_talisman", "act1_strong_mountain",
            "act1_boss_xiangliu",
        };

        foreach (string id in required)
        {
            Ensure(EncounterPool.Act1.TryGetDefinition(id, out var definition), $"遭遇定义缺失：{id}");
            Ensure(EnemyDefinitionValidator.TryValidate(definition, out var definitionError), definitionError);
        }
        foreach (var card in DataDefs.BossRewardCardPool)
            Ensure(card.Id.StartsWith("wx_a1_boss_"), $"Boss 天品卡 ID 不符合专属池：{card.Id}");

        int encounterNodes = 0;
        var fingerprints = new HashSet<string>();
        for (int i = 0; i < seedCount; i++)
        {
            ulong seed = 0xB2_2026_0713_0001UL + (ulong)i * 0x9E3779B97F4A7C15UL;
            Ensure(generator.TryGenerate(seed, act, out var graph, out _, out var error), error);
            fingerprints.Add(graph.Fingerprint);

            var streams = new StableRandomStreams(seed, act.ActId, act.RuleVersion);
            foreach (var node in graph.AllNodes())
            {
                if (node.NodeType != MapGraphNodeType.Battle && node.NodeType != MapGraphNodeType.Boss)
                {
                    if (node.NodeType == MapGraphNodeType.Shop)
                        Ensure(ShopDefinitionCatalog.TryGet(node.ContentId, out _, out var shopError), shopError);
                    if (node.NodeType == MapGraphNodeType.Event)
                        Ensure(EventDefinitionCatalog.TryGet(node.ContentId, out _, out var eventError), eventError);
                    continue;
                }

                encounterNodes++;
                var context = new NodeContext
                {
                    NodeId = node.NodeId,
                    NodeType = node.NodeType,
                    ActIndex = 1,
                    LayerIndex = node.LayerIndex,
                    EncounterTier = node.Tier,
                    PoolId = node.PoolId,
                    Seed = node.NodeSeed,
                };
                Ensure(EncounterPool.Act1.TryResolveForNode(context, streams, out var first, out error), error);
                Ensure(EncounterPool.Act1.TryResolveForNode(context, streams, out var repeat, out error), error);
                Ensure(first.Id == repeat.Id, $"同 seed 遭遇不稳定：{node.NodeId}");
                Ensure(first.Mechanic != EnemyMechanicKind.None, $"遭遇缺少机制标签：{first.Id}");
            }
        }

        Ensure(fingerprints.Count > 1, "不同 seed 未产生不同生产地图");
        Ensure(ResourceLoader.Exists("res://scenes/Shop/Shop.tscn"), "Shop.tscn 不存在");
        Ensure(ResourceLoader.Exists("res://scenes/Event/Event.tscn"), "Event.tscn 不存在");
        Ensure(ResourceLoader.Exists("res://scenes/Debug/BattleDebugTools.tscn"), "BattleDebugTools.tscn 不存在");
        VerifyDuplicateRejection(generator, act);

        GD.Print($"[B2EncounterProductionSelfCheck] PASS seeds={seedCount} encounterNodes={encounterNodes} " +
            $"fingerprints={fingerprints.Count} pools=weak/strong/boss pages=Shop/Event");
    }

    private static void VerifyDuplicateRejection(MapGraphGenerator generator, ActDefinition act)
    {
        Ensure(generator.TryGenerate(0xB2D0_0713_0001UL, act, out var graph, out _, out var error), error);
        var validator = new MapGraphValidator();
        var duplicateEdges = graph.Edges.ToList();
        duplicateEdges.Add(new MapGraphEdge
        {
            FromNodeId = duplicateEdges[0].FromNodeId,
            ToNodeId = duplicateEdges[0].ToNodeId,
        });
        var duplicateEdgeGraph = new MapGraph(graph.Layers.Select(layer => layer.ToList()).ToList(), duplicateEdges,
            graph.ActId, graph.RuleVersion);
        Ensure(!validator.Validate(duplicateEdgeGraph, act).IsValid, "Validator 未拒绝重复边");

        var duplicateLayers = graph.Layers.Select(layer => layer.ToList()).ToList();
        duplicateLayers[1].Add(duplicateLayers[1][0]);
        var duplicateNodeGraph = new MapGraph(duplicateLayers, graph.Edges.ToList(), graph.ActId, graph.RuleVersion);
        Ensure(!validator.Validate(duplicateNodeGraph, act).IsValid, "Validator 未拒绝重复 NodeId");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[B2EncounterProductionSelfCheck] {error}");
    }
}
