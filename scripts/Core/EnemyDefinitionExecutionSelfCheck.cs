using Godot;

/// <summary>
/// Debug 构建的敌人定义执行自检。
/// 目标是验证机制/阶段数据通过同一生产解析入口驱动实际行动，而不是在测试中复制生产常量。
/// </summary>
public static class EnemyDefinitionExecutionSelfCheck
{
    public static void Run()
    {
        var manager = new GameManager();
        EnsureFindAndCheck(manager, MatchesGuardBrokenMechanic, CheckGuardBrokenTrigger,
            "缺少可验证的护体破除机制定义");
        EnsureFindAndCheck(manager, MatchesHealthThresholdMechanic, CheckHealthThresholdTrigger,
            "缺少可验证的生命阈值机制定义");
        EnsureFindAndCheck(manager, HasMultiplePhases, CheckPhaseResolution,
            "缺少可验证的多阶段敌人定义", bossOnly: true);
        EnsureFindAndCheck(manager, HasSinglePhase, CheckPhaseResolution,
            "缺少可验证的单阶段阶段游标定义", preferStrong: true);
        EnsureFindAndCheck(manager, HasAttackAndGuard, CheckAttackAndGuard,
            "缺少可验证的攻击+护体意图定义");
        EnsureFindAndCheck(manager, MatchesOpeningThenLoop, CheckOpeningLoop,
            "缺少可验证的 OpeningThenLoop 敌人定义");
        CheckMultipleOpeningFixture(manager);
        CheckInvalidIntent(manager);
        manager.Free();
        GD.Print("[EnemyDefinitionExecutionSelfCheck] PASS definition resolution and execution");
    }

    private static void EnsureFindAndCheck(GameManager manager,
        System.Func<EnemyInfo, bool> predicate, System.Action<GameManager, EnemyInfo> check,
        string missingError, bool bossOnly = false, bool preferStrong = false)
    {
        for (ulong attempt = 0; attempt < 512; attempt++)
        {
            Ensure(manager.StartNewRun("wuzhu", 0xC3_2026_0713_0000UL + attempt),
                "无法创建敌人定义执行自检新局");
            var node = FindBattleNode(manager, bossOnly, preferStrong);
            Ensure(manager.TryEnterBattle(node, out var enterError), enterError);
            Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
            var enemy = manager.ActiveEncounter?.EnemyInfo;
            if (enemy != null && predicate(enemy))
            {
                check(manager, enemy);
                var result = manager.CreateNodeResult(NodeResultType.Abandoned,
                    "敌人定义执行自检离场", out var resultError);
                Ensure(result != null && manager.SubmitNodeResult(result, out resultError), resultError);
                return;
            }

            var skip = manager.CreateNodeResult(NodeResultType.Abandoned,
                "敌人定义执行自检换样本", out var skipError);
            Ensure(skip != null && manager.SubmitNodeResult(skip, out skipError), skipError);
        }

        throw new System.InvalidOperationException($"[EnemyDefinitionExecutionSelfCheck] {missingError}");
    }

    private static void CheckGuardBrokenTrigger(GameManager manager, EnemyInfo enemy)
    {
        var mechanic = enemy.MechanicDefinition;
        Ensure(mechanic.TriggeredIntent != null, "护体破除机制缺少触发意图");

        manager.EnemyTurnIndex = 1;
        Ensure(enemy.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
            out var guardIntent, out var guardError), guardError);
        Ensure(guardIntent.GuardValue > 0, "护体破除样本没有先执行护体意图");
        manager.ExecuteEnemyTurn();
        Ensure(manager.EnemyHuti == guardIntent.GuardValue, "护体意图未通过正式敌方行动入口生效");

        var breaker = new CardRuntime
        {
            Info = new CardInfo
            {
                Id = "fixture_guard_breaker",
                Name = "测试破盾牌",
                Type = CardType.斗击,
                TargetMode = CardTargetMode.Enemy,
                Cost = 0,
                Value = guardIntent.GuardValue + 1,
            }
        };
        manager.ActiveBattle.Hand.Add(breaker);
        manager.PlayerLingli = manager.PlayerMaxLingli;
        Ensure(manager.PlayCard(breaker), "正式受伤入口未能执行破盾卡牌");
        Ensure(manager.ActiveBattle.EnemyGuardWasBroken, "破盾后未记录 GuardBroken 状态");
        manager.ActiveBattle.EnemySpecialTriggered = false;
        Ensure(enemy.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
            out var resolved, out var resolveError), resolveError);
        Ensure(resolved.Value == mechanic.TriggeredIntent.Value && resolved.Name == mechanic.TriggeredIntent.Name,
            "护体破除后的解析意图未绑定机制定义");

        int hpBefore = manager.PlayerHp;
        manager.ExecuteEnemyTurn();
        Ensure(hpBefore - manager.PlayerHp == resolved.Value,
            "护体破除后的实际结算未消费同一解析意图");
    }

    private static void CheckHealthThresholdTrigger(GameManager manager, EnemyInfo enemy)
    {
        var mechanic = enemy.MechanicDefinition;
        Ensure(mechanic.TriggeredIntent != null && mechanic.HealthThreshold >= 0,
            "生命阈值机制定义不完整");
        manager.EnemyHp = mechanic.HealthThreshold;
        manager.ActiveBattle.EnemySpecialTriggered = false;
        manager.EnemyTurnIndex = 0;
        Ensure(enemy.TryResolveIntent(0, manager.ActiveBattle, out var resolved, out var resolveError), resolveError);
        Ensure(resolved.Value == mechanic.TriggeredIntent.Value && resolved.Name == mechanic.TriggeredIntent.Name,
            "生命阈值后的解析意图未绑定机制定义");
        string presentation = IntentPresentation.Format(resolved);
        Ensure(presentation.Contains($"造成{resolved.Value}点伤害") &&
            presentation.Contains($"获得{resolved.MechanicGuardValue}点护体"),
            "生命阈值意图展示遗漏伤害或机制护体");

        int hpBefore = manager.PlayerHp;
        manager.ExecuteEnemyTurn();
        Ensure(hpBefore - manager.PlayerHp == resolved.Value,
            "生命阈值后的实际结算未消费同一解析意图");
        Ensure(manager.EnemyHuti == resolved.MechanicGuardValue,
            "生命阈值后的护体未来自解析意图");
    }

    private static void CheckPhaseResolution(GameManager manager, EnemyInfo enemy)
    {
        Ensure(enemy.PhaseDefinitions != null && enemy.PhaseDefinitions.Count >= 1,
            "多阶段敌人缺少阶段定义");
        Ensure(enemy.Intents != null && enemy.Intents.Count > 0, "阶段型敌人缺少基础阶段序列");
        foreach (int globalTurn in new[] { 1, 4, 5 })
            CheckSequence(manager, enemy, enemy.Intents, 0, string.Empty, enemy.MaxHp, globalTurn);

        foreach (var phase in enemy.PhaseDefinitions)
        {
            Ensure(phase != null && phase.Intents != null && phase.Intents.Count > 0,
                "阶段定义缺少意图序列");
            foreach (int globalTurn in new[] { 1, 4, 5 })
                CheckSequence(manager, enemy, phase.Intents, phase.PhaseIndex, phase.Id,
                    phase.HealthAtOrBelow, globalTurn);
        }
    }

    private static void CheckSequence(GameManager manager, EnemyInfo enemy,
        System.Collections.Generic.IReadOnlyList<EnemyIntent> expectedIntents,
        int expectedPhase, string expectedPhaseId, int enemyHp, int globalTurn)
    {
        manager.EnemyHp = enemyHp;
        manager.ActiveBattle.EnemyPhaseId = string.Empty;
        manager.ActiveBattle.EnemyPhase = 0;
        manager.ActiveBattle.EnemyPhaseTurnIndex = 0;
        manager.ActiveBattle.EnemyPhaseOpeningTurnIndex = 0;
        manager.ActiveBattle.EnemyPhaseOpeningCompleted = true;
        manager.ActiveBattle.EnemySpecialTriggered = true;
        manager.EnemyTurnIndex = globalTurn;
        manager.PlayerHp = manager.PlayerMaxHp;
        manager.PlayerHuti = 0;

        for (int sequenceTurn = 0; sequenceTurn < 3; sequenceTurn++)
        {
            Ensure(enemy.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
                out var resolved, out var resolveError), resolveError);
            var expected = expectedIntents[sequenceTurn % expectedIntents.Count];
            Ensure(resolved.PhaseIndex == expectedPhase &&
                (resolved.SequenceId ?? string.Empty) == expectedPhaseId &&
                resolved.Name == expected.Name && resolved.SequenceIndex == sequenceTurn % expectedIntents.Count,
                $"阶段 {expectedPhaseId} 全局回合 {globalTurn} 的阶段内游标错误");
            Ensure(IntentPresentation.Format(resolved) == IntentPresentation.Format(expected),
                $"阶段 {expectedPhaseId} 的 UI/解析意图摘要不一致");
            int hpBefore = manager.PlayerHp;
            manager.ExecuteEnemyTurn();
            if (expected.IntentType == EnemyIntentType.攻击)
                Ensure(hpBefore - manager.PlayerHp == expected.Value,
                    $"阶段 {expectedPhaseId} 的实际伤害不匹配解析意图");
            if (expected.GuardValue > 0 || expected.MechanicGuardValue > 0)
                Ensure(manager.EnemyHuti == expected.GuardValue + expected.MechanicGuardValue,
                    $"阶段 {expectedPhaseId} 的实际护体不匹配解析意图");
        }
    }

    private static void CheckOpeningLoop(GameManager manager, EnemyInfo enemy)
    {
        Ensure(enemy.OpeningIntents != null && enemy.OpeningIntents.Count > 0 &&
            enemy.Intents != null && enemy.Intents.Count > 0, "Opening/Loop 定义不完整");

        for (int turn = 0; turn < 7; turn++)
        {
            manager.EnemyTurnIndex = new[] { 1, 4, 5, 7, 8, 9, 10 }[turn];
            Ensure(enemy.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
                out var resolved, out var resolveError), resolveError);
            if (turn == 0)
            {
                Ensure(resolved.IsOpeningIntent && resolved.Name == enemy.OpeningIntents[0].Name,
                    "Opening 意图没有只在首回合出现");
                string presentation = IntentPresentation.Format(resolved);
                Ensure(presentation.Contains("不攻击") &&
                    (!string.IsNullOrWhiteSpace(resolved.MechanicDisplayName)
                        ? presentation.Contains($"激活{resolved.MechanicDisplayName}")
                        : presentation.Contains("激活机制")),
                    "Opening 意图摘要遗漏不攻击或机制激活");
            }
            else
            {
                var expected = enemy.Intents[(turn - 1) % enemy.Intents.Count];
                Ensure(!resolved.IsOpeningIntent && resolved.Name == expected.Name &&
                    resolved.Value == expected.Value, "Loop 意图未按阶段内游标循环");
            }

            manager.ExecuteEnemyTurn();
        }
    }

    private static void CheckAttackAndGuard(GameManager manager, EnemyInfo enemy)
    {
        for (int turn = 0; turn < 8; turn++)
        {
            Ensure(enemy.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
                out var resolved, out var resolveError), resolveError);
            if (resolved.IntentType == EnemyIntentType.攻击 && resolved.Value > 0 && resolved.GuardValue > 0)
            {
                manager.PlayerHuti = 0;
                int hpBefore = manager.PlayerHp;
                manager.ExecuteEnemyTurn();
                Ensure(hpBefore - manager.PlayerHp == resolved.Value,
                    "攻击+护体意图的伤害未消费解析结果");
                Ensure(manager.EnemyHuti == resolved.GuardValue + resolved.MechanicGuardValue,
                    "攻击+护体意图的护体未消费解析结果");
                return;
            }
            manager.ExecuteEnemyTurn();
        }
        Ensure(false, $"敌人 {enemy.Id} 在定义序列中未找到攻击+护体意图");
    }

    private static void CheckMultipleOpeningFixture(GameManager manager)
    {
        var fixture = new EnemyInfo
        {
            Id = "fixture_multi_opening",
            Name = "多 Opening 测试定义",
            MaxHp = 30,
            SequencePolicy = EnemyIntentSequencePolicy.OpeningThenLoop,
            OpeningIntents = new System.Collections.Generic.List<EnemyIntent>
            {
                new() { Name = "开场一", IntentType = EnemyIntentType.强化, Description = "第一开场效果" },
                new() { Name = "开场二", IntentType = EnemyIntentType.强化, Description = "第二开场效果" },
            },
            Intents = new System.Collections.Generic.List<EnemyIntent>
            {
                new() { Name = "循环攻击", IntentType = EnemyIntentType.攻击, Value = 1 },
            },
        };
        Ensure(EnemyDefinitionValidator.TryValidate(fixture, out var validationError), validationError);
        manager.ActiveBattle.EnemyInfo = fixture;
        manager.ActiveBattle.EnemyHp = fixture.MaxHp;
        manager.ActiveBattle.EnemyTurnIndex = 0;
        manager.ActiveBattle.EnemyOpeningTurnIndex = 0;
        manager.ActiveBattle.EnemyOpeningCompleted = false;
        manager.ActiveBattle.EnemyPhaseId = string.Empty;
        manager.ActiveBattle.EnemyPhaseTurnIndex = 0;
        for (int index = 0; index < fixture.OpeningIntents.Count; index++)
        {
            Ensure(fixture.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
                out var resolved, out var resolveError), resolveError);
            Ensure(resolved.IsOpeningIntent && resolved.SequenceIndex == index &&
                resolved.Name == fixture.OpeningIntents[index].Name,
                "多 Opening 定义未按 0..N-1 顺序执行");
            manager.ExecuteEnemyTurn();
        }
        Ensure(manager.ActiveBattle.EnemyOpeningCompleted, "多 Opening 执行后未进入 Loop");
        Ensure(fixture.TryResolveIntent(manager.EnemyTurnIndex, manager.ActiveBattle,
            out var loopIntent, out var loopError), loopError);
        Ensure(!loopIntent.IsOpeningIntent && loopIntent.Name == fixture.Intents[0].Name,
            "多 Opening 执行后未进入 Loop 序列");
    }

    private static void CheckInvalidIntent(GameManager manager)
    {
        var invalid = new EnemyInfo
        {
            Id = "fixture_empty_intent",
            Intents = new System.Collections.Generic.List<EnemyIntent>(),
            PhaseDefinitions = new System.Collections.Generic.List<EnemyPhaseDefinition>(),
        };
        Ensure(!invalid.TryResolveIntent(0, new BattleState { EnemyHp = 1 }, out _, out var error) &&
            !string.IsNullOrWhiteSpace(error), "空意图未被显式拒绝");

        var invalidType = new EnemyInfo
        {
            Id = "fixture_invalid_type",
            Intents = new System.Collections.Generic.List<EnemyIntent>
            {
                new() { Name = "非法意图", IntentType = (EnemyIntentType)99, Value = 1 }
            },
        };
        Ensure(!EnemyDefinitionValidator.TryValidate(invalidType, out _), "非法 IntentType 未被集中验证器拒绝");

        var missingPhases = new EnemyInfo
        {
            Id = "fixture_missing_phases",
            RequiresPhaseDefinitions = true,
            Intents = new System.Collections.Generic.List<EnemyIntent>
            {
                new() { Name = "测试攻击", IntentType = EnemyIntentType.攻击, Value = 1 }
            },
        };
        Ensure(!EnemyDefinitionValidator.TryValidate(missingPhases, out _),
            "阶段型定义缺少 PhaseDefinitions 时未被拒绝");

        var invalidFixtures = new[]
        {
            invalidType,
            new EnemyInfo
            {
                Id = "fixture_invalid_mechanic_trigger",
                Name = "非法机制触发",
                MaxHp = 10,
                Mechanic = EnemyMechanicKind.GuardFormation,
                MechanicDefinition = new EnemyMechanicDefinition
                {
                    Id = "fixture_mechanic",
                    DisplayName = "测试机制",
                    Description = "测试机制",
                    TriggerType = (EnemyMechanicTriggerType)99,
                },
                Intents = new System.Collections.Generic.List<EnemyIntent>
                {
                    new() { Name = "测试攻击", IntentType = EnemyIntentType.攻击, Value = 1 },
                },
            },
            new EnemyInfo
            {
                Id = "fixture_invalid_condition",
                Name = "非法条件伤害",
                MaxHp = 10,
                Intents = new System.Collections.Generic.List<EnemyIntent>
                {
                    new()
                    {
                        Name = "测试攻击", IntentType = EnemyIntentType.攻击, Value = 1,
                        AlternateDamageCondition = (EnemyAlternateDamageCondition)99,
                        AlternateValue = 1,
                    },
                },
            },
        };

        foreach (var fixture in invalidFixtures)
            EnsureRejectedWithoutMutation(manager, fixture);
    }

    private static void EnsureRejectedWithoutMutation(GameManager manager, EnemyInfo invalid)
    {
        var battle = manager.ActiveBattle;
        battle.EnemyInfo = invalid;
        battle.EnemyHp = 17;
        battle.EnemyHuti = 6;
        battle.EnemyYirong = 2;
        battle.EnemyTurnIndex = 4;
        battle.EnemyPhase = 1;
        battle.EnemyPhaseId = "fixture_phase";
        battle.EnemyPhaseTurnIndex = 3;
        battle.EnemyPhaseOpeningTurnIndex = 2;
        battle.EnemyPhaseOpeningCompleted = false;
        battle.EnemyOpeningTurnIndex = 1;
        battle.EnemyOpeningCompleted = false;
        battle.EnemyMechanicActive = true;
        battle.EnemySpecialTriggered = true;
        battle.EnemyGuardWasBroken = true;
        battle.EnemyMechanicCounters["fixture"] = 4;
        manager.PlayerHp = 37;
        manager.PlayerHuti = 5;
        manager.PlayerYirongCeng = 2;
        var before = new InvalidStateSnapshot(manager);
        Ensure(!manager.TryExecuteEnemyTurn(out _), $"非法定义 {invalid.Id} 错误执行成功");
        Ensure(before.Equals(new InvalidStateSnapshot(manager)),
            $"非法定义 {invalid.Id} 拒绝后战斗状态发生变化");
    }

    private sealed record InvalidStateSnapshot(
        int PlayerHp, int PlayerHuti, int PlayerYirong, int EnemyHp, int EnemyHuti,
        int EnemyYirong, int EnemyTurnIndex, int EnemyPhase, string EnemyPhaseId,
        int PhaseTurnIndex, int PhaseOpeningIndex, bool PhaseOpeningCompleted,
        int OpeningIndex, bool OpeningCompleted, bool MechanicActive,
        bool SpecialTriggered, bool GuardBroken, int Counter)
    {
        public InvalidStateSnapshot(GameManager manager) : this(
            manager.PlayerHp, manager.PlayerHuti, manager.PlayerYirongCeng,
            manager.ActiveBattle.EnemyHp, manager.ActiveBattle.EnemyHuti,
            manager.ActiveBattle.EnemyYirong, manager.ActiveBattle.EnemyTurnIndex,
            manager.ActiveBattle.EnemyPhase, manager.ActiveBattle.EnemyPhaseId,
            manager.ActiveBattle.EnemyPhaseTurnIndex, manager.ActiveBattle.EnemyPhaseOpeningTurnIndex,
            manager.ActiveBattle.EnemyPhaseOpeningCompleted, manager.ActiveBattle.EnemyOpeningTurnIndex,
            manager.ActiveBattle.EnemyOpeningCompleted, manager.ActiveBattle.EnemyMechanicActive,
            manager.ActiveBattle.EnemySpecialTriggered, manager.ActiveBattle.EnemyGuardWasBroken,
            ReadCounter(manager)) { }

        private static int ReadCounter(GameManager manager)
        {
            return manager.ActiveBattle.EnemyMechanicCounters.TryGetValue("fixture", out var value)
                ? value
                : 0;
        }
    }

    private static MapNodeDefinition FindBattleNode(GameManager manager, bool bossOnly, bool preferStrong)
    {
        MapNodeDefinition first = null;
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
        {
            if (node.NodeType != (bossOnly ? MapGraphNodeType.Boss : MapGraphNodeType.Battle))
                continue;
            first ??= node;
            if (preferStrong && node.Tier == EncounterTier.Strong)
                return node;
        }

        return first ?? throw new System.InvalidOperationException("生产 MapGraph 缺少战斗节点。");
    }

    private static bool MatchesGuardBrokenMechanic(EnemyInfo enemy) =>
        enemy.MechanicDefinition?.TriggerType == EnemyMechanicTriggerType.GuardBroken;

    private static bool MatchesHealthThresholdMechanic(EnemyInfo enemy) =>
        enemy.MechanicDefinition?.TriggerType == EnemyMechanicTriggerType.HealthThreshold;

    private static bool HasMultiplePhases(EnemyInfo enemy) =>
        enemy.PhaseDefinitions != null && enemy.PhaseDefinitions.Count >= 2;

    private static bool HasSinglePhase(EnemyInfo enemy) =>
        enemy.PhaseDefinitions != null && enemy.PhaseDefinitions.Count == 1;

    private static bool HasAttackAndGuard(EnemyInfo enemy)
    {
        foreach (var intent in enemy.Intents ?? new System.Collections.Generic.List<EnemyIntent>())
            if (intent?.IntentType == EnemyIntentType.攻击 && intent.Value > 0 && intent.GuardValue > 0)
                return true;
        return false;
    }

    private static bool MatchesOpeningThenLoop(EnemyInfo enemy) =>
        enemy.SequencePolicy == EnemyIntentSequencePolicy.OpeningThenLoop;

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[EnemyDefinitionExecutionSelfCheck] {error}");
    }
}
