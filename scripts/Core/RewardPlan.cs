using System;
using System.Collections.Generic;

/// <summary>卡牌奖励来源合并规则。MVP 每个来源独立生成一个奖励槽。</summary>
public enum RewardSourceMergeRule
{
    Independent = 0,
    /// <summary>显式配置的合并路径；MVP 暂不将不同来源压成同一 UI 行。</summary>
    Combined = 1,
}

/// <summary>战斗或额外奖励的集中定义；控制器只消费解析后的计划。</summary>
public sealed class RewardProfileDefinition
{
    public string ProfileId { get; init; }
    public string SlotId { get; init; }
    public string CardPoolId { get; init; }
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public int CandidateCount { get; init; }
    public int ChoiceCount { get; init; }
    public RewardSourceMergeRule MergeRule { get; init; }
}

/// <summary>
/// 不可变卡牌奖励计划。随机抽取、来源文案和奖励槽在 UI 创建前已经完成并校验，
/// 因此 BattleController/CardRewardController 不需要知道具体卡池或遭遇层级。
/// </summary>
public sealed class RewardPlan
{
    public string NodeId { get; init; }
    public string EncounterId { get; init; }
    public string ProfileId { get; init; }
    public string SourceId { get; init; }
    public string RewardSlotId { get; init; }
    public string CardPoolId { get; init; }
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public int CandidateCount { get; init; }
    public int ChoiceCount { get; init; }
    public RewardSourceMergeRule MergeRule { get; init; }
    public IReadOnlyList<CardInfo> Candidates { get; init; }
}

/// <summary>奖励计划的集中校验器；UI 不解释来源合并或选择规则。</summary>
public static class RewardPlanValidator
{
    /// <summary>比较奖励计划的所有字段和候选卡完整定义，不只比较 CardId。</summary>
    public static bool AreEquivalent(RewardPlan left, RewardPlan right)
    {
        if (left == null || right == null || left.NodeId != right.NodeId ||
            left.EncounterId != right.EncounterId || left.ProfileId != right.ProfileId ||
            left.SourceId != right.SourceId || left.RewardSlotId != right.RewardSlotId ||
            left.CardPoolId != right.CardPoolId || left.DisplayName != right.DisplayName ||
            left.Description != right.Description || left.CandidateCount != right.CandidateCount ||
            left.ChoiceCount != right.ChoiceCount || left.MergeRule != right.MergeRule ||
            left.Candidates == null || right.Candidates == null ||
            left.Candidates.Count != right.Candidates.Count)
            return false;

        for (int index = 0; index < left.Candidates.Count; index++)
            if (!AreCardDefinitionsEqual(left.Candidates[index], right.Candidates[index]))
                return false;
        return true;
    }

    public static bool TryValidate(RewardPlan plan, out string error)
    {
        error = "";
        if (plan == null || string.IsNullOrWhiteSpace(plan.NodeId) ||
            string.IsNullOrWhiteSpace(plan.ProfileId) || string.IsNullOrWhiteSpace(plan.RewardSlotId) ||
            string.IsNullOrWhiteSpace(plan.CardPoolId) || string.IsNullOrWhiteSpace(plan.DisplayName) ||
            string.IsNullOrWhiteSpace(plan.Description) || plan.Candidates == null ||
            plan.CandidateCount <= 0 || plan.CandidateCount != plan.Candidates.Count ||
            plan.ChoiceCount <= 0 || plan.ChoiceCount > plan.CandidateCount ||
            !System.Enum.IsDefined(typeof(RewardSourceMergeRule), plan.MergeRule))
        {
            error = "奖励计划字段、候选数量或合并规则非法。";
            return false;
        }

        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var card in plan.Candidates)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Id) || !ids.Add(card.Id))
            {
                error = $"奖励计划候选为空或重复：{plan.RewardSlotId}";
                return false;
            }
        }
        return true;
    }

    /// <summary>逐字段比对计划候选与声明卡池，防止只替换 CardId 绕过奖励档案。</summary>
    public static bool TryValidateCandidatesAgainstPool(RewardPlan plan,
        CardPoolDefinition pool, out string error)
    {
        error = "";
        if (!TryValidate(plan, out error) || !CardPoolCatalog.TryValidate(pool, out error))
            return false;

        foreach (var candidate in plan.Candidates)
        {
            bool matched = false;
            foreach (var poolCard in pool.Cards)
            {
                if (AreCardDefinitionsEqual(candidate, poolCard))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                error = $"奖励候选完整定义不属于声明卡池：{plan.RewardSlotId}/{candidate.Id}";
                return false;
            }
        }
        return true;
    }

    /// <summary>复制卡牌定义，避免计划候选与全局 Catalog 共享可变 CardInfo 引用。</summary>
    public static CardInfo CloneCardDefinition(CardInfo source)
    {
        if (source == null)
            return null;
        return new CardInfo
        {
            DefinitionId = source.DefinitionId,
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Cost = source.Cost,
            Value = source.Value,
            Description = source.Description,
            SelfDamage = source.SelfDamage,
            HasSecondary = source.HasSecondary,
            SecondaryValue = source.SecondaryValue,
            SecondaryType = source.SecondaryType,
            TargetMode = source.TargetMode,
            RequiresEnemyTarget = source.RequiresEnemyTarget,
            Exhausts = source.Exhausts,
            SelfGuardValue = source.SelfGuardValue,
            UpgradeToId = source.UpgradeToId,
        };
    }

    private static bool AreCardDefinitionsEqual(CardInfo left, CardInfo right)
    {
        return left != null && right != null && left.Id == right.Id && left.Name == right.Name &&
            left.Type == right.Type && left.Cost == right.Cost && left.Value == right.Value &&
            left.Description == right.Description && left.SelfDamage == right.SelfDamage &&
            left.HasSecondary == right.HasSecondary && left.SecondaryValue == right.SecondaryValue &&
            left.SecondaryType == right.SecondaryType && left.TargetMode == right.TargetMode &&
            left.RequiresEnemyTarget == right.RequiresEnemyTarget && left.Exhausts == right.Exhausts &&
            left.SelfGuardValue == right.SelfGuardValue && left.UpgradeToId == right.UpgradeToId;
    }
}
