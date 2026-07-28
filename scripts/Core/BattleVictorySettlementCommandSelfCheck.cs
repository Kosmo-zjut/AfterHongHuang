using Godot;
using System;

/// <summary>Focused proof for the battle-victory command boundary and its no-partial-permanent-state failures.</summary>
public static class BattleVictorySettlementCommandSelfCheck
{
    public static void Run()
    {
        CheckNormalAndDuplicateResolution();
        CheckMissingBattleContext();
        CheckCommitFailureLeavesPermanentRewardsUntouched();
        GD.Print("[BattleVictorySettlementCommandSelfCheck] PASS normal, duplicate, stale context and commit failure");
    }

    private static void CheckNormalAndDuplicateResolution()
    {
        var manager = CreateVictoryReadyManager();
        var command = new BattleVictorySettlementCommand(manager);
        var result = command.ResolveTerminalBattleState();
        Ensure(result.Status == BattleVictorySettlementStatus.VictoryCommitted && result.VictoryPlan != null,
            "正常胜利没有产出已提交的胜利计划。");
        Ensure(ReferenceEquals(result.VictoryPlan, manager.ActiveBattleVictoryPlan) &&
            result.NodeId == manager.ActiveNode.NodeId && result.EncounterId == manager.ActiveEncounter.EnemyId,
            "胜利结果没有绑定当前节点、遭遇和唯一计划。");

        int resultCount = manager.RunState.AppliedResultIds.Count;
        int unclaimedLingYun = manager.UnclaimedLingYun;
        var duplicate = command.ResolveTerminalBattleState();
        Ensure(duplicate.Status == BattleVictorySettlementStatus.VictoryAlreadyCommitted &&
            ReferenceEquals(duplicate.VictoryPlan, result.VictoryPlan), "重复结算没有复用已提交的唯一胜利计划。");
        Ensure(manager.RunState.AppliedResultIds.Count == resultCount && manager.UnclaimedLingYun == unclaimedLingYun,
            "重复结算写入了额外节点结果或奖励状态。");
        manager.Free();
    }

    private static void CheckMissingBattleContext()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xA903_0002UL), "无法创建缺失上下文自检新局。");
        int deckSize = manager.GetDeckSize();
        int lingYun = manager.LingYun;
        var result = new BattleVictorySettlementCommand(manager).ResolveTerminalBattleState();
        Ensure(result.Status == BattleVictorySettlementStatus.Rejected &&
            manager.GetDeckSize() == deckSize && manager.LingYun == lingYun &&
            manager.RunState.AppliedResultIds.Count == 0,
            "缺失活动遭遇或节点时仍写入了胜利状态。");
        manager.Free();
    }

    private static void CheckCommitFailureLeavesPermanentRewardsUntouched()
    {
        var manager = CreateVictoryReadyManager();
        int deckSize = manager.GetDeckSize();
        int lingYun = manager.LingYun;
        int unclaimedLingYun = manager.UnclaimedLingYun;
        int resultCount = manager.RunState.AppliedResultIds.Count;
#if DEBUG
        manager.InjectBattleVictoryCommitFailureForSelfCheck();
#endif
        var result = new BattleVictorySettlementCommand(manager).ResolveTerminalBattleState();
        Ensure(result.Status == BattleVictorySettlementStatus.VictorySettlementFailed &&
            manager.BattleOver && manager.PlayerWon && manager.ActiveBattleVictoryPlan == null,
            "胜利计划提交失败没有返回明确的已结束战斗错误状态。");
        // 节点 Completed 在敌人生命归零时已经独立提交；本注入只验证奖励提交没有额外写入。
        Ensure(manager.GetDeckSize() == deckSize && manager.LingYun == lingYun &&
            manager.UnclaimedLingYun == unclaimedLingYun && manager.RunState.AppliedResultIds.Count == resultCount + 1,
            "胜利计划提交失败污染了永久奖励状态。");
        manager.Free();
    }

    private static GameManager CreateVictoryReadyManager()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xA903_0001UL), "无法创建胜利结算自检新局。");
        var node = FindBattleNode(manager);
        Ensure(manager.TryEnterBattle(node, out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        manager.EnemyHp = 0;
        return manager;
    }

    private static MapNodeDefinition FindBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Battle)
                return node;
        throw new InvalidOperationException("胜利结算自检找不到战斗节点。");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[BattleVictorySettlementCommandSelfCheck] {message}");
    }
}
