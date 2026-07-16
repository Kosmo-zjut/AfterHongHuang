using System.Collections.Generic;
using System.Linq;

/// <summary>事件解析阶段生成的不可变计划，不在此阶段修改 RunState。</summary>
public sealed class EventResolutionPlan
{
    public string NodeId { get; init; }
    public string DefinitionId { get; init; }
    public string OptionId { get; init; }
    public IReadOnlyList<EventEffectCommand> Commands { get; init; }
    public CardRuntime RemoveTarget { get; init; }
    public CardInfo AddedCard { get; init; }
    public int ResultHp { get; init; }
    public int ResultLingYun { get; init; }
    public bool RequiresCardChoice { get; init; }
    public bool RequestsExit { get; init; }
}

/// <summary>事件事务提交后的 UI 结果；页面只消费结果，不自行提交节点。</summary>
public sealed class EventTransactionResult
{
    public bool RequiresCardChoice { get; init; }
    public bool RequestsExit { get; init; }
    public CardInfo AddedCard { get; init; }
    public string Summary { get; init; }
}

/// <summary>
/// 事件通用解析端口。调用方只提交稳定 OptionId，定义必须从当前 ActiveNode.ContentId 解析，
/// 防止外部传入任意 Definition/Option 绕过节点归属校验。
/// </summary>
public static class EventEffectExecutor
{
    /// <summary>只用当前活动节点和稳定 OptionId 构建计划，不修改任何持久状态。</summary>
    public static bool TryBuildPlan(GameManager manager, string optionId, CardRuntime selectedCard,
        out EventResolutionPlan plan, out string error)
    {
        plan = null;
        error = "";
        var node = manager?.ActiveNode;
        if (manager == null || node == null || node.NodeType != MapGraphNodeType.Event ||
            string.IsNullOrWhiteSpace(node.NodeId) ||
            string.IsNullOrWhiteSpace(optionId))
        {
            error = "事件计划缺少有效 ActiveNode 或 OptionId。";
            return false;
        }
        var graphNode = manager.MapGraph?.GetNode(node.NodeId);
        if (graphNode == null || graphNode.NodeType != MapGraphNodeType.Event ||
            string.IsNullOrWhiteSpace(graphNode.ContentId))
        {
            error = "ActiveNode 缺少匹配的 Event MapNodeDefinition/ContentId。";
            return false;
        }
        if (!EventDefinitionCatalog.TryGet(graphNode.ContentId, out var definition, out error))
            return false;

        var option = definition.Options?.FirstOrDefault(candidate => candidate?.Id == optionId);
        if (option == null)
        {
            error = $"选项不属于当前事件定义：{definition.Id}/{optionId}";
            return false;
        }
        if (!EventDefinitionCatalog.TryValidate(definition, out error))
            return false;
        if (!option.Enabled)
        {
            error = option.DisabledReason ?? "当前事件选项不可用。";
            return false;
        }
        if (manager.PlayerHp < option.MinimumHp || manager.GetDeckSize() < option.MinimumDeckSize)
        {
            error = "当前状态不满足事件选项条件。";
            return false;
        }

        var commands = ResolveCommands(manager, option, node.NodeId, out error);
        if (commands == null)
            return false;

        CardRuntime removeTarget = null;
        CardInfo addedCard = null;
        bool requestsExit = false;
        foreach (var command in commands)
        {
            if (command == null || !System.Enum.IsDefined(typeof(EventEffectType), command.EffectType))
            {
                error = "事件效果类型未定义。";
                return false;
            }

            switch (command.EffectType)
            {
                case EventEffectType.RemoveCardChoice:
                    if (selectedCard == null)
                    {
                        plan = new EventResolutionPlan
                        {
                            NodeId = node.NodeId, DefinitionId = definition.Id, OptionId = option.Id,
                            Commands = commands, RequiresCardChoice = true, RequestsExit = false,
                        };
                        return true;
                    }
                    if (!manager.GetAllDeckCards().Contains(selectedCard))
                    {
                        error = "事件移除目标不在永久牌组中，拒绝结算。";
                        return false;
                    }
                    removeTarget = selectedCard;
                    break;
                case EventEffectType.AddCardFromPool:
                    if (!CardPoolCatalog.TryGet(command.CardPoolId, out var pool, out error))
                        return false;
                    var random = manager.RandomStreams.CreateEventStream(node.NodeId, 1);
                    addedCard = pool.Cards[random.NextInt(0, pool.Cards.Count)];
                    break;
                case EventEffectType.LoseHp:
                case EventEffectType.GainLingyun:
                case EventEffectType.Exit:
                    break;
                default:
                    error = $"事件效果类型不支持：{command.EffectType}";
                    return false;
            }

            if (command.EffectType == EventEffectType.Exit)
                requestsExit = true;
        }

        if (!requestsExit)
        {
            error = $"事件实际分支缺少 Exit：{definition.Id}/{option.Id}";
            return false;
        }

        int hp = manager.PlayerHp;
        int lingyun = manager.LingYun;
        foreach (var command in commands)
        {
            if (command.EffectType == EventEffectType.LoseHp)
                hp = System.Math.Max(0, hp - command.Value);
            else if (command.EffectType == EventEffectType.GainLingyun)
                lingyun += command.Value;
        }

        plan = new EventResolutionPlan
        {
            NodeId = node.NodeId,
            DefinitionId = definition.Id,
            OptionId = option.Id,
            Commands = commands,
            RemoveTarget = removeTarget,
            AddedCard = addedCard,
            ResultHp = hp,
            ResultLingYun = lingyun,
            RequiresCardChoice = false,
            RequestsExit = requestsExit,
        };
        return true;
    }

    private static IReadOnlyList<EventEffectCommand> ResolveCommands(GameManager manager,
        EventOptionDefinition option, string nodeId, out string error)
    {
        error = "";
        if (option.SuccessPercent < 0)
            return option.Effects;
        if (option.SuccessEffects == null || option.SuccessEffects.Count == 0 ||
            option.FailureEffects == null || option.FailureEffects.Count == 0)
        {
            error = $"事件选项缺少完整成功/失败分支：{option.Id}";
            return null;
        }
        var random = manager.RandomStreams.CreateEventStream(nodeId, 0);
        return random.NextInt(0, 100) < option.SuccessPercent
            ? option.SuccessEffects
            : option.FailureEffects;
    }
}
