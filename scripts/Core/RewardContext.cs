using System.Collections.Generic;

/// <summary>单个额外卡牌奖励来源的显示与结算上下文。</summary>
public sealed class RewardSourceContext
{
    public string SourceId { get; init; }
    public string SlotId { get; init; }
    public string SourceName { get; init; }
    public string DisplayDescription { get; init; }
    public int CardRewardCount { get; init; }
    public string CardPoolId { get; init; }
    public int CandidateCount { get; init; }
    public int ChoiceCount { get; init; }
    public RewardSourceMergeRule MergeRule { get; init; }
}

/// <summary>
/// 战斗奖励上下文。奖励 UI 只消费此上下文，不根据道痕效果枚举或具体 ID 推断来源。
/// </summary>
public sealed class RewardContext
{
    private readonly List<RewardSourceContext> _extraCardSources = new();

    public IReadOnlyList<RewardSourceContext> ExtraCardSources => _extraCardSources;

    /// <summary>复制已验证的来源字段，供胜利事务保存不可被 UI 修改的计划快照。</summary>
    public RewardContext Clone()
    {
        var clone = new RewardContext();
        foreach (var source in _extraCardSources)
            clone._extraCardSources.Add(new RewardSourceContext
            {
                SourceId = source.SourceId,
                SlotId = source.SlotId,
                SourceName = source.SourceName,
                DisplayDescription = source.DisplayDescription,
                CardRewardCount = source.CardRewardCount,
                CardPoolId = source.CardPoolId,
                CandidateCount = source.CandidateCount,
                ChoiceCount = source.ChoiceCount,
                MergeRule = source.MergeRule,
            });
        return clone;
    }

    /// <summary>确认奖励入口使用的来源确实属于本场解析出的奖励上下文。</summary>
    public bool ContainsSource(RewardSourceContext source)
    {
        if (source == null)
            return false;

        foreach (var candidate in _extraCardSources)
        {
                if (candidate.SourceId == source.SourceId && candidate.SourceName == source.SourceName &&
                    candidate.DisplayDescription == source.DisplayDescription &&
                candidate.CardRewardCount == source.CardRewardCount && candidate.SlotId == source.SlotId &&
                candidate.CardPoolId == source.CardPoolId && candidate.CandidateCount == source.CandidateCount &&
                candidate.ChoiceCount == source.ChoiceCount && candidate.MergeRule == source.MergeRule)
                return true;
        }

        return false;
    }

    /// <summary>统一生成奖励行文案；控制器不根据道痕效果类型推断来源名称。</summary>
    public static string FormatCardRewardLabel(RewardSourceContext source,
        string baseLabel = "卡牌奖励")
    {
        if (source == null)
            return baseLabel;

        return $"{source.SourceName}：{source.DisplayDescription}";
    }

    /// <summary>从当前 RunState 的道痕定义构建奖励来源；缺少来源定义时显式失败。</summary>
    public static bool TryCreate(IEnumerable<DaoMarkInfo> daoMarks, out RewardContext context, out string error)
    {
        context = new RewardContext();
        error = "";

        if (daoMarks == null)
        {
            error = "奖励来源集合为空。";
            return false;
        }

        foreach (var daoMark in daoMarks)
        {
            if (daoMark == null)
            {
                error = "奖励来源定义为空。";
                return false;
            }

            if (daoMark.EffectType != DaoMarkEffect.额外奖励)
                continue;

            if (string.IsNullOrWhiteSpace(daoMark.Id) || string.IsNullOrWhiteSpace(daoMark.Name) ||
                string.IsNullOrWhiteSpace(daoMark.RewardDisplayDescription) || daoMark.EffectValue <= 0 ||
                string.IsNullOrWhiteSpace(daoMark.RewardSlotId) || string.IsNullOrWhiteSpace(daoMark.RewardCardPoolId) || daoMark.RewardCandidateCount <= 0 ||
                daoMark.RewardChoiceCount <= 0 || daoMark.RewardChoiceCount > daoMark.RewardCandidateCount ||
                !System.Enum.IsDefined(typeof(RewardSourceMergeRule), daoMark.RewardMergeRule))
            {
                error = $"额外奖励来源定义不完整：{daoMark.Id ?? "<missing-id>"}。";
                return false;
            }

            context._extraCardSources.Add(new RewardSourceContext
            {
                SourceId = daoMark.Id,
                SlotId = daoMark.RewardSlotId,
                SourceName = daoMark.Name,
                DisplayDescription = daoMark.RewardDisplayDescription,
                CardRewardCount = daoMark.EffectValue,
                CardPoolId = daoMark.RewardCardPoolId,
                CandidateCount = daoMark.RewardCandidateCount,
                ChoiceCount = daoMark.RewardChoiceCount,
                MergeRule = daoMark.RewardMergeRule,
            });
            if (!CardPoolCatalog.TryGet(daoMark.RewardCardPoolId, out var pool, out error) ||
                daoMark.RewardCandidateCount > pool.Cards.Count)
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = $"额外奖励来源候选数量超过卡池：{daoMark.Id}";
                context = new RewardContext();
                return false;
            }
        }

        return true;
    }
}
