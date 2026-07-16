using Godot;

/// <summary>验证最终意图缓存的同一对象消费和条件伤害快照，不复制生产数值。</summary>
public static class PreparedIntentSelfCheck
{
    public static void Run()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xC6_1E00_0000UL), "无法创建最终意图自检新局");
        var node = FindBattleNode(manager);
        Ensure(manager.TryEnterBattle(node, out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        manager.ActiveBattle.PreparedEnemyIntent = null;
        manager.ActiveBattle.IntentStateVersion++;
        Ensure(manager.TryPrepareEnemyIntent(out var prepared, out var prepareError), prepareError);
        string resolutionId = prepared.ResolutionId;
        string presentation = IntentPresentation.Format(prepared);
        Ensure(ReferenceEquals(prepared, manager.ActiveBattle.PreparedEnemyIntent),
            "Prepare 未缓存同一最终意图对象");
        Ensure(manager.TryPrepareEnemyIntent(out var same, out prepareError) &&
            ReferenceEquals(prepared, same) && same.ResolutionId == resolutionId,
            "重复 Prepare 未复用同一 ResolutionId");

        int hpBefore = manager.PlayerHp;
        int finalDamage = prepared.FinalDamage;
        Ensure(manager.TryExecutePreparedEnemyIntent(prepared, out var executeError), executeError);
        Ensure(hpBefore - manager.PlayerHp == finalDamage,
            "最终意图展示值与实际 HP 差值不一致");
        Ensure(presentation == IntentPresentation.Format(prepared),
            "执行前后同一最终意图的展示结果不一致");
        manager.Free();
        CheckConditionalSnapshot();
        CheckAttackConditionUsesDisplayedResolution();
        CheckUnversionedStateMutationsInvalidateResolution();
        GD.Print("[PreparedIntentSelfCheck] PASS frozen resolution/presentation/execution");
    }

    /// <summary>
    /// 直接修改战斗字段是开发期测试仍可能做的事情；这些字段不公开给外部模块，
    /// 但本自检故意绕过正常命令验证完整快照仍能拒绝旧 Resolution，而不是依赖手工清缓存。
    /// </summary>
    private static void CheckUnversionedStateMutationsInvalidateResolution()
    {
        var mutations = new (string Name, System.Action<GameManager> Apply)[]
        {
            ("玩家护体", manager => manager.PlayerHuti++),
            ("本回合攻击标记", manager =>
                manager.ActiveBattle.PlayerAttackedThisTurn = !manager.ActiveBattle.PlayerAttackedThisTurn),
            ("机制激活", manager =>
                manager.ActiveBattle.EnemyMechanicActive = !manager.ActiveBattle.EnemyMechanicActive),
            ("敌人 HP 跨阶段", manager => manager.EnemyHp = 0),
            ("阶段编号", manager => manager.ActiveBattle.EnemyPhase++),
            ("特殊机制触发", manager =>
                manager.ActiveBattle.EnemySpecialTriggered = !manager.ActiveBattle.EnemySpecialTriggered),
            ("阶段游标", manager => manager.ActiveBattle.EnemyPhaseTurnIndex++),
            ("敌人身份", manager => manager.ActiveBattle.EnemyInfo = null),
        };

        foreach (var mutation in mutations)
        {
            var manager = new GameManager();
            Ensure(manager.StartNewRun("wuzhu", 0xC8_1E20_0000UL + (ulong)mutation.Name.Length),
                $"无法创建未版本化状态自检新局：{mutation.Name}");
            Ensure(manager.TryEnterBattle(FindBattleNode(manager), out var enterError), enterError);
            Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
            Ensure(manager.TryPrepareEnemyIntent(out var prepared, out var prepareError), prepareError);

            int versionBefore = manager.ActiveBattle.IntentStateVersion;
            int turnBefore = manager.EnemyTurnIndex;
            int hpBefore = manager.PlayerHp;
            mutation.Apply(manager);
            Ensure(manager.ActiveBattle.IntentStateVersion == versionBefore,
                $"{mutation.Name} 意外手工推进了意图版本");
            Ensure(!manager.TryExecutePreparedEnemyIntent(prepared, out _),
                $"{mutation.Name} 直接变化后旧 Resolution 仍可执行");
            Ensure(manager.EnemyTurnIndex == turnBefore && manager.PlayerHp == hpBefore,
                $"{mutation.Name} 拒绝旧 Resolution 时推进了战斗状态");
            manager.Free();
        }
    }

    private static void CheckAttackConditionUsesDisplayedResolution()
    {
        var manager = new GameManager();
        bool checkedCondition = false;
        for (ulong attempt = 0; attempt < 512 && !checkedCondition; attempt++)
        {
            Ensure(manager.StartNewRun("wuzhu", 0xC7_1E10_0000UL + attempt), "无法创建残军合击自检新局");
            Ensure(manager.TryEnterBattle(FindStrongBattleNode(manager), out var enterError), enterError);
            Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
            ResolvedEnemyIntent initial = null;
            string prepareError = "";
            for (int turn = 0; turn < 8; turn++)
            {
                Ensure(manager.TryPrepareEnemyIntent(out initial, out prepareError), prepareError);
                if (initial.Intent.BonusIfNoPlayerAttack > 0)
                    break;
                Ensure(manager.TryExecutePreparedEnemyIntent(initial, out prepareError), prepareError);
            }

            if (initial == null || initial.Intent.BonusIfNoPlayerAttack <= 0)
            {
                var skipped = manager.CreateNodeResult(NodeResultType.Abandoned, "攻击条件自检换样本", out var resultError);
                Ensure(skipped != null && manager.SubmitNodeResult(skipped, out resultError), resultError);
                continue;
            }

            manager.ActiveBattle.PlayerAttackedThisTurn = true;
            manager.ActiveBattle.PreparedEnemyIntent = null;
            manager.ActiveBattle.IntentStateVersion++;
            Ensure(manager.TryPrepareEnemyIntent(out var displayed, out prepareError), prepareError);
            string displayedId = displayed.ResolutionId;
            string displayedText = IntentPresentation.Format(displayed);
            Ensure(displayed.FinalDamage < initial.FinalDamage &&
                displayedText.Contains($"本次造成{displayed.FinalDamage}点伤害"),
                "玩家已攻击后的最终意图未立即使用当前回合条件");
            Ensure(manager.TryCommitEnemyTurn(out var executed, out var executeError), executeError);
            Ensure(ReferenceEquals(displayed, executed) && executed.ResolutionId == displayedId &&
                IntentPresentation.Format(executed) == displayedText,
                "结束回合重新解释了与 UI 不同的敌方意图事实");
            checkedCondition = true;
        }
        Ensure(checkedCondition, "未找到可验证当前回合攻击条件的生产意图");
        manager.Free();
    }

    private static void CheckConditionalSnapshot()
    {
        var manager = new GameManager();
        bool checkedConditional = false;
        for (ulong attempt = 0; attempt < 512 && !checkedConditional; attempt++)
        {
            Ensure(manager.StartNewRun("wuzhu", 0xC6_1E10_0000UL + attempt), "无法创建条件意图自检新局");
            Ensure(manager.TryEnterBattle(FindConditionalBattleNode(manager), out var enterError), enterError);
            Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
            if (HasConditionalIntent(manager.ActiveEncounter.EnemyInfo))
                checkedConditional = TryCheckConditionalState(manager);
            if (!checkedConditional)
            {
                var result = manager.CreateNodeResult(NodeResultType.Abandoned, "条件意图自检换样本", out var resultError);
                Ensure(result != null && manager.SubmitNodeResult(result, out resultError), resultError);
            }
        }
        Ensure(checkedConditional, "未找到可验证条件伤害的生产遭遇");
        manager.Free();
    }

    private static bool TryCheckConditionalState(GameManager manager)
    {
        int turnLimit = 8;
        for (int turn = 0; turn < turnLimit; turn++)
        {
            manager.ActiveBattle.PreparedEnemyIntent = null;
            manager.ActiveBattle.IntentStateVersion++;
            if (!manager.TryPrepareEnemyIntent(out var prepared, out var error))
                throw new System.InvalidOperationException(error);
            if (prepared.Intent.AlternateDamageCondition == EnemyAlternateDamageCondition.None)
            {
                if (!manager.TryExecutePreparedEnemyIntent(prepared, out error))
                    throw new System.InvalidOperationException(error);
                continue;
            }

            manager.EnemyHuti = 3;
            manager.ActiveBattle.EnemyGuardWasBroken = false;
            manager.ActiveBattle.PreparedEnemyIntent = null;
            manager.ActiveBattle.IntentStateVersion++;
            Ensure(manager.TryPrepareEnemyIntent(out var guarded, out error), error);
            Ensure(!guarded.DamageConditionTriggered, "保留护体时条件伤害错误触发");

            manager.EnemyHuti = 0;
            manager.ActiveBattle.EnemyGuardWasBroken = true;
            manager.ActiveBattle.PreparedEnemyIntent = null;
            manager.ActiveBattle.IntentStateVersion++;
            Ensure(manager.TryPrepareEnemyIntent(out var broken, out error), error);
            Ensure(broken.DamageConditionTriggered && broken.FinalDamage == broken.Intent.AlternateValue,
                "正式破盾状态未解析为条件伤害最终值");
            string presentation = IntentPresentation.Format(broken);
            Ensure(presentation.Contains("已触发"), "条件伤害展示未反映触发状态");
            int hpBefore = manager.PlayerHp;
            Ensure(manager.TryExecutePreparedEnemyIntent(broken, out error), error);
            Ensure(hpBefore - manager.PlayerHp == broken.FinalDamage,
                "条件伤害实际结算未消费最终解析值");
            return true;
        }
        return false;
    }

    private static bool HasConditionalIntent(EnemyInfo enemy)
    {
        foreach (var intent in enemy.Intents)
            if (intent != null && intent.AlternateDamageCondition != EnemyAlternateDamageCondition.None)
                return true;
        return false;
    }

    private static MapNodeDefinition FindBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Battle)
                return node;
        throw new System.InvalidOperationException("最终意图自检找不到战斗节点");
    }

    private static MapNodeDefinition FindStrongBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Battle && node.Tier == EncounterTier.Strong)
                return node;
        return FindBattleNode(manager);
    }

    private static MapNodeDefinition FindConditionalBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Battle && node.Tier == EncounterTier.Strong)
                return node;
        return FindBattleNode(manager);
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[PreparedIntentSelfCheck] {error}");
    }
}
