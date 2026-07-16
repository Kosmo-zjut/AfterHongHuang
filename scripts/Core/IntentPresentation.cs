using System.Collections.Generic;

/// <summary>把已解析的敌方意图转换为 UI/日志共享的效果摘要。</summary>
public static class IntentPresentation
{
    /// <summary>格式化已冻结的最终意图；实际伤害和条件状态来自同一解析对象。</summary>
    public static string Format(ResolvedEnemyIntent resolved)
    {
        if (resolved?.Intent == null)
            return "意图数据错误";

        var intent = resolved.Intent;
        var effects = new List<string>();
        if (intent.IntentType == EnemyIntentType.攻击 && resolved.FinalDamage > 0)
            effects.Add($"本次造成{resolved.FinalDamage}点伤害");
        if (intent.GuardValue > 0)
            effects.Add($"获得{intent.GuardValue}点护体");
        if (intent.MechanicGuardValue > 0)
            effects.Add($"获得{intent.MechanicGuardValue}点护体");
        if (intent.VulnerableValue > 0)
            effects.Add($"施加{intent.VulnerableValue}层易损");
        if (intent.BonusIfNoPlayerAttack > 0)
            effects.Add($"本回合无斗击时伤害+{intent.BonusIfNoPlayerAttack}");
        if (intent.AlternateDamageCondition != EnemyAlternateDamageCondition.None)
        {
            string condition = string.IsNullOrWhiteSpace(resolved.DamageConditionDisplayName)
                ? resolved.DamageConditionId
                : resolved.DamageConditionDisplayName;
            effects.Add(resolved.DamageConditionTriggered
                ? $"已触发{condition}"
                : $"未触发{condition}");
        }
        if (intent.MechanicAction != EnemyMechanicAction.None)
            effects.Add(string.IsNullOrWhiteSpace(intent.MechanicDisplayName)
                ? "激活机制"
                : $"激活{intent.MechanicDisplayName}");
        if (intent.IsMechanicTrigger)
            effects.Add(string.IsNullOrWhiteSpace(intent.MechanicDisplayName)
                ? "触发机制"
                : $"触发{intent.MechanicDisplayName}");
        if (intent.TriggerSpecial)
            effects.Add("触发特殊效果");
        if (intent.IntentType != EnemyIntentType.攻击)
            effects.Insert(0, "不攻击");
        if (!string.IsNullOrWhiteSpace(intent.Description))
            effects.Add(intent.Description);

        return effects.Count == 0
            ? $"意图：{intent.Name}"
            : $"意图：{intent.Name}：{string.Join("，", effects)}";
    }

    /// <summary>只消费解析结果字段，不按敌人或机制 ID 补文案和数值。</summary>
    public static string Format(EnemyIntent intent)
    {
        if (intent == null || string.IsNullOrWhiteSpace(intent.Name))
            return "意图数据错误";

        var effects = new List<string>();
        if (intent.IntentType == EnemyIntentType.攻击 && intent.Value > 0)
            effects.Add($"造成{intent.Value}点伤害");
        if (intent.GuardValue > 0)
            effects.Add($"获得{intent.GuardValue}点护体");
        if (intent.MechanicGuardValue > 0)
            effects.Add($"获得{intent.MechanicGuardValue}点护体");
        if (intent.VulnerableValue > 0)
            effects.Add($"施加{intent.VulnerableValue}层易损");
        if (intent.BonusIfNoPlayerAttack > 0)
            effects.Add($"本回合无斗击时伤害+{intent.BonusIfNoPlayerAttack}");
        if (intent.AlternateDamageCondition != EnemyAlternateDamageCondition.None && intent.AlternateValue > 0)
            effects.Add($"条件伤害{intent.AlternateValue}");
        if (intent.MechanicAction != EnemyMechanicAction.None)
            effects.Add(string.IsNullOrWhiteSpace(intent.MechanicDisplayName)
                ? "激活机制"
                : $"激活{intent.MechanicDisplayName}");
        if (intent.IsMechanicTrigger)
            effects.Add(string.IsNullOrWhiteSpace(intent.MechanicDisplayName)
                ? "触发机制"
                : $"触发{intent.MechanicDisplayName}");
        if (intent.TriggerSpecial)
            effects.Add("触发特殊效果");
        if (intent.IntentType != EnemyIntentType.攻击)
            effects.Insert(0, "不攻击");
        if (!string.IsNullOrWhiteSpace(intent.Description))
            effects.Add(intent.Description);

        return effects.Count == 0
            ? $"意图：{intent.Name}"
            : $"意图：{intent.Name}：{string.Join("，", effects)}";
    }
}
