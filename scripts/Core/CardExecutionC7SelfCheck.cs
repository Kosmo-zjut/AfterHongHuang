using Godot;
using System;

/// <summary>C7 专用门禁：非法计划必须在任何战斗事务前被 canonical validator 拒绝。</summary>
public static class CardExecutionC7SelfCheck
{
    public static void Run()
    {
        var validPolicy = new CardTargetPolicyDefinition
        {
            SelectionMode = CardSelectionMode.Required, Scope = CardTargetScope.SingleEnemy,
            MinimumTargets = 1, MaximumTargets = 1, AllowDeadTargets = false,
            RetargetOnInvalid = CardRetargetPolicy.RejectPlay,
        };
        Ensure(!CardDefinitionValidator.TryValidateExecutionPlan(new CardExecutionPlan("fixture", 1, validPolicy,
            new[] { Effect(2, CardEffectKind.DealDamage, CardEffectTarget.SelectedTarget, CardDurationScope.Battle) }), out _),
            "单项 Order=2 被接受。");
        Ensure(!CardDefinitionValidator.TryValidateExecutionPlan(new CardExecutionPlan("fixture", 1, validPolicy,
            new[] { Effect(1, CardEffectKind.DealDamage, CardEffectTarget.SelectedTarget, (CardDurationScope)99) }), out _),
            "未知 DurationScope 被接受。");
        Ensure(!CardDefinitionValidator.TryValidateExecutionPlan(new CardExecutionPlan("fixture", 1, validPolicy,
            new[] { Effect(1, CardEffectKind.GainBlock, CardEffectTarget.SelectedTarget, CardDurationScope.Battle) }), out _),
            "效果目标不匹配被接受。");
        VerifyPlayCardTransactions();
        GD.Print("[CardExecutionC7SelfCheck] PASS canonical validator and frozen-card transactions");
    }

    private static void VerifyPlayCardTransactions()
    {
        var manager = new GameManager();
        try
        {
            Ensure(manager.StartNewRun("wuzhu", 0xC700_0001UL), "无法创建卡牌事务自检新局。 ");
            var battleNode = FindNextBattle(manager);
            Ensure(manager.TryEnterBattle(battleNode, out var enterError), enterError);
            Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
            manager.StartPlayerTurn();
            manager.Hand.Clear();
            manager.DiscardPile.Clear();
            manager.ExhaustPile.Clear();
            manager.PlayerLingli = 3;

            var enemyCard = CreateEnemyCard("fixture_enemy_nonzero");
            manager.Hand.Add(enemyCard);
            int enemyHpBefore = manager.EnemyHp;
            Ensure(manager.TryPrepareCardExecution(enemyCard, out var enemyResolved, out var prepareEnemyError),
                prepareEnemyError);
            Ensure(manager.PlayCard(enemyCard, enemyResolved), "非零费敌方目标牌执行失败。 ");
            Ensure(manager.EnemyHp == enemyHpBefore - 1, "敌方目标牌未只结算一次伤害。 ");
            Ensure(manager.PlayerLingli == 2, "敌方目标牌灵力未只扣除一次。 ");
            Ensure(manager.DiscardPile.Contains(enemyCard), "敌方目标牌未进入弃牌堆。 ");

            var selfCard = CreateSelfCard("fixture_self_nonzero");
            manager.Hand.Add(selfCard);
            int selfGuardBefore = manager.PlayerHuti;
            Ensure(manager.TryPrepareCardExecution(selfCard, out var selfResolved, out var prepareSelfError),
                prepareSelfError);
            Ensure(manager.PlayCard(selfCard, selfResolved), "非零费自身目标牌执行失败。 ");
            Ensure(manager.PlayerHuti == selfGuardBefore + 2, "自身目标牌效果未只结算一次。 ");
            Ensure(manager.PlayerLingli == 1, "自身目标牌灵力未只扣除一次。 ");

            var noTargetCard = CreateNoTargetCard("fixture_no_target");
            manager.Hand.Add(noTargetCard);
            Ensure(manager.TryPrepareCardExecution(noTargetCard, out var noTargetResolved, out var prepareNoneError),
                prepareNoneError);
            Ensure(manager.PlayCard(noTargetCard, noTargetResolved), "无目标牌执行失败。 ");
            Ensure(manager.PlayerLingli == 0, "无目标牌灵力未只扣除一次。 ");
            Ensure(manager.ExhaustPile.Contains(noTargetCard), "无目标牌未按冻结移区效果消弭。 ");

            var staleCard = CreateEnemyCard("fixture_stale");
            manager.Hand.Add(staleCard);
            manager.PlayerLingli = 3;
            Ensure(manager.TryPrepareCardExecution(staleCard, out var staleResolved, out var prepareStaleError),
                prepareStaleError);
            int staleEnemyHuti = manager.EnemyHuti + 1;
            manager.EnemyHuti = staleEnemyHuti;
            int staleEnemyHp = manager.EnemyHp;
            int staleEnergy = manager.PlayerLingli;
            int staleHandCount = manager.Hand.Count;
            int staleDiscardCount = manager.DiscardPile.Count;
            int staleExhaustCount = manager.ExhaustPile.Count;
            int staleTraceCount = manager.LastCardExecutionTrace.Count;
            var staleLastResolved = manager.LastResolvedCardExecution;
            Ensure(!manager.PlayCard(staleCard, staleResolved), "陈旧冻结计划被错误执行。 ");
            Ensure(manager.EnemyHuti == staleEnemyHuti && manager.EnemyHp == staleEnemyHp &&
                manager.PlayerLingli == staleEnergy && manager.Hand.Count == staleHandCount &&
                manager.DiscardPile.Count == staleDiscardCount && manager.ExhaustPile.Count == staleExhaustCount &&
                manager.LastCardExecutionTrace.Count == staleTraceCount &&
                ReferenceEquals(manager.LastResolvedCardExecution, staleLastResolved),
                "陈旧冻结计划拒绝后仍发生了状态或诊断副作用。 ");
        }
        finally
        {
            manager.Free();
        }
    }

    private static MapNodeDefinition FindNextBattle(GameManager manager)
    {
        foreach (var edge in manager.MapGraph.GetOutgoing(manager.CurrentMapNodeId))
        {
            var node = manager.MapGraph.GetNode(edge.ToNodeId);
            if (node?.NodeType is MapGraphNodeType.Battle or MapGraphNodeType.Boss)
                return node;
        }
        throw new InvalidOperationException("[CardExecutionC7SelfCheck] 起点后缺少战斗节点。");
    }

    private static CardRuntime CreateEnemyCard(string id)
    {
        return new CardRuntime(new CardInfo { Id = id, DefinitionId = id, Name = id },
            new CardExecutionPlan(id, 1, new CardTargetPolicyDefinition
            {
                SelectionMode = CardSelectionMode.Required,
                Scope = CardTargetScope.SingleEnemy,
                MinimumTargets = 1,
                MaximumTargets = 1,
                AllowDeadTargets = false,
                RetargetOnInvalid = CardRetargetPolicy.RejectPlay,
            }, new[]
            {
                Effect(1, CardEffectKind.DealDamage, CardEffectTarget.SelectedTarget, CardDurationScope.Battle),
            }));
    }

    private static CardRuntime CreateSelfCard(string id)
    {
        return new CardRuntime(new CardInfo { Id = id, DefinitionId = id, Name = id },
            new CardExecutionPlan(id, 1, new CardTargetPolicyDefinition
            {
                SelectionMode = CardSelectionMode.None,
                Scope = CardTargetScope.Self,
                MinimumTargets = 0,
                MaximumTargets = 0,
                AllowDeadTargets = false,
                RetargetOnInvalid = CardRetargetPolicy.RejectPlay,
            }, new[]
            {
                Effect(1, CardEffectKind.GainBlock, CardEffectTarget.Self, CardDurationScope.Battle, amount: 2),
            }));
    }

    private static CardRuntime CreateNoTargetCard(string id)
    {
        return new CardRuntime(new CardInfo { Id = id, DefinitionId = id, Name = id },
            new CardExecutionPlan(id, 1, new CardTargetPolicyDefinition
            {
                SelectionMode = CardSelectionMode.None,
                Scope = CardTargetScope.None,
                MinimumTargets = 0,
                MaximumTargets = 0,
                AllowDeadTargets = false,
                RetargetOnInvalid = CardRetargetPolicy.RejectPlay,
            }, new[]
            {
                Effect(1, CardEffectKind.MoveSelfToZone, CardEffectTarget.Self, CardDurationScope.Battle,
                    destination: CardDestinationZone.Exhaust),
            }));
    }

    private static CardEffectDefinition Effect(int order, CardEffectKind type, CardEffectTarget target,
        CardDurationScope duration, int amount = 1, CardDestinationZone destination = CardDestinationZone.None) => new()
    {
        Order = order, EffectType = type, TargetSelector = target, Amount = amount,
        StatusKind = CardStatusKind.None, DurationScope = duration, DestinationZone = destination,
    };

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"[CardExecutionC7SelfCheck] {message}");
    }
}
