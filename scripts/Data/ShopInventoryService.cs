using System.Collections.Generic;

/// <summary>已解析的商店库存；控制器只消费结果，不重新解释池和价格规则。</summary>
public sealed class ShopInventoryResult
{
    public IReadOnlyList<CardInfo> Cards { get; init; }
    public IReadOnlyList<int> Prices { get; init; }
}

/// <summary>根据 ShopDefinition 和稳定商店随机流构建库存。</summary>
public static class ShopInventoryService
{
    public static bool TryBuild(ShopDefinition definition, StableRandom random,
        out ShopInventoryResult result, out string error)
    {
        result = null;
        error = "";
        if (!ShopDefinitionCatalog.TryValidate(definition, out error))
            return false;
        if (random == null)
        {
            error = "商店库存构建缺少稳定随机流。";
            return false;
        }
        if (!CardPoolCatalog.TryGet(definition.CardPoolId, out var pool, out error))
            return false;
        if (definition.CardPrices.Length > pool.Cards.Count)
        {
            error = $"商店商品数量超过卡池库存：{definition.Id}/{definition.CardPrices.Length}/{pool.Cards.Count}";
            return false;
        }

        var shuffled = new List<CardInfo>(pool.Cards);
        random.Shuffle(shuffled);
        var cards = new List<CardInfo>(definition.CardPrices.Length);
        for (int index = 0; index < definition.CardPrices.Length; index++)
            cards.Add(shuffled[index]);

        result = new ShopInventoryResult
        {
            Cards = cards,
            Prices = (int[])definition.CardPrices.Clone(),
        };
        return true;
    }
}
