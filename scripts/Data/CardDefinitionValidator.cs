using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>纯卡牌 Schema 校验器。它不读取 RunState 或 UI，编辑器和运行时共享同一规则。</summary>
public static class CardDefinitionValidator
{
    public const int CurrentSchemaVersion = 1;

    public static bool TryValidateCatalog(IReadOnlyList<CardDefinitionResource> cards,
        IReadOnlyList<CardPoolIndexDefinition> pools, out IReadOnlyList<string> errors)
    {
        var issues = new List<string>();
        if (cards == null || cards.Count == 0)
            issues.Add("catalog.cards: 缺少卡牌定义。");
        if (pools == null || pools.Count == 0)
            issues.Add("catalog.pools: 缺少卡池索引。");
        if (issues.Count > 0)
        {
            errors = issues;
            return false;
        }

        var ids = new HashSet<string>();
        foreach (var card in cards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Id))
            {
                issues.Add("card: 存在空 ID 定义。");
                continue;
            }
            if (!ids.Add(card.Id))
                issues.Add($"card[{card.Id}].id: 重复 ID。");
        }

        var poolIds = new HashSet<string>();
        foreach (var pool in pools)
        {
            if (pool == null || string.IsNullOrWhiteSpace(pool.Id))
            {
                issues.Add("pool: 存在空 ID 索引。");
                continue;
            }
            if (!poolIds.Add(pool.Id)) issues.Add($"pool[{pool.Id}].id: 重复卡池 ID。");
            if (pool.CardIds == null || pool.CardIds.Count == 0) issues.Add($"pool[{pool.Id}].cardIds: 卡池为空。");
            else foreach (var cardId in pool.CardIds)
                if (string.IsNullOrWhiteSpace(cardId) || !ids.Contains(cardId))
                    issues.Add($"pool[{pool.Id}].cardIds: 引用了未知卡牌 {cardId}。");
        }

        foreach (var card in cards.Where(card => card != null))
            ValidateCard(card, ids, poolIds, issues);

        ValidateUpgradeGraph(cards, issues);
        errors = issues;
        return issues.Count == 0;
    }

    public static bool TryValidateCard(CardDefinitionResource card, IReadOnlyCollection<string> knownCardIds,
        IReadOnlyCollection<string> knownPoolIds, out IReadOnlyList<string> errors)
    {
        var issues = new List<string>();
        ValidateCard(card, knownCardIds ?? Array.Empty<string>(), knownPoolIds ?? Array.Empty<string>(), issues);
        errors = issues;
        return issues.Count == 0;
    }

    /// <summary>运行时复用 Schema 的目标/效果语义门禁，防止手工构造计划绕过 Catalog 校验。</summary>
    public static bool TryValidateExecutionPlan(CardExecutionPlan plan, out string error)
    {
        var issues = new List<string>();
        if (plan == null || string.IsNullOrWhiteSpace(plan.CardId)) issues.Add("执行计划缺少 CardId。");
        if (plan == null || plan.EnergyCost < 0) issues.Add("执行计划灵力费用非法。");
        var policy = plan?.TargetPolicy;
        if (policy == null || !Enum.IsDefined(typeof(CardSelectionMode), policy.SelectionMode) ||
            !Enum.IsDefined(typeof(CardTargetScope), policy.Scope) || !Enum.IsDefined(typeof(CardRetargetPolicy), policy.RetargetOnInvalid) ||
            policy.MinimumTargets < 0 || policy.MaximumTargets < policy.MinimumTargets)
            issues.Add("执行计划目标策略字段非法。");
        else
        {
            if (policy.Scope is not (CardTargetScope.None or CardTargetScope.Self or CardTargetScope.SingleEnemy)) issues.Add("执行计划目标范围不受支持。");
            if (policy.Scope == CardTargetScope.SingleEnemy && (policy.SelectionMode != CardSelectionMode.Required || policy.MinimumTargets != 1 || policy.MaximumTargets != 1)) issues.Add("单敌目标策略非法。");
            if (policy.Scope != CardTargetScope.SingleEnemy && policy.SelectionMode == CardSelectionMode.Required) issues.Add("Required 目标策略非法。");
        }
        var effects = plan?.Effects;
        if (effects == null || effects.Count == 0) issues.Add("执行计划缺少效果。");
        else for (int index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect == null || effect.Order != index + 1 || effect.Amount < 0 ||
                !Enum.IsDefined(typeof(CardEffectKind), effect.EffectType) || !Enum.IsDefined(typeof(CardEffectTarget), effect.TargetSelector) ||
                !Enum.IsDefined(typeof(CardStatusKind), effect.StatusKind) || !Enum.IsDefined(typeof(CardDurationScope), effect.DurationScope) ||
                !Enum.IsDefined(typeof(CardDestinationZone), effect.DestinationZone))
                { issues.Add($"执行计划效果 {index} 字段或 Order 非法。"); continue; }
            if (effect.EffectType is not (CardEffectKind.DealDamage or CardEffectKind.GainBlock or CardEffectKind.LoseHealth or CardEffectKind.AddStatus or CardEffectKind.MoveSelfToZone)) issues.Add($"执行计划效果 {index} 类型不受支持。");
            if (effect.TargetSelector == CardEffectTarget.SelectedTarget && policy?.Scope != CardTargetScope.SingleEnemy) issues.Add($"执行计划效果 {index} 目标不匹配。");
            if ((effect.EffectType is CardEffectKind.GainBlock or CardEffectKind.LoseHealth) && effect.TargetSelector != CardEffectTarget.Self) issues.Add($"执行计划效果 {index} 必须以自身为目标。");
            if (effect.EffectType == CardEffectKind.DealDamage && effect.TargetSelector != CardEffectTarget.SelectedTarget) issues.Add($"执行计划效果 {index} 伤害必须指向已选敌方目标。");
            if (effect.EffectType == CardEffectKind.AddStatus && effect.StatusKind == CardStatusKind.None) issues.Add($"执行计划效果 {index} 缺少状态。");
            if (effect.EffectType == CardEffectKind.MoveSelfToZone && effect.DestinationZone != CardDestinationZone.Exhaust) issues.Add($"执行计划效果 {index} 移区非法。");
        }
        error = string.Join(" | ", issues);
        return issues.Count == 0;
    }

    private static void ValidateCard(CardDefinitionResource card, IReadOnlyCollection<string> knownCardIds,
        IReadOnlyCollection<string> knownPoolIds, List<string> issues)
    {
        string path = $"card[{card?.Id ?? "<missing>"}]";
        if (card == null) { issues.Add("card: 定义为空。"); return; }
        if (card.EditorDraft) issues.Add($"{path}.editorDraft: 未配置草稿不得进入 Catalog 或运行时。");
        if (card.SchemaVersion != CurrentSchemaVersion) issues.Add($"{path}.schemaVersion: 不支持 {card.SchemaVersion}。");
        if (string.IsNullOrWhiteSpace(card.Id)) issues.Add($"{path}.id: 不能为空。");
        if (string.IsNullOrWhiteSpace(card.DisplayName)) issues.Add($"{path}.displayName: 不能为空。");
        if (string.IsNullOrWhiteSpace(card.DescriptionFallback)) issues.Add($"{path}.descriptionFallback: 不能为空。");
        if (!Enum.IsDefined(typeof(CardOwnerKind), card.OwnerKind) ||
            !Enum.IsDefined(typeof(CardCategory), card.Category) || !Enum.IsDefined(typeof(CardRarity), card.Rarity))
            issues.Add($"{path}: owner/category/rarity 枚举非法。");
        if (card.OwnerKind == CardOwnerKind.Character && string.IsNullOrWhiteSpace(card.OwnerCharacterId))
            issues.Add($"{path}.ownerCharacterId: 角色卡必须声明角色 ID。");
        if (card.OwnerKind == CardOwnerKind.Neutral && !string.IsNullOrWhiteSpace(card.OwnerCharacterId))
            issues.Add($"{path}.ownerCharacterId: 中立卡不得声明角色 ID。");

        CardDefinitionReader.TryReadRuntimeFields(card, out var costs, out var policy, out var effects, out var upgrade,
            out var parseErrors);
        issues.AddRange(parseErrors);
        if (costs.Count == 0) issues.Add($"{path}.costs: 不能为空。");
        foreach (var cost in costs)
        {
            if (!Enum.IsDefined(typeof(CardCostKind), cost.CostType) || cost.Amount < 0 ||
                !Enum.IsDefined(typeof(CardCostTiming), cost.Timing) ||
                !Enum.IsDefined(typeof(CardCostFailurePolicy), cost.FailurePolicy))
                issues.Add($"{path}.costs: 费用字段非法。");
            if (cost.CostType == CardCostKind.Health && !cost.CanCauseDefeat)
                issues.Add($"{path}.costs: 生命费用必须声明 CanCauseDefeat。");
            if (cost.CostType is CardCostKind.XEnergy or CardCostKind.None)
                issues.Add($"{path}.costs: ACT1 不支持 XEnergy/None 费用。");
        }

        if (!Enum.IsDefined(typeof(CardSelectionMode), policy.SelectionMode) ||
            !Enum.IsDefined(typeof(CardTargetScope), policy.Scope) ||
            !Enum.IsDefined(typeof(CardRetargetPolicy), policy.RetargetOnInvalid) ||
            policy.MinimumTargets < 0 || policy.MaximumTargets < policy.MinimumTargets)
            issues.Add($"{path}.targetPolicy: 字段非法。");
        if (policy.Scope is not (CardTargetScope.None or CardTargetScope.Self or CardTargetScope.SingleEnemy))
            issues.Add($"{path}.targetPolicy.scope: ACT1 不支持该范围。");
        if (policy.Scope == CardTargetScope.SingleEnemy && policy.SelectionMode != CardSelectionMode.Required)
            issues.Add($"{path}.targetPolicy: 单敌目标必须 Required。");
        if (policy.Scope != CardTargetScope.SingleEnemy && policy.SelectionMode == CardSelectionMode.Required)
            issues.Add($"{path}.targetPolicy: Required 只能用于当前支持的单敌目标。");

        if (effects.Count == 0) issues.Add($"{path}.effects: 不能为空。");
        for (int index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect.Order != index + 1) issues.Add($"{path}.effects[{index}].order: 必须从 1 连续递增。");
            if (!Enum.IsDefined(typeof(CardEffectKind), effect.EffectType) ||
                !Enum.IsDefined(typeof(CardEffectTarget), effect.TargetSelector) ||
                !Enum.IsDefined(typeof(CardStatusKind), effect.StatusKind) ||
                !Enum.IsDefined(typeof(CardDurationScope), effect.DurationScope) ||
                !Enum.IsDefined(typeof(CardDestinationZone), effect.DestinationZone) || effect.Amount < 0)
                issues.Add($"{path}.effects[{index}]: 枚举或数值非法。");
            if (effect.EffectType is not (CardEffectKind.DealDamage or CardEffectKind.GainBlock or CardEffectKind.LoseHealth or CardEffectKind.AddStatus or CardEffectKind.MoveSelfToZone))
                issues.Add($"{path}.effects[{index}]: ACT1 尚不支持该效果。");
            if (effect.TargetSelector == CardEffectTarget.SelectedTarget && policy.Scope != CardTargetScope.SingleEnemy)
                issues.Add($"{path}.effects[{index}]: SelectedTarget 与目标策略不匹配。");
            if (effect.EffectType == CardEffectKind.AddStatus && effect.StatusKind == CardStatusKind.None)
                issues.Add($"{path}.effects[{index}]: AddStatus 缺少 StatusKind。");
            if (effect.EffectType == CardEffectKind.MoveSelfToZone && effect.DestinationZone != CardDestinationZone.Exhaust)
                issues.Add($"{path}.effects[{index}]: ACT1 仅支持移入 Exhaust。");
        }

        // Resource/Catalog 与运行时共用同一个执行语义门禁，禁止两套规则逐步漂移。
        int energyCost = costs.Where(cost => cost.CostType == CardCostKind.Energy).Sum(cost => cost.Amount);
        if (!TryValidateExecutionPlan(new CardExecutionPlan(card.Id ?? "", energyCost, policy, effects), out var executionError))
            issues.Add($"{path}.executionPlan: {executionError}");

        if (card.RewardPoolIds == null || card.RewardPoolIds.Count == 0)
            issues.Add($"{path}.rewardPoolIds: 不能为空。");
        else
        {
            var declaredPoolIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var poolId in card.RewardPoolIds)
            {
                if (string.IsNullOrWhiteSpace(poolId) || !knownPoolIds.Contains(poolId))
                    issues.Add($"{path}.rewardPoolIds: 未知卡池 {poolId}。");
                else if (!declaredPoolIds.Add(poolId))
                    issues.Add($"{path}.rewardPoolIds: 重复卡池 {poolId}。");
            }
        }
        if (string.IsNullOrWhiteSpace(upgrade.FamilyId)) issues.Add($"{path}.upgrade.familyId: 不能为空。");
        if (upgrade.CanUpgrade && (string.IsNullOrWhiteSpace(upgrade.NextCardId) || !knownCardIds.Contains(upgrade.NextCardId)))
            issues.Add($"{path}.upgrade.nextCardId: 升级目标缺失或未知。");
        if (!upgrade.CanUpgrade && !string.IsNullOrWhiteSpace(upgrade.NextCardId))
            issues.Add($"{path}.upgrade.nextCardId: 不可升级卡不得声明后继。");
        if (card.Availability == null || string.IsNullOrWhiteSpace(CardDefinitionReader.ReadString(card.Availability, "minContentVersion")))
            issues.Add($"{path}.availability: 缺少 minContentVersion。");
        if (card.Preview == null || string.IsNullOrWhiteSpace(CardDefinitionReader.ReadString(card.Preview, "frameKey")))
            issues.Add($"{path}.preview: 缺少 frameKey。");
        if (card.Balance == null || string.IsNullOrWhiteSpace(CardDefinitionReader.ReadString(card.Balance, "role")))
            issues.Add($"{path}.balance: 缺少 role。");
    }

    private static void ValidateUpgradeGraph(IReadOnlyList<CardDefinitionResource> cards, List<string> issues)
    {
        var byId = cards.Where(card => card != null && !string.IsNullOrWhiteSpace(card.Id))
            .ToDictionary(card => card.Id, StringComparer.Ordinal);
        foreach (var card in byId.Values)
        {
            var seen = new HashSet<string>();
            var cursor = card;
            while (cursor.GetUpgrade().CanUpgrade)
            {
                if (!seen.Add(cursor.Id) || !byId.TryGetValue(cursor.GetUpgrade().NextCardId, out cursor))
                {
                    issues.Add($"card[{card.Id}].upgrade: 存在断裂或循环升级图。");
                    break;
                }
                if (cursor.OwnerKind != card.OwnerKind || cursor.OwnerCharacterId != card.OwnerCharacterId)
                {
                    issues.Add($"card[{card.Id}].upgrade: 升级跨越不兼容所有者。");
                    break;
                }
            }
        }
    }
}
