using Godot;
using System;
using System.Collections.Generic;

/// <summary>卡牌归属。内容所有权与显示文本解耦，运行时不得按角色名称分支。</summary>
public enum CardOwnerKind { Character = 0, Neutral = 1 }

/// <summary>Schema v1 的逻辑类别，UI 可投影为现有斗击/术法等展示类别。</summary>
public enum CardCategory { Attack = 0, Skill = 1, Power = 2, Curse = 3, Status = 4 }

public enum CardRarity { Yellow = 0, Black = 1, Earth = 2, Heaven = 3 }
public enum CardCostKind { Energy = 0, Health = 1, XEnergy = 2, None = 3 }
public enum CardCostTiming { OnPlay = 0 }
public enum CardCostFailurePolicy { RejectPlay = 0 }
public enum CardSelectionMode { None = 0, Optional = 1, Required = 2 }
public enum CardTargetScope { None = 0, Self = 1, SingleEnemy = 2, SingleAlly = 3, AnyAlly = 4, AllEnemies = 5, AllAllies = 6 }
public enum CardRetargetPolicy { RejectPlay = 0, ReopenSelection = 1, SkipInvalidTargets = 2 }
public enum CardEffectKind { DealDamage = 0, GainBlock = 1, LoseHealth = 2, AddStatus = 3, MoveSelfToZone = 4, DrawCards = 5, GainEnergy = 6, ModifyCost = 7 }
public enum CardEffectTarget { SelectedTarget = 0, Self = 1, AllEnemies = 2, AllAllies = 3 }
public enum CardStatusKind { None = 0, Strength = 1, Vulnerable = 2, EternalFlame = 3 }
public enum CardDurationScope { Battle = 0, Turn = 1, PermanentEnemy = 2 }
public enum CardDestinationZone { None = 0, Exhaust = 1 }

/// <summary>从 Resource 字典转换后的强类型费用定义。</summary>
public sealed class CardCostDefinition
{
    public CardCostKind CostType { get; init; }
    public int Amount { get; init; }
    public CardCostTiming Timing { get; init; }
    public CardCostFailurePolicy FailurePolicy { get; init; }
    public bool CanCauseDefeat { get; init; }
}

/// <summary>目标选择契约。控制器只读此契约，不读取卡名或旧布尔字段。</summary>
public sealed class CardTargetPolicyDefinition
{
    public CardSelectionMode SelectionMode { get; init; }
    public CardTargetScope Scope { get; init; }
    public int MinimumTargets { get; init; }
    public int MaximumTargets { get; init; }
    public bool AllowDeadTargets { get; init; }
    public CardRetargetPolicy RetargetOnInvalid { get; init; }
}

/// <summary>按 Order 执行的结构化效果定义。</summary>
public sealed class CardEffectDefinition
{
    public int Order { get; init; }
    public CardEffectKind EffectType { get; init; }
    public CardEffectTarget TargetSelector { get; init; }
    public int Amount { get; init; }
    public CardStatusKind StatusKind { get; init; }
    public CardDurationScope DurationScope { get; init; }
    public CardDestinationZone DestinationZone { get; init; }
}

/// <summary>升级图的一条边；升级不从显示名推导。</summary>
public sealed class CardUpgradeDefinition
{
    public bool CanUpgrade { get; init; }
    public string NextCardId { get; init; }
    public string FamilyId { get; init; }
}

/// <summary>
/// Schema v1 的单卡事实资源。Godot Inspector 使用 Dictionary 导出字段保存嵌套结构，
/// 运行时通过强类型投影读取，避免把描述文本或旧 CardInfo 当作规则来源。
/// </summary>
[Tool]
[GlobalClass]
public partial class CardDefinitionResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    /// <summary>仅编辑器草稿使用；现有生产资源缺失该字段时保持 false，避免枚举迁移。</summary>
    [Export] public bool EditorDraft { get; set; }
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string DescriptionFallback { get; set; } = "";
    [Export] public CardOwnerKind OwnerKind { get; set; } = CardOwnerKind.Character;
    [Export] public string OwnerCharacterId { get; set; } = "";
    [Export] public CardCategory Category { get; set; } = CardCategory.Skill;
    [Export] public CardRarity Rarity { get; set; } = CardRarity.Yellow;
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Costs { get; set; } = new();
    [Export] public Godot.Collections.Dictionary TargetPolicy { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Effects { get; set; } = new();
    [Export] public Godot.Collections.Array<string> RewardPoolIds { get; set; } = new();
    [Export] public Godot.Collections.Dictionary Upgrade { get; set; } = new();
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
    [Export] public Godot.Collections.Dictionary Availability { get; set; } = new();
    [Export] public Godot.Collections.Dictionary Preview { get; set; } = new();
    [Export] public Godot.Collections.Dictionary Balance { get; set; } = new();

    public IReadOnlyList<CardCostDefinition> GetCosts() => CardDefinitionReader.ReadCosts(Costs);
    public CardTargetPolicyDefinition GetTargetPolicy() => CardDefinitionReader.ReadTargetPolicy(TargetPolicy);
    public IReadOnlyList<CardEffectDefinition> GetEffects() => CardDefinitionReader.ReadEffects(Effects);
    public CardUpgradeDefinition GetUpgrade() => CardDefinitionReader.ReadUpgrade(Upgrade);

    /// <summary>供仅编辑器插件调用的字段级校验入口；不会写入 Catalog 或运行时状态。</summary>
    public string[] GetEditorValidationErrors()
    {
        if (!CardCatalogService.TryGetAllCards(out var loadedCards, out var loadError) ||
            !CardCatalogService.TryGetPoolIds(out var poolIds, out loadError))
            return new[] { $"catalog: {loadError}" };
        var ids = new List<string>();
        foreach (var card in loadedCards) ids.Add(card.Id);
        if (!ids.Contains(Id)) ids.Add(Id);
        CardDefinitionValidator.TryValidateCard(this, ids, poolIds, out var errors);
        return new List<string>(errors).ToArray();
    }

    /// <summary>暴露给 Godot 编辑器脚本的只读校验结果；不序列化进卡牌资源。</summary>
    [Export]
    public string[] EditorValidationErrors
    {
        get => GetEditorValidationErrors();
        set { }
    }
}

/// <summary>
/// CardDefinitionResource 的唯一字典解析器。兼容读取 API 仅供显示投影使用；
/// Catalog 和执行计划必须使用 TryRead*，这样缺字段不会被伪装成合法的枚举零值。
/// </summary>
public static class CardDefinitionReader
{
    public static IReadOnlyList<CardCostDefinition> ReadCosts(Godot.Collections.Array<Godot.Collections.Dictionary> entries)
    {
        var result = new List<CardCostDefinition>();
        if (entries == null) return result;
        foreach (var entry in entries)
        {
            result.Add(new CardCostDefinition
            {
                CostType = ReadEnum(entry, "costType", CardCostKind.None),
                Amount = ReadInt(entry, "amount"),
                Timing = ReadEnum(entry, "timing", CardCostTiming.OnPlay),
                FailurePolicy = ReadEnum(entry, "failurePolicy", CardCostFailurePolicy.RejectPlay),
                CanCauseDefeat = ReadBool(entry, "canCauseDefeat"),
            });
        }
        return result;
    }

    public static CardTargetPolicyDefinition ReadTargetPolicy(Godot.Collections.Dictionary entry) => new()
    {
        SelectionMode = ReadEnum(entry, "selectionMode", CardSelectionMode.None),
        Scope = ReadEnum(entry, "scope", CardTargetScope.None),
        MinimumTargets = ReadInt(entry, "minimumTargets"),
        MaximumTargets = ReadInt(entry, "maximumTargets"),
        AllowDeadTargets = ReadBool(entry, "allowDeadTargets"),
        RetargetOnInvalid = ReadEnum(entry, "retargetOnInvalid", CardRetargetPolicy.RejectPlay),
    };

    public static IReadOnlyList<CardEffectDefinition> ReadEffects(Godot.Collections.Array<Godot.Collections.Dictionary> entries)
    {
        var result = new List<CardEffectDefinition>();
        if (entries == null) return result;
        foreach (var entry in entries)
        {
            result.Add(new CardEffectDefinition
            {
                Order = ReadInt(entry, "order"),
                EffectType = ReadEnum(entry, "effectType", CardEffectKind.ModifyCost),
                TargetSelector = ReadEnum(entry, "targetSelector", CardEffectTarget.Self),
                Amount = ReadInt(entry, "amount"),
                StatusKind = ReadEnum(entry, "statusKind", CardStatusKind.None),
                DurationScope = ReadEnum(entry, "durationScope", CardDurationScope.Battle),
                DestinationZone = ReadEnum(entry, "destinationZone", CardDestinationZone.None),
            });
        }
        return result;
    }

    public static CardUpgradeDefinition ReadUpgrade(Godot.Collections.Dictionary entry) => new()
    {
        CanUpgrade = ReadBool(entry, "canUpgrade"),
        NextCardId = ReadString(entry, "nextCardId"),
        FamilyId = ReadString(entry, "familyId"),
    };

    public static string ReadString(Godot.Collections.Dictionary entry, string key) =>
        entry != null && entry.TryGetValue(key, out var value) ? value.AsString() : "";

    public static int ReadInt(Godot.Collections.Dictionary entry, string key) =>
        entry != null && entry.TryGetValue(key, out var value) ? (int)value.AsInt64() : 0;

    public static bool ReadBool(Godot.Collections.Dictionary entry, string key) =>
        entry != null && entry.TryGetValue(key, out var value) && value.AsBool();

    public static TEnum ReadEnum<TEnum>(Godot.Collections.Dictionary entry, string key, TEnum fallback)
        where TEnum : struct, Enum
    {
        int raw = ReadInt(entry, key);
        return Enum.IsDefined(typeof(TEnum), raw) ? (TEnum)Enum.ToObject(typeof(TEnum), raw) : fallback;
    }

    /// <summary>严格解析全部会影响运行时语义的字段，并保留原始字段缺失/类型/枚举错误。</summary>
    public static bool TryReadRuntimeFields(CardDefinitionResource definition,
        out IReadOnlyList<CardCostDefinition> costs, out CardTargetPolicyDefinition targetPolicy,
        out IReadOnlyList<CardEffectDefinition> effects, out CardUpgradeDefinition upgrade,
        out IReadOnlyList<string> errors)
    {
        var issues = new List<string>();
        string path = $"card[{definition?.Id ?? "<missing>"}]";
        costs = TryReadCosts(definition?.Costs, $"{path}.costs", issues);
        targetPolicy = TryReadTargetPolicy(definition?.TargetPolicy, $"{path}.targetPolicy", issues);
        effects = TryReadEffects(definition?.Effects, $"{path}.effects", issues);
        upgrade = TryReadUpgrade(definition?.Upgrade, $"{path}.upgrade", issues);
        errors = issues;
        return issues.Count == 0;
    }

    private static IReadOnlyList<CardCostDefinition> TryReadCosts(Godot.Collections.Array<Godot.Collections.Dictionary> entries,
        string path, List<string> issues)
    {
        var result = new List<CardCostDefinition>();
        if (entries == null) { issues.Add($"{path}: 缺少字段。"); return result; }
        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            result.Add(new CardCostDefinition
            {
                CostType = ReadRequiredEnum(entry, "costType", CardCostKind.None, $"{path}[{index}]", issues),
                Amount = ReadRequiredInt(entry, "amount", $"{path}[{index}]", issues),
                Timing = ReadRequiredEnum(entry, "timing", CardCostTiming.OnPlay, $"{path}[{index}]", issues),
                FailurePolicy = ReadRequiredEnum(entry, "failurePolicy", CardCostFailurePolicy.RejectPlay, $"{path}[{index}]", issues),
                CanCauseDefeat = ReadRequiredBool(entry, "canCauseDefeat", $"{path}[{index}]", issues),
            });
        }
        return result;
    }

    private static CardTargetPolicyDefinition TryReadTargetPolicy(Godot.Collections.Dictionary entry, string path, List<string> issues) => new()
    {
        SelectionMode = ReadRequiredEnum(entry, "selectionMode", CardSelectionMode.None, path, issues),
        Scope = ReadRequiredEnum(entry, "scope", CardTargetScope.None, path, issues),
        MinimumTargets = ReadRequiredInt(entry, "minimumTargets", path, issues),
        MaximumTargets = ReadRequiredInt(entry, "maximumTargets", path, issues),
        AllowDeadTargets = ReadRequiredBool(entry, "allowDeadTargets", path, issues),
        RetargetOnInvalid = ReadRequiredEnum(entry, "retargetOnInvalid", CardRetargetPolicy.RejectPlay, path, issues),
    };

    private static IReadOnlyList<CardEffectDefinition> TryReadEffects(Godot.Collections.Array<Godot.Collections.Dictionary> entries,
        string path, List<string> issues)
    {
        var result = new List<CardEffectDefinition>();
        if (entries == null) { issues.Add($"{path}: 缺少字段。"); return result; }
        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            string entryPath = $"{path}[{index}]";
            result.Add(new CardEffectDefinition
            {
                Order = ReadRequiredInt(entry, "order", entryPath, issues),
                EffectType = ReadRequiredEnum(entry, "effectType", CardEffectKind.ModifyCost, entryPath, issues),
                TargetSelector = ReadRequiredEnum(entry, "targetSelector", CardEffectTarget.Self, entryPath, issues),
                Amount = ReadRequiredInt(entry, "amount", entryPath, issues),
                StatusKind = ReadRequiredEnum(entry, "statusKind", CardStatusKind.None, entryPath, issues),
                DurationScope = ReadRequiredEnum(entry, "durationScope", CardDurationScope.Battle, entryPath, issues),
                DestinationZone = ReadRequiredEnum(entry, "destinationZone", CardDestinationZone.None, entryPath, issues),
            });
        }
        return result;
    }

    private static CardUpgradeDefinition TryReadUpgrade(Godot.Collections.Dictionary entry, string path, List<string> issues) => new()
    {
        CanUpgrade = ReadRequiredBool(entry, "canUpgrade", path, issues),
        NextCardId = ReadRequiredString(entry, "nextCardId", path, issues),
        FamilyId = ReadRequiredString(entry, "familyId", path, issues),
    };

    private static TEnum ReadRequiredEnum<TEnum>(Godot.Collections.Dictionary entry, string key, TEnum fallback,
        string path, List<string> issues) where TEnum : struct, Enum
    {
        if (!TryGet(entry, key, out var value)) { issues.Add($"{path}.{key}: 缺少字段。"); return fallback; }
        if (value.VariantType != Variant.Type.Int) { issues.Add($"{path}.{key}: 类型必须为整数枚举。"); return fallback; }
        int raw = (int)value.AsInt64();
        if (!Enum.IsDefined(typeof(TEnum), raw))
        {
            issues.Add($"{path}.{key}: 未知枚举值 {raw}。");
            return fallback;
        }
        return (TEnum)Enum.ToObject(typeof(TEnum), raw);
    }

    private static int ReadRequiredInt(Godot.Collections.Dictionary entry, string key, string path, List<string> issues)
    {
        if (!TryGet(entry, key, out var value)) { issues.Add($"{path}.{key}: 缺少字段。"); return 0; }
        if (value.VariantType != Variant.Type.Int) { issues.Add($"{path}.{key}: 类型必须为整数。"); return 0; }
        return (int)value.AsInt64();
    }

    private static bool ReadRequiredBool(Godot.Collections.Dictionary entry, string key, string path, List<string> issues)
    {
        if (!TryGet(entry, key, out var value)) { issues.Add($"{path}.{key}: 缺少字段。"); return false; }
        if (value.VariantType != Variant.Type.Bool) { issues.Add($"{path}.{key}: 类型必须为布尔值。"); return false; }
        return value.AsBool();
    }

    private static string ReadRequiredString(Godot.Collections.Dictionary entry, string key, string path, List<string> issues)
    {
        if (!TryGet(entry, key, out var value)) { issues.Add($"{path}.{key}: 缺少字段。"); return ""; }
        if (value.VariantType != Variant.Type.String) { issues.Add($"{path}.{key}: 类型必须为字符串。"); return ""; }
        return value.AsString();
    }

    private static bool TryGet(Godot.Collections.Dictionary entry, string key, out Variant value)
    {
        value = default;
        return entry != null && entry.TryGetValue(key, out value);
    }
}
