using Godot;
using System.Collections.Generic;

/// <summary>卡牌 Catalog 的 Resource 根。卡牌文件和池成员均由显式索引声明。</summary>
[Tool]
[GlobalClass]
public partial class CardCatalogResource : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;
    [Export] public Godot.Collections.Array<string> CardResourcePaths { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Pools { get; set; } = new();

    public IReadOnlyList<CardPoolIndexDefinition> GetPools()
    {
        var result = new List<CardPoolIndexDefinition>();
        foreach (var entry in Pools)
        {
            var ids = new List<string>();
            if (entry.TryGetValue("cardIds", out var cardIds))
                foreach (var id in cardIds.AsGodotArray())
                    ids.Add(id.AsString());
            result.Add(new CardPoolIndexDefinition
            {
                Id = CardDefinitionReader.ReadString(entry, "id"),
                CardIds = ids,
            });
        }
        return result;
    }

}

/// <summary>Catalog 中一个可供奖励、事件或商店引用的显式卡池索引。</summary>
public sealed class CardPoolIndexDefinition
{
    public string Id { get; init; }
    public IReadOnlyList<string> CardIds { get; init; }
}
