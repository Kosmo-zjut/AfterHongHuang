using System.Collections.Generic;

/// <summary>一条遭遇池条目。权重和内容都属于数据，不由 BattleScene 推断。</summary>
public sealed class EncounterPoolEntry
{
    public EnemyDefinition Definition { get; init; }
    public int Weight { get; init; }
}

/// <summary>敌人定义层。运行时会复制为 EnemyInfo，避免战斗临时状态污染共享数据。</summary>
public sealed class EnemyDefinition
{
    public string Id { get; init; }
    public string Name { get; init; }
    public int MaxHp { get; init; }
    public EncounterTier Tier { get; init; }
    public EnemyMechanicKind Mechanic { get; init; }
    public EnemyMechanicDefinition MechanicDefinition { get; init; }
    public int RewardMin { get; init; }
    public int RewardMax { get; init; }
    public string RewardProfileId { get; init; }
    public bool RequiresPhaseDefinitions { get; init; }
    public EnemyIntentSequencePolicy SequencePolicy { get; init; } = EnemyIntentSequencePolicy.Loop;
    public List<EnemyIntent> OpeningIntents { get; init; } = new();
    public List<EnemyIntent> Intents { get; init; } = new();
    public List<EnemyPhaseDefinition> PhaseDefinitions { get; init; } = new();

    public EnemyInfo CreateRuntimeInfo()
    {
        return new EnemyInfo
        {
            Id = Id,
            Name = Name,
            MaxHp = MaxHp,
            Mechanic = Mechanic,
            MechanicDefinition = CloneMechanic(MechanicDefinition),
            RewardMin = RewardMin,
            RewardMax = RewardMax,
            RewardProfileId = RewardProfileId,
            RequiresPhaseDefinitions = RequiresPhaseDefinitions,
            SequencePolicy = SequencePolicy,
            OpeningIntents = CloneIntents(OpeningIntents),
            Intents = CloneIntents(Intents),
            PhaseDefinitions = ClonePhases(PhaseDefinitions),
        };
    }

    private static List<EnemyIntent> CloneIntents(List<EnemyIntent> source)
    {
        var result = new List<EnemyIntent>();
        if (source == null)
            return result;

        foreach (var intent in source)
        {
            result.Add(intent?.Clone());
        }
        return result;
    }

    private static List<EnemyPhaseDefinition> ClonePhases(List<EnemyPhaseDefinition> source)
    {
        var result = new List<EnemyPhaseDefinition>();
        if (source == null)
            return result;

        foreach (var phase in source)
        {
            if (phase == null)
            {
                result.Add(null);
                continue;
            }

            result.Add(new EnemyPhaseDefinition
            {
                Id = phase.Id,
                PhaseIndex = phase.PhaseIndex,
                HealthAtOrBelow = phase.HealthAtOrBelow,
                ResetGuardOnEnter = phase.ResetGuardOnEnter,
                SequencePolicy = phase.SequencePolicy,
                OpeningIntents = CloneIntents(phase.OpeningIntents),
                Intents = CloneIntents(phase.Intents),
            });
        }
        return result;
    }

    private static EnemyMechanicDefinition CloneMechanic(EnemyMechanicDefinition source)
    {
        if (source == null)
            return null;

        return new EnemyMechanicDefinition
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Description = source.Description,
            TriggerType = source.TriggerType,
            HealthThreshold = source.HealthThreshold,
            TriggeredIntent = source.TriggeredIntent?.Clone(),
            CounterThreshold = source.CounterThreshold,
            CounterKey = source.CounterKey,
            DamageOverride = source.DamageOverride,
            HasDamageOverride = source.HasDamageOverride,
        };
    }
}

/// <summary>
/// ACT1 遭遇池。选择只消费 Encounter 命名流，返回的新 EnemyInfo 是本场副本。
/// 目前不包含多敌人，后续可在 EncounterDefinition 上扩展队伍成员而不改地图入口。
/// </summary>
public sealed class EncounterPool
{
    public const string WeakPoolId = "encounter_weak";
    public const string StrongPoolId = "encounter_strong";
    public const string BossPoolId = "boss_pool";

    private readonly Dictionary<string, List<EncounterPoolEntry>> _pools = new();

    public static EncounterPool Act1 { get; } = CreateAct1();

    public bool TryResolve(string poolId, StableRandom rng, out EnemyInfo enemy, out string error)
    {
        enemy = null;
        error = "";
        if (!_pools.TryGetValue(poolId ?? "", out var entries) || entries.Count == 0)
        {
            error = $"遭遇池不存在或为空：{poolId}";
            return false;
        }

        int totalWeight = 0;
        foreach (var entry in entries)
            totalWeight += entry.Weight;
        if (totalWeight <= 0)
        {
            error = $"遭遇池权重无效：{poolId}";
            return false;
        }

        int roll = rng.NextInt(0, totalWeight);
        foreach (var entry in entries)
        {
            roll -= entry.Weight;
            if (roll < 0)
            {
                if (!EnemyDefinitionValidator.TryValidate(entry.Definition, out error))
                    return false;
                enemy = entry.Definition.CreateRuntimeInfo();
                if (!EnemyDefinitionValidator.TryValidate(enemy, out error))
                {
                    enemy = null;
                    return false;
                }
                return true;
            }
        }

        error = $"遭遇池抽取越界：{poolId}";
        return false;
    }

    public bool TryResolveForNode(NodeContext node, StableRandomStreams streams,
        out EnemyInfo enemy, out string error)
    {
        enemy = null;
        error = "";
        if (node == null)
        {
            error = "遭遇请求缺少 NodeContext。";
            return false;
        }

        return TryResolve(node.PoolId, streams.CreateEncounterStream(node.NodeId, 0), out enemy, out error);
    }

    public bool TryGetDefinition(string id, out EnemyDefinition definition)
    {
        foreach (var pool in _pools.Values)
        foreach (var entry in pool)
        {
            if (entry.Definition.Id == id)
            {
                definition = entry.Definition;
                return true;
            }
        }

        definition = null;
        return false;
    }

    private static EncounterPool CreateAct1()
    {
        var pool = new EncounterPool();
        pool._pools[WeakPoolId] = new List<EncounterPoolEntry>
        {
            new() { Weight = 45, Definition = Monkey() },
            new() { Weight = 30, Definition = Stone() },
            new() { Weight = 25, Definition = Scavenger() },
        };
        pool._pools[StrongPoolId] = new List<EncounterPoolEntry>
        {
            new() { Weight = 40, Definition = Guard() },
            new() { Weight = 35, Definition = Talisman() },
            new() { Weight = 25, Definition = MountainKing() },
        };
        pool._pools[BossPoolId] = new List<EncounterPoolEntry>
        {
            new() { Weight = 1, Definition = Xiangliu() },
        };
        return pool;
    }

    private static EnemyDefinition Monkey() => new()
    {
        Id = "act1_weak_monkey", Name = "山野妖猴", MaxHp = 60, Tier = EncounterTier.Weak,
        Mechanic = EnemyMechanicKind.MonkeyDodge, RewardMin = 12, RewardMax = 16, RewardProfileId = "battle_default",
        MechanicDefinition = Mechanic("monkey_dodge", "灵猴闪避", "每受3次斗击，第3次伤害归零",
            counterKey: "dodge", counterThreshold: 3, damageOverride: 0),
        SequencePolicy = EnemyIntentSequencePolicy.OpeningThenLoop,
        OpeningIntents = new List<EnemyIntent>
        {
            Strengthen("嬉斗", "", EnemyMechanicAction.ActivateDodge),
        },
        Intents = new List<EnemyIntent>
        {
            Attack(7, "猛扑"), Attack(13, "啃咬"),
        },
    };

    private static EnemyDefinition Stone() => new()
    {
        Id = "act1_weak_stone", Name = "碎石山精", MaxHp = 52, Tier = EncounterTier.Weak,
        Mechanic = EnemyMechanicKind.StoneShatter, RewardMin = 11, RewardMax = 15, RewardProfileId = "battle_default",
        MechanicDefinition = Mechanic("stone_shatter", "碎岩", "伏石护体被破后，下一次行动转为碎岩",
            triggerType: EnemyMechanicTriggerType.GuardBroken, triggeredIntent: TriggerAttack(12, "碎岩")),
        Intents = new List<EnemyIntent>
        {
            Guard("伏石", 8), Attack(7, "掷石"), Attack(10, "裂地"),
        },
    };

    private static EnemyDefinition Scavenger() => new()
    {
        Id = "act1_weak_scavenger", Name = "掠灵散修", MaxHp = 48, Tier = EncounterTier.Weak,
        Mechanic = EnemyMechanicKind.ScavengerRetreat, RewardMin = 14, RewardMax = 18, RewardProfileId = "battle_default",
        MechanicDefinition = Mechanic("scavenger_retreat", "退势", "生命降至阈值后施放符火并获得定义中的护体",
            triggerType: EnemyMechanicTriggerType.HealthThreshold, healthThreshold: 24,
            triggeredIntent: TriggerAttack(8, "符火", 10)),
        Intents = new List<EnemyIntent>
        {
            Guard("观望", 6), Attack(8, "符火"),
            new() { IntentType = EnemyIntentType.攻击, Name = "游走", Value = 5, GuardValue = 6 },
        },
    };

    private static EnemyDefinition Guard() => new()
    {
        Id = "act1_strong_guard", Name = "不周残兵", MaxHp = 82, Tier = EncounterTier.Strong,
        Mechanic = EnemyMechanicKind.GuardFormation, RewardMin = 20, RewardMax = 26, RewardProfileId = "battle_default",
        MechanicDefinition = Mechanic("guard_formation", "结阵", "护体与合击由当前意图定义执行"),
        Intents = new List<EnemyIntent>
        {
            Guard("结阵", 10), Attack(10, "长戈"),
            new() { IntentType = EnemyIntentType.攻击, Name = "残军合击", Value = 14, BonusIfNoPlayerAttack = 4 },
            Guard("回阵", 6),
        },
    };

    private static EnemyDefinition Talisman() => new()
    {
        Id = "act1_strong_talisman", Name = "符阵散修", MaxHp = 78, Tier = EncounterTier.Strong,
        Mechanic = EnemyMechanicKind.TalismanDetonation, RewardMin = 21, RewardMax = 27, RewardProfileId = "battle_default",
        MechanicDefinition = Mechanic("talisman_detonation", "引爆", "护体状态决定当前意图的条件伤害"),
        Intents = new List<EnemyIntent>
        {
            Guard("布符", 8),
            new() { IntentType = EnemyIntentType.攻击, Name = "火符", Value = 9, VulnerableValue = 1 },
            new() { IntentType = EnemyIntentType.攻击, Name = "引爆", Value = 16, AlternateValue = 10,
                AlternateDamageCondition = EnemyAlternateDamageCondition.GuardBrokenOrEmpty,
                DamageConditionId = "guard_broken_or_empty", DamageConditionDisplayName = "护体已破或为空" },
            Guard("续符", 8),
        },
    };

    private static EnemyDefinition MountainKing() => new()
    {
        Id = "act1_strong_mountain", Name = "山魈王", MaxHp = 92, Tier = EncounterTier.Strong,
        Mechanic = EnemyMechanicKind.MountainFrenzy, RewardMin = 22, RewardMax = 28, RewardProfileId = "battle_default",
        RequiresPhaseDefinitions = true,
        MechanicDefinition = Mechanic("mountain_frenzy", "狂势", "阶段阈值与强化意图由阶段定义提供"),
        Intents = new List<EnemyIntent>
        {
            new() { IntentType = EnemyIntentType.强化, Name = "震吼", VulnerableValue = 1 },
            Attack(14, "裂地"), Attack(10, "撕咬"), Guard("喘息", 12),
        },
        PhaseDefinitions = new List<EnemyPhaseDefinition>
        {
            new()
            {
                Id = "frenzy",
                PhaseIndex = 1,
                HealthAtOrBelow = 46,
                ResetGuardOnEnter = true,
                Intents = new List<EnemyIntent>
                {
                    new() { IntentType = EnemyIntentType.强化, Name = "狂啸", VulnerableValue = 2 },
                    Attack(17, "裂地"), Attack(13, "撕咬"), Guard("喘息", 12),
                }
            }
        },
    };

    private static EnemyDefinition Xiangliu() => new()
    {
        Id = "act1_boss_xiangliu", Name = "相柳之骸", MaxHp = 120, Tier = EncounterTier.Boss,
        Mechanic = EnemyMechanicKind.XiangliuPhases, RewardMin = 60, RewardMax = 80, RewardProfileId = "battle_boss",
        RequiresPhaseDefinitions = true,
        MechanicDefinition = Mechanic("xiangliu_phases", "三首阶段", "阶段阈值与当前意图由阶段定义提供"),
        Intents = new List<EnemyIntent>
        {
            Guard("守巢", 12), Attack(10, "三首噬"),
            new() { IntentType = EnemyIntentType.攻击, Name = "浊水", Value = 8, VulnerableValue = 1 },
        },
        PhaseDefinitions = new List<EnemyPhaseDefinition>
        {
            new()
            {
                Id = "phase_one",
                PhaseIndex = 1,
                HealthAtOrBelow = 80,
                ResetGuardOnEnter = true,
                Intents = new List<EnemyIntent>
                {
                    Attack(16, "九首齐噬"), Guard("回潮护身", 10),
                    new() { IntentType = EnemyIntentType.攻击, Name = "浊水反噬", Value = 10, VulnerableValue = 1 },
                }
            },
            new()
            {
                Id = "phase_two",
                PhaseIndex = 2,
                HealthAtOrBelow = 40,
                ResetGuardOnEnter = true,
                Intents = new List<EnemyIntent>
                {
                    Attack(19, "九首齐噬"),
                    new() { IntentType = EnemyIntentType.攻击, Name = "浊水反噬", Value = 13, VulnerableValue = 1 },
                    Guard("回潮护身", 10),
                }
            }
        },
    };

    private static EnemyIntent Attack(int value, string name) => new()
    {
        IntentType = EnemyIntentType.攻击, Value = value, Name = name,
    };

    private static EnemyIntent Guard(string name, int value) => new()
    {
        IntentType = EnemyIntentType.强化, Name = name, GuardValue = value,
    };

    private static EnemyIntent Strengthen(string name, string description,
        EnemyMechanicAction mechanicAction = EnemyMechanicAction.None) => new()
    {
        IntentType = EnemyIntentType.强化, Name = name, Description = description,
        MechanicAction = mechanicAction,
    };

    private static EnemyIntent TriggerAttack(int value, string name, int guardValue = 0) => new()
    {
        IntentType = EnemyIntentType.攻击,
        Name = name,
        Value = value,
        IsMechanicTrigger = true,
        MechanicGuardValue = guardValue,
    };

    private static EnemyMechanicDefinition Mechanic(string id, string displayName, string description,
        EnemyMechanicTriggerType triggerType = EnemyMechanicTriggerType.None,
        int healthThreshold = -1, EnemyIntent triggeredIntent = null,
        string counterKey = null, int counterThreshold = 0, int damageOverride = 0) => new()
    {
        Id = id,
        DisplayName = displayName,
        Description = description,
        TriggerType = triggerType,
        HealthThreshold = healthThreshold,
        TriggeredIntent = triggeredIntent,
        CounterKey = counterKey,
        CounterThreshold = counterThreshold,
        DamageOverride = damageOverride,
        HasDamageOverride = counterKey != null,
    };
}
