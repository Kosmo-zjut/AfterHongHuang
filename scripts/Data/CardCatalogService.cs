using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

/// <summary>
/// 运行时卡牌定义的唯一读取端口。它只加载显式 Catalog 与单卡 Resource，
/// 加载/校验失败会把错误返回给调用方，绝不回退到 DataDefs 的旧数组。
/// </summary>
public static class CardCatalogService
{
    public const string CatalogPath = "res://resources/cards/CardCatalog.tres";

    private static bool _initialized;
    private static string _initializationError = "";
    private static IReadOnlyDictionary<string, CardDefinitionResource> _cards =
        new ReadOnlyDictionary<string, CardDefinitionResource>(new Dictionary<string, CardDefinitionResource>());
    private static IReadOnlyDictionary<string, CardPoolDefinition> _pools =
        new ReadOnlyDictionary<string, CardPoolDefinition>(new Dictionary<string, CardPoolDefinition>());
    private static IReadOnlyDictionary<string, CardExecutionPlan> _plans =
        new ReadOnlyDictionary<string, CardExecutionPlan>(new Dictionary<string, CardExecutionPlan>());

    /// <summary>清除缓存供编辑器重载和纯自检使用；不会修改任何 RunState。</summary>
    public static void ResetCacheForEditor()
    {
        _initialized = false;
        _initializationError = "";
        _cards = new ReadOnlyDictionary<string, CardDefinitionResource>(new Dictionary<string, CardDefinitionResource>());
        _pools = new ReadOnlyDictionary<string, CardPoolDefinition>(new Dictionary<string, CardPoolDefinition>());
        _plans = new ReadOnlyDictionary<string, CardExecutionPlan>(new Dictionary<string, CardExecutionPlan>());
    }

    public static bool TryGetCard(string cardId, out CardDefinitionResource card, out string error)
    {
        card = null;
        if (!EnsureLoaded(out error)) return false;
        if (string.IsNullOrWhiteSpace(cardId) || !_cards.TryGetValue(cardId, out card))
        {
            error = $"CardCatalog 不存在卡牌：{cardId}";
            return false;
        }
        return true;
    }

    public static bool TryGetCardProjection(string cardId, out CardInfo card, out string error)
    {
        card = null;
        if (!TryGetCard(cardId, out var definition, out error)) return false;
        return CardDefinitionProjection.TryCreate(definition, out card, out error);
    }

    /// <summary>创建独立战斗实例；Plan 与兼容 CardInfo 都来自同一已校验 Definition。</summary>
    public static bool TryCreateRuntimeCard(string cardId, out CardRuntime runtime, out string error)
    {
        runtime = null;
        if (!EnsureLoaded(out error)) return false;
        if (!_plans.TryGetValue(cardId, out var plan) || !TryGetCardProjection(cardId, out var projection, out error))
        {
            error = string.IsNullOrWhiteSpace(error) ? $"CardCatalog 缺少执行计划：{cardId}" : error;
            return false;
        }
        runtime = new CardRuntime(RewardPlanValidator.CloneCardDefinition(projection), plan);
        return true;
    }

    public static bool TryGetExecutionPlan(string cardId, out CardExecutionPlan plan, out string error)
    {
        plan = null;
        if (!EnsureLoaded(out error)) return false;
        if (!_plans.TryGetValue(cardId, out plan))
        {
            error = $"CardCatalog 不存在执行计划：{cardId}";
            return false;
        }
        return true;
    }

    public static bool TryGetPool(string poolId, out CardPoolDefinition pool, out string error)
    {
        pool = null;
        if (!EnsureLoaded(out error)) return false;
        if (string.IsNullOrWhiteSpace(poolId) || !_pools.TryGetValue(poolId, out pool))
        {
            error = $"CardCatalog 不存在卡池：{poolId}";
            return false;
        }
        return true;
    }

    public static bool TryGetAllCards(out IReadOnlyCollection<CardDefinitionResource> cards, out string error)
    {
        cards = Array.Empty<CardDefinitionResource>();
        if (!EnsureLoaded(out error)) return false;
        cards = new List<CardDefinitionResource>(_cards.Values);
        return true;
    }

    /// <summary>返回当前 Catalog 的显式池 ID，供编辑器草稿做字段级引用校验。</summary>
    public static bool TryGetPoolIds(out IReadOnlyCollection<string> poolIds, out string error)
    {
        poolIds = Array.Empty<string>();
        if (!EnsureLoaded(out error)) return false;
        poolIds = new List<string>(_pools.Keys);
        return true;
    }

    public static bool EnsureLoaded(out string error)
    {
        if (_initialized)
        {
            error = _initializationError;
            return string.IsNullOrEmpty(error);
        }

        _initialized = true;
        var catalog = ResourceLoader.Load<CardCatalogResource>(CatalogPath);
        if (catalog == null)
        {
            _initializationError = $"CardCatalog 资源加载失败：{CatalogPath}";
            error = _initializationError;
            GD.PrintErr($"[CardCatalog] {error}");
            return false;
        }

        var loadedCards = new List<CardDefinitionResource>();
        foreach (var path in catalog.CardResourcePaths)
        {
            var card = ResourceLoader.Load<CardDefinitionResource>(path);
            if (card == null)
            {
                _initializationError = $"CardCatalog 单卡资源加载失败：{path}";
                error = _initializationError;
                GD.PrintErr($"[CardCatalog] {error}");
                return false;
            }
            loadedCards.Add(card);
        }

        var semanticValidation = CardCatalogSemanticValidator.Validate(catalog, loadedCards);
        if (!semanticValidation.IsValid)
        {
            _initializationError = string.Join(" | ", semanticValidation.Issues.Select(issue => issue.Message));
            error = _initializationError;
            GD.PrintErr($"[CardCatalog] Schema 校验失败：{error}");
            return false;
        }

        var poolIndexes = catalog.GetPools();

        var cards = new Dictionary<string, CardDefinitionResource>(StringComparer.Ordinal);
        var plans = new Dictionary<string, CardExecutionPlan>(StringComparer.Ordinal);
        foreach (var definition in loadedCards)
        {
            if (!CardDefinitionProjection.TryCreate(definition, out _, out var plan, out var projectionError))
            {
                _initializationError = $"卡牌 {definition.Id} 无法创建执行计划：{projectionError}";
                error = _initializationError;
                GD.PrintErr($"[CardCatalog] {error}");
                return false;
            }
            cards.Add(definition.Id, definition);
            plans.Add(definition.Id, plan);
        }
        var pools = new Dictionary<string, CardPoolDefinition>(StringComparer.Ordinal);
        foreach (var index in poolIndexes)
        {
            var entries = new List<CardInfo>();
            foreach (var cardId in index.CardIds)
            {
                if (!CardDefinitionProjection.TryCreate(cards[cardId], out var projection, out _, out var projectionError))
                {
                    _initializationError = $"卡牌 {cardId} 无法转换为运行时投影：{projectionError}";
                    error = _initializationError;
                    GD.PrintErr($"[CardCatalog] {error}");
                    return false;
                }
                entries.Add(projection);
            }
            pools.Add(index.Id, new CardPoolDefinition { Id = index.Id, Cards = entries.AsReadOnly() });
        }

        _cards = new ReadOnlyDictionary<string, CardDefinitionResource>(cards);
        _pools = new ReadOnlyDictionary<string, CardPoolDefinition>(pools);
        _plans = new ReadOnlyDictionary<string, CardExecutionPlan>(plans);
        _initializationError = "";
        error = "";
        return true;
    }
}

/// <summary>
/// 为既有 Battle/UI 提供的只读兼容投影。所有字段都由 Resource 的结构化费用、目标和效果推导，
/// 旧控制器不会再从 DataDefs 取得生产 CardInfo。
/// </summary>
public static class CardDefinitionProjection
{
    public static bool TryCreate(CardDefinitionResource definition, out CardInfo card, out string error)
    {
        return TryCreate(definition, out card, out _, out error);
    }

    /// <summary>从同一严格解析结果同时生成兼容展示投影与不可变执行计划。</summary>
    public static bool TryCreate(CardDefinitionResource definition, out CardInfo card, out CardExecutionPlan plan, out string error)
    {
        card = null;
        plan = null;
        error = "";
        if (definition == null)
        {
            error = "卡牌定义为空。";
            return false;
        }
        if (!CardDefinitionValidator.TryValidateCard(definition, new[] { definition.Id, definition.GetUpgrade().NextCardId },
                definition.RewardPoolIds, out var errors))
        {
            // 单卡投影只报告局部可判定错误；完整 Catalog 校验会额外检查池和升级图。
            foreach (var candidate in errors)
            {
                if (!candidate.Contains("未知卡池") && !candidate.Contains("升级目标缺失或未知"))
                {
                    error = candidate;
                    return false;
                }
            }
        }

        if (!CardDefinitionReader.TryReadRuntimeFields(definition, out var costs, out var policy, out var effects,
                out var upgrade, out var parseErrors))
        {
            error = string.Join(" | ", parseErrors);
            return false;
        }
        int energyCost = 0;
        foreach (var cost in costs)
        {
            if (cost.CostType != CardCostKind.Energy)
            {
                error = $"当前执行器不支持非灵力费用：{definition.Id}";
                return false;
            }
            energyCost += cost.Amount;
        }

        int value = 0;
        int selfDamage = 0;
        int selfGuard = 0;
        bool hasSecondary = false;
        int secondaryValue = 0;
        SecondaryEffect secondaryType = SecondaryEffect.无;
        bool exhausts = false;
        foreach (var effect in effects)
        {
            switch (effect.EffectType)
            {
                case CardEffectKind.DealDamage:
                    value = effect.Amount;
                    break;
                case CardEffectKind.GainBlock:
                    value = effect.Amount;
                    selfGuard = effect.Amount;
                    break;
                case CardEffectKind.LoseHealth:
                    selfDamage += effect.Amount;
                    break;
                case CardEffectKind.AddStatus:
                    if (!TryMapStatus(effect.StatusKind, out secondaryType))
                    {
                        error = $"当前执行器不支持状态效果：{definition.Id}/{effect.StatusKind}";
                        return false;
                    }
                    hasSecondary = true;
                    secondaryValue = effect.Amount;
                    if (value == 0) value = effect.Amount;
                    break;
                case CardEffectKind.MoveSelfToZone:
                    exhausts = true;
                    break;
                default:
                    error = $"当前执行器不支持效果：{definition.Id}/{effect.EffectType}";
                    return false;
            }
        }

        CardTargetMode targetMode = policy.Scope switch
        {
            CardTargetScope.SingleEnemy => CardTargetMode.Enemy,
            CardTargetScope.Self => CardTargetMode.Self,
            CardTargetScope.None => CardTargetMode.None,
            _ => CardTargetMode.None,
        };
        card = new CardInfo
        {
            DefinitionId = definition.Id,
            Id = definition.Id,
            Name = definition.DisplayName,
            Type = definition.Category == CardCategory.Attack ? CardType.斗击 : CardType.术法,
            Cost = energyCost,
            Value = value,
            Description = definition.DescriptionFallback,
            ExecutionSummary = CardExecutionPlanFormatter.Format(new CardExecutionPlan(definition.Id, energyCost, policy, effects)),
            SelfDamage = selfDamage,
            SelfGuardValue = selfGuard,
            HasSecondary = hasSecondary,
            SecondaryValue = secondaryValue,
            SecondaryType = secondaryType,
            TargetMode = targetMode,
            RequiresEnemyTarget = targetMode == CardTargetMode.Enemy,
            Exhausts = exhausts,
            UpgradeToId = upgrade.NextCardId,
        };
        plan = new CardExecutionPlan(definition.Id, energyCost, policy, effects);
        return true;
    }

    private static bool TryMapStatus(CardStatusKind status, out SecondaryEffect effect)
    {
        effect = status switch
        {
            CardStatusKind.Strength => SecondaryEffect.斗劲,
            CardStatusKind.Vulnerable => SecondaryEffect.易损,
            CardStatusKind.EternalFlame => SecondaryEffect.永炎,
            _ => SecondaryEffect.无,
        };
        return effect != SecondaryEffect.无;
    }
}
