using Godot;
using System.Linq;

/// <summary>
/// Debug 构建的状态契约自检。使用独立 GameManager 实例，不触碰玩家当前运行状态。
/// </summary>
public static class StateContractSelfCheck
{
    public static void Run()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu"), "无法创建巫祝测试新局");
        Ensure(manager.GetDeckSize() == 10, "新局永久套牌不是 10 张");

        var firstNode = manager.MapGraph.Layers[1][0];
        Ensure(manager.TryEnterBattle(firstNode, out var enterError), enterError);
        Ensure(manager.CurrentMapLayer == 0 && manager.CurrentMapIndex == 0, "进入战斗时提前推进了地图坐标");
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);

        var permanentBeforeBattle = new System.Collections.Generic.List<CardRuntime>(manager.GetPermanentDeckCards());
        Ensure(manager.ActiveBattle.DrawPile.Count == permanentBeforeBattle.Count, "第一场战斗未完整复制永久套牌");
        foreach (var battleCard in manager.ActiveBattle.DrawPile)
            Ensure(!permanentBeforeBattle.Contains(battleCard), "战斗牌堆复用了永久 CardRuntime 引用");
        foreach (var battleCard in manager.ActiveBattle.DrawPile)
            Ensure(!permanentBeforeBattle.Any(permanentCard => ReferenceEquals(battleCard.Info, permanentCard.Info)),
                "战斗牌堆复用了永久 CardInfo 投影引用");

        manager.StartPlayerTurn();
        Ensure(manager.Hand.Count == 5, "第一场战斗起手不是 5 张");
        Ensure(CountBattleCards(manager) == manager.GetDeckSize(), "第一场起手后战斗卡总数不等于永久套牌");

        manager.PlayerHp = 55;
        MoveOneCardToExhaust(manager);
        Ensure(CountBattleCards(manager) == manager.GetDeckSize(), "残留手牌/弃牌/消弭后战斗卡总数改变");

        var victoryResult = manager.CreateNodeResult(NodeResultType.Completed, "自检胜利", out var resultError);
        Ensure(victoryResult != null, resultError);
        Ensure(manager.SubmitNodeResult(victoryResult, out resultError), resultError);
        Ensure(manager.RunState.NodeStates[firstNode.NodeId] == NodeLifecycleState.Completed, "胜利未完成节点");
        Ensure(manager.CurrentMapLayer == firstNode.LayerIndex && manager.CurrentMapIndex == firstNode.IndexInLayer,
            "胜利结果未推进地图坐标");
        Ensure(!manager.SubmitNodeResult(victoryResult, out _), "同一 ResultId 重复提交未被拒绝");

        Ensure(CardPoolCatalog.TryGet("reward_cards", out var rewardPool, out var rewardPoolError), rewardPoolError);
        manager.AddCardToDeck(rewardPool.Cards[0]);
        Ensure(manager.GetDeckSize() == 11, "奖励卡没有写入永久套牌");
        Ensure(CountBattleCards(manager) == 10, "战斗结算奖励错误改变了已结束战斗牌堆");
        Ensure(manager.ExitBattleToMap(out var exitError), exitError);
        Ensure(manager.PlayerHp == 55, "销毁战斗实例时 HP 被重置");
        Ensure(manager.GetDeckSize() == 11, "销毁战斗实例时永久套牌被污染");

        var secondNode = manager.MapGraph.Layers[1][1];
        Ensure(manager.TryEnterBattle(secondNode, out enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out beginError), beginError);
        manager.StartPlayerTurn();
        Ensure(manager.Hand.Count == 5, "第二场战斗起手不是 5 张");
        Ensure(manager.DiscardPile.Count == 0 && manager.ExhaustPile.Count == 0, "第二场继承了上一场弃牌/消弭");
        Ensure(manager.GetDeckSize() == 11, "第二场战斗继承了临时牌堆数量");
        Ensure(CountBattleCards(manager) == 11, "第二场战斗卡总数不等于永久套牌");
        Ensure(manager.PlayerHp == 55, "第二场战斗未保留 RunState HP");

        var defeatResult = manager.CreateNodeResult(NodeResultType.Defeated, "自检失败", out resultError);
        Ensure(defeatResult != null && manager.SubmitNodeResult(defeatResult, out resultError), resultError);
        Ensure(manager.RunState.NodeStates[secondNode.NodeId] == NodeLifecycleState.Abandoned, "失败结果未记录为 Abandoned");
        int layerAfterDefeat = manager.CurrentMapLayer;
        int indexAfterDefeat = manager.CurrentMapIndex;
        Ensure(manager.ExitBattleToTitleAfterDefeat(out exitError), exitError);
        Ensure(manager.CurrentMapLayer == layerAfterDefeat && manager.CurrentMapIndex == indexAfterDefeat,
            "失败离场推进了地图路线");

        VerifySkippedAndAbandonedAreTerminal();
        VerifyInvalidLingmaiRecovery();
        manager.Free();

        GD.Print("[StateContractSelfCheck] PASS RunState/BattleState/NodeResult 契约自检通过。");
    }

    private static int CountBattleCards(GameManager manager)
    {
        return manager.ActiveBattle.DrawPile.Count + manager.ActiveBattle.Hand.Count +
            manager.ActiveBattle.DiscardPile.Count + manager.ActiveBattle.ExhaustPile.Count;
    }

    private static void MoveOneCardToExhaust(GameManager manager)
    {
        Ensure(manager.Hand.Count > 0, "无法构造残留手牌");
        var card = manager.Hand[0];
        manager.Hand.RemoveAt(0);
        manager.ExhaustPile.Add(card);
        manager.DiscardPile.AddRange(manager.Hand);
        manager.Hand.Clear();
    }

    private static void VerifySkippedAndAbandonedAreTerminal()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu"), "无法创建终态自检新局");
        var lingmaiNode = manager.MapGraph.AllNodes()
            .FirstOrDefault(node => node.NodeType == MapGraphNodeType.Lingmai);
        Ensure(lingmaiNode != null, "随机混合图缺少灵脉节点");
        Ensure(manager.TryEnterLingmai(lingmaiNode, out var error), error);
        var exited = manager.CreateNodeResult(NodeResultType.Exited, "自检跳过灵脉", out error);
        Ensure(exited != null && manager.SubmitNodeResult(exited, out error), error);
        Ensure(manager.RunState.NodeStates[lingmaiNode.NodeId] == NodeLifecycleState.Skipped, "Exited 未记录为 Skipped");
        Ensure(!manager.RunState.CompletedNodeIds.Contains(lingmaiNode.NodeId), "Skipped 错误进入 Completed 集合");
        Ensure(manager.CurrentMapLayer == lingmaiNode.LayerIndex, "Exited 未推进灵脉路线");
        Ensure(!manager.SubmitNodeResult(manager.CreateNodeResult(NodeResultType.NodeSkipped, "覆盖跳过", out _), out _),
            "Skipped 被第二个结果覆盖");
        Ensure(manager.ExitLingmaiToMap(out error), error);

        var battleNode = manager.MapGraph.Layers[1][0];
        Ensure(manager.TryEnterBattle(battleNode, out error), error);
        Ensure(manager.TryBeginActiveBattle(out error), error);
        var abandoned = manager.CreateNodeResult(NodeResultType.Abandoned, "自检放弃", out error);
        Ensure(abandoned != null && manager.SubmitNodeResult(abandoned, out error), error);
        Ensure(manager.RunState.NodeStates[battleNode.NodeId] == NodeLifecycleState.Abandoned, "Abandoned 未记录终态");
        Ensure(!manager.SubmitNodeResult(manager.CreateNodeResult(NodeResultType.Defeated, "覆盖放弃", out _), out _),
            "Abandoned 被第二个结果覆盖");
        Ensure(manager.ExitBattleToTitleAfterDefeat(out error), error);
        manager.Free();
    }

    private static void VerifyInvalidLingmaiRecovery()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xB1_1A_1E_01UL), "无法创建非法入口自检新局");
        int originalLayer = manager.CurrentMapLayer;
        int originalIndex = manager.CurrentMapIndex;
        Ensure(manager.RecoverFromInvalidNodeEntry(out var error), error);
        Ensure(manager.CurrentMapLayer == originalLayer && manager.CurrentMapIndex == originalIndex,
            "ActiveNode 缺失恢复推进了地图路线");

        var battleNode = manager.MapGraph.Layers[1][0];
        Ensure(manager.TryEnterBattle(battleNode, out error), error);
        Ensure(manager.RecoverFromInvalidNodeEntry(out error), error);
        Ensure(!manager.RunState.NodeStates.ContainsKey(battleNode.NodeId), "非法类型恢复留下了活动节点状态");
        Ensure(manager.TryEnterBattle(battleNode, out error), error);
        manager.RecoverFromInvalidNodeEntry(out error);
        manager.Free();
    }

    private static void Ensure(bool condition, string error)
    {
        if (condition)
            return;

        throw new System.InvalidOperationException($"[StateContractSelfCheck] {error}");
    }
}
