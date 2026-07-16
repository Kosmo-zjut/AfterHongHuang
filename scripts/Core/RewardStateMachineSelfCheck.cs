using Godot;
using System.Collections.Generic;

/// <summary>
/// C9 奖励状态机自检。覆盖胜利暂存/最终离场事务、来源防伪、候选不可变边界、
/// 多选领取和离场失败重试，不复制生产卡池常量。
/// </summary>
public static class RewardStateMachineSelfCheck
{
    public static void Run()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xC7_2026_0713_0001UL), "无法创建 C7 奖励自检新局");
        var battleNode = FindBattleNode(manager);
        Ensure(manager.TryEnterBattle(battleNode, out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        manager.EnemyHp = 0;
        Ensure(manager.TryRegisterEnemyDefeated(out var defeatError), defeatError);
        Ensure(manager.BattleOver && manager.PlayerWon && !manager.IsPlayerTurn,
            "敌人生命归零后未立即锁定胜利战斗状态");

        int deckBeforeInvalid = manager.GetDeckSize();
        int lingYunBeforeInvalid = manager.LingYun;
        int appliedResultsBeforeInvalid = manager.RunState.AppliedResultIds.Count;
        manager.DaoMarks.Add(new DaoMarkInfo
        {
            Id = "fixture_invalid_reward_source",
            Name = "无效来源",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 1,
            RewardDisplayDescription = "无效卡池",
            RewardSlotId = "fixture_invalid_slot",
            RewardCardPoolId = "missing_pool",
            RewardCandidateCount = 3,
            RewardChoiceCount = 1,
        });
        Ensure(!manager.TryBuildBattleVictoryPlan(out _, out _), "缺失卡池仍生成了胜利计划");
        Ensure(manager.BattleOver && manager.PlayerWon && !manager.IsPlayerTurn &&
            manager.UnclaimedLingYun == 0 &&
            manager.GetDeckSize() == deckBeforeInvalid && manager.LingYun == lingYunBeforeInvalid &&
            manager.RunState.AppliedResultIds.Count == appliedResultsBeforeInvalid &&
            manager.RunState.NodeStates[battleNode.NodeId] == NodeLifecycleState.Completed,
            "奖励计划失败后错误地恢复或改变了已结束战斗状态");
        manager.DaoMarks.RemoveAt(manager.DaoMarks.Count - 1);
        manager.DaoMarks.Add(new DaoMarkInfo
        {
            Id = "fixture_choice_two_source",
            Name = "多选来源",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 1,
            RewardDisplayDescription = "测试 3 选 2",
            RewardSlotId = "fixture_choice_two_slot",
            RewardCardPoolId = "reward_cards",
            RewardCandidateCount = 3,
            RewardChoiceCount = 2,
            RewardMergeRule = RewardSourceMergeRule.Independent,
        });

        Ensure(manager.TryBuildBattleVictoryPlan(out var plan, out var planError), planError);
        var basePlan = plan.CardRewards[0];
        var forgedVictory = new BattleVictoryPlan
        {
            NodeId = plan.NodeId,
            EncounterId = plan.EncounterId,
            RewardProfileId = plan.RewardProfileId,
            LingYunRewardId = plan.LingYunRewardId,
            LingYunAmount = plan.LingYunAmount,
            RewardContext = plan.RewardContext,
            CardRewards = new List<RewardPlan>(),
        };
        Ensure(!manager.TryCommitBattleVictory(forgedVictory, out _),
            "缺少完整奖励槽的外部胜利计划被错误提交");

        var replacementPool = CardPoolCatalog.TryGet(basePlan.CardPoolId, out var pool, out var poolError)
            ? pool.Cards
            : null;
        Ensure(replacementPool != null, poolError);
        CardInfo replacement = null;
        foreach (var card in replacementPool)
        {
            bool alreadyPresent = false;
            foreach (var candidate in basePlan.Candidates)
                if (candidate.Id == card.Id)
                    alreadyPresent = true;
            if (!alreadyPresent)
            {
                replacement = RewardPlanValidator.CloneCardDefinition(card);
                break;
            }
        }
        Ensure(replacement != null, "奖励自检卡池没有可用于替换候选的定义");
        var replacedCandidates = new List<CardInfo>(basePlan.Candidates);
        replacedCandidates[0] = replacement;
        var forgedCandidatePlan = new RewardPlan
        {
            NodeId = basePlan.NodeId,
            EncounterId = basePlan.EncounterId,
            ProfileId = basePlan.ProfileId,
            SourceId = basePlan.SourceId,
            RewardSlotId = basePlan.RewardSlotId,
            CardPoolId = basePlan.CardPoolId,
            DisplayName = basePlan.DisplayName,
            Description = basePlan.Description,
            CandidateCount = basePlan.CandidateCount,
            ChoiceCount = basePlan.ChoiceCount,
            MergeRule = basePlan.MergeRule,
            Candidates = replacedCandidates.AsReadOnly(),
        };
        var forgedCandidateVictory = new BattleVictoryPlan
        {
            NodeId = plan.NodeId,
            EncounterId = plan.EncounterId,
            RewardProfileId = plan.RewardProfileId,
            LingYunRewardId = plan.LingYunRewardId,
            LingYunAmount = plan.LingYunAmount,
            RewardContext = plan.RewardContext,
            CardRewards = new[] { forgedCandidatePlan },
        };
        Ensure(!manager.TryCommitBattleVictory(forgedCandidateVictory, out _),
            "替换同卡池候选的外部胜利计划被错误提交");

        Ensure(CardPoolCatalog.TryGet("boss_rewards", out var bossPool, out var bossPoolError), bossPoolError);
        Ensure(bossPool.Cards.Count >= basePlan.CandidateCount, "Boss 卡池候选不足，无法执行防伪自检");
        var bossCandidates = new List<CardInfo>();
        for (int index = 0; index < basePlan.CandidateCount; index++)
            bossCandidates.Add(RewardPlanValidator.CloneCardDefinition(bossPool.Cards[index]));
        var forgedBossPlan = new RewardPlan
        {
            NodeId = basePlan.NodeId,
            EncounterId = basePlan.EncounterId,
            ProfileId = basePlan.ProfileId,
            SourceId = basePlan.SourceId,
            RewardSlotId = basePlan.RewardSlotId,
            CardPoolId = "boss_rewards",
            DisplayName = basePlan.DisplayName,
            Description = basePlan.Description,
            CandidateCount = basePlan.CandidateCount,
            ChoiceCount = basePlan.ChoiceCount,
            MergeRule = basePlan.MergeRule,
            Candidates = bossCandidates.AsReadOnly(),
        };
        var forgedBossVictory = new BattleVictoryPlan
        {
            NodeId = plan.NodeId,
            EncounterId = plan.EncounterId,
            RewardProfileId = plan.RewardProfileId,
            LingYunRewardId = plan.LingYunRewardId,
            LingYunAmount = plan.LingYunAmount,
            RewardContext = plan.RewardContext,
            CardRewards = new[] { forgedBossPlan },
        };
        Ensure(!manager.TryCommitBattleVictory(forgedBossVictory, out _),
            "普通战斗计划伪造成 Boss 卡池后被错误提交");

        Ensure(manager.TryCommitBattleVictory(plan, out var commitError), commitError);
        Ensure(manager.BattleOver && manager.PlayerWon && manager.ActiveBattleVictoryPlan == plan,
            "合法胜利计划未提交或未由 GameManager 持有");
        Ensure(manager.RunState.NodeStates[battleNode.NodeId] == NodeLifecycleState.Completed &&
            manager.RunState.AppliedResultIds.Count == appliedResultsBeforeInvalid,
            "敌人死亡后未立即提交唯一战斗节点结果");
        int deckBeforeClaim = manager.GetDeckSize();
        int lingYunBeforeClaim = manager.LingYun;
        var extraPlan = plan.CardRewards[1];
        Ensure(manager.TryClaimBattleCard(plan, basePlan, 0, out bool complete, out var claimError), claimError);
        Ensure(complete && manager.GetDeckSize() == deckBeforeClaim + 1,
            "ChoiceCount=1 奖励未在一次领取后完成");
        Ensure(!manager.TryClaimBattleCard(plan, basePlan, 0, out _, out _),
            "重复领取同一候选未被拒绝");
        Ensure(manager.TryClaimBattleCard(plan, extraPlan, 0, out complete, out claimError), claimError);
        Ensure(!complete && manager.TryClaimBattleCard(plan, extraPlan, 1, out complete, out claimError), claimError);
        Ensure(complete && manager.GetDeckSize() == deckBeforeClaim + 3,
            "ChoiceCount=2 未完成两次真实领取");
        Ensure(!manager.TryClaimBattleCard(plan, extraPlan, 2, out _, out _),
            "ChoiceCount=2 第三次领取未被拒绝");

        var forged = new RewardPlan
        {
            NodeId = basePlan.NodeId,
            EncounterId = basePlan.EncounterId,
            ProfileId = basePlan.ProfileId,
            SourceId = basePlan.SourceId,
            RewardSlotId = basePlan.RewardSlotId,
            CardPoolId = basePlan.CardPoolId,
            DisplayName = basePlan.DisplayName,
            Description = basePlan.Description,
            CandidateCount = basePlan.CandidateCount,
            ChoiceCount = basePlan.ChoiceCount,
            MergeRule = basePlan.MergeRule,
            Candidates = basePlan.Candidates,
        };
        Ensure(!manager.TryClaimBattleCard(plan, forged, 1, out _, out _),
            "外部伪造 RewardPlan 被错误接受");

        var multiChoice = new RewardPlan
        {
            NodeId = basePlan.NodeId,
            ProfileId = "fixture_multi_choice",
            RewardSlotId = "fixture_multi_choice_slot",
            CardPoolId = basePlan.CardPoolId,
            DisplayName = "测试多选奖励",
            Description = "测试 3 选 2",
            CandidateCount = 3,
            ChoiceCount = 2,
            MergeRule = RewardSourceMergeRule.Independent,
            Candidates = new List<CardInfo>(basePlan.Candidates),
        };
        Ensure(RewardPlanValidator.TryValidate(multiChoice, out var multiError), multiError);
        Ensure(!manager.TryClaimBattleCard(plan, multiChoice, 0, out _, out _),
            "不同槽位的多选计划被错误接受");

        Ensure(manager.ClaimUnclaimedLingYun(plan.LingYunRewardId, out var lingYunError), lingYunError);
        Ensure(manager.LingYun == lingYunBeforeClaim + plan.LingYunAmount,
            "灵韵领取未写入永久资源");
#if DEBUG
        manager.InjectBattleExitFailureForSelfCheck();
#endif
        int deckAfterClaims = manager.GetDeckSize();
        int lingYunAfterClaims = manager.LingYun;
        Ensure(!manager.TryExitBattleToMapAfterVictory(out var injectedExitError),
            "离场失败注入未阻止胜利页保持");
        Ensure(manager.GetDeckSize() == deckAfterClaims && manager.LingYun == lingYunAfterClaims &&
            manager.UnclaimedLingYun == 0 &&
            manager.RunState.NodeStates[battleNode.NodeId] == NodeLifecycleState.Completed &&
            manager.ActiveBattle != null && manager.BattleOver && manager.PlayerWon,
            $"离场失败后奖励/节点/战斗状态被改变：{injectedExitError}");

        Ensure(manager.TryExitBattleToMapAfterVictory(out var exitError), exitError);
        Ensure(manager.RunState.NodeStates[battleNode.NodeId] == NodeLifecycleState.Completed &&
            manager.ActiveBattle == null && manager.GetDeckSize() == deckBeforeClaim + 3 &&
            manager.PendingMapEntry == MapEntryMode.None,
            "直接进入下一节点时残留了 MapScene 地图入口意图");
        manager.Free();
        CheckContinueWithUnclaimedReward();
        CheckCandidateIsolation();
        GD.Print("[RewardStateMachineSelfCheck] PASS victory plan transaction, multi-choice validation and owned-plan claims");
    }

    /// <summary>
    /// C11 回归：继续只销毁已结束战斗实例并打开地图目标，未领取奖励不阻止导航，
    /// 已领取卡牌不会被回滚，且继续不新增 NodeResult 或永久灵韵。
    /// </summary>
    private static void CheckContinueWithUnclaimedReward()
    {
        var failedRewardManager = new GameManager();
        Ensure(failedRewardManager.StartNewRun("wuzhu", 0xC11_FA1UL), "无法创建奖励失败继续自检新局");
        var failedNode = FindBattleNode(failedRewardManager);
        Ensure(failedRewardManager.TryEnterBattle(failedNode, out var failedEnterError), failedEnterError);
        Ensure(failedRewardManager.TryBeginActiveBattle(out var failedBeginError), failedBeginError);
        failedRewardManager.EnemyHp = 0;
        Ensure(failedRewardManager.TryRegisterEnemyDefeated(out var failedDefeatError), failedDefeatError);
        failedRewardManager.DaoMarks.Add(new DaoMarkInfo
        {
            Id = "fixture_c11_missing_pool",
            Name = "奖励错误 fixture",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 1,
            RewardDisplayDescription = "缺失卡池",
            RewardSlotId = "fixture_c11_missing_slot",
            RewardCardPoolId = "missing_pool",
            RewardCandidateCount = 3,
            RewardChoiceCount = 1,
        });
        int failedDeck = failedRewardManager.GetDeckSize();
        int failedLingYun = failedRewardManager.LingYun;
        int failedResultCount = failedRewardManager.RunState.AppliedResultIds.Count;
        Ensure(!failedRewardManager.TryBuildBattleVictoryPlan(out _, out _),
            "奖励生成失败 fixture 未被拒绝");
        Ensure(failedRewardManager.BattleOver && failedRewardManager.PlayerWon,
            "奖励生成失败后战斗被错误恢复");
        Ensure(failedRewardManager.TryExitBattleToMapAfterVictory(out var failedExitError), failedExitError);
        Ensure(failedRewardManager.ActiveBattle == null && failedRewardManager.GetDeckSize() == failedDeck &&
            failedRewardManager.LingYun == failedLingYun && failedRewardManager.UnclaimedLingYun == 0 &&
            failedRewardManager.RunState.AppliedResultIds.Count == failedResultCount &&
            failedRewardManager.PendingMapEntry == MapEntryMode.None,
            "奖励生成失败后继续残留了 MapScene 地图入口意图");
        failedRewardManager.Free();

        var partialManager = new GameManager();
        Ensure(partialManager.StartNewRun("wuzhu", 0xC11_CA7DUL), "无法创建部分领取继续自检新局");
        var partialNode = FindBattleNode(partialManager);
        Ensure(partialManager.TryEnterBattle(partialNode, out var partialEnterError), partialEnterError);
        Ensure(partialManager.TryBeginActiveBattle(out var partialBeginError), partialBeginError);
        partialManager.EnemyHp = 0;
        Ensure(partialManager.TryRegisterEnemyDefeated(out var partialDefeatError), partialDefeatError);
        Ensure(partialManager.TryBuildBattleVictoryPlan(out var partialPlan, out var partialPlanError), partialPlanError);
        Ensure(partialManager.TryCommitBattleVictory(partialPlan, out var partialCommitError), partialCommitError);
        int partialDeck = partialManager.GetDeckSize();
        int partialLingYun = partialManager.LingYun;
        int partialResultCount = partialManager.RunState.AppliedResultIds.Count;
        Ensure(partialManager.TryClaimBattleCard(partialPlan, partialPlan.CardRewards[0], 0,
            out bool partialSlotComplete, out var partialClaimError), partialClaimError);
        Ensure(partialSlotComplete && partialManager.GetDeckSize() == partialDeck + 1,
            "部分领取继续自检未真实写入已选卡牌");
        Ensure(partialManager.TryExitBattleToMapAfterVictory(out var partialExitError), partialExitError);
        Ensure(partialManager.ActiveBattle == null && partialManager.GetDeckSize() == partialDeck + 1 &&
            partialManager.LingYun == partialLingYun && partialManager.UnclaimedLingYun == 0 &&
            partialManager.RunState.AppliedResultIds.Count == partialResultCount &&
            partialManager.PendingMapEntry == MapEntryMode.None,
            "部分领取后继续残留了 MapScene 地图入口意图");
        partialManager.Free();
    }

    private static MapNodeDefinition FindBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Battle)
                return node;
        throw new System.InvalidOperationException("奖励自检找不到战斗节点。");
    }

    private static MapNodeDefinition FindBossNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Boss)
                return node;
        throw new System.InvalidOperationException("奖励自检找不到 Boss 节点。");
    }

    /// <summary>普通与 Boss 计划都验证候选篡改拒绝，以及领取后永久牌组不持有旧候选引用。</summary>
    private static void CheckCandidateIsolation()
    {
        CheckCandidateIsolationForNode(false);
        CheckCandidateIsolationForNode(true);
    }

    private static void CheckCandidateIsolationForNode(bool boss)
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", boss ? 0xC9_B055_0001UL : 0xC9_0D00_0001UL),
            "无法创建候选不可变边界自检新局");
        var node = boss ? FindBossNode(manager) : FindBattleNode(manager);
        Ensure(manager.TryEnterBattle(node, out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        manager.EnemyHp = 0;
        Ensure(manager.TryRegisterEnemyDefeated(out var defeatError), defeatError);
        Ensure(manager.TryBuildBattleVictoryPlan(out var plan, out var planError), planError);
        Ensure(manager.TryCommitBattleVictory(plan, out var commitError), commitError);

        var candidate = plan.CardRewards[0].Candidates[0];
        string originalName = candidate.Name;
        int originalValue = candidate.Value;
        int originalCost = candidate.Cost;
        string originalDescription = candidate.Description;
        CardType originalType = candidate.Type;
        int deckBefore = manager.GetDeckSize();
        candidate.Name = "篡改候选";
        candidate.Value += 999;
        candidate.Cost += 1;
        candidate.Description = "篡改描述";
        candidate.Type = originalType == CardType.斗击 ? CardType.术法 : CardType.斗击;
        Ensure(!manager.TryClaimBattleCard(plan, plan.CardRewards[0], 0, out _, out _),
            "奖励候选被篡改后仍允许领取");
        Ensure(manager.GetDeckSize() == deckBefore, "候选篡改失败路径改变了永久牌组");

        candidate.Name = originalName;
        candidate.Value = originalValue;
        candidate.Cost = originalCost;
        candidate.Description = originalDescription;
        candidate.Type = originalType;
        Ensure(manager.TryClaimBattleCard(plan, plan.CardRewards[0], 0, out _, out var claimError), claimError);
        var grantedCard = manager.GetPermanentDeckCards()[manager.GetDeckSize() - 1];
        string grantedName = grantedCard.Info.Name;
        candidate.Name = "领取后再次篡改";
        Ensure(grantedCard.Info.Name == grantedName,
            "永久牌组仍引用奖励计划的可变候选 CardInfo");
        manager.Free();
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[RewardStateMachineSelfCheck] {error}");
    }

    private static string DeckSignature(IReadOnlyList<CardRuntime> cards)
    {
        var parts = new List<string>();
        foreach (var card in cards)
            parts.Add(card?.Info?.Id ?? "<missing-card>");
        return string.Join("|", parts);
    }
}
