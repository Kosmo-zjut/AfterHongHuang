using System.Collections.Generic;

/// <summary>第一大关 MapGraph 的结构规则，不包含 UI 或运行时节点状态。</summary>
public sealed class ActDefinition
{
    private static readonly IReadOnlyList<int> Act1LayerCounts = new[] { 1, 2, 3, 3, 4, 4, 4, 3, 1 };

    public string ActId { get; init; }
    public string DisplayTitle { get; init; }
    public int RuleVersion { get; init; }
    public IReadOnlyList<int> LayerNodeCounts { get; init; }
    public IReadOnlyList<ActLayerDefinition> Layers { get; init; }
    public IReadOnlyList<NodeTypeWindowDefinition> NodeTypeWindows { get; init; }
    public IReadOnlyList<NodeAdjacencyRule> AdjacencyRules { get; init; }
    public int RouteLaneCount { get; init; }
    public int MinCompletePaths { get; init; }
    public int MaxCompletePaths { get; init; }
    public int MinStrongPerPath { get; init; }
    public int MinLingmaiPerPath { get; init; }
    public int MaxEventPerPath { get; init; }
    public int MaxShopPerPath { get; init; }
    public int MinReachableShops { get; init; }
    public int MaxReachableShops { get; init; }
    public int RequiredShopPathFromLayer { get; init; }
    public bool RequireNoShopPath { get; init; }

    public static ActDefinition Act1 { get; } = new()
    {
        ActId = "act1",
        DisplayTitle = "不周废墟 · 外围",
        RuleVersion = 2,
        LayerNodeCounts = Act1LayerCounts,
        RouteLaneCount = 4,
        MinCompletePaths = 4,
        MaxCompletePaths = 12,
        MinStrongPerPath = 1,
        MinLingmaiPerPath = 1,
        MaxEventPerPath = 1,
        MaxShopPerPath = 1,
        MinReachableShops = 1,
        MaxReachableShops = 3,
        RequiredShopPathFromLayer = 3,
        RequireNoShopPath = true,
        NodeTypeWindows = new[]
        {
            new NodeTypeWindowDefinition
            {
                WindowId = "act1_start_window",
                MinLayer = 0,
                MaxLayer = 0,
                AllowedNodeTypes = new[] { MapGraphNodeType.Start },
                MinDistinctTypesPerLayer = 1,
                MinCombatNodesPerLayer = 0,
            },
            new NodeTypeWindowDefinition
            {
                WindowId = "act1_weak_window",
                MinLayer = 1,
                MaxLayer = 3,
                AllowedNodeTypes = new[] { MapGraphNodeType.Battle },
                MinDistinctTypesPerLayer = 1,
                MinCombatNodesPerLayer = 1,
            },
            new NodeTypeWindowDefinition
            {
                WindowId = "act1_mixed_window",
                MinLayer = 4,
                MaxLayer = 7,
                AllowedNodeTypes = new[]
                {
                    MapGraphNodeType.Battle, MapGraphNodeType.Lingmai,
                    MapGraphNodeType.Event, MapGraphNodeType.Shop,
                },
                MinDistinctTypesPerLayer = 2,
                MinCombatNodesPerLayer = 1,
                PerLayerCaps = new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Lingmai] = 2,
                    [MapGraphNodeType.Event] = 1,
                    [MapGraphNodeType.Shop] = 1,
                },
                GraphCaps = new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Lingmai] = 6,
                    [MapGraphNodeType.Event] = 3,
                    [MapGraphNodeType.Shop] = 3,
                },
            },
            new NodeTypeWindowDefinition
            {
                WindowId = "act1_boss_window",
                MinLayer = 8,
                MaxLayer = 8,
                AllowedNodeTypes = new[] { MapGraphNodeType.Boss },
                MinDistinctTypesPerLayer = 1,
                MinCombatNodesPerLayer = 1,
            },
        },
        AdjacencyRules = new[]
        {
            new NodeAdjacencyRule { RuleId = "no_same_special_edge", AppliesTo = new[]
            {
                MapGraphNodeType.Lingmai, MapGraphNodeType.Event, MapGraphNodeType.Shop,
            }},
        },
        Layers = BuildAct1Layers(Act1LayerCounts),
    };

    /// <summary>按 RunState.ActId 解析页面上下文；缺失时由调用方显式阻止显示。</summary>
    public static bool TryGet(string actId, out ActDefinition definition, out string error)
    {
        definition = null;
        error = "";
        if (string.IsNullOrWhiteSpace(actId))
        {
            error = "页面上下文缺少 ActId。";
            return false;
        }
        if (Act1.ActId == actId && !string.IsNullOrWhiteSpace(Act1.DisplayTitle))
        {
            definition = Act1;
            return true;
        }
        error = $"页面上下文不存在：{actId}";
        return false;
    }

    private static IReadOnlyList<ActLayerDefinition> BuildAct1Layers(IReadOnlyList<int> layerCounts)
    {
        return new[]
        {
            Layer(0, layerCounts[0], "act1_start_window", EncounterTier.Weak,
                new[] { MapGraphNodeType.Start }, new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Start] = 1,
                }),
            Layer(1, layerCounts[1], "act1_weak_window", EncounterTier.Weak,
                new[] { MapGraphNodeType.Battle }, new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 1,
                }),
            Layer(2, layerCounts[2], "act1_weak_window", EncounterTier.Weak,
                new[] { MapGraphNodeType.Battle }, new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 1,
                }),
            Layer(3, layerCounts[3], "act1_weak_window", EncounterTier.Weak,
                new[] { MapGraphNodeType.Battle }, new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 1,
                }),
            Layer(4, layerCounts[4], "act1_mixed_window", EncounterTier.Strong,
                new[] { MapGraphNodeType.Battle, MapGraphNodeType.Lingmai, MapGraphNodeType.Event },
                new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 45,
                    [MapGraphNodeType.Lingmai] = 35,
                    [MapGraphNodeType.Event] = 20,
                }),
            Layer(5, layerCounts[5], "act1_mixed_window", EncounterTier.Strong,
                new[] { MapGraphNodeType.Battle, MapGraphNodeType.Lingmai, MapGraphNodeType.Event, MapGraphNodeType.Shop },
                new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 30,
                    [MapGraphNodeType.Lingmai] = 25,
                    [MapGraphNodeType.Event] = 25,
                    [MapGraphNodeType.Shop] = 20,
                }),
            Layer(6, layerCounts[6], "act1_mixed_window", EncounterTier.Strong,
                new[] { MapGraphNodeType.Battle, MapGraphNodeType.Lingmai, MapGraphNodeType.Event, MapGraphNodeType.Shop },
                new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 30,
                    [MapGraphNodeType.Lingmai] = 20,
                    [MapGraphNodeType.Event] = 20,
                    [MapGraphNodeType.Shop] = 30,
                }),
            Layer(7, layerCounts[7], "act1_mixed_window", EncounterTier.Strong,
                new[] { MapGraphNodeType.Battle, MapGraphNodeType.Lingmai, MapGraphNodeType.Shop },
                new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Battle] = 30,
                    [MapGraphNodeType.Lingmai] = 20,
                    [MapGraphNodeType.Shop] = 50,
                }),
            Layer(8, layerCounts[8], "act1_boss_window", EncounterTier.Boss,
                new[] { MapGraphNodeType.Boss }, new Dictionary<MapGraphNodeType, int>
                {
                    [MapGraphNodeType.Boss] = 1,
                }),
        };
    }

    private static ActLayerDefinition Layer(int layerIndex, int nodeCount, string windowId, EncounterTier battleTier,
        IReadOnlyList<MapGraphNodeType> allowedTypes,
        IReadOnlyDictionary<MapGraphNodeType, int> weights)
    {
        return new ActLayerDefinition
        {
            LayerIndex = layerIndex,
            NodeCount = nodeCount,
            WindowIds = new[] { windowId },
            AllowedNodeTypes = allowedTypes,
            RequiredNodeTypes = allowedTypes.Count == 1 ? allowedTypes : new MapGraphNodeType[0],
            BattleTier = battleTier,
            NodeTypeWeights = weights,
            StableOrderPolicy = "ascending_lane_order",
        };
    }
}
