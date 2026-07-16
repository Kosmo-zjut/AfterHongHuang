using System.Collections.Generic;

/// <summary>
/// 单场战斗临时状态。离开 BattleScene 时整个对象销毁，不回写永久牌组。
/// </summary>
public sealed class BattleState
{
    public List<CardRuntime> DrawPile { get; } = new();
    public List<CardRuntime> Hand { get; } = new();
    public List<CardRuntime> DiscardPile { get; } = new();
    public List<CardRuntime> ExhaustPile { get; } = new();
    public EnemyInfo EnemyInfo { get; internal set; }
    public int EnemyHp { get; internal set; }
    public int EnemyMaxHp { get; internal set; }
    public int EnemyHuti { get; internal set; }
    public int EnemyYongyan { get; internal set; }
    public int EnemyYirong { get; internal set; }
    public bool EnemyMechanicActive { get; internal set; }
    public Dictionary<string, int> EnemyMechanicCounters { get; } = new();
    /// <summary>敌方全局行动统计，只用于日志/回放，不参与阶段序列索引。</summary>
    public int EnemyTurnIndex { get; internal set; }
    /// <summary>当前阶段定义 ID；空值表示基础循环序列。</summary>
    public string EnemyPhaseId { get; internal set; } = string.Empty;
    /// <summary>当前阶段内循环游标，阶段切换时归零。</summary>
    public int EnemyPhaseTurnIndex { get; internal set; }
    /// <summary>基础序列的 Opening intents 是否已经执行完。</summary>
    public bool EnemyOpeningCompleted { get; internal set; }
    /// <summary>基础序列当前 Opening 游标，支持多个 Opening intent。</summary>
    public int EnemyOpeningTurnIndex { get; internal set; }
    /// <summary>当前阶段 Opening 游标；阶段切换时归零。</summary>
    public int EnemyPhaseOpeningTurnIndex { get; internal set; }
    /// <summary>当前阶段 Opening 是否已执行完。</summary>
    public bool EnemyPhaseOpeningCompleted { get; internal set; }
    public int PlayerLingli { get; internal set; }
    public int PlayerDoujin { get; internal set; }
    public int PlayerHuti { get; internal set; }
    public int PlayerYongyan { get; internal set; }
    public int PlayerYirongCeng { get; internal set; }
    public bool IsPlayerTurn { get; internal set; } = true;
    public bool BattleOver { get; internal set; }
    public bool PlayerWon { get; internal set; }
    public bool ResultSubmitted { get; internal set; }
    public int ShuffleIndex { get; internal set; }
    public bool PlayerAttackedThisTurn { get; internal set; }
    public bool LastPlayerTurnHadAttack { get; internal set; }
    /// <summary>影响敌方意图快照的状态版本；卡牌、回合和机制变更会使缓存失效。</summary>
    public int IntentStateVersion { get; internal set; }
    public bool EnemyGuardWasBroken { get; internal set; }
    public bool EnemySpecialTriggered { get; internal set; }
    public int EnemyPhase { get; internal set; }
    /// <summary>当前敌方回合已解析并展示给 UI 的意图；正式执行消费同一实例。</summary>
    public ResolvedEnemyIntent PreparedEnemyIntent { get; internal set; }
    /// <summary>最近一次成功执行的解析对象，供日志记录与验证使用。</summary>
    public ResolvedEnemyIntent LastExecutedEnemyIntent { get; internal set; }
    public int LastEnemyActualDamage { get; internal set; }
}
