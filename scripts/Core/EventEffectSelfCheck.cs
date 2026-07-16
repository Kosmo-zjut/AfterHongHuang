using Godot;

/// <summary>
/// Debug 构建事件端口自检：OptionId 归属、分支离场契约和原子提交均经过生产入口。
/// 自检不直接调用控制器状态写入，也不把外部 Definition 注入生产解析器。
/// </summary>
public static class EventEffectSelfCheck
{
    public static void Run()
    {
        CheckDefinitionValidation();
        CheckProductionEventTransactions();
        GD.Print("[EventEffectSelfCheck] PASS production event ownership and atomic transactions");
    }

    private static void CheckDefinitionValidation()
    {
        var missingExit = new EventDefinition
        {
            Id = "fixture_missing_exit",
            Title = "缺少离场",
            Description = "fixture",
            Options = new[]
            {
                new EventOptionDefinition
                {
                    Id = "option",
                    Title = "选项",
                    Detail = "fixture",
                    Effects = new[] { new EventEffectCommand { EffectType = EventEffectType.GainLingyun, Value = 1 } },
                },
            },
        };
        Ensure(!EventDefinitionCatalog.TryValidate(missingExit, out _),
            "缺少 Exit 的实际分支定义被错误接受");
    }

    private static void CheckProductionEventTransactions()
    {
        foreach (var definition in EventDefinitionCatalog.All)
        {
            bool found = false;
            for (ulong attempt = 0; attempt < 512 && !found; attempt++)
            {
                var manager = new GameManager();
                Ensure(manager.StartNewRun("wuzhu", 0xC6_E7E7_0000UL + attempt),
                    "无法创建事件事务自检新局");
                var node = FindNode(manager, definition.Id);
                if (node == null)
                {
                    manager.Free();
                    continue;
                }

                Ensure(manager.TryEnterEvent(node, out var enterError), enterError);
                var option = FindStableAvailableOption(definition, manager);
                Ensure(option != null, $"事件缺少可执行选项：{definition.Id}");

                // 当前事件不能接受另一个事件的 OptionId，且拒绝前后 RunState 必须一致。
                string foreignId = FindForeignOptionId(definition.Id);
                if (!string.IsNullOrWhiteSpace(foreignId))
                {
                    int hpBeforeForeign = manager.PlayerHp;
                    int deckBeforeForeign = manager.GetDeckSize();
                    int lingyunBeforeForeign = manager.LingYun;
                    Ensure(!manager.TryCommitEventOption(foreignId, null, out _, out _),
                        "外来事件 OptionId 被错误接受");
                    Ensure(manager.PlayerHp == hpBeforeForeign && manager.GetDeckSize() == deckBeforeForeign &&
                        manager.LingYun == lingyunBeforeForeign, "外来 OptionId 拒绝后状态发生变化");
                }

                while (manager.GetDeckSize() < option.MinimumDeckSize)
                    manager.AddCardToDeck(manager.GetAllDeckCards()[0].Info);

                CardRuntime selected = option.RequiresCardChoice ? manager.GetAllDeckCards()[0] : null;
                int hpBefore = manager.PlayerHp;
                int lingyunBefore = manager.LingYun;
                int deckBefore = manager.GetDeckSize();
                Ensure(manager.TryCommitEventOption(option.Id, selected, out var transaction, out var error), error);
                if (transaction.RequiresCardChoice)
                {
                    Ensure(manager.TryCommitEventOption(option.Id, selected, out transaction, out error), error);
                }
                Ensure(transaction.RequestsExit && manager.ActiveNode != null &&
                    manager.ActiveNodeResultSubmitted,
                    $"事件 {definition.Id}/{option.Id} 未完成节点结算并保留结果页上下文");
                Ensure(manager.GetDeckSize() >= deckBefore - (option.RequiresCardChoice ? 1 : 0),
                    "事件牌组变化未通过统一事务提交");
                Ensure(manager.PlayerHp <= hpBefore && manager.LingYun >= lingyunBefore,
                    "事件事务结果未反映 Definition 效果");

                Ensure(manager.TryExitCompletedNodeToMap(out var exitError), exitError);
                Ensure(manager.ActiveNode == null && manager.CurrentState == PlayerState.空闲,
                    "事件地图节点离场未通过统一端口完成");

                // 已离场/终态节点不能再次提交同一选项，且不会继续改变永久状态。
                int hpAfter = manager.PlayerHp;
                int deckAfter = manager.GetDeckSize();
                int lingyunAfter = manager.LingYun;
                Ensure(!manager.TryCommitEventOption(option.Id, null, out _, out _),
                    "事件离场后二次提交未被拒绝");
                Ensure(manager.PlayerHp == hpAfter && manager.GetDeckSize() == deckAfter &&
                    manager.LingYun == lingyunAfter, "事件重复提交改变了永久状态");
                manager.Free();
                found = true;
            }
            Ensure(found, $"未找到生产事件节点：{definition.Id}");
        }
    }

    private static EventOptionDefinition FindStableAvailableOption(EventDefinition definition, GameManager manager)
    {
        foreach (var option in definition.Options)
        {
            if (option == null || !option.Enabled ||
                manager.PlayerHp < option.MinimumHp || manager.GetDeckSize() < option.MinimumDeckSize)
                continue;
            return option;
        }
        return null;
    }

    private static string FindForeignOptionId(string definitionId)
    {
        foreach (var definition in EventDefinitionCatalog.All)
        {
            if (definition.Id == definitionId)
                continue;
            foreach (var option in definition.Options)
                if (option != null && !string.IsNullOrWhiteSpace(option.Id))
                    return option.Id;
        }
        return "";
    }

    private static MapNodeDefinition FindNode(GameManager manager, string contentId)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
            if (node.NodeType == MapGraphNodeType.Event && node.ContentId == contentId)
                return node;
        return null;
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[EventEffectSelfCheck] {error}");
    }
}
