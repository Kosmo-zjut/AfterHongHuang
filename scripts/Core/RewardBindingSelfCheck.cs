using Godot;

/// <summary>Debug 构建奖励/角色定义绑定自检，反证同一解析入口不依赖具体首项名称。</summary>
public static class RewardBindingSelfCheck
{
    public static void Run()
    {
        Ensure(DataDefs.TryGetCharacterDefinition("wuzhu", out var wuzhu), "未找到生产角色定义");
        Ensure(CharacterDeckFactory.TryCreate(wuzhu, out var productionDeck, out var deckError), deckError);

        var fixtureCharacter = new CharacterInfo
        {
            Id = "fixture_character_b",
            Name = "测试角色 B",
            StarterDeck = new[]
            {
                new CardInfo { Id = "fixture_card_b", Name = "测试牌 B", Type = CardType.术法, TargetMode = CardTargetMode.Self }
            }
        };
        Ensure(CharacterDeckFactory.TryCreate(fixtureCharacter, out var fixtureDeck, out deckError), deckError);
        Ensure(productionDeck.Count != fixtureDeck.Count || productionDeck[0].Info.Id != fixtureDeck[0].Info.Id,
            "两个 CharacterDefinition 未经同一初始牌组解析入口区分");

        Ensure(!DataDefs.TryGetCharacterDefinition("fixture_missing_character", out _),
            "缺失角色定义被错误解析");
        var emptyDeckCharacter = new CharacterInfo
        {
            Id = "fixture_empty_deck",
            Name = "空牌组测试角色",
            StarterDeck = System.Array.Empty<CardInfo>(),
        };
        Ensure(!CharacterDeckFactory.TryCreate(emptyDeckCharacter, out _, out _),
            "空初始牌组被错误接受");

        var sourceA = new DaoMarkInfo
        {
            Id = "fixture_reward_source_a",
            Name = "测试来源 A",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 1,
            RewardDisplayDescription = "来源 A 的额外卡牌奖励",
            RewardSlotId = "fixture_source_a",
            RewardCardPoolId = "reward_cards",
            RewardCandidateCount = 3,
            RewardChoiceCount = 1,
            RewardMergeRule = RewardSourceMergeRule.Independent,
        };
        var sourceB = new DaoMarkInfo
        {
            Id = "fixture_reward_source_b",
            Name = "测试来源 B",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 2,
            RewardDisplayDescription = "来源 B 的额外卡牌奖励",
            RewardSlotId = "fixture_source_b",
            RewardCardPoolId = "boss_rewards",
            RewardCandidateCount = 3,
            RewardChoiceCount = 1,
            RewardMergeRule = RewardSourceMergeRule.Combined,
        };
        Ensure(RewardContext.TryCreate(new[] { sourceA }, out var contextA, out var contextError), contextError);
        Ensure(RewardContext.TryCreate(new[] { sourceB }, out var contextB, out contextError), contextError);
        Ensure(RewardContext.TryCreate(new[] { sourceA, sourceB }, out var combinedContext, out contextError),
            contextError);
        Ensure(contextA.ExtraCardSources.Count == 1 && contextA.ExtraCardSources[0].SourceName == sourceA.Name &&
            contextA.ExtraCardSources[0].CardRewardCount == sourceA.EffectValue, "奖励来源 A 上下文绑定错误");
        Ensure(contextB.ExtraCardSources.Count == 1 && contextB.ExtraCardSources[0].SourceName == sourceB.Name &&
            contextB.ExtraCardSources[0].CardRewardCount == sourceB.EffectValue, "奖励来源 B 上下文绑定错误");
        Ensure(combinedContext.ExtraCardSources.Count == 2 && combinedContext.ContainsSource(
            combinedContext.ExtraCardSources[0]) && combinedContext.ContainsSource(
            combinedContext.ExtraCardSources[1]), "奖励来源 A+B 未共享同一 RewardContext");
        Ensure(RewardContext.FormatCardRewardLabel(contextA.ExtraCardSources[0]) !=
            RewardContext.FormatCardRewardLabel(contextB.ExtraCardSources[0]),
            "奖励来源 UI 格式化未绑定上下文字段");

        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xC3E7_0001UL), "无法创建奖励上下文自检新局");
        int deckBeforeInvalid = manager.GetDeckSize();
        int lingYunBeforeInvalid = manager.LingYun;
        Ensure(!manager.TryCreateRewardId("card", "invalid", out _, out _),
            "缺少 ActiveNode 时错误创建了奖励 ID");
        Ensure(!manager.GrantReward(new RewardGrant
            {
                RewardId = "card:missing-node:0",
                GrantType = RewardGrantType.Card,
                Card = fixtureCharacter.StarterDeck[0],
            }, out _), "缺少 ActiveNode 时错误写入奖励");
        Ensure(manager.GetDeckSize() == deckBeforeInvalid && manager.LingYun == lingYunBeforeInvalid,
            "无活动节点奖励调用改变了永久状态");

        var battleNode = manager.MapGraph.Layers[1][0];
        Ensure(manager.TryEnterBattle(battleNode, out var enterError), enterError);
        Ensure(RewardResolver.TryResolveBattleCardPlan(manager.ActiveEncounter,
                manager.RandomStreams.CreateRewardStream(manager.ActiveNode.NodeId, 3),
            out var basePlan, out var planError), planError);
        Ensure(basePlan.CardPoolId == "reward_cards" && basePlan.Candidates.Count == 3,
            "普通战斗 RewardProfile 未解析正确候选计划");
        var bossRequest = new EncounterRequest
        {
            Node = manager.ActiveNode,
            EnemyInfo = new EnemyInfo { Id = "fixture_boss_reward", RewardProfileId = "battle_boss" },
            RewardProfileId = "battle_boss",
        };
        Ensure(RewardResolver.TryResolveBattleCardPlan(bossRequest,
                manager.RandomStreams.CreateRewardStream(manager.ActiveNode.NodeId, 6),
            out var bossPlan, out planError), planError);
        Ensure(bossPlan.CardPoolId == "boss_rewards" && bossPlan.DisplayName != basePlan.DisplayName,
            "不同战斗 RewardProfile 未通过同一入口区分卡池和标题");
        Ensure(RewardResolver.TryResolveExtraCardPlan(contextA.ExtraCardSources[0], manager.ActiveNode.NodeId, 0,
            manager.RandomStreams.CreateRewardStream(manager.ActiveNode.NodeId, 4), out var sourcePlanA, out planError), planError);
        Ensure(RewardResolver.TryResolveExtraCardPlan(contextB.ExtraCardSources[0], manager.ActiveNode.NodeId, 0,
            manager.RandomStreams.CreateRewardStream(manager.ActiveNode.NodeId, 5), out var sourcePlanB, out planError), planError);
        Ensure(sourcePlanA.CardPoolId != sourcePlanB.CardPoolId && sourcePlanA.DisplayName != sourcePlanB.DisplayName,
            "不同额外来源未通过同一 RewardResolver 绑定不同卡池/文案");
        Ensure(sourcePlanA.MergeRule == RewardSourceMergeRule.Independent &&
            sourcePlanB.MergeRule == RewardSourceMergeRule.Combined,
            "额外来源 MergeRule 未从 Context 传入 RewardPlan");
        Ensure(manager.TryCreateRewardId("card", "fixture", out var validRewardId, out var idError), idError);
        int deckBeforeValid = manager.GetDeckSize();
        Ensure(manager.GrantReward(new RewardGrant
            {
                RewardId = validRewardId,
                GrantType = RewardGrantType.Card,
                // 领取自检必须使用生产 Catalog 候选；fixture 卡仅用于角色定义反证，不能绕过执行计划门禁。
                Card = basePlan.Candidates[0],
            }, out var grantError), grantError);
        Ensure(manager.GetDeckSize() == deckBeforeValid + 1, "有效卡牌奖励未增加永久牌组");
        Ensure(manager.TryCreateRewardId("lingyun", "fixture", out var validLingYunId, out idError), idError);
        int lingYunBeforeValid = manager.LingYun;
        Ensure(manager.GrantReward(new RewardGrant
            {
                RewardId = validLingYunId,
                GrantType = RewardGrantType.LingYun,
                LingYunAmount = 7,
            }, out grantError), grantError);
        Ensure(manager.LingYun == lingYunBeforeValid + 7, "有效灵韵奖励未增加 RunState 灵韵");
        manager.Free();

        var invalidSource = new DaoMarkInfo
        {
            Id = "fixture_invalid_reward_source",
            Name = "非法来源",
            EffectType = DaoMarkEffect.额外奖励,
            EffectValue = 0,
            RewardDisplayDescription = ""
        };
        Ensure(!RewardContext.TryCreate(new[] { invalidSource }, out _, out _), "非法奖励来源被错误接受");

        var invalidEnemy = new EnemyInfo { Id = "fixture_invalid_reward_enemy", RewardMin = 5, RewardMax = 2 };
        Ensure(!RewardResolver.TryResolveLingYunAmount(invalidEnemy, new StableRandom(1), out _, out _),
            "非法灵韵区间被错误解析");

        GD.Print("[RewardBindingSelfCheck] PASS character starter decks, reward sources, invalid reward bounds");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[RewardBindingSelfCheck] {error}");
    }
}
