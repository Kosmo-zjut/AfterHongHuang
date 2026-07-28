using System;
using System.Collections.Generic;

/// <summary>
/// A short-lived command for one Shop scene's inventory. It owns only the sold flags for that
/// inventory and commits a purchase only after the catalog has created the exact runtime card.
/// </summary>
public sealed class ShopPurchaseCommand
{
    private readonly RunState _runState;
    private readonly ShopOffer[] _offers;
    private readonly bool[] _soldSlots;
    private readonly Action<RunState, CardRuntime> _commitCard;

    /// <summary>
    /// Creates a purchase boundary for a validated inventory. The optional committer exists for
    /// focused tests; production uses the direct RunState permanent-deck append.
    /// </summary>
    public ShopPurchaseCommand(RunState runState, IReadOnlyList<CardInfo> inventory,
        IReadOnlyList<int> prices, Action<RunState, CardRuntime> commitCard = null)
    {
        _runState = runState ?? throw new ArgumentNullException(nameof(runState));
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (prices == null) throw new ArgumentNullException(nameof(prices));
        if (inventory.Count != prices.Count)
            throw new ArgumentException("商店库存和价格槽位数量不一致。", nameof(prices));

        _offers = new ShopOffer[inventory.Count];
        for (int index = 0; index < inventory.Count; index++)
        {
            var card = inventory[index];
            if (card == null || string.IsNullOrWhiteSpace(card.DefinitionId))
                throw new ArgumentException($"商店商品缺少 Catalog DefinitionId：slot={index}", nameof(inventory));
            if (prices[index] <= 0)
                throw new ArgumentException($"商店商品价格非法：slot={index}/price={prices[index]}", nameof(prices));

            // Freeze only the catalog identity and price. Presentation may retain CardInfo, but it
            // can never change which definition this command purchases.
            _offers[index] = new ShopOffer(card.DefinitionId, prices[index]);
        }

        _soldSlots = new bool[_offers.Length];
        _commitCard = commitCard ?? CommitToPermanentDeck;
    }

    public int SlotCount => _offers.Length;

    /// <summary>Returns whether this command has successfully sold the requested slot.</summary>
    public bool IsSold(int slot) => slot >= 0 && slot < _soldSlots.Length && _soldSlots[slot];

    /// <summary>
    /// Validates then commits one purchase. All observable run and inventory state is restored if
    /// the final deck write fails, so callers can refresh UI from this command without compensation.
    /// </summary>
    public bool TryPurchase(int slot, out ShopPurchaseResult result)
    {
        result = null;
        if (slot < 0 || slot >= _offers.Length)
        {
            result = ShopPurchaseResult.Failed(slot, "商店商品槽位无效。");
            return false;
        }
        if (_soldSlots[slot])
        {
            result = ShopPurchaseResult.Failed(slot, "该商品已经售出。");
            return false;
        }

        var offer = _offers[slot];
        if (_runState.LingYun < offer.Price)
        {
            result = ShopPurchaseResult.Failed(slot, "灵韵不足，未购买卡牌。");
            return false;
        }

        // Catalog creation is a precondition, not part of the commit. The same instance is passed
        // into the single deck-write operation below; no second parse or fallback can diverge it.
        if (!CardCatalogService.TryCreateRuntimeCard(offer.CardDefinitionId, out var runtimeCard, out var catalogError))
        {
            result = ShopPurchaseResult.Failed(slot, $"商品卡牌无法创建：{catalogError}");
            return false;
        }

        int lingYunBefore = _runState.LingYun;
        bool soldBefore = _soldSlots[slot];
        var deckBefore = new List<CardRuntime>(_runState.PermanentDeck);
        try
        {
            _runState.LingYun = lingYunBefore - offer.Price;
            _commitCard(_runState, runtimeCard);
            _soldSlots[slot] = true;
            result = ShopPurchaseResult.CreateSuccess(slot, offer.Price, offer.CardDefinitionId);
            return true;
        }
        catch (Exception error)
        {
            try
            {
                _runState.LingYun = lingYunBefore;
                _runState.PermanentDeck.Clear();
                _runState.PermanentDeck.AddRange(deckBefore);
                _soldSlots[slot] = soldBefore;
            }
            catch (Exception rollbackError)
            {
                result = ShopPurchaseResult.Failed(slot,
                    $"购买提交失败且回滚失败：{error.Message}；{rollbackError.Message}");
                return false;
            }

            result = ShopPurchaseResult.Failed(slot, $"购买提交失败，已回滚：{error.Message}");
            return false;
        }
    }

    private readonly struct ShopOffer
    {
        public ShopOffer(string cardDefinitionId, int price)
        {
            CardDefinitionId = cardDefinitionId;
            Price = price;
        }

        public string CardDefinitionId { get; }
        public int Price { get; }
    }

    private static void CommitToPermanentDeck(RunState state, CardRuntime runtime)
    {
        state.PermanentDeck.Add(runtime);
    }
}

/// <summary>Immutable outcome returned to presentation after one purchase command attempt.</summary>
public sealed class ShopPurchaseResult
{
    private ShopPurchaseResult(bool succeeded, int slot, int price, string cardDefinitionId, string message)
    {
        Succeeded = succeeded;
        Slot = slot;
        Price = price;
        CardDefinitionId = cardDefinitionId;
        Message = message;
    }

    public bool Succeeded { get; }
    public int Slot { get; }
    public int Price { get; }
    public string CardDefinitionId { get; }
    public string Message { get; }

    public static ShopPurchaseResult CreateSuccess(int slot, int price, string cardDefinitionId) =>
        new(true, slot, price, cardDefinitionId, "购买成功。");

    public static ShopPurchaseResult Failed(int slot, string message) =>
        new(false, slot, 0, string.Empty, message);
}
