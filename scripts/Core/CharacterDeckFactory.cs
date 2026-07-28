using System.Collections.Generic;

/// <summary>从角色定义创建新局永久牌组的唯一入口。</summary>
public static class CharacterDeckFactory
{
    /// <summary>
    /// Validates the exact deck source consumed by a new run without mutating RunState. Character
    /// selection uses this to reject incomplete definitions before it asks GameManager to start.
    /// </summary>
    public static bool TryValidate(CharacterInfo character, out string error)
    {
        error = "";
        if (character == null)
        {
            error = "角色定义为空。";
            return false;
        }

        if (character.StarterDeckEntries != null && character.StarterDeckEntries.Length > 0)
        {
            foreach (var entry in character.StarterDeckEntries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.CardId) || entry.Count <= 0)
                {
                    error = $"角色 {character.Id} 的初始牌组配置包含非法条目。";
                    return false;
                }
                if (!CardCatalogService.TryGetCardProjection(entry.CardId, out _, out var cardError))
                {
                    error = $"角色 {character.Id} 的初始牌组配置无法从 Catalog 解析：{cardError}";
                    return false;
                }
                if (!CardCatalogService.TryGetExecutionPlan(entry.CardId, out _, out var planError))
                {
                    error = $"角色 {character.Id} 的初始牌组配置缺少执行计划：{planError}";
                    return false;
                }
            }
            return true;
        }

        if (character.StarterDeck != null && character.StarterDeck.Length > 0)
        {
            foreach (var card in character.StarterDeck)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.Id))
                {
                    error = $"角色 {character.Id} 的 fixture 初始牌组包含无效卡牌定义。";
                    return false;
                }
            }
            return true;
        }

        error = $"角色 {character.Id} 缺少初始牌组配置。";
        return false;
    }

    /// <summary>复制角色定义中的卡牌实例；缺失定义或牌组时显式失败。</summary>
    public static bool TryCreate(CharacterInfo character, out List<CardRuntime> deck, out string error)
    {
        deck = new List<CardRuntime>();
        if (!TryValidate(character, out error))
            return false;

        // 生产角色用 ID+数量配置从 Resource Catalog 创建独立运行时投影。
        if (character.StarterDeckEntries != null && character.StarterDeckEntries.Length > 0)
        {
            foreach (var entry in character.StarterDeckEntries)
            {
                for (int copy = 0; copy < entry.Count; copy++)
                {
                    if (!CardCatalogService.TryCreateRuntimeCard(entry.CardId, out var runtime, out error))
                    {
                        error = $"角色 {character.Id} 的初始牌组配置非法：{error}";
                        deck.Clear();
                        return false;
                    }
                    deck.Add(runtime);
                }
            }
        }
        else if (character.StarterDeck != null && character.StarterDeck.Length > 0)
        {
            // 测试 fixture 仍可使用旧投影，但不能被生产 CharacterDefinition 使用。
            foreach (var card in character.StarterDeck)
            {
                // Fixture 只用于不进入 PlayCard 的旧自检；生产角色必须走上方 Catalog 入口。
                deck.Add(new CardRuntime(RewardPlanValidator.CloneCardDefinition(card), null));
            }
        }
        else
        {
            error = $"角色 {character.Id} 缺少初始牌组配置。";
            return false;
        }

        if (deck.Count == 0)
        {
            error = $"角色 {character.Id} 的初始牌组为空。";
            return false;
        }

        return true;
    }
}
