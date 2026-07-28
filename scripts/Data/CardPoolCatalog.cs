using System.Collections.Generic;

/// <summary>卡池定义。库存消费者只通过 ID 解析卡池，不直接依赖 DataDefs 的具体数组。</summary>
public sealed class CardPoolDefinition
{
    public string Id { get; init; }
    public IReadOnlyList<CardInfo> Cards { get; init; }
}

/// <summary>
/// 生产卡池目录及共享解析入口。奖励、事件和商店可以复用同一份卡牌定义，
/// fixture 也必须经过同一校验路径，避免控制器私自兜底到首个卡池。
/// </summary>
public static class CardPoolCatalog
{
    /// <summary>生产读取统一委托给 Resource Catalog，禁止回退旧 DataDefs 数组。</summary>
    public static bool TryGet(string id, out CardPoolDefinition definition, out string error) =>
        CardCatalogService.TryGetPool(id, out definition, out error);

    /// <summary>目录与测试 fixture 共用的卡池绑定入口。</summary>
    public static bool TryResolve(string id, IEnumerable<CardPoolDefinition> definitions,
        out CardPoolDefinition definition, out string error)
    {
        definition = null;
        error = "";
        if (string.IsNullOrWhiteSpace(id) || definitions == null)
        {
            error = "卡池绑定缺少 ID 或定义集合。";
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

        error = $"卡池不存在：{id}";
        return false;
    }

    public static bool TryValidate(CardPoolDefinition definition, out string error)
    {
        error = "";
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
            definition.Cards == null || definition.Cards.Count == 0)
        {
            error = "卡池定义字段不完整或为空。";
            return false;
        }

        for (int index = 0; index < definition.Cards.Count; index++)
        {
            var card = definition.Cards[index];
            if (card == null || string.IsNullOrWhiteSpace(card.Id) ||
                string.IsNullOrWhiteSpace(card.Name) || card.Cost < 0)
            {
                error = $"卡池 {definition.Id} 包含无效商品：index={index}";
                return false;
            }
        }

        return true;
    }
}
