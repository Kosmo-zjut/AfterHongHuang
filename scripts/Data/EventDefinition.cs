using System;
using System.Collections.Generic;

/// <summary>事件效果命令。控制器只提交命令，效果执行器负责校验和修改 RunState。</summary>
public enum EventEffectType
{
    LoseHp = 0,
    GainLingyun = 1,
    AddCardFromPool = 2,
    RemoveCardChoice = 3,
    Exit = 4,
}

/// <summary>一个事件选项的通用数据和效果命令。</summary>
public sealed class EventOptionDefinition
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Detail { get; init; }
    public IReadOnlyList<EventEffectCommand> Effects { get; init; } = Array.Empty<EventEffectCommand>();
    public IReadOnlyList<EventEffectCommand> SuccessEffects { get; init; }
    public IReadOnlyList<EventEffectCommand> FailureEffects { get; init; }
    public int SuccessPercent { get; init; } = -1;
    public int MinimumHp { get; init; }
    public int MinimumDeckSize { get; init; }
    public bool Enabled { get; init; } = true;
    public string DisabledReason { get; init; }

    public bool RequiresCardChoice
    {
        get
        {
            foreach (var command in Effects ?? Array.Empty<EventEffectCommand>())
                if (command?.EffectType == EventEffectType.RemoveCardChoice)
                    return true;
            return false;
        }
    }
}

/// <summary>事件效果命令参数。具体事件只在集中定义中组合这些通用命令。</summary>
public sealed class EventEffectCommand
{
    public EventEffectType EffectType { get; init; }
    public int Value { get; init; }
    public string CardPoolId { get; init; }
}

/// <summary>ACT1 事件定义；没有事件专名枚举，页面按选项列表渲染。</summary>
public sealed class EventDefinition
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public IReadOnlyList<EventOptionDefinition> Options { get; init; } = Array.Empty<EventOptionDefinition>();
}

/// <summary>事件定义目录与通用绑定校验。</summary>
public static class EventDefinitionCatalog
{
    private static readonly IReadOnlyList<EventDefinition> Definitions = new[]
    {
        new EventDefinition
        {
            Id = "act1_event_heavenly_river",
            Title = "天河倒灌",
            Description = "天崩后的残水冲入废墟，灵气和碎符一起被卷来。选择一次行动，或绕开水口。",
            Options = new[]
            {
                new EventOptionDefinition
                {
                    Id = "stabilize",
                    Title = "稳住堤口",
                    Detail = "失去6点生命，获得30灵韵",
                    MinimumHp = 7,
                    Effects = new EventEffectCommand[]
                    {
                        new() { EffectType = EventEffectType.LoseHp, Value = 6 },
                        new() { EffectType = EventEffectType.GainLingyun, Value = 30 },
                        new() { EffectType = EventEffectType.Exit },
                    },
                },
                new EventOptionDefinition
                {
                    Id = "reverse_current",
                    Title = "逆流取符",
                    Detail = "70%获得一张ACT1奖赏卡；30%失去12生命并获得15灵韵",
                    MinimumHp = 13,
                    SuccessPercent = 70,
                    SuccessEffects = new EventEffectCommand[]
                    {
                        new() { EffectType = EventEffectType.AddCardFromPool, CardPoolId = "reward_cards" },
                        new() { EffectType = EventEffectType.Exit },
                    },
                    FailureEffects = new EventEffectCommand[]
                    {
                        new() { EffectType = EventEffectType.LoseHp, Value = 12 },
                        new() { EffectType = EventEffectType.GainLingyun, Value = 15 },
                        new() { EffectType = EventEffectType.Exit },
                    },
                },
                new EventOptionDefinition
                {
                    Id = "leave_river",
                    Title = "绕开水口",
                    Detail = "无收益、无代价，结束事件",
                    Effects = new EventEffectCommand[] { new() { EffectType = EventEffectType.Exit } },
                },
            },
        },
        new EventDefinition
        {
            Id = "act1_event_scavenger_seal",
            Title = "散修遗篆",
            Description = "半座护体符阵仍等着最后一笔。可以以卡换灵，也可以不取遗篆。",
            Options = new[]
            {
                new EventOptionDefinition
                {
                    Id = "exchange_card",
                    Title = "以卡换灵",
                    Detail = "牌组至少11张：移除一张牌，获得45灵韵",
                    MinimumDeckSize = 11,
                    Effects = new EventEffectCommand[]
                    {
                        new() { EffectType = EventEffectType.RemoveCardChoice },
                        new() { EffectType = EventEffectType.GainLingyun, Value = 45 },
                        new() { EffectType = EventEffectType.Exit },
                    },
                },
                new EventOptionDefinition
                {
                    Id = "reveal_treasure",
                    Title = "揭符取宝",
                    Detail = "法宝道缘尚未显现（当前系统未开放）",
                    Enabled = false,
                    DisabledReason = "法宝道缘尚未显现",
                },
                new EventOptionDefinition
                {
                    Id = "leave_seal",
                    Title = "不取遗篆",
                    Detail = "无收益、无代价，结束事件",
                    Effects = new EventEffectCommand[] { new() { EffectType = EventEffectType.Exit } },
                },
            },
        },
    };

    public static IReadOnlyList<EventDefinition> All => Definitions;

    public static bool TryGet(string id, out EventDefinition definition, out string error) =>
        TryResolve(id, Definitions, out definition, out error);

    /// <summary>目录与测试 fixture 共用的事件定义绑定入口。</summary>
    public static bool TryResolve(string id, IEnumerable<EventDefinition> definitions,
        out EventDefinition definition, out string error)
    {
        definition = null;
        error = "";
        if (string.IsNullOrWhiteSpace(id) || definitions == null)
        {
            error = "事件定义绑定缺少 ID 或定义集合。";
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

        error = $"事件定义不存在：{id}";
        return false;
    }

    public static bool TryValidate(EventDefinition definition, out string error)
    {
        error = "";
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
            string.IsNullOrWhiteSpace(definition.Title) || string.IsNullOrWhiteSpace(definition.Description) ||
            definition.Options == null || definition.Options.Count == 0)
        {
            error = "事件定义字段不完整。";
            return false;
        }

        var optionIds = new HashSet<string>();
        foreach (var option in definition.Options)
        {
            if (option == null || string.IsNullOrWhiteSpace(option.Id) ||
                string.IsNullOrWhiteSpace(option.Title) || string.IsNullOrWhiteSpace(option.Detail) ||
                !optionIds.Add(option.Id) || option.MinimumHp < 0 || option.MinimumDeckSize < 0)
            {
                error = $"事件 {definition.Id} 存在无效或重复选项。";
                return false;
            }
            if (option.SuccessPercent < -1 || option.SuccessPercent > 100)
            {
                error = $"事件 {definition.Id}/{option.Id} 的成功概率无效。";
                return false;
            }
            if (!ValidateCommands($"事件 {definition.Id}/{option.Id}", option.Effects, out error) ||
                !ValidateCommands($"事件 {definition.Id}/{option.Id}/success", option.SuccessEffects, out error) ||
                !ValidateCommands($"事件 {definition.Id}/{option.Id}/failure", option.FailureEffects, out error))
                return false;

            if (!option.Enabled)
                continue;
            if (option.SuccessPercent >= 0 &&
                ((option.SuccessEffects == null || option.SuccessEffects.Count == 0) ||
                 (option.FailureEffects == null || option.FailureEffects.Count == 0)))
            {
                error = $"事件 {definition.Id}/{option.Id} 缺少成功或失败效果。";
                return false;
            }
            var directEffects = option.SuccessPercent >= 0 ? option.SuccessEffects : option.Effects;
            if (option.SuccessPercent < 0 && (directEffects == null || directEffects.Count == 0))
            {
                error = $"事件 {definition.Id}/{option.Id} 缺少效果命令。";
                return false;
            }
            bool hasExit = option.SuccessPercent >= 0
                ? ContainsExit(option.SuccessEffects) && ContainsExit(option.FailureEffects)
                : ContainsExit(option.Effects);
            if (!hasExit)
            {
                error = $"事件 {definition.Id}/{option.Id} 的实际结果分支缺少 Exit 离场命令。";
                return false;
            }
        }

        return true;
    }

    private static bool ValidateCommands(string label, IReadOnlyList<EventEffectCommand> commands, out string error)
    {
        error = "";
        if (commands == null)
            return true;
        foreach (var command in commands)
        {
            if (command == null || !Enum.IsDefined(typeof(EventEffectType), command.EffectType))
            {
                error = $"{label} 存在未知效果命令。";
                return false;
            }
            switch (command.EffectType)
            {
                case EventEffectType.LoseHp:
                case EventEffectType.GainLingyun:
                    if (command.Value <= 0)
                    {
                        error = $"{label} 的数值效果必须大于0。";
                        return false;
                    }
                    break;
                case EventEffectType.AddCardFromPool:
                    if (string.IsNullOrWhiteSpace(command.CardPoolId))
                    {
                        error = $"{label} 的 AddCardFromPool 缺少卡池。";
                        return false;
                    }
                    if (!CardPoolCatalog.TryGet(command.CardPoolId, out _, out error))
                        return false;
                    break;
                case EventEffectType.RemoveCardChoice:
                case EventEffectType.Exit:
                    break;
            }
        }
        return true;
    }

    private static bool ContainsExit(IReadOnlyList<EventEffectCommand> commands)
    {
        if (commands == null)
            return false;
        foreach (var command in commands)
            if (command?.EffectType == EventEffectType.Exit)
                return true;
        return false;
    }
}
