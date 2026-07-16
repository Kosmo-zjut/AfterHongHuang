using System.Collections.Generic;

/// <summary>
/// 战斗胜利前一次性解析出的不可变奖励计划。计划成功前不改变 Battle/Run/Node 状态，
/// 领取阶段只能通过 GameManager 持有的同一计划提交。
/// </summary>
public sealed class BattleVictoryPlan
{
    public string NodeId { get; init; }
    public string EncounterId { get; init; }
    public string RewardProfileId { get; init; }
    public string LingYunRewardId { get; init; }
    public int LingYunAmount { get; init; }
    public RewardContext RewardContext { get; init; }
    public IReadOnlyList<RewardPlan> CardRewards { get; init; }
}
