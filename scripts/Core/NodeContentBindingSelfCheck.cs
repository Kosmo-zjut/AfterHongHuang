using Godot;
using System.Linq;

/// <summary>验证 Shop/Event 内容由定义绑定入口提供，控制器不依赖固定标题。</summary>
public static class NodeContentBindingSelfCheck
{
    public static void Run()
    {
        Ensure(ShopDefinitionCatalog.TryGet("act1_shop_wayfarer", out var shop, out var shopError), shopError);
        Ensure(!string.IsNullOrWhiteSpace(shop.Title) && !string.IsNullOrWhiteSpace(shop.Description),
            "生产商店定义缺少标题或说明");

        var shopFixture = new ShopDefinition
        {
            Id = "fixture_shop_b",
            Title = "测试商店 B",
            Description = "测试商店 B 说明",
            CardPoolId = "boss_rewards",
            CardPrices = new[] { 3, 5 },
        };
        Ensure(ShopDefinitionCatalog.TryResolve(shopFixture.Id, new[] { shopFixture },
                out var boundShopFixture, out var fixtureShopError), fixtureShopError);
        Ensure(boundShopFixture.Title != shop.Title && boundShopFixture.Description != shop.Description,
            "第二个商店定义未通过同一绑定入口区分");
        Ensure(ShopInventoryService.TryBuild(shop,
                new StableRandom(0xC5_0001UL), out var productionInventory, out var productionInventoryError),
            productionInventoryError);
        Ensure(ShopInventoryService.TryBuild(boundShopFixture,
                new StableRandom(0xC5_0002UL), out var fixtureInventory, out var fixtureInventoryError),
            fixtureInventoryError);
        Ensure(productionInventory.Cards.Count != fixtureInventory.Cards.Count ||
            productionInventory.Cards[0].Id != fixtureInventory.Cards[0].Id ||
            productionInventory.Prices.Count != fixtureInventory.Prices.Count,
            "两个商店定义未通过同一库存服务区分卡池、商品或槽位");

        var eventDefinitions = EventDefinitionCatalog.All.ToArray();
        Ensure(eventDefinitions.Length >= 2, "生产事件定义不足两个");
        Ensure(EventDefinitionCatalog.TryGet(eventDefinitions[0].Id, out var firstEvent, out var firstError), firstError);
        Ensure(EventDefinitionCatalog.TryGet(eventDefinitions[1].Id, out var secondEvent, out var secondError), secondError);
        Ensure(firstEvent.Title != secondEvent.Title && firstEvent.Description != secondEvent.Description,
            "两个生产事件定义未通过同一绑定入口区分");

        var eventFixture = new EventDefinition
        {
            Id = "fixture_event_b",
            Title = "测试事件 B",
            Description = "测试事件 B 说明",
            Options = new[]
            {
                new EventOptionDefinition
                {
                    Id = "fixture_leave",
                    Title = "测试离开",
                    Detail = "fixture",
                    Effects = new[]
                    {
                        new EventEffectCommand { EffectType = EventEffectType.Exit },
                    },
                },
            },
        };
        Ensure(EventDefinitionCatalog.TryResolve(eventFixture.Id, new[] { eventFixture },
                out var boundEventFixture, out var fixtureEventError), fixtureEventError);
        Ensure(boundEventFixture.Title != firstEvent.Title && boundEventFixture.Description != firstEvent.Description,
            "第二个事件定义未通过同一绑定入口区分");

        GD.Print("[NodeContentBindingSelfCheck] PASS Shop/Event definition bindings");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[NodeContentBindingSelfCheck] {error}");
    }
}
