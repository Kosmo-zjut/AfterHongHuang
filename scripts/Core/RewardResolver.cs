using System.Collections.Generic;

/// <summary>集中处理奖励定义、卡池候选和稳定随机取值。</summary>
public static class RewardResolver
{
    /// <summary>返回集中定义，不向 UI 暴露具体卡池数组。</summary>
    public static bool TryGetProfile(string profileId, out RewardProfileDefinition profile, out string error)
        => RewardProfileCatalog.TryGet(profileId, out profile, out error);

    /// <summary>为战斗主奖励解析不可变计划；奖励档案来自遭遇定义而非控制器层级判断。</summary>
    /// <summary>从遭遇请求生成主卡牌奖励计划。</summary>
    public static bool TryResolveBattleCardPlan(EncounterRequest request, StableRandom random,
        out RewardPlan plan, out string error)
    {
        plan = null;
        error = "";
        if (request == null || request.Node == null || request.EnemyInfo == null ||
            string.IsNullOrWhiteSpace(request.RewardProfileId))
        {
            error = "战斗奖励缺少 EncounterRequest、NodeContext 或 RewardProfileId。";
            return false;
        }
        if (request.EnemyInfo.RewardProfileId != request.RewardProfileId)
        {
            error = "EncounterRequest 的 RewardProfileId 与敌人定义不一致。";
            return false;
        }
        if (!TryGetProfile(request.RewardProfileId, out var profile, out error))
            return false;
        return TryBuildPlan(profile, null, request.Node.NodeId, profile.SlotId,
            request.EnemyInfo.Id, random, out plan, out error);
    }

    [System.Obsolete("生产流程不再由调用方传入奖励槽位。")]
    public static bool TryResolveBattleCardPlan(EncounterRequest request, string rewardSlotId,
        StableRandom random, out RewardPlan plan, out string error)
        => TryResolveBattleCardPlan(request, random, out plan, out error);

    /// <summary>为道痕等额外来源解析独立计划；卡池和槽位规则必须由来源定义声明。</summary>
    /// <summary>从显式奖励来源生成独立额外卡牌奖励计划。</summary>
    public static bool TryResolveExtraCardPlan(RewardSourceContext source, string nodeId,
        int sourceIndex, StableRandom random, out RewardPlan plan, out string error)
    {
        plan = null;
        error = "";
        if (source == null || string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(source.SourceId) ||
            string.IsNullOrWhiteSpace(source.SlotId) || string.IsNullOrWhiteSpace(source.CardPoolId) || source.CandidateCount <= 0 ||
            source.ChoiceCount <= 0 || sourceIndex < 0)
        {
            error = "额外奖励来源缺少明确卡池、候选数量、选择数量或节点上下文。";
            return false;
        }
        if (!CardPoolCatalog.TryGet(source.CardPoolId, out var pool, out error))
            return false;
        if (source.CandidateCount > pool.Cards.Count || source.ChoiceCount > source.CandidateCount)
        {
            error = $"额外奖励来源候选/选择数量非法：{source.SourceId}";
            return false;
        }

        var definition = new RewardProfileDefinition
        {
            ProfileId = $"source:{source.SourceId}",
            SlotId = source.SlotId,
            CardPoolId = source.CardPoolId,
            DisplayName = source.SourceName,
            Description = source.DisplayDescription,
            CandidateCount = source.CandidateCount,
            ChoiceCount = source.ChoiceCount,
            MergeRule = source.MergeRule,
        };
        return TryBuildPlan(definition, source.SourceId, nodeId,
            $"{source.SlotId}:{sourceIndex}", source.SourceId, random, out plan, out error);
    }

    private static bool TryBuildPlan(RewardProfileDefinition profile, string sourceId, string nodeId,
        string rewardSlotId, string encounterId, StableRandom random, out RewardPlan plan, out string error)
    {
        plan = null;
        error = "";
        if (!ValidateProfile(profile, out error) || random == null || string.IsNullOrWhiteSpace(nodeId) ||
            string.IsNullOrWhiteSpace(rewardSlotId))
        {
            if (string.IsNullOrEmpty(error))
                error = "奖励计划缺少节点、奖励槽或稳定随机流。";
            return false;
        }
        if (!CardPoolCatalog.TryGet(profile.CardPoolId, out var pool, out error))
            return false;

        var cards = new List<CardInfo>(pool.Cards);
        if (profile.CandidateCount > cards.Count || profile.ChoiceCount > profile.CandidateCount)
        {
            error = $"奖励档案候选/选择数量超过卡池：{profile.ProfileId}";
            return false;
        }
        random.Shuffle(cards);
        var selectedCards = new List<CardInfo>(profile.CandidateCount);
        for (int index = 0; index < profile.CandidateCount; index++)
            selectedCards.Add(RewardPlanValidator.CloneCardDefinition(cards[index]));

        plan = new RewardPlan
        {
            ProfileId = profile.ProfileId,
            NodeId = nodeId,
            EncounterId = encounterId,
            SourceId = sourceId,
            RewardSlotId = rewardSlotId,
            CardPoolId = profile.CardPoolId,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            CandidateCount = profile.CandidateCount,
            ChoiceCount = profile.ChoiceCount,
            MergeRule = profile.MergeRule,
            Candidates = selectedCards.AsReadOnly(),
        };
        if (!RewardPlanValidator.TryValidate(plan, out error))
        {
            plan = null;
            return false;
        }
        return true;
    }

    private static bool ValidateProfile(RewardProfileDefinition profile, out string error)
    {
        error = "";
        if (profile == null || string.IsNullOrWhiteSpace(profile.ProfileId) ||
            string.IsNullOrWhiteSpace(profile.CardPoolId) || string.IsNullOrWhiteSpace(profile.DisplayName) ||
            string.IsNullOrWhiteSpace(profile.Description) || profile.CandidateCount <= 0 ||
            profile.ChoiceCount <= 0 || profile.ChoiceCount > profile.CandidateCount ||
            !System.Enum.IsDefined(typeof(RewardSourceMergeRule), profile.MergeRule))
        {
            error = "奖励档案字段或合并规则非法。";
            return false;
        }
        return true;
    }

    /// <summary>
    /// 解析敌人定义中的闭区间灵韵奖励。非法区间直接失败，不返回零奖励继续结算。
    /// </summary>
    public static bool TryResolveLingYunAmount(EnemyInfo enemy, StableRandom random, out int amount, out string error)
    {
        amount = 0;
        error = "";
        if (enemy == null)
        {
            error = "灵韵奖励缺少敌人定义。";
            return false;
        }

        if (random == null)
        {
            error = $"遭遇 {enemy.Id ?? "<missing-id>"} 缺少奖励随机流。";
            return false;
        }

        if (enemy.RewardMin < 0 || enemy.RewardMax < enemy.RewardMin || enemy.RewardMax == int.MaxValue)
        {
            error = $"遭遇 {enemy.Id ?? "<missing-id>"} 的灵韵奖励区间非法：{enemy.RewardMin}..{enemy.RewardMax}。";
            return false;
        }

        amount = random.NextInt(enemy.RewardMin, enemy.RewardMax + 1);
        return true;
    }
}
