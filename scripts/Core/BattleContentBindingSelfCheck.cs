using Godot;
using System.Collections.Generic;

/// <summary>
/// Debug 构建的战斗内容绑定自检。
/// 通过同一个 Map 节点 -> EncounterRequest -> BattleState 入口抽取不同遭遇，
/// 并用解析出的攻击意图验证 UI/执行层消费的是同一份定义数据。
/// </summary>
public static class BattleContentBindingSelfCheck
{
    public static void Run()
    {
        var manager = new GameManager();
        EnemySnapshot first = null;
        EnemySnapshot second = null;
        bool foundAttackWithGuard = false;
        int attackWithGuardValue = 0;

        for (ulong attempt = 0; attempt < 256; attempt++)
        {
            Ensure(manager.StartNewRun("wuzhu", 0xC0_2026_0713_0000UL + attempt),
                "无法创建战斗内容绑定自检新局");
            var node = FindBattleNode(manager);
            Ensure(manager.TryEnterBattle(node, out var enterError), enterError);
            Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);

            var enemy = manager.ActiveEncounter?.EnemyInfo;
            Ensure(enemy != null && !string.IsNullOrWhiteSpace(enemy.Id), "生产战斗入口缺少 EnemyInfo/稳定 ID");
            Ensure(!string.IsNullOrWhiteSpace(enemy.Name) && enemy.MaxHp > 0,
                $"遭遇 {enemy.Id} 的显示名称或最大生命无效");

            manager.EnemyTurnIndex = 0;
            if (enemy.MechanicDefinition != null &&
                enemy.MechanicDefinition.TriggerType != EnemyMechanicTriggerType.None)
                Ensure(!manager.EnemyMechanicActive, $"遭遇 {enemy.Id} 开战前机制错误激活");
            EnemySnapshot snapshot = null;
            bool foundAttack = false;
            for (int turn = 0; turn < 8; turn++)
            {
                Ensure(manager.TryPrepareEnemyIntent(out var prepared, out var intentError), intentError);
                var candidate = prepared.Intent;
                Ensure(candidate != null && !string.IsNullOrWhiteSpace(candidate.Name) &&
                    !string.IsNullOrWhiteSpace(prepared.ResolutionId),
                    $"遭遇 {enemy.Id} 解析意图缺少名称");
                if (candidate.IntentType == EnemyIntentType.攻击 && candidate.Value > 0 &&
                    candidate.AlternateDamageCondition == EnemyAlternateDamageCondition.None &&
                    candidate.BonusIfNoPlayerAttack == 0)
                {
                    string presentation = IntentPresentation.Format(prepared);
                    int hpBefore = manager.PlayerHp;
                    Ensure(presentation.Contains($"本次造成{prepared.FinalDamage}点伤害"),
                        $"遭遇 {enemy.Id} 的最终意图展示缺少冻结伤害");
                    Ensure(manager.TryExecutePreparedEnemyIntent(prepared, out var executeError), executeError);
                    Ensure(hpBefore - manager.PlayerHp == prepared.FinalDamage,
                        $"遭遇 {enemy.Id} 的显示解析意图与实际伤害不一致：{prepared.FinalDamage}/{hpBefore - manager.PlayerHp}");
                    Ensure(manager.EnemyHuti == candidate.GuardValue + candidate.MechanicGuardValue,
                        $"遭遇 {enemy.Id} 的攻击+护体结果未消费同一解析意图");
                    snapshot ??= new EnemySnapshot(enemy.Id, enemy.Name, enemy.MaxHp,
                        candidate.Name, candidate.Value, candidate.GuardValue + candidate.MechanicGuardValue);
                    if (candidate.GuardValue + candidate.MechanicGuardValue > 0)
                    {
                        foundAttackWithGuard = true;
                        attackWithGuardValue = candidate.GuardValue + candidate.MechanicGuardValue;
                    }
                    foundAttack = true;
                    if (foundAttackWithGuard)
                        break;
                    continue;
                }

                Ensure(manager.TryExecutePreparedEnemyIntent(prepared, out var turnError), turnError);
                if (candidate.MechanicAction != EnemyMechanicAction.None)
                    Ensure(manager.EnemyMechanicActive, $"遭遇 {enemy.Id} 执行定义机制动作后未激活");
            }
            Ensure(foundAttack, $"遭遇 {enemy.Id} 缺少可验证的非零定义攻击意图");
            Ensure(snapshot != null, $"遭遇 {enemy.Id} 缺少攻击结果快照");
            first ??= snapshot;
            if (first.Id != snapshot.Id)
                second ??= snapshot;

            var result = manager.CreateNodeResult(NodeResultType.Abandoned,
                "战斗内容绑定自检离场", out var resultError);
            Ensure(result != null && manager.SubmitNodeResult(result, out resultError), resultError);

            if (second != null && foundAttackWithGuard)
                break;
        }

        Ensure(first != null && second != null, "同一生产战斗入口未抽到两个不同 EnemyDefinition");
        Ensure(first.Id != second.Id && first.Name != second.Name,
            "不同 EnemyDefinition 的 ID/名称绑定结果未区分");
        Ensure(first.MaxHp != second.MaxHp || first.IntentName != second.IntentName ||
            first.IntentValue != second.IntentValue,
            "不同 EnemyDefinition 的 HP/意图未通过同一入口区分");
        Ensure(foundAttackWithGuard, "BattleContentBindingSelfCheck 未覆盖攻击+护体意图");
        manager.Free();
        GD.Print($"[BattleContentBindingSelfCheck] PASS production binding: " +
            $"{first.Id}/{first.Name}/{first.MaxHp}/{first.IntentName}/{first.IntentValue}/{first.IntentGuard} | " +
            $"{second.Id}/{second.Name}/{second.MaxHp}/{second.IntentName}/{second.IntentValue}/{second.IntentGuard} " +
            $"attack_guard={attackWithGuardValue}");
    }

    private static MapNodeDefinition FindBattleNode(GameManager manager)
    {
        foreach (var layer in manager.MapGraph.Layers)
        foreach (var node in layer)
        {
            if (node.NodeType == MapGraphNodeType.Battle)
                return node;
        }

        throw new System.InvalidOperationException("生产 MapGraph 缺少战斗节点。");
    }

    private sealed record EnemySnapshot(string Id, string Name, int MaxHp, string IntentName,
        int IntentValue, int IntentGuard);

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[BattleContentBindingSelfCheck] {error}");
    }
}
