using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 经过 Catalog 校验后冻结的运行时卡牌事实。UI 可继续读取 CardInfo 兼容投影，
/// 但战斗结算必须按 Effects 的 Order 消费本计划，不能再重组旧字段。
/// </summary>
public sealed class CardExecutionPlan
{
    public string CardId { get; }
    public int EnergyCost { get; }
    public CardTargetPolicyDefinition TargetPolicy { get; }
    public IReadOnlyList<CardEffectDefinition> Effects { get; }

    public CardExecutionPlan(string cardId, int energyCost, CardTargetPolicyDefinition targetPolicy,
        IEnumerable<CardEffectDefinition> effects)
    {
        CardId = cardId ?? throw new ArgumentNullException(nameof(cardId));
        EnergyCost = energyCost;
        TargetPolicy = targetPolicy ?? throw new ArgumentNullException(nameof(targetPolicy));
        Effects = new ReadOnlyCollection<CardEffectDefinition>(new List<CardEffectDefinition>(effects ??
            throw new ArgumentNullException(nameof(effects))));
    }
}

/// <summary>目标与战斗状态确定后冻结的一次出牌解析结果，禁止执行端重新解释 Effects。</summary>
public sealed class ResolvedCardExecution
{
    public CardExecutionPlan Plan { get; }
    public int BattleStateVersion { get; }
    public string StateFingerprint { get; }
    public string Summary { get; }

    public ResolvedCardExecution(CardExecutionPlan plan, int battleStateVersion, string stateFingerprint, string summary)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        BattleStateVersion = battleStateVersion;
        StateFingerprint = stateFingerprint ?? "";
        Summary = summary ?? "";
    }
}

/// <summary>统一格式化有序效果，卡面、确认提示和日志不得按 CardInfo 旧字段重建。</summary>
public static class CardExecutionPlanFormatter
{
    public static string Format(CardExecutionPlan plan)
    {
        if (plan == null) return "";
        var parts = new List<string>();
        foreach (var effect in plan.Effects)
        {
            string text = effect.EffectType switch
            {
                CardEffectKind.DealDamage => $"造成 {effect.Amount} 伤害",
                CardEffectKind.GainBlock => $"获得 {effect.Amount} 护体",
                CardEffectKind.LoseHealth => $"失去 {effect.Amount} 生命",
                CardEffectKind.AddStatus => $"施加 {effect.StatusKind} {effect.Amount}",
                CardEffectKind.MoveSelfToZone => $"移入 {effect.DestinationZone}",
                _ => $"{effect.EffectType} {effect.Amount}",
            };
            parts.Add($"{effect.Order}. {text}");
        }
        return string.Join(" -> ", parts);
    }
}

/// <summary>一张战斗实例的执行轨迹，供日志和自检确认预览/执行顺序同源。</summary>
public sealed class CardExecutionTraceEntry
{
    public int Order { get; init; }
    public CardEffectKind EffectType { get; init; }
    public int Amount { get; init; }
}
