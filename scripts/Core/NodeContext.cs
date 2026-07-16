/// <summary>当前活动地图节点的稳定上下文。</summary>
public sealed class NodeContext
{
    public string NodeId { get; init; }
    public MapGraphNodeType NodeType { get; init; }
    public int ActIndex { get; init; }
    public int LayerIndex { get; init; }
    public EncounterTier EncounterTier { get; init; }
    public string PoolId { get; init; }
    public ulong Seed { get; init; }
}

/// <summary>遭遇层级枚举，避免后续用字符串拼接决定敌人池。</summary>
public enum EncounterTier
{
    Weak = 0,
    Strong = 1,
    Elite = 2,
    Boss = 3,
}

/// <summary>进入遭遇场景的显式请求，BattleScene 不自行猜测敌人。</summary>
public sealed class EncounterRequest
{
    public NodeContext Node { get; init; }
    public string ActId { get; init; }
    public int Layer { get; init; }
    public string PoolId { get; init; }
    public string EnemyId { get; init; }
    public EnemyInfo EnemyInfo { get; init; }
    public ulong EncounterSeed { get; init; }
    public ulong CombatSeed { get; init; }
    public string RewardProfileId { get; init; }
    public ulong Seed => EncounterSeed;
}

/// <summary>节点运行时结果类型。每个活动节点只能提交一个合法终态。</summary>
public enum NodeResultType
{
    Completed = 0,
    Exited = 1,
    NodeSkipped = 2,
    Defeated = 3,
    Abandoned = 4,
}

/// <summary>
/// 节点退出结果。ConsumeNode 和 AdvanceRoute 由 GameManager 工厂按 ResultType 生成，
/// SubmitNodeResult 会再次校验，避免调用方用布尔值伪造流程语义。
/// </summary>
public class NodeResult
{
    public string ResultId { get; init; }
    public string NodeId { get; init; }
    public NodeResultType ResultType { get; init; }
    public bool ConsumeNode { get; init; }
    public bool AdvanceRoute { get; init; }
    public string Summary { get; init; }
}

/// <summary>旧名称兼容层；新流程必须使用 NodeResult 和 NodeResultType。</summary>
[System.Obsolete("Use NodeResult instead.")]
public sealed class EncounterResult : NodeResult
{
}
