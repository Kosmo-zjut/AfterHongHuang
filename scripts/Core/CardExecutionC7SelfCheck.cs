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
        GD.Print("[CardExecutionC7SelfCheck] PASS canonical validator rejects illegal plans before execution");
    }

    private static CardEffectDefinition Effect(int order, CardEffectKind type, CardEffectTarget target, CardDurationScope duration) => new()
    {
        Order = order, EffectType = type, TargetSelector = target, Amount = 1,
        StatusKind = CardStatusKind.None, DurationScope = duration, DestinationZone = CardDestinationZone.None,
    };

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"[CardExecutionC7SelfCheck] {message}");
    }
}
