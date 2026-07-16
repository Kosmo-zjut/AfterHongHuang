using System.Collections.Generic;

/// <summary>战斗奖励档案集中目录；流程层只按 ProfileId 解析，不按遭遇层级猜测卡池。</summary>
public static class RewardProfileCatalog
{
    private static readonly IReadOnlyList<RewardProfileDefinition> Definitions = new[]
    {
        new RewardProfileDefinition
        {
            ProfileId = "battle_default", SlotId = "battle_main", CardPoolId = "reward_cards", DisplayName = "卡牌奖励",
            Description = "选择一张卡牌加入牌组", CandidateCount = 3, ChoiceCount = 1,
            MergeRule = RewardSourceMergeRule.Independent,
        },
        new RewardProfileDefinition
        {
            ProfileId = "battle_boss", SlotId = "battle_boss_main", CardPoolId = "boss_rewards", DisplayName = "首领卡牌奖励",
            Description = "选择一张卡牌加入牌组", CandidateCount = 3, ChoiceCount = 1,
            MergeRule = RewardSourceMergeRule.Independent,
        },
    };

    /// <summary>按 ProfileId 解析并校验奖励档案。</summary>
    public static bool TryGet(string profileId, out RewardProfileDefinition profile, out string error)
    {
        profile = null;
        error = "";
        foreach (var candidate in Definitions)
        {
            if (candidate.ProfileId != profileId)
                continue;
            if (candidate.CandidateCount <= 0 || candidate.ChoiceCount <= 0 ||
                candidate.ChoiceCount > candidate.CandidateCount ||
                string.IsNullOrWhiteSpace(candidate.SlotId) ||
                string.IsNullOrWhiteSpace(candidate.CardPoolId) ||
                string.IsNullOrWhiteSpace(candidate.DisplayName) ||
                string.IsNullOrWhiteSpace(candidate.Description) ||
                !System.Enum.IsDefined(typeof(RewardSourceMergeRule), candidate.MergeRule))
            {
                error = $"奖励档案定义非法：{profileId}";
                return false;
            }
            if (!CardPoolCatalog.TryGet(candidate.CardPoolId, out _, out error))
                return false;
            profile = candidate;
            return true;
        }

        error = $"奖励档案不存在：{profileId}";
        return false;
    }
}
