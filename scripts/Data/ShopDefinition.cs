using System.Collections.Generic;

/// <summary>商店页面的显示、库存池和价格定义。</summary>
public sealed class ShopDefinition
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public string CardPoolId { get; init; }
    public int[] CardPrices { get; init; }
}

/// <summary>商店定义目录；ShopController 只消费当前节点 ContentId 解析出的定义。</summary>
public static class ShopDefinitionCatalog
{
    private static readonly IReadOnlyList<ShopDefinition> Definitions = new[]
    {
        new ShopDefinition
        {
            Id = "act1_shop_wayfarer",
            Title = "行脚宝商",
            Description = "灵韵可购买牌架上的卡牌；购买后商品保留为已售，法宝货架当前未开放。",
            CardPoolId = "reward_cards",
            CardPrices = new[] { 40, 65, 110 },
        },
    };

    public static bool TryGet(string id, out ShopDefinition definition, out string error) =>
        TryResolve(id, Definitions, out definition, out error);

    /// <summary>目录与测试 fixture 共用的纯绑定入口。</summary>
    public static bool TryResolve(string id, IEnumerable<ShopDefinition> definitions,
        out ShopDefinition definition, out string error)
    {
        definition = null;
        error = "";
        if (string.IsNullOrWhiteSpace(id) || definitions == null)
        {
            error = "商店定义绑定缺少 ID 或定义集合。";
            return false;
        }

        foreach (var candidate in definitions)
        {
            if (candidate?.Id != id)
                continue;
            if (!TryValidate(candidate, out error))
                return false;
            definition = candidate;
            return true;
        }

        error = $"商店定义不存在：{id}";
        return false;
    }

    public static bool TryValidate(ShopDefinition definition, out string error)
    {
        error = "";
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
            string.IsNullOrWhiteSpace(definition.Title) || string.IsNullOrWhiteSpace(definition.Description) ||
            string.IsNullOrWhiteSpace(definition.CardPoolId) || definition.CardPrices == null ||
            definition.CardPrices.Length == 0)
        {
            error = "商店定义字段不完整。";
            return false;
        }

        foreach (var price in definition.CardPrices)
        {
            if (price <= 0)
            {
                error = $"商店定义包含非法价格：{definition.Id}/{price}";
                return false;
            }
        }

        if (!CardPoolCatalog.TryGet(definition.CardPoolId, out var pool, out error))
            return false;
        if (definition.CardPrices.Length > pool.Cards.Count)
        {
            error = $"商店商品数量超过卡池库存：{definition.Id}";
            return false;
        }

        return true;
    }
}
