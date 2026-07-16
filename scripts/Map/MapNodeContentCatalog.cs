using System.Collections.Generic;

/// <summary>地图节点内容定义。生成器只按节点类型和遭遇层级消费此集中数据。</summary>
public sealed class MapNodeContentDefinition
{
    public MapGraphNodeType NodeType { get; init; }
    public EncounterTier Tier { get; init; }
    public string PoolId { get; init; }
    public string ContentId { get; init; }
    public string DisplayName { get; init; }
    public int Weight { get; init; } = 1;
}

/// <summary>ACT1 地图节点内容目录，避免 MapGraphGenerator 绑定具体事件或 Boss 文本。</summary>
public static class MapNodeContentCatalog
{
    private static readonly IReadOnlyList<MapNodeContentDefinition> Definitions = new[]
    {
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Start, PoolId = "start",
            ContentId = "act1_start", DisplayName = "道韵起点",
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Battle, Tier = EncounterTier.Weak,
            PoolId = EncounterPool.WeakPoolId, ContentId = "encounter_pool", DisplayName = "遭遇节点",
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Battle, Tier = EncounterTier.Strong,
            PoolId = EncounterPool.StrongPoolId, ContentId = "encounter_pool", DisplayName = "遭遇节点",
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Lingmai, PoolId = "lingmai_mvp",
            ContentId = "lingmai_mvp", DisplayName = "灵脉节点",
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Shop, PoolId = "shop_mvp",
            ContentId = "act1_shop_wayfarer", DisplayName = "行脚宝商",
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Event, PoolId = "event_mvp",
            ContentId = "act1_event_heavenly_river", DisplayName = "天河倒灌", Weight = 1,
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Event, PoolId = "event_mvp",
            ContentId = "act1_event_scavenger_seal", DisplayName = "散修遗篆", Weight = 1,
        },
        new MapNodeContentDefinition
        {
            NodeType = MapGraphNodeType.Boss, Tier = EncounterTier.Boss,
            PoolId = EncounterPool.BossPoolId, ContentId = "act1_boss_xiangliu", DisplayName = "相柳之骸",
        },
    };

    /// <summary>按节点类型、遭遇层级和稳定随机流解析一个可进入的内容定义。</summary>
    public static bool TryResolve(MapGraphNodeType nodeType, EncounterTier tier, StableRandom rng,
        out MapNodeContentDefinition definition, out string error)
    {
        definition = null;
        error = "";
        var matches = new List<MapNodeContentDefinition>();
        foreach (var candidate in Definitions)
        {
            if (candidate.NodeType == nodeType &&
                (nodeType == MapGraphNodeType.Battle || nodeType == MapGraphNodeType.Boss
                    ? candidate.Tier == tier
                    : true))
                matches.Add(candidate);
        }

        if (matches.Count == 0)
        {
            error = $"没有节点内容定义：type={nodeType}, tier={tier}";
            return false;
        }

        int totalWeight = 0;
        foreach (var match in matches)
            totalWeight += match.Weight;
        int roll = rng.NextInt(0, totalWeight);
        foreach (var match in matches)
        {
            roll -= match.Weight;
            if (roll < 0)
            {
                definition = match;
                return true;
            }
        }

        error = $"节点内容定义抽取越界：type={nodeType}, tier={tier}";
        return false;
    }
}
