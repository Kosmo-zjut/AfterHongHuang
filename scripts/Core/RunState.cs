using System.Collections.Generic;

/// <summary>
/// 一局游戏的持久状态。战斗结束后只保留这里的数据，战斗临时牌堆不会写回永久牌组。
/// </summary>
public sealed class RunState
{
    public int PlayerMaxHp { get; set; } = 80;
    public int PlayerHp { get; set; } = 80;
    public int PlayerMaxLingli { get; set; } = 3;
    public int LingYun { get; set; }
    public int UnclaimedLingYun { get; set; }
    public int ActIndex { get; set; } = 1;
    public string ActId { get; set; } = "act1";
    public int RuleVersion { get; set; } = 1;
    public ulong RunSeed { get; set; }
    public bool MapNodesUnlocked { get; set; }
    public int CurrentMapLayer { get; set; }
    public int CurrentMapIndex { get; set; }
    /// <summary>当前路线位置的稳定节点 ID；坐标字段仅保留给旧 UI 兼容。</summary>
    public string CurrentMapNodeId { get; set; } = string.Empty;
    /// <summary>生产运行图的唯一事实源，普通节点页面不得自行生成第二张地图。</summary>
    public MapGraph MapGraph { get; set; }
    public List<CardRuntime> PermanentDeck { get; } = new();
    public List<DaoMarkInfo> DaoMarks { get; } = new();
    /// <summary>Run-level party state used by future node interactions such as ally healing.</summary>
    public PartyRoster Party { get; } = new();
    public bool DaoMarkSelected { get; set; }
    public List<DaoMarkInfo> CurrentChoices { get; } = new();
    public Dictionary<string, NodeLifecycleState> NodeStates { get; } = new();
    public HashSet<string> CompletedNodeIds { get; } = new();
    public HashSet<string> AppliedResultIds { get; } = new();
    public HashSet<string> AppliedRouteResultIds { get; } = new();
}

/// <summary>地图节点生命周期，Active 不等同于 Completed。</summary>
public enum NodeLifecycleState
{
    Active = 0,
    Completed = 1,
    Skipped = 2,
    Abandoned = 3,
}
