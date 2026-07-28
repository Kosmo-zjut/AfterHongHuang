using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Debug 构建的迁移门禁：验证 21 张 Resource、池索引、旧快照双读和 B1 的受控差异。
/// 内容 ID 仅作为迁移 fixture，不参与生产控制器分支。
/// </summary>
public static class CardCatalogSelfCheck
{
    private static readonly IReadOnlyDictionary<string, (int Value, int Guard, int SelfDamage, int Secondary, bool Exhaust)> B1Overrides =
        new Dictionary<string, (int, int, int, int, bool)>
        {
            ["wx_02_p"] = (7, 7, 0, 0, false),
            ["wx_r_005"] = (10, 10, 2, 0, false),
            ["wx_r_006"] = (1, 0, 3, 1, true),
            ["wx_r_007"] = (11, 0, 2, 1, false),
            ["wx_r_008"] = (2, 0, 4, 2, true),
            ["wx_r_009"] = (16, 16, 3, 0, false),
            ["wx_a1_boss_002"] = (16, 16, 0, 1, false),
        };

    public static void Run()
    {
        CardCatalogService.ResetCacheForEditor();
        Ensure(CardCatalogService.TryGetAllCards(out var definitions, out var catalogError), catalogError);
        Ensure(definitions.Count == 21, $"Catalog 单卡数量错误：{definitions.Count}");
        Ensure(CardPoolCatalog.TryGet("reward_cards", out var normalPool, out var poolError), poolError);
        Ensure(CardPoolCatalog.TryGet("boss_rewards", out var bossPool, out poolError), poolError);
        Ensure(normalPool.Cards.Count == 10 && bossPool.Cards.Count == 3, "奖励池成员数量与 Catalog 索引不一致。");

#pragma warning disable CS0618
        foreach (var legacy in EnumerateLegacyUniqueCards())
        {
            Ensure(CardCatalogService.TryGetCardProjection(legacy.Id, out var migrated, out var projectionError), projectionError);
            if (B1Overrides.TryGetValue(legacy.Id, out var expected))
            {
                Ensure(migrated.Value == expected.Value && migrated.SelfGuardValue == expected.Guard &&
                    migrated.SelfDamage == expected.SelfDamage && migrated.SecondaryValue == expected.Secondary &&
                    migrated.Exhausts == expected.Exhaust,
                    $"B1 覆盖没有按资源事实迁入：{legacy.Id} " +
                    $"actual=({migrated.Value},{migrated.SelfGuardValue},{migrated.SelfDamage},{migrated.SecondaryValue},{migrated.Exhausts}) " +
                    $"expected=({expected.Value},{expected.Guard},{expected.SelfDamage},{expected.Secondary},{expected.Exhaust})");
            }
            else
            {
                Ensure(AreEqualExceptDefinitionId(legacy, migrated), $"迁移双读不一致：{legacy.Id}");
            }
        }
#pragma warning restore CS0618

        Ensure(CardCatalogService.TryGetCardProjection("wx_r_005", out var attack, out var attackError), attackError);
        Ensure(CardCatalogService.TryGetCardProjection("wx_r_010", out var targetSkill, out var skillError), skillError);
        Ensure(attack.Name != targetSkill.Name && attack.Cost != targetSkill.Cost &&
            attack.TargetMode != targetSkill.TargetMode && attack.Exhausts != targetSkill.Exhausts,
            "两张显著不同卡牌经过同一 Catalog 投影后发生串用。");
        Ensure(targetSkill.Exhausts && targetSkill.TargetMode == CardTargetMode.Enemy,
            "目标策略或有序移区没有正确投影到运行时卡牌。");
        Ensure(CardCatalogService.TryGetExecutionPlan(targetSkill.Id, out var targetPlan, out var planError), planError);
        Ensure(targetPlan.Effects.Count > 0 && targetPlan.Effects[0].Order == 1,
            "Catalog 没有生成有序运行时执行计划。");

        // 缺失/未知 TargetPolicy 必须保留原始解析错误，不能静默投影为 None。
        var invalidTargetPolicy = definitions.First().Duplicate(true) as CardDefinitionResource;
        invalidTargetPolicy.TargetPolicy = new Godot.Collections.Dictionary
        {
            ["selectionMode"] = 999,
            ["scope"] = 999,
        };
        Ensure(!CardDefinitionValidator.TryValidateCard(invalidTargetPolicy,
            new[] { invalidTargetPolicy.Id, invalidTargetPolicy.GetUpgrade().NextCardId },
            invalidTargetPolicy.RewardPoolIds, out var invalidPolicyErrors) && invalidPolicyErrors.Count > 0,
            "未知/缺失 TargetPolicy 被错误接受。");
        GD.Print("[CardCatalogSelfCheck] PASS definitions=21 pools=4 migration=B1-20260720");
    }

    private static IEnumerable<CardInfo> EnumerateLegacyUniqueCards()
    {
        var seen = new HashSet<string>();
        foreach (var source in new[] { DataDefs.WuZhuStarterDeck, DataDefs.WuZhuStarterUpgrades,
                                        DataDefs.RewardCardPool, DataDefs.BossRewardCardPool })
            foreach (var card in source)
                if (seen.Add(card.Id)) yield return card;
    }

    private static bool AreEqualExceptDefinitionId(CardInfo left, CardInfo right) =>
        left.Id == right.Id && left.Name == right.Name && left.Type == right.Type && left.Cost == right.Cost &&
        left.Value == right.Value && left.Description == right.Description && left.SelfDamage == right.SelfDamage &&
        left.HasSecondary == right.HasSecondary && left.SecondaryValue == right.SecondaryValue &&
        left.SecondaryType == right.SecondaryType && left.TargetMode == right.TargetMode &&
        // RequiresEnemyTarget 是迁移前与 TargetMode 重叠且历史上不一致的兼容字段；
        // Schema v1 以结构化 TargetPolicy/TargetMode 的单一语义为准。
        right.RequiresEnemyTarget == (right.TargetMode == CardTargetMode.Enemy) && left.Exhausts == right.Exhausts &&
        left.SelfGuardValue == right.SelfGuardValue &&
        string.Equals(left.UpgradeToId ?? string.Empty, right.UpgradeToId ?? string.Empty, StringComparison.Ordinal);

    private static void Ensure(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException($"[CardCatalogSelfCheck] {error}");
    }
}
