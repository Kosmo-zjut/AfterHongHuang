using Godot;
using System.Linq;

/// <summary>Debug-only proof for full-health rest consumption and roster-derived companion visibility.</summary>
public static class LingmaiInteractionSelfCheck
{
    public static void Run()
    {
        CheckPageScopedCommand();

        var fullHealthRest = LingmaiActionRules.ResolveRest(80, 80);
        Ensure(fullHealthRest.WasAtFullHealth && fullHealthRest.ResultingHp == 80,
            "满血休养必须是有效的一次性无数值变化行动。");

        var damagedRest = LingmaiActionRules.ResolveRest(40, 80);
        Ensure(damagedRest.ActualHealing > 0 && damagedRest.ResultingHp <= 80,
            "受伤休养必须回复生命且不超过上限。");

        var roster = new PartyRoster();
        roster.Add(new PartyMember { MemberId = "fixture_player", DisplayName = "测试玩家", CurrentHp = 80, MaxHp = 80 });
        Ensure(roster.GetEligibleHealingTargets("fixture_player").Count == 0,
            "单人队伍不应出现可疗愈道友目标。");

        roster.Add(new PartyMember { MemberId = "fixture_ally_a", DisplayName = "测试同行者甲", CurrentHp = 20, MaxHp = 50 });
        roster.Add(new PartyMember { MemberId = "fixture_ally_b", DisplayName = "测试同行者乙", CurrentHp = 50, MaxHp = 100 });
        var targets = roster.GetEligibleHealingTargets("fixture_player");
        Ensure(targets.Count == 2 && targets[0].MemberId != targets[1].MemberId &&
            targets[0].DisplayName != targets[1].DisplayName,
            "两个真实可用友方必须按稳定 ID/显示名通过同一 roster 查询出现。");
        int allyABefore = targets[0].CurrentHp;
        int allyBBefore = targets[1].CurrentHp;
        Ensure(roster.TryApplyLingmaiHealing("fixture_player", targets[0].MemberId, out var healA, out var healError), healError);
        Ensure(healA.MemberId == targets[0].MemberId && targets[0].CurrentHp > allyABefore &&
            targets[1].CurrentHp == allyBBefore,
            "疗愈甲必须只改变显式目标，不能污染另一位队友。");
        Ensure(roster.TryApplyLingmaiHealing("fixture_player", targets[1].MemberId, out var healB, out healError), healError);
        Ensure(healB.MemberId == targets[1].MemberId && targets[1].CurrentHp > allyBBefore,
            "疗愈乙必须可通过同一入口独立选择。");

        roster.Add(new PartyMember { MemberId = "fixture_ally_full", DisplayName = "测试满血同行者", CurrentHp = 60, MaxHp = 60 });
        var fullTarget = roster.GetEligibleHealingTargets("fixture_player")
            .FirstOrDefault(member => member.MemberId == "fixture_ally_full");
        Ensure(fullTarget != null, "满血合规队友必须仍是可选疗愈目标。");
        int playerHpBefore = roster.Members[0].CurrentHp;
        int otherTargetHpBefore = targets[1].CurrentHp;
        Ensure(LingmaiActionRules.TryResolveCompanionHealing(roster, "fixture_player", fullTarget.MemberId,
            out var fullAction, out healError), healError);
        Ensure(fullAction.Healing.Rest.WasAtFullHealth && fullAction.Healing.Rest.ActualHealing == 0 &&
            fullTarget.CurrentHp == fullTarget.MaxHp,
            "满血队友疗愈必须不改变 HP 且明确返回无实际治疗结果。");
        Ensure(fullAction.ActionConsumed && !LingmaiActionRules.IsCompanionActionEnabled(fullAction.ActionConsumed) &&
            fullAction.Feedback.Contains("气血已满"),
            "满血疗愈后必须通过同一结果产生反馈并驱动动作灰置。");
        Ensure(roster.Members[0].CurrentHp == playerHpBefore && targets[1].CurrentHp == otherTargetHpBefore,
            "满血队友疗愈不得污染玩家或非目标队友。 ");

        GD.Print("[LingmaiInteractionSelfCheck] PASS full-health rest and roster eligibility");
    }

    /// <summary>Exercises the same GameManager-backed command that LingmaiController requests in production.</summary>
    private static void CheckPageScopedCommand()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xA904_0001UL), "无法创建灵脉命令自检新局。");
        var lingmaiNode = FindLingmaiNodeWithPredecessor(manager, out var predecessor);
        manager.CurrentMapLayer = predecessor.LayerIndex;
        manager.CurrentMapIndex = predecessor.IndexInLayer;
        manager.CurrentMapNodeId = predecessor.NodeId;

        Ensure(manager.TryEnterNode(lingmaiNode, out var enterError), enterError);
        Ensure(manager.ActiveNode?.NodeId == lingmaiNode.NodeId &&
            manager.CurrentMapNodeId == lingmaiNode.NodeId && manager.CurrentMapLayer == lingmaiNode.LayerIndex,
            "有效灵脉入口没有通过核心端口激活节点并推进路线位置。");

        var command = new LingmaiActionCommand(manager);
        manager.PlayerHp = manager.PlayerMaxHp;
        Ensure(command.TryRest(out var rest, out var restError), restError);
        Ensure(rest.WasNoOp && rest.Consumed && manager.PlayerHp == manager.PlayerMaxHp && !command.CanRest,
            "满血休养没有作为核心命令的有效一次性无数值变化行动。 ");
        int resultCountBeforeRepeatedRest = manager.RunState.AppliedResultIds.Count;
        Ensure(!command.TryRest(out _, out _), "重复休养生息被错误接受。");
        Ensure(manager.RunState.AppliedResultIds.Count == resultCountBeforeRepeatedRest &&
            manager.RunState.NodeStates[lingmaiNode.NodeId] == NodeLifecycleState.Active,
            "重复休养错误写入了节点终态或结果。 ");

        Ensure(manager.GetEligibleLingmaiHealingTargets().Count == 0,
            "单人灵脉不应暴露疗愈道友目标。 ");
        manager.RunState.Party.Add(new PartyMember
        {
            MemberId = "fixture_lingmai_ally",
            DisplayName = "测试同行者",
            CurrentHp = 20,
            MaxHp = 50,
        });
        var ally = manager.GetEligibleLingmaiHealingTargets()[0];
        int allyBefore = ally.CurrentHp;
        Ensure(command.TryHealCompanion(ally.MemberId, out var companion, out var companionError), companionError);
        Ensure(companion.Consumed && ally.CurrentHp > allyBefore && !command.CanHealCompanion,
            "队友疗愈没有通过核心命令只修改实际目标。 ");

        var upgradeable = manager.GetUpgradeableCards();
        Ensure(upgradeable.Count > 0, "灵脉命令自检缺少可升级卡牌。 ");
        string originalCardId = upgradeable[0].Info.DefinitionId ?? upgradeable[0].Info.Id;
        Ensure(command.TryUpgrade(upgradeable[0], out var upgrade, out var upgradeError), upgradeError);
        Ensure(upgrade.Consumed && !command.CanUpgrade &&
            (upgradeable[0].Info.DefinitionId ?? upgradeable[0].Info.Id) != originalCardId,
            "精进道行没有通过核心命令更新指定永久卡牌。 ");
        manager.Free();

        var invalidManager = new GameManager();
        Ensure(invalidManager.StartNewRun("wuzhu", 0xA904_0002UL), "无法创建非法灵脉自检新局。");
        int hpBeforeInvalid = invalidManager.PlayerHp;
        string nodeBeforeInvalid = invalidManager.CurrentMapNodeId;
        Ensure(!new LingmaiActionCommand(invalidManager).TryRest(out _, out _),
            "没有活动灵脉节点时仍允许结算休养。 ");
        Ensure(invalidManager.PlayerHp == hpBeforeInvalid && invalidManager.CurrentMapNodeId == nodeBeforeInvalid &&
            invalidManager.ActiveNode == null, "非法灵脉行动污染了 RunState。 ");

        var invalidNode = new MapNodeDefinition
        {
            NodeId = "fixture_missing_lingmai_node",
            NodeType = MapGraphNodeType.Lingmai,
        };
        Ensure(!invalidManager.TryEnterNode(invalidNode, out _), "不属于生产地图的灵脉节点被错误进入。 ");
        Ensure(invalidManager.ActiveNode == null && invalidManager.CurrentMapNodeId == nodeBeforeInvalid,
            "非法灵脉入口改变了活动节点或路线位置。 ");
        invalidManager.Free();
    }

    private static MapNodeDefinition FindLingmaiNodeWithPredecessor(GameManager manager,
        out MapNodeDefinition predecessor)
    {
        foreach (var node in manager.MapGraph.AllNodes())
        {
            if (node.NodeType != MapGraphNodeType.Lingmai)
                continue;
            foreach (var edge in manager.MapGraph.Edges)
            {
                if (edge.ToNodeId != node.NodeId)
                    continue;
                predecessor = manager.MapGraph.GetNode(edge.FromNodeId);
                if (predecessor != null)
                    return node;
            }
        }

        throw new System.InvalidOperationException("灵脉命令自检找不到具有前置节点的灵脉。 ");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[LingmaiInteractionSelfCheck] {error}");
    }
}
