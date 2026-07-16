using System.Collections.Generic;

/// <summary>从角色定义创建新局永久牌组的唯一入口。</summary>
public static class CharacterDeckFactory
{
    /// <summary>复制角色定义中的卡牌实例；缺失定义或牌组时显式失败。</summary>
    public static bool TryCreate(CharacterInfo character, out List<CardRuntime> deck, out string error)
    {
        deck = new List<CardRuntime>();
        if (!DataDefs.TryResolveStarterDeck(character, out var starterCards, out error))
            return false;

        foreach (var card in starterCards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.Id))
            {
                error = $"角色 {character.Id} 的初始牌组包含无效卡牌定义。";
                deck.Clear();
                return false;
            }

            deck.Add(new CardRuntime { Info = card });
        }

        if (deck.Count == 0)
        {
            error = $"角色 {character.Id} 的初始牌组为空。";
            return false;
        }

        return true;
    }
}
