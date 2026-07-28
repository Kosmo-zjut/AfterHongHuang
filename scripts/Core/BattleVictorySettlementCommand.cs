/// <summary>战斗生命归零后的核心结算结果。UI 只能消费此对象呈现页面，不能据此自行写入运行状态。</summary>
public sealed class BattleVictorySettlementResult
{
    public BattleVictorySettlementResult(BattleVictorySettlementStatus status, BattleVictoryPlan victoryPlan,
        string nodeId, string encounterId, string error)
    {
        Status = status;
        VictoryPlan = victoryPlan;
        NodeId = nodeId ?? "";
        EncounterId = encounterId ?? "";
        Error = error ?? "";
    }

    public BattleVictorySettlementStatus Status { get; }
    public BattleVictoryPlan VictoryPlan { get; }
    public string NodeId { get; }
    public string EncounterId { get; }
    public string Error { get; }
}

public enum BattleVictorySettlementStatus
{
    NoOutcome,
    DefeatRegistered,
    VictoryCommitted,
    VictoryAlreadyCommitted,
    VictorySettlementFailed,
    Rejected,
}

/// <summary>
/// Owns the terminal battle-victory command boundary. It is intentionally a short-lived ordinary C# object:
/// GameManager owns RunState/BattleState, while this command only composes the existing state and reward transactions.
/// </summary>
public sealed class BattleVictorySettlementCommand
{
    private readonly GameManager _gameManager;

    public BattleVictorySettlementCommand(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    /// <summary>
    /// Resolves a terminal HP condition exactly once. A successful victory returns the single committed plan held by
    /// GameManager; reward-plan failures keep the already-ended battle explicit instead of letting UI fabricate rewards.
    /// </summary>
    public BattleVictorySettlementResult ResolveTerminalBattleState()
    {
        if (_gameManager == null || _gameManager.ActiveBattle == null)
            return Rejected("缺少活动战斗上下文，不能结算战斗。");

        if (_gameManager.BattleOver)
            return ResolveExistingTerminalState();

        if (_gameManager.PlayerHp <= 0)
        {
            if (!_gameManager.TryRegisterPlayerDefeated(out var defeatError))
                return Rejected(defeatError);
            return new BattleVictorySettlementResult(BattleVictorySettlementStatus.DefeatRegistered,
                null, _gameManager.ActiveNode?.NodeId, _gameManager.ActiveEncounter?.EnemyId, "");
        }

        if (_gameManager.EnemyHp > 0)
            return new BattleVictorySettlementResult(BattleVictorySettlementStatus.NoOutcome,
                null, _gameManager.ActiveNode?.NodeId, _gameManager.ActiveEncounter?.EnemyId, "");

        if (!_gameManager.TryRegisterEnemyDefeated(out var registerError))
            return Rejected(registerError);

        if (!_gameManager.TryBuildBattleVictoryPlan(out var victoryPlan, out var planError) ||
            !_gameManager.TryCommitBattleVictory(victoryPlan, out planError))
        {
            return new BattleVictorySettlementResult(BattleVictorySettlementStatus.VictorySettlementFailed,
                null, _gameManager.ActiveNode?.NodeId, _gameManager.ActiveEncounter?.EnemyId, planError);
        }

        return new BattleVictorySettlementResult(BattleVictorySettlementStatus.VictoryCommitted,
            victoryPlan, victoryPlan.NodeId, victoryPlan.EncounterId, "");
    }

    private BattleVictorySettlementResult ResolveExistingTerminalState()
    {
        if (!_gameManager.PlayerWon)
            return new BattleVictorySettlementResult(BattleVictorySettlementStatus.DefeatRegistered,
                null, _gameManager.ActiveNode?.NodeId, _gameManager.ActiveEncounter?.EnemyId, "");

        var victoryPlan = _gameManager.ActiveBattleVictoryPlan;
        if (victoryPlan == null)
        {
            return new BattleVictorySettlementResult(BattleVictorySettlementStatus.VictorySettlementFailed,
                null, _gameManager.ActiveNode?.NodeId, _gameManager.ActiveEncounter?.EnemyId,
                "战斗已胜利，但当前没有可呈现的已提交奖励计划。");
        }

        return new BattleVictorySettlementResult(BattleVictorySettlementStatus.VictoryAlreadyCommitted,
            victoryPlan, victoryPlan.NodeId, victoryPlan.EncounterId, "");
    }

    private BattleVictorySettlementResult Rejected(string error)
    {
        return new BattleVictorySettlementResult(BattleVictorySettlementStatus.Rejected,
            null, _gameManager?.ActiveNode?.NodeId, _gameManager?.ActiveEncounter?.EnemyId,
            string.IsNullOrWhiteSpace(error) ? "战斗结算命令被拒绝。" : error);
    }
}
