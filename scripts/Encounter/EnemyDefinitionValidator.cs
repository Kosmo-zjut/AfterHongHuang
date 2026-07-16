using System;
using System.Collections.Generic;

/// <summary>
/// 集中验证敌人、阶段和意图定义。战斗入口在创建运行时副本后再次调用，
/// 确保非法内容不会通过 UI 或执行器的默认路径被消费。
/// </summary>
public static class EnemyDefinitionValidator
{
    public static bool TryValidate(EnemyDefinition definition, out string error)
    {
        error = "";
        if (definition == null)
        {
            error = "敌人定义为空。";
            return false;
        }

        return ValidateCore(definition.Id, definition.Name, definition.MaxHp, definition.Mechanic,
            definition.MechanicDefinition, definition.RequiresPhaseDefinitions, definition.SequencePolicy,
            definition.OpeningIntents, definition.Intents, definition.PhaseDefinitions, out error);
    }

    public static bool TryValidate(EnemyInfo info, out string error)
    {
        error = "";
        if (info == null)
        {
            error = "敌人运行时定义为空。";
            return false;
        }

        return ValidateCore(info.Id, info.Name, info.MaxHp, info.Mechanic, info.MechanicDefinition,
            info.RequiresPhaseDefinitions, info.SequencePolicy, info.OpeningIntents, info.Intents,
            info.PhaseDefinitions, out error);
    }

    private static bool ValidateCore(string id, string name, int maxHp, EnemyMechanicKind mechanic,
        EnemyMechanicDefinition mechanicDefinition, bool requiresPhases,
        EnemyIntentSequencePolicy sequencePolicy, List<EnemyIntent> openingIntents,
        List<EnemyIntent> intents, List<EnemyPhaseDefinition> phases, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || maxHp <= 0)
        {
            error = $"敌人定义基础字段无效：{id ?? "<missing-id>"}";
            return false;
        }

        if (!Enum.IsDefined(typeof(EnemyMechanicKind), mechanic))
        {
            error = $"敌人 {id} 的机制类型未定义：{mechanic}。";
            return false;
        }

        if (!ValidateMechanic(id, mechanic, mechanicDefinition, out error) ||
            !ValidateSequence($"敌人 {id} 基础序列", sequencePolicy, openingIntents, intents, out error))
            return false;

        if (requiresPhases && (phases == null || phases.Count == 0))
        {
            error = $"敌人 {id} 声明阶段序列但缺少 PhaseDefinitions。";
            return false;
        }

        if (phases == null)
            return true;

        var phaseIds = new HashSet<string>();
        int previousThreshold = int.MaxValue;
        for (int index = 0; index < phases.Count; index++)
        {
            var phase = phases[index];
            if (phase == null || string.IsNullOrWhiteSpace(phase.Id) || !phaseIds.Add(phase.Id))
            {
                error = $"敌人 {id} 阶段定义为空或 ID 重复：index={index}。";
                return false;
            }

            if (phase.PhaseIndex != index + 1 || phase.HealthAtOrBelow < 0 ||
                phase.HealthAtOrBelow >= previousThreshold)
            {
                error = $"敌人 {id} 阶段顺序或阈值无效：{phase.Id}/{phase.PhaseIndex}/{phase.HealthAtOrBelow}。";
                return false;
            }

            previousThreshold = phase.HealthAtOrBelow;
            if (!ValidateSequence($"敌人 {id} 阶段 {phase.Id}", phase.SequencePolicy,
                    phase.OpeningIntents, phase.Intents, out error))
                return false;
        }

        return true;
    }

    private static bool ValidateMechanic(string id, EnemyMechanicKind mechanic,
        EnemyMechanicDefinition definition, out string error)
    {
        error = "";
        if (mechanic != EnemyMechanicKind.None && definition == null)
        {
            error = $"敌人 {id} 声明机制但缺少机制定义。";
            return false;
        }

        if (definition == null)
            return true;

        if (!Enum.IsDefined(typeof(EnemyMechanicTriggerType), definition.TriggerType))
        {
            error = $"敌人 {id} 机制触发类型未定义：{definition.TriggerType}。";
            return false;
        }
        if (definition.TriggerType == EnemyMechanicTriggerType.None && definition.TriggeredIntent != null)
        {
            error = $"敌人 {id} 机制无触发类型却包含触发意图。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.DisplayName) ||
            string.IsNullOrWhiteSpace(definition.Description))
        {
            error = $"敌人 {id} 机制定义字段不完整。";
            return false;
        }

        if (definition.TriggerType != EnemyMechanicTriggerType.None && definition.TriggeredIntent == null)
        {
            error = $"敌人 {id} 机制 {definition.Id} 缺少触发意图。";
            return false;
        }

        if (definition.TriggerType == EnemyMechanicTriggerType.HealthThreshold &&
            definition.HealthThreshold < 0)
        {
            error = $"敌人 {id} 机制 {definition.Id} 缺少生命阈值。";
            return false;
        }

        if (definition.CounterThreshold > 0 && string.IsNullOrWhiteSpace(definition.CounterKey))
        {
            error = $"敌人 {id} 机制 {definition.Id} 缺少计数键。";
            return false;
        }

        if (definition.CounterThreshold < 0 ||
            (definition.TriggerType == EnemyMechanicTriggerType.HealthThreshold && definition.HealthThreshold < 0) ||
            (definition.HasDamageOverride && definition.DamageOverride < 0))
        {
            error = $"敌人 {id} 机制参数非法。";
            return false;
        }

        return definition.TriggeredIntent == null || ValidateIntent(
            $"敌人 {id} 机制 {definition.Id}", definition.TriggeredIntent, out error);
    }

    private static bool ValidateSequence(string label, EnemyIntentSequencePolicy policy,
        List<EnemyIntent> openingIntents, List<EnemyIntent> intents, out string error)
    {
        error = "";
        if (!Enum.IsDefined(typeof(EnemyIntentSequencePolicy), policy))
        {
            error = $"{label} 的序列策略未定义：{policy}。";
            return false;
        }

        if (policy == EnemyIntentSequencePolicy.OpeningThenLoop &&
            (openingIntents == null || openingIntents.Count == 0))
        {
            error = $"{label} 声明 OpeningThenLoop 但没有 Opening intents。";
            return false;
        }

        if (intents == null || intents.Count == 0)
        {
            error = $"{label} 缺少 Loop intents。";
            return false;
        }

        if (openingIntents != null)
        {
            for (int index = 0; index < openingIntents.Count; index++)
            {
                if (!ValidateIntent($"{label} Opening[{index}]", openingIntents[index], out error))
                    return false;
            }
        }

        for (int index = 0; index < intents.Count; index++)
        {
            if (!ValidateIntent($"{label} Loop[{index}]", intents[index], out error))
                return false;
        }

        return true;
    }

    private static bool ValidateIntent(string label, EnemyIntent intent, out string error)
    {
        error = "";
        if (intent == null || string.IsNullOrWhiteSpace(intent.Name))
        {
            error = $"{label} 缺少显示名称。";
            return false;
        }

        if (!Enum.IsDefined(typeof(EnemyMechanicAction), intent.MechanicAction) ||
            !Enum.IsDefined(typeof(EnemyAlternateDamageCondition), intent.AlternateDamageCondition))
        {
            error = $"{label} 的机制动作或条件伤害类型未定义。";
            return false;
        }

        if (!Enum.IsDefined(typeof(EnemyIntentType), intent.IntentType))
        {
            error = $"{label} 的 IntentType 未定义：{intent.IntentType}。";
            return false;
        }

        if (intent.IntentType == EnemyIntentType.攻击 && intent.Value <= 0)
        {
            error = $"{label} 的攻击伤害无效：{intent.Value}。";
            return false;
        }

        if (intent.Value < 0 || intent.GuardValue < 0 || intent.VulnerableValue < 0 ||
            intent.AlternateValue < 0 || intent.BonusIfNoPlayerAttack < 0 || intent.MechanicGuardValue < 0)
        {
            error = $"{label} 的效果数值不能为负。";
            return false;
        }

        if (intent.IntentType == EnemyIntentType.强化 && intent.GuardValue <= 0 &&
            intent.VulnerableValue <= 0 && intent.MechanicAction == EnemyMechanicAction.None &&
            string.IsNullOrWhiteSpace(intent.Description))
        {
            error = $"{label} 的强化意图没有可执行效果。";
            return false;
        }

        if (intent.AlternateDamageCondition != EnemyAlternateDamageCondition.None &&
            intent.AlternateValue <= 0)
        {
            error = $"{label} 的条件伤害缺少 AlternateValue。";
            return false;
        }

        if (intent.AlternateDamageCondition != EnemyAlternateDamageCondition.None &&
            (string.IsNullOrWhiteSpace(intent.DamageConditionId) ||
             string.IsNullOrWhiteSpace(intent.DamageConditionDisplayName)))
        {
            error = $"{label} 的条件伤害缺少显示定义。";
            return false;
        }

        if (intent.AlternateDamageCondition == EnemyAlternateDamageCondition.None && intent.AlternateValue != 0)
        {
            error = $"{label} 没有条件伤害却包含替代值。";
            return false;
        }

        return true;
    }
}
