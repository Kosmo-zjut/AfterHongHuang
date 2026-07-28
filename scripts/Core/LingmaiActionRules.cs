using Godot;

/// <summary>Pure Lingmai action calculations shared by UI and debug self-checks.</summary>
public static class LingmaiActionRules
{
    /// <summary>Resolves a rest action, including the valid no-op outcome when HP is already full.</summary>
    public static LingmaiRestResult ResolveRest(int currentHp, int maximumHp)
    {
        if (maximumHp <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(maximumHp), "最大生命必须大于零。");

        int clampedCurrentHp = Mathf.Clamp(currentHp, 0, maximumHp);
        int requestedHealing = Mathf.Max(1, Mathf.FloorToInt(maximumHp * 0.3f));
        int resultingHp = Mathf.Min(maximumHp, clampedCurrentHp + requestedHealing);
        return new LingmaiRestResult(clampedCurrentHp, resultingHp, requestedHealing);
    }

    /// <summary>
    /// Executes the roster-backed companion action through its production lookup and returns the
    /// exact UI feedback contract. A full-health target is a valid no-op that still consumes this
    /// one Lingmai action; callers must use ActionConsumed to disable every companion entry.
    /// </summary>
    public static bool TryResolveCompanionHealing(PartyRoster roster, string playerMemberId,
        string targetMemberId, out LingmaiCompanionActionResult result, out string error)
    {
        result = default;
        error = "";
        if (roster == null || !roster.TryApplyLingmaiHealing(playerMemberId, targetMemberId,
                out var healing, out error))
            return false;

        string feedback = healing.Rest.WasAtFullHealth
            ? $"疗愈道友：{healing.DisplayName} 气血已满，生命保持 {healing.Rest.ResultingHp}；本次行动已使用"
            : $"疗愈道友：{healing.DisplayName} 生命 {healing.Rest.PreviousHp} → {healing.Rest.ResultingHp}";
        result = new LingmaiCompanionActionResult(healing, true, feedback);
        return true;
    }

    /// <summary>UI 与自检共用的一次性入口状态规则，避免单独按钮遗留可重复治疗路径。</summary>
    public static bool IsCompanionActionEnabled(bool actionConsumed) => !actionConsumed;
}

/// <summary>Immutable result of a single rest action.</summary>
public readonly record struct LingmaiRestResult(int PreviousHp, int ResultingHp, int RequestedHealing)
{
    public int ActualHealing => ResultingHp - PreviousHp;
    public bool WasAtFullHealth => ActualHealing == 0;
}

/// <summary>一次同伴疗愈动作的结果，包含与按钮灰置同源的消费事实。</summary>
public readonly record struct LingmaiCompanionActionResult(
    PartyHealingResult Healing,
    bool ActionConsumed,
    string Feedback);
