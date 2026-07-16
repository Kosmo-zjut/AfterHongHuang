using Godot;

/// <summary>验证结束回合失败时玩家牌堆、资源和敌方游标全部回滚。</summary>
public static class EnemyTurnTransactionSelfCheck
{
    public static void Run()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xC6_EE00_0001UL), "无法创建敌方回合事务自检新局");
        Ensure(manager.TryEnterBattle(FindBattleNode(manager), out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        manager.DrawCards(2);
        int hp = manager.PlayerHp;
        int lingli = manager.PlayerLingli;
        int deck = manager.GetDeckSize();
        int draw = manager.DrawPile.Count;
        int hand = manager.Hand.Count;
        int discard = manager.DiscardPile.Count;
        int exhaust = manager.ExhaustPile.Count;
        int version = manager.ActiveBattle.IntentStateVersion;
        manager.ActiveBattle.EnemyPhaseId = "fixture_unknown_phase";

        Ensure(!manager.TryCommitEnemyTurn(out _), "未知阶段错误提交了敌方回合");
        Ensure(manager.PlayerHp == hp && manager.PlayerLingli == lingli && manager.GetDeckSize() == deck &&
            manager.DrawPile.Count == draw && manager.Hand.Count == hand &&
            manager.DiscardPile.Count == discard && manager.ExhaustPile.Count == exhaust &&
            manager.ActiveBattle.IntentStateVersion == version &&
            manager.ActiveBattle.EnemyPhaseId == "fixture_unknown_phase" && manager.IsPlayerTurn,
            "敌方回合失败后未完整恢复玩家/敌方状态");

        manager.Free();
        GD.Print("[EnemyTurnTransactionSelfCheck] PASS full rollback on prepare failure");
    }

    private static MapNodeDefinition FindBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Battle)
                return node;
        throw new System.InvalidOperationException("敌方回合事务自检找不到战斗节点");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[EnemyTurnTransactionSelfCheck] {error}");
    }
}
