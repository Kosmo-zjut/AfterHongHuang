using Godot;

/// <summary>
/// Debug 构建角色选择绑定自检。
/// 用两个不同的 CharacterInfo fixture 经过同一牌组解析入口，防止 UI 或新局入口靠默认首项兜底。
/// </summary>
public static class CharacterSelectionBindingSelfCheck
{
    public static void Run()
    {
        var first = new CharacterInfo
        {
            Id = "fixture_selection_a",
            Name = "fixture A",
            Unlocked = true,
            StarterDeck = new[]
            {
                new CardInfo { Id = "fixture_selection_card_a", Type = CardType.斗击,
                    TargetMode = CardTargetMode.Enemy }
            }
        };
        var second = new CharacterInfo
        {
            Id = "fixture_selection_b",
            Name = "fixture B",
            Unlocked = true,
            StarterDeck = new[]
            {
                new CardInfo { Id = "fixture_selection_card_b", Type = CardType.术法,
                    TargetMode = CardTargetMode.Self }
            }
        };

        Ensure(CharacterDeckFactory.TryCreate(first, out var firstDeck, out var firstError), firstError);
        Ensure(CharacterDeckFactory.TryCreate(second, out var secondDeck, out var secondError), secondError);
        Ensure(firstDeck.Count == 1 && secondDeck.Count == 1 &&
            firstDeck[0].Info.Id != secondDeck[0].Info.Id,
            "两个角色定义没有通过同一入口解析出不同初始牌组");
        var definitions = new[] { first, second };
        Ensure(CharacterSelectionResolver.TryChooseUnlocked(definitions, 0, out var firstIndex, out var pickFirstError),
            pickFirstError);
        Ensure(CharacterSelectionResolver.TryChooseUnlocked(definitions, 1, out var secondIndex, out var pickSecondError),
            pickSecondError);
        Ensure(firstIndex != secondIndex,
            "随机角色入口没有覆盖全部已解锁 CharacterDefinition");
        Ensure(!DataDefs.TryGetCharacterDefinition("fixture_selection_missing", out _),
            "缺失角色定义被错误解析");

        GD.Print("[CharacterSelectionBindingSelfCheck] PASS definition-driven selection");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[CharacterSelectionBindingSelfCheck] {error}");
    }
}
