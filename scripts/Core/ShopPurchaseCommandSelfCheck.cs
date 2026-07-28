using Godot;
using System;

/// <summary>Focused debug proof that a shop purchase never leaves partial RunState or sold-slot writes.</summary>
public static class ShopPurchaseCommandSelfCheck
{
    public static void Run()
    {
        var validCard = GetAnyCatalogCard();
        CheckSuccess(validCard);
        CheckInsufficientLingYun(validCard);
        CheckCatalogFailure();
        CheckCommitFailure(validCard);
        GD.Print("[ShopPurchaseCommandSelfCheck] PASS success, insufficient balance, catalog failure and commit rollback");
    }

    private static CardInfo GetAnyCatalogCard()
    {
        Ensure(CardCatalogService.TryGetAllCards(out var definitions, out var catalogError), catalogError);
        foreach (var definition in definitions)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                continue;
            if (CardCatalogService.TryGetCardProjection(definition.Id, out var card, out _))
                return card;
        }
        throw new InvalidOperationException("[ShopPurchaseCommandSelfCheck] Catalog 中没有可创建的测试卡牌。");
    }

    private static void CheckSuccess(CardInfo card)
    {
        var runState = new RunState { LingYun = 9 };
        var command = new ShopPurchaseCommand(runState, new[] { card }, new[] { 4 });
        Ensure(command.TryPurchase(0, out var result), result.Message);
        Ensure(runState.LingYun == 5 && runState.PermanentDeck.Count == 1 && command.IsSold(0),
            "成功购买没有同时写入灵韵、牌组和售出状态。");
        Ensure(runState.PermanentDeck[0].Info.DefinitionId == card.DefinitionId,
            "成功购买没有写入经 Catalog 创建的对应运行时卡牌。");
    }

    private static void CheckInsufficientLingYun(CardInfo card)
    {
        var runState = new RunState { LingYun = 3 };
        var command = new ShopPurchaseCommand(runState, new[] { card }, new[] { 4 });
        Ensure(!command.TryPurchase(0, out _), "灵韵不足的购买被错误接受。");
        Ensure(runState.LingYun == 3 && runState.PermanentDeck.Count == 0 && !command.IsSold(0),
            "灵韵不足路径污染了持久状态或售出状态。");
    }

    private static void CheckCatalogFailure()
    {
        var invalidCard = new CardInfo { DefinitionId = "fixture_missing_catalog_card", Id = "fixture_missing_catalog_card" };
        var runState = new RunState { LingYun = 9 };
        var command = new ShopPurchaseCommand(runState, new[] { invalidCard }, new[] { 4 });
        Ensure(!command.TryPurchase(0, out _), "缺失 Catalog 卡牌被错误购买。");
        Ensure(runState.LingYun == 9 && runState.PermanentDeck.Count == 0 && !command.IsSold(0),
            "Catalog 创建失败路径污染了持久状态或售出状态。");
    }

    private static void CheckCommitFailure(CardInfo card)
    {
        var runState = new RunState { LingYun = 9 };
        var originalCard = new CardRuntime(new CardInfo { Id = "fixture_existing" }, null);
        runState.PermanentDeck.Add(originalCard);
        var command = new ShopPurchaseCommand(runState, new[] { card }, new[] { 4 },
            static (state, runtime) =>
            {
                state.PermanentDeck.Add(runtime);
                throw new InvalidOperationException("fixture commit failure");
            });

        Ensure(!command.TryPurchase(0, out _), "提交故障被错误报告为成功。");
        Ensure(runState.LingYun == 9 && runState.PermanentDeck.Count == 1 &&
            ReferenceEquals(runState.PermanentDeck[0], originalCard) && !command.IsSold(0),
            "提交故障后没有完整恢复灵韵、永久牌组和售出状态。");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"[ShopPurchaseCommandSelfCheck] {message}");
    }
}
