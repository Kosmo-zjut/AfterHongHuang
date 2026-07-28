using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 所有游戏静态数据定义。
/// MVP 阶段直接硬编码，后续迁移至 JSON 配置表。
/// </summary>
public static class DataDefs
{
    // ==================== 角色数据 ====================

    public static readonly CharacterInfo[] Characters = new CharacterInfo[]
    {
        new()
        {
            Id = "wuzhu", Name = "巫祝", Title = "巫祝",
            Introduction = "祝融血脉最后的传人，以烬骨为甲、以怒火为拳。\n她体内流淌着祖巫之血，也背负着不属于自己的噩梦。",
            Unlocked = true,
            MaxHp = 80,
            MaxLingli = 3,
            DeckPreview = "拳袭×4 格挡×4 烈拳×1 燃血×1",
            Difficulty = "地仙",
        },
        new()
        {
            Id = "daobao", Name = "盗宝者", Title = "遗迹猎人",
            Introduction = "海外十三岛的散修猎人，无门无派无师承。\n在废墟里刨食十年，靠倒卖废墟废品发了财。",
            Unlocked = false,
            MaxHp = 75,
            MaxLingli = 3,
            DeckPreview = "待设计",
            Difficulty = "地仙",
        },
        new()
        {
            Id = "jiejiao", Name = "截教门徒", Title = "末路仙人",
            Introduction = "通天教主的外门弟子，截教万仙来朝中的一员。\n教主说'有教无类'，但ta开始怀疑这话到底是什么意思。",
            Unlocked = false,
            MaxHp = 70,
            MaxLingli = 3,
            DeckPreview = "待设计",
            Difficulty = "地仙",
        },
        new()
        {
            Id = "jinwu", Name = "金乌遗孤", Title = "化形之妖",
            Introduction = "帝俊麾下星辰幡执旗手的后代，一个从未见过父母的小妖族。\nta要听到帝俊的遗言，要一个答案。",
            Unlocked = false,
            MaxHp = 75,
            MaxLingli = 3,
            DeckPreview = "待设计",
            Difficulty = "地仙",
        },
        new()
        {
            Id = "fanren", Name = "凡人", Title = "无根之人",
            Introduction = "没有灵根、没有血脉、没有师承。\n出生在南赡部洲一个连名字都没有的小村庄。",
            Unlocked = false,
            MaxHp = 65,
            MaxLingli = 3,
            DeckPreview = "待设计",
            Difficulty = "凡人",
        },
    };

    // 角色数组不再绑定 CardInfo[]。初始牌重复量是角色套牌配置，单卡事实由 CardCatalogResource 持有。
    static DataDefs()
    {
        if (!TryGetCharacterDefinition("wuzhu", out var wuzhu))
            throw new System.InvalidOperationException("角色数据缺少巫祝定义，无法绑定初始牌组。");
        wuzhu.StarterDeckEntries = new[]
        {
            new StarterDeckEntry { CardId = "wx_01", Count = 4 },
            new StarterDeckEntry { CardId = "wx_02", Count = 4 },
            new StarterDeckEntry { CardId = "wx_03", Count = 1 },
            new StarterDeckEntry { CardId = "wx_04", Count = 1 },
        };
    }

    // ==================== 巫祝初始卡组（10张） ====================

    public static readonly CardInfo[] WuZhuStarterDeck = new CardInfo[]
    {
        new() { Id = "wx_01", Name = "拳袭", Type = CardType.斗击, Cost = 1, Value = 6,
                 TargetMode = CardTargetMode.Enemy, Description = "造成{0}点伤害", UpgradeToId = "wx_01_p" },
        new() { Id = "wx_01", Name = "拳袭", Type = CardType.斗击, Cost = 1, Value = 6,
                 TargetMode = CardTargetMode.Enemy, Description = "造成{0}点伤害", UpgradeToId = "wx_01_p" },
        new() { Id = "wx_01", Name = "拳袭", Type = CardType.斗击, Cost = 1, Value = 6,
                 TargetMode = CardTargetMode.Enemy, Description = "造成{0}点伤害", UpgradeToId = "wx_01_p" },
        new() { Id = "wx_01", Name = "拳袭", Type = CardType.斗击, Cost = 1, Value = 6,
                 TargetMode = CardTargetMode.Enemy, Description = "造成{0}点伤害", UpgradeToId = "wx_01_p" },
        new() { Id = "wx_02", Name = "格挡", Type = CardType.术法, Cost = 1, Value = 5, SelfGuardValue = 5,
                 TargetMode = CardTargetMode.Self, Description = "获得{0}点护体", UpgradeToId = "wx_02_p" },
        new() { Id = "wx_02", Name = "格挡", Type = CardType.术法, Cost = 1, Value = 5,
                 TargetMode = CardTargetMode.Self, Description = "获得{0}点护体", UpgradeToId = "wx_02_p" },
        new() { Id = "wx_02", Name = "格挡", Type = CardType.术法, Cost = 1, Value = 5,
                 TargetMode = CardTargetMode.Self, Description = "获得{0}点护体", UpgradeToId = "wx_02_p" },
        new() { Id = "wx_02", Name = "格挡", Type = CardType.术法, Cost = 1, Value = 5,
                 TargetMode = CardTargetMode.Self, Description = "获得{0}点护体", UpgradeToId = "wx_02_p" },
        new() { Id = "wx_03", Name = "烈拳", Type = CardType.斗击, Cost = 2, Value = 8,
                 TargetMode = CardTargetMode.Enemy,
                 Description = "造成{0}点伤害，施加2层易损",
                 HasSecondary = true, SecondaryValue = 2, SecondaryType = SecondaryEffect.易损,
                 UpgradeToId = "wx_03_p" },
        new() { Id = "wx_04", Name = "燃血", Type = CardType.术法, Cost = 1, Value = 1,
                 TargetMode = CardTargetMode.Self,
                 Description = "失去2血，获得{0}点斗劲",
                 SelfDamage = 2, HasSecondary = true, SecondaryValue = 1, SecondaryType = SecondaryEffect.斗劲,
                 UpgradeToId = "wx_04_p" },
    };

    // ==================== 巫祝初始卡升级版 ====================

    public static readonly CardInfo[] WuZhuStarterUpgrades = new CardInfo[]
    {
        new() { Id = "wx_01_p", Name = "拳袭+", Type = CardType.斗击, Cost = 1, Value = 9,
                 TargetMode = CardTargetMode.Enemy, Description = "造成{0}点伤害" },
        new() { Id = "wx_02_p", Name = "格挡+", Type = CardType.术法, Cost = 1, Value = 8, SelfGuardValue = 8,
                 TargetMode = CardTargetMode.Self, Description = "获得{0}点护体" },
        new() { Id = "wx_03_p", Name = "烈拳+", Type = CardType.斗击, Cost = 2, Value = 8,
                 TargetMode = CardTargetMode.Enemy,
                 Description = "造成{0}点伤害，施加3层易损",
                 HasSecondary = true, SecondaryValue = 3, SecondaryType = SecondaryEffect.易损 },
        new() { Id = "wx_04_p", Name = "燃血+", Type = CardType.术法, Cost = 1, Value = 1,
                 TargetMode = CardTargetMode.Self,
                 Description = "失去1血，获得{0}点斗劲",
                 SelfDamage = 1, HasSecondary = true, SecondaryValue = 1, SecondaryType = SecondaryEffect.斗劲 },
    };

    // ==================== 奖赏卡池 ====================

    public static readonly CardInfo[] RewardCardPool = new CardInfo[]
    {
        new() { Id = "wx_r_001", Name = "燎掌", Type = CardType.斗击, Cost = 1, Value = 8,
                 TargetMode = CardTargetMode.Enemy, Description = "造成{0}点伤害" },
        new() { Id = "wx_r_002", Name = "碎骨拳", Type = CardType.斗击, Cost = 1, Value = 5,
                 TargetMode = CardTargetMode.Enemy,
                 Description = "造成{0}点伤害，施加1层易损",
                 HasSecondary = true, SecondaryValue = 1, SecondaryType = SecondaryEffect.易损 },
        new() { Id = "wx_r_003", Name = "踏火冲", Type = CardType.斗击, Cost = 2, Value = 14,
                 TargetMode = CardTargetMode.Enemy,
                 Description = "失去2点生命，造成{0}点伤害",
                 SelfDamage = 2 },
        new() { Id = "wx_r_004", Name = "骨甲", Type = CardType.术法, Cost = 1, Value = 8, SelfGuardValue = 8,
                 TargetMode = CardTargetMode.Self, Description = "获得{0}点护体" },
        new() { Id = "wx_r_005", Name = "血挡", Type = CardType.术法, Cost = 1, Value = 12, SelfGuardValue = 12,
                 TargetMode = CardTargetMode.Self,
                 Description = "失去2点生命，获得{0}点护体",
                 SelfDamage = 2 },
        new() { Id = "wx_r_006", Name = "裂肤引火", Type = CardType.术法, Cost = 0, Value = 1,
                 TargetMode = CardTargetMode.Self,
                 Description = "失去3点生命，获得{0}点斗劲",
                 SelfDamage = 3, HasSecondary = true, SecondaryValue = 1, SecondaryType = SecondaryEffect.斗劲 },
        new() { Id = "wx_r_007", Name = "焚脉重拳", Type = CardType.斗击, Cost = 2, Value = 11,
                 TargetMode = CardTargetMode.Enemy,
                 Description = "失去2点生命，造成{0}点伤害，施加2层易损",
                 SelfDamage = 2, HasSecondary = true, SecondaryValue = 2, SecondaryType = SecondaryEffect.易损 },
        new() { Id = "wx_r_008", Name = "祭血凝劲", Type = CardType.术法, Cost = 1, Value = 2,
                 TargetMode = CardTargetMode.Self,
                 Description = "失去4点生命，获得{0}点斗劲",
                 SelfDamage = 4, HasSecondary = true, SecondaryValue = 2, SecondaryType = SecondaryEffect.斗劲 },
        new() { Id = "wx_r_009", Name = "烬骨守势", Type = CardType.术法, Cost = 2, Value = 18, SelfGuardValue = 18,
                 TargetMode = CardTargetMode.Self,
                 Description = "失去3点生命，获得{0}点护体",
                 SelfDamage = 3 },
        new() { Id = "wx_r_010", Name = "祝融残焰", Type = CardType.术法, Cost = 2, Value = 4,
                 TargetMode = CardTargetMode.Enemy,
                 Description = "失去3点生命，给予目标敌人{0}层永炎，消弭",
                 SelfDamage = 3, HasSecondary = true, SecondaryValue = 4, SecondaryType = SecondaryEffect.永炎,
                 RequiresEnemyTarget = true, Exhausts = true },
    };

    /// <summary>ACT1 Boss 固定天品池，当前只供相柳之骸胜利奖励使用。</summary>
    public static readonly CardInfo[] BossRewardCardPool = new CardInfo[]
    {
        new() { Id = "wx_a1_boss_001", Name = "祝融天火", Type = CardType.斗击, Cost = 3, Value = 22,
                 TargetMode = CardTargetMode.Enemy, SelfDamage = 3, HasSecondary = true,
                 SecondaryValue = 3, SecondaryType = SecondaryEffect.易损,
                 Description = "失去3点生命，造成{0}点伤害，施加3层易损" },
        new() { Id = "wx_a1_boss_002", Name = "九首回潮", Type = CardType.术法, Cost = 2, Value = 20,
                 SelfGuardValue = 20, TargetMode = CardTargetMode.Self, HasSecondary = true,
                 SecondaryValue = 1, SecondaryType = SecondaryEffect.斗劲,
                 Description = "获得{0}点护体，并获得1点斗劲" },
        new() { Id = "wx_a1_boss_003", Name = "浊潮蚀骨", Type = CardType.术法, Cost = 2, Value = 6,
                 TargetMode = CardTargetMode.Enemy, RequiresEnemyTarget = true, HasSecondary = true,
                 SecondaryValue = 6, SecondaryType = SecondaryEffect.永炎,
                 Description = "给予目标敌人6层永炎" },
    };

    // ==================== 地图路线 ====================
    // 生产路线唯一来源为 RunState.MapGraph。

    // ==================== 道痕数据 ====================

    public static readonly DaoMarkInfo[] DaoMarkPool = new DaoMarkInfo[]
    {
        new()
        {
            Id = "dm_01", Name = "九乌坠日", Grade = "下品",
            Description = "九大金乌怨念聚合体。\n每场战斗开始时，给予所有敌人3层永炎。",
            DetailDesc = "永炎：回合结束时扣X血（X=当前层数），不衰减。",
            EffectType = DaoMarkEffect.永炎开局,
            EffectValue = 3
        },
        new()
        {
            Id = "dm_06", Name = "符修遗箓", Grade = "下品",
            Description = "陨落符修的遗存箓文。\n击败敌人后额外获得一次卡牌奖励。",
            DetailDesc = "符修耗尽心血推演的最后一卦，终于算对了——只是太晚了。",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 1,
            RewardDisplayDescription = "额外获得一次卡牌奖励",
            RewardSlotId = "dao_extra_card",
            RewardCardPoolId = "reward_cards",
            RewardCandidateCount = 3,
            RewardChoiceCount = 1,
            RewardMergeRule = RewardSourceMergeRule.Independent,
        },
        new()
        {
            Id = "dm_07", Name = "体修金刚", Grade = "下品",
            Description = "陨落体修的金刚意志。\n最大生命值+10。",
            DetailDesc = "那份'硬扛不周山碎片'的倔强还留在道韵里。",
            EffectType = DaoMarkEffect.加血上限,
            EffectValue = 10
        },
    };

    /// <summary>按角色 ID 解析集中定义，不向调用方暴露默认首项兜底。</summary>
    public static bool TryGetCharacterDefinition(string characterId, out CharacterInfo character)
    {
        character = null;
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        foreach (var candidate in Characters)
        {
            if (candidate != null && candidate.Id == characterId)
            {
                character = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>解析角色定义携带的永久初始牌组；缺失牌组时显式失败。</summary>
    public static bool TryResolveStarterDeck(CharacterInfo character, out CardInfo[] starterDeck, out string error)
    {
        starterDeck = null;
        error = "";
        if (character == null)
        {
            error = "角色定义为空。";
            return false;
        }

        if (character.StarterDeck == null || character.StarterDeck.Length == 0)
        {
            error = $"角色 {character.Id} 缺少初始牌组定义。";
            return false;
        }

        starterDeck = character.StarterDeck;
        return true;
    }

    /// <summary>
    /// 迁移期旧卡牌快照查询，仅供 CardCatalog 双读自检/fixture 使用。
    /// 生产奖励、套牌、升级和战斗不得调用本方法。
    /// </summary>
    [System.Obsolete("仅迁移 fixture 使用；生产请通过 CardCatalogService 读取 CardDefinitionResource。")]
    public static bool TryGetCardById(string id, out CardInfo cardInfo)
    {
        foreach (var card in WuZhuStarterDeck)
        {
            if (card.Id == id)
            {
                cardInfo = card;
                return true;
            }
        }

        foreach (var card in WuZhuStarterUpgrades)
        {
            if (card.Id == id)
            {
                cardInfo = card;
                return true;
            }
        }

        foreach (var card in RewardCardPool)
        {
            if (card.Id == id)
            {
                cardInfo = card;
                return true;
            }
        }

        foreach (var card in BossRewardCardPool)
        {
            if (card.Id == id)
            {
                cardInfo = card;
                return true;
            }
        }

        cardInfo = null;
        return false;
    }
}

// ==================== 枚举 ====================

public enum CardType
{
    斗击 = 0,
    术法 = 1,
    道行 = 2,
    劫数 = 3,
}

public enum CardTargetMode
{
    Enemy = 0,
    Self = 1,
    None = 2,
}

public enum SecondaryEffect
{
    无 = 0,
    易损 = 1,
    斗劲 = 2,
    永炎 = 3,
}

public enum DaoMarkEffect
{
    永炎开局 = 0,
    额外奖励 = 1,
    加血上限 = 2,
}

public enum EnemyIntentType
{
    攻击 = 0,
    强化 = 1,
}

/// <summary>敌人特殊行为标签。战斗逻辑按标签处理，不按敌人名称分支。</summary>
public enum EnemyMechanicKind
{
    None = 0,
    MonkeyDodge = 1,
    StoneShatter = 2,
    ScavengerRetreat = 3,
    GuardFormation = 4,
    TalismanDetonation = 5,
    MountainFrenzy = 6,
    XiangliuPhases = 7,
}

/// <summary>敌人意图执行时可触发的机制动作。机制激活必须由数据显式声明。</summary>
public enum EnemyMechanicAction
{
    None = 0,
    ActivateDodge = 1,
}

/// <summary>意图序列策略。Opening 只执行一次，Loop 按阶段内游标循环。</summary>
public enum EnemyIntentSequencePolicy
{
    Loop = 0,
    OpeningThenLoop = 1,
}

/// <summary>机制触发条件由遭遇定义声明，Core 不按具体敌人分支。</summary>
public enum EnemyMechanicTriggerType
{
    None = 0,
    GuardBroken = 1,
    HealthThreshold = 2,
}

/// <summary>意图条件伤害的通用状态条件。</summary>
public enum EnemyAlternateDamageCondition
{
    None = 0,
    GuardBrokenOrEmpty = 1,
}

public enum MapNodeType
{
    道韵 = 0,
    战斗 = 1,
    灵脉 = 2,
    Boss = 3,
}

/// <summary>
/// 游戏难度（对应角色修为境界，MVP 阶段仅用于显示）。
/// </summary>
public enum DifficultyLevel
{
    凡人 = 0,
    地仙 = 1,
    天仙 = 2,
}

/// <summary>玩家阶段状态（用于节点切换判断）</summary>
public enum PlayerState
{
    空闲 = 0,   // 可在地图上选下一节点
    战斗中 = 1,
    灵脉中 = 2,
    战斗胜利结算 = 3,
    失败 = 4,
    商店中 = 5,
    事件中 = 6,
}

// ==================== 数据类 ====================

public class CharacterInfo
{
    public string Id;
    public string Name;
    public string Title;
    public string Introduction;
    public bool Unlocked;
    public int MaxHp;
    public int MaxLingli;
    public string DeckPreview;
    public string Difficulty; // 角色难度标签（凡人/地仙/天仙）
    /// <summary>迁移 fixture 的旧初始牌组；生产角色使用 StarterDeckEntries。</summary>
    public CardInfo[] StarterDeck;
    /// <summary>生产初始牌组配置。重复数量只在这里声明，不复制单卡定义。</summary>
    public StarterDeckEntry[] StarterDeckEntries;
}

/// <summary>角色套牌配置的一项，CardId 必须由 CardCatalog 解析。</summary>
public sealed class StarterDeckEntry
{
    public string CardId;
    public int Count;
}

public class CardInfo
{
    /// <summary>来源 Resource 的稳定 ID。兼容投影不允许脱离 Catalog 作为生产定义使用。</summary>
    public string DefinitionId;
    public string Id;
    public string Name;
    public CardType Type;
    public int Cost;
    public int Value;
    public string Description;
    /// <summary>由 CardExecutionPlan 生成的有序效果摘要；卡面优先使用它而非旧聚合数值。</summary>
    public string ExecutionSummary;
    public int SelfDamage;
    public bool HasSecondary;
    public int SecondaryValue;
    public SecondaryEffect SecondaryType;
    public CardTargetMode TargetMode;
    public bool RequiresEnemyTarget;
    public bool Exhausts;
    public int SelfGuardValue;
    public string UpgradeToId;

    /// <summary>
    /// 需要选中敌方目标才能打出。旧字段 RequiresEnemyTarget 仅保留给历史数据兼容，新交互以 TargetMode 为准。
    /// </summary>
    public bool TargetsEnemy => TargetMode == CardTargetMode.Enemy;
}

public class DaoMarkInfo
{
    public string Id;
    public string Name;
    public string Grade;
    public string Description;
    public string DetailDesc;
    public DaoMarkEffect EffectType;
    public int EffectValue;
    /// <summary>奖励 UI 使用的来源说明，由道痕定义提供，不由控制器按效果类型猜测。</summary>
    public string RewardDisplayDescription;
    /// <summary>额外卡牌奖励使用的集中卡池和槽位规则。</summary>
    public string RewardSlotId;
    public string RewardCardPoolId;
    public int RewardCandidateCount = 3;
    public int RewardChoiceCount = 1;
    public RewardSourceMergeRule RewardMergeRule = RewardSourceMergeRule.Independent;
}

public class EnemyIntent
{
    public int TurnIndex;
    public EnemyIntentType IntentType;
    public string Name;
    public string Description;
    public int Value;
    public int GuardValue;
    public int VulnerableValue;
    public int AlternateValue;
    public int BonusIfNoPlayerAttack;
    public bool TriggerSpecial;
    public EnemyMechanicAction MechanicAction;
    public EnemyAlternateDamageCondition AlternateDamageCondition;
    public bool IsMechanicTrigger;
    public int MechanicGuardValue;
    public string MechanicDisplayName;
    public string DamageConditionId;
    public string DamageConditionDisplayName;
    public bool ClearGuardBrokenAfterExecute;
    public int PhaseIndex;
    public bool ResetGuardOnPhaseEnter;
    public string SequenceId;
    public int SequenceIndex;
    public bool IsOpeningIntent;
    public bool IsPhaseEntry;

    /// <summary>复制定义意图，运行态解析只修改副本元数据。</summary>
    public EnemyIntent Clone()
    {
        return new EnemyIntent
        {
            TurnIndex = TurnIndex,
            IntentType = IntentType,
            Name = Name,
            Description = Description,
            Value = Value,
            GuardValue = GuardValue,
            VulnerableValue = VulnerableValue,
            AlternateValue = AlternateValue,
            BonusIfNoPlayerAttack = BonusIfNoPlayerAttack,
            TriggerSpecial = TriggerSpecial,
            MechanicAction = MechanicAction,
            AlternateDamageCondition = AlternateDamageCondition,
            IsMechanicTrigger = IsMechanicTrigger,
            MechanicGuardValue = MechanicGuardValue,
            MechanicDisplayName = MechanicDisplayName,
            DamageConditionId = DamageConditionId,
            DamageConditionDisplayName = DamageConditionDisplayName,
            ClearGuardBrokenAfterExecute = ClearGuardBrokenAfterExecute,
            PhaseIndex = PhaseIndex,
            ResetGuardOnPhaseEnter = ResetGuardOnPhaseEnter,
            SequenceId = SequenceId,
            SequenceIndex = SequenceIndex,
            IsOpeningIntent = IsOpeningIntent,
            IsPhaseEntry = IsPhaseEntry,
        };
    }
}

/// <summary>
/// 基于一次 BattleState 快照解析出的最终敌方意图。FinalDamage 与条件触发结果
/// 在这里冻结，展示和执行不得再次读取护体或玩家上一回合状态重算。
/// </summary>
public sealed class ResolvedEnemyIntent
{
    public EnemyIntent Intent { get; init; }
    /// <summary>冻结解析时的定义对象身份，换敌人或替换定义后旧结果不可执行。</summary>
    public EnemyInfo EnemyDefinition { get; init; }
    public string EnemyId { get; init; }
    public int EnemyHp { get; init; }
    public int EnemyPhase { get; init; }
    public bool EnemySpecialTriggered { get; init; }
    public string PhaseId { get; init; }
    public int PhaseTurnIndex { get; init; }
    public int PhaseOpeningTurnIndex { get; init; }
    public int OpeningTurnIndex { get; init; }
    public string IntentId { get; init; }
    public int PlayerHuti { get; init; }
    public int EnemyHuti { get; init; }
    public bool EnemyGuardWasBroken { get; init; }
    public bool EnemyMechanicActive { get; init; }
    public bool PlayerAttackedThisTurn { get; init; }
    public bool LastPlayerTurnHadAttack { get; init; }
    public string MechanicStateFingerprint { get; init; }
    public string ResolutionId { get; init; }
    public int StateVersion { get; init; }
    public int FinalDamage { get; init; }
    public bool DamageConditionTriggered { get; init; }
    public string DamageConditionId { get; init; }
    public string DamageConditionDisplayName { get; init; }
}

/// <summary>敌人血量阶段定义，阈值和进入阶段副作用均属于遭遇数据。</summary>
public sealed class EnemyPhaseDefinition
{
    public string Id;
    public int PhaseIndex;
    public int HealthAtOrBelow;
    public bool ResetGuardOnEnter;
    public EnemyIntentSequencePolicy SequencePolicy { get; set; } = EnemyIntentSequencePolicy.Loop;
    public List<EnemyIntent> OpeningIntents = new();
    public List<EnemyIntent> Intents = new();
}

/// <summary>敌人机制定义，描述触发、计数和机制意图，不由 Core/UI 猜测。</summary>
public sealed class EnemyMechanicDefinition
{
    public string Id;
    public string DisplayName;
    public string Description;
    public EnemyMechanicTriggerType TriggerType;
    public int HealthThreshold;
    public EnemyIntent TriggeredIntent;
    public int CounterThreshold;
    public string CounterKey;
    public int DamageOverride;
    public bool HasDamageOverride;
}

public class EnemyInfo
{
    public string Id;
    public string Name;
    public int MaxHp;
    public List<EnemyIntent> Intents;
    public EnemyMechanicKind Mechanic;
    public EnemyMechanicDefinition MechanicDefinition;
    public int RewardMin;
    public int RewardMax;
    public string RewardProfileId;
    public bool RequiresPhaseDefinitions;
    public EnemyIntentSequencePolicy SequencePolicy { get; set; } = EnemyIntentSequencePolicy.Loop;
    public List<EnemyIntent> OpeningIntents { get; set; } = new();
    public List<EnemyPhaseDefinition> PhaseDefinitions;

    /// <summary>
    /// 根据本场运行态选择阶段序列。阶段切换只看 EnemyHp/临时 BattleState，
    /// 因此同一遭遇定义可以被不同节点稳定复用。
    /// </summary>
    public bool TryResolveIntent(int turnIndex, BattleState battle, out EnemyIntent intent, out string error)
    {
        intent = null;
        error = "";
        if (turnIndex < 0)
        {
            error = $"敌人 {Id} 的意图索引非法：{turnIndex}。";
            return false;
        }

        if (battle != null && (battle.EnemyPhaseTurnIndex < 0 || battle.EnemyPhase < 0))
        {
            error = $"敌人 {Id} 的阶段游标状态非法。";
            return false;
        }

        var mechanic = MechanicDefinition;
        if (battle == null)
        {
            error = $"敌人 {Id} 缺少 BattleState，无法解析意图。";
            return false;
        }

        if (Mechanic != EnemyMechanicKind.None && mechanic == null)
        {
            error = $"敌人 {Id} 声明了机制 {Mechanic} 但缺少机制定义。";
            return false;
        }

        if (mechanic != null)
        {
            if (string.IsNullOrWhiteSpace(mechanic.Id) ||
                string.IsNullOrWhiteSpace(mechanic.DisplayName) ||
                string.IsNullOrWhiteSpace(mechanic.Description))
            {
                error = $"敌人 {Id} 的机制定义缺少 ID、显示名或说明。";
                return false;
            }

            if (mechanic.TriggerType != EnemyMechanicTriggerType.None &&
                mechanic.TriggeredIntent == null)
            {
                error = $"敌人 {Id} 的机制 {mechanic.Id} 缺少触发意图。";
                return false;
            }

            if (mechanic.TriggerType == EnemyMechanicTriggerType.HealthThreshold &&
                mechanic.HealthThreshold < 0)
            {
                error = $"敌人 {Id} 的机制 {mechanic.Id} 缺少有效生命阈值。";
                return false;
            }

            if (mechanic.CounterThreshold > 0 && string.IsNullOrWhiteSpace(mechanic.CounterKey))
            {
                error = $"敌人 {Id} 的机制 {mechanic.Id} 缺少计数键。";
                return false;
            }
        }

        if (RequiresPhaseDefinitions && (PhaseDefinitions == null || PhaseDefinitions.Count == 0))
        {
            error = $"敌人 {Id} 声明阶段序列但缺少 PhaseDefinitions。";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(battle.EnemyPhaseId) &&
            (PhaseDefinitions == null || PhaseDefinitions.Find(phase => phase?.Id == battle.EnemyPhaseId) == null))
        {
            error = $"敌人 {Id} 的当前阶段 ID 不存在：{battle.EnemyPhaseId}。";
            return false;
        }

        if (mechanic != null && !battle.EnemySpecialTriggered &&
            ((mechanic.TriggerType == EnemyMechanicTriggerType.GuardBroken && battle.EnemyGuardWasBroken) ||
             (mechanic.TriggerType == EnemyMechanicTriggerType.HealthThreshold && battle.EnemyHp <= mechanic.HealthThreshold)))
        {
            intent = mechanic.TriggeredIntent.Clone();
            if (string.IsNullOrWhiteSpace(intent.Name))
            {
                error = $"敌人 {Id} 的机制 {mechanic.Id} 触发意图缺少显示名称。";
                intent = null;
                return false;
            }
            intent.TurnIndex = turnIndex;
            intent.SequenceId = mechanic.Id;
            intent.SequenceIndex = 0;
            intent.IsMechanicTrigger = true;
            intent.Description = mechanic.Description;
            intent.MechanicDisplayName = mechanic.DisplayName;
            if (!Enum.IsDefined(typeof(EnemyIntentType), intent.IntentType) ||
                (intent.IntentType == EnemyIntentType.攻击 && intent.Value <= 0))
            {
                error = $"敌人 {Id} 的机制触发意图类型或伤害无效。";
                intent = null;
                return false;
            }
            intent.MechanicGuardValue = mechanic.TriggerType == EnemyMechanicTriggerType.HealthThreshold
                ? mechanic.TriggeredIntent.MechanicGuardValue
                : 0;
            intent.ClearGuardBrokenAfterExecute = mechanic.TriggerType == EnemyMechanicTriggerType.GuardBroken;
            return true;
        }

        var phase = ResolvePhase(battle.EnemyHp, battle.EnemyPhase);
        if (RequiresPhaseDefinitions && battle.EnemyPhase > 0 && phase == null)
        {
            error = $"敌人 {Id} 当前阶段无法解析，禁止回退基础意图序列。";
            return false;
        }
        bool enteringPhase = phase != null && battle.EnemyPhaseId != phase.Id;

        if (phase == null && SequencePolicy == EnemyIntentSequencePolicy.OpeningThenLoop &&
            !battle.EnemyOpeningCompleted && OpeningIntents != null && OpeningIntents.Count > 0)
        {
            int openingIndex = battle.EnemyOpeningTurnIndex;
            if (openingIndex < 0 || openingIndex >= OpeningIntents.Count)
            {
                error = $"敌人 {Id} 的基础 Opening 游标越界：{openingIndex}/{OpeningIntents.Count}。";
                return false;
            }
            intent = OpeningIntents[openingIndex]?.Clone();
            if (intent == null || string.IsNullOrWhiteSpace(intent.Name))
            {
                error = $"敌人 {Id} 的基础 Opening 意图无效。";
                intent = null;
                return false;
            }

            if (!Enum.IsDefined(typeof(EnemyIntentType), intent.IntentType) ||
                (intent.IntentType == EnemyIntentType.攻击 && intent.Value <= 0))
            {
                error = $"敌人 {Id} 的基础 Opening 意图类型或伤害无效。";
                intent = null;
                return false;
            }

            intent.TurnIndex = turnIndex;
            intent.SequenceId = "";
            intent.SequenceIndex = openingIndex;
            intent.IsOpeningIntent = true;
            intent.MechanicDisplayName = mechanic?.DisplayName;
            return true;
        }

        bool phaseUsesOpening = phase != null && phase.SequencePolicy == EnemyIntentSequencePolicy.OpeningThenLoop;
        if (phaseUsesOpening && (phase.OpeningIntents == null || phase.OpeningIntents.Count == 0))
        {
            error = $"敌人 {Id} 阶段 {phase.Id} 声明 OpeningThenLoop 但缺少 Opening intents。";
            return false;
        }
        bool phaseOpeningActive = phaseUsesOpening && !battle.EnemyPhaseOpeningCompleted;
        var sequence = phaseOpeningActive ? phase.OpeningIntents : phase?.Intents ?? Intents;
        if (sequence == null || sequence.Count == 0)
        {
            error = $"敌人 {Id} 缺少当前阶段意图定义。";
            return false;
        }

        bool enteringPhaseOpening = phaseOpeningActive;
        int index = enteringPhaseOpening
            ? (enteringPhase ? 0 : battle.EnemyPhaseOpeningTurnIndex)
            : (enteringPhase ? 0 : battle.EnemyPhaseTurnIndex % sequence.Count);
        if (index < 0 || index >= sequence.Count)
        {
            error = $"敌人 {Id} 当前序列游标越界：{phase?.Id ?? "base"}/{index}/{sequence.Count}。";
            return false;
        }
        intent = sequence[index]?.Clone();
        if (intent == null)
        {
            error = $"敌人 {Id} 的意图定义为空：index={index}。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(intent.Name))
        {
            error = $"敌人 {Id} 的意图缺少显示名称：index={index}。";
            intent = null;
            return false;
        }

        if (!Enum.IsDefined(typeof(EnemyIntentType), intent.IntentType) ||
            (intent.IntentType == EnemyIntentType.攻击 && intent.Value <= 0))
        {
            error = $"敌人 {Id} 的意图类型或攻击伤害无效：index={index}。";
            intent = null;
            return false;
        }

        if (intent.MechanicAction != EnemyMechanicAction.None && mechanic == null)
        {
            error = $"敌人 {Id} 的意图声明机制动作但缺少机制定义。";
            intent = null;
            return false;
        }

        if (intent.MechanicAction != EnemyMechanicAction.None && string.IsNullOrWhiteSpace(intent.Description))
            intent.Description = mechanic.Description;

        intent.TurnIndex = turnIndex;
        intent.SequenceId = phase?.Id ?? string.Empty;
        intent.SequenceIndex = index;
        intent.IsOpeningIntent = enteringPhaseOpening;
        intent.IsPhaseEntry = enteringPhase;
        intent.MechanicDisplayName = mechanic?.DisplayName;
        if (phase != null)
        {
            intent.PhaseIndex = phase.PhaseIndex;
            intent.ResetGuardOnPhaseEnter = phase.ResetGuardOnEnter && phase.PhaseIndex > battle.EnemyPhase;
        }
        return true;
    }

    private EnemyPhaseDefinition ResolvePhase(int enemyHp, int currentPhaseIndex)
    {
        EnemyPhaseDefinition selected = null;
        if (PhaseDefinitions == null)
            return null;

        foreach (var phase in PhaseDefinitions)
        {
            if (phase == null || phase.HealthAtOrBelow < 0 || enemyHp > phase.HealthAtOrBelow)
                continue;
            if (phase.PhaseIndex < currentPhaseIndex)
                continue;
            if (selected == null || phase.PhaseIndex > selected.PhaseIndex)
                selected = phase;
        }
        return selected;
    }
}
