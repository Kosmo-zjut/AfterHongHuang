using Godot;
using System;

/// <summary>Focused startup proof for node-page map access and explicit rejected-route boundaries.</summary>
public static class NodePageNavigationSelfCheck
{
    public static void Run()
    {
        VerifyBattleReadOnlyMap();
        VerifyActiveNodeMapMarker();
        VerifyAbandonRunReset();
        VerifyServicePageMapAccess(MapGraphNodeType.Lingmai);
        VerifyServicePageMapAccess(MapGraphNodeType.Shop);
        VerifyExistingMapToggleClosesSettingsFirst();
        GD.Print("[NodePageNavigationSelfCheck] PASS Battle/Lingmai/Shop map access and rejected routes");
    }

    private static void VerifyActiveNodeMapMarker()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xA002_0004UL), "无法创建活动节点地图标记自检新局。 ");
        string settledNodeId = manager.CurrentMapNodeId;
        var battleNode = FindNextNode(manager, MapGraphNodeType.Battle);
        Ensure(manager.TryEnterBattle(battleNode, out var enterError), enterError);
        Ensure(manager.RunState.NodeStates.TryGetValue(battleNode.NodeId, out var lifecycle) &&
            lifecycle == NodeLifecycleState.Active, "进入战斗后活动节点未保持 Active 生命周期。 ");
        Ensure(manager.CurrentMapNodeId == settledNodeId,
            "进入战斗提前推进了 CurrentMapNodeId。 ");
        Ensure(MapRenderer.IsNodeCurrentForDisplay(manager, battleNode),
            "地图表现未优先标记 ActiveNode。 ");
        var settledNode = manager.MapGraph.GetNode(settledNodeId);
        Ensure(!MapRenderer.IsNodeCurrentForDisplay(manager, settledNode),
            "已结算路线位置错误地覆盖了活动战斗节点标记。 ");
        manager.Free();
    }

    private static void VerifyAbandonRunReset()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xA002_0005UL), "无法创建放弃本局自检新局。 ");
        var battleNode = FindNextNode(manager, MapGraphNodeType.Battle);
        Ensure(manager.TryEnterBattle(battleNode, out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        manager.LingYun = 7;
        Ensure(manager.TryClearRunStateForSelfCheck(out var abandonError), abandonError);
        Ensure(manager.ActiveNode == null && manager.ActiveEncounter == null && manager.ActiveBattle == null,
            "放弃本局后活动节点/遭遇/战斗临时状态未清理。 ");
        Ensure(manager.MapGraph == null && manager.GetDeckSize() == 0 && manager.RunState.NodeStates.Count == 0 &&
            manager.RunState.AppliedResultIds.Count == 0 && string.IsNullOrEmpty(manager.CurrentMapNodeId) &&
            manager.LingYun == 0 && manager.PlayerMaxHp == 0,
            "放弃本局错误保留了路线、奖励或永久状态。 ");
        Ensure(manager.StartNewRun("wuzhu", 0xA002_0006UL), "放弃本局后无法创建新局。 ");
        Ensure(manager.MapGraph != null && manager.GetDeckSize() > 0 && manager.ActiveNode == null &&
            !string.IsNullOrEmpty(manager.CurrentMapNodeId),
            "新局继承了旧局的放弃状态。 ");
        manager.Free();
    }

    private static void VerifyExistingMapToggleClosesSettingsFirst()
    {
        var pageHost = new Node();
        var manager = new GameManager();
        var coordinator = new NodePageNavigationCoordinator(pageHost, manager, _ => { });
        var settings = new Panel();
        bool settingsClosed = false;
        bool mapClosed = false;

        Ensure(OverlayCoordinator.TryPrepareGlobalSettings(settings, () =>
        {
            settingsClosed = true;
            OverlayCoordinator.Unregister(settings);
            settings.Free();
        }, out var settingsError), settingsError);

        Ensure(NodePageNavigationCoordinator.TryPrepareMapToggleForSelfCheck(
            () => true, () => mapClosed = true, out bool closedExisting, out var toggleError),
            toggleError);
        Ensure(closedExisting && mapClosed,
            "地图已存在时 toggle 未执行已有地图关闭分支。 ");
        Ensure(settingsClosed,
            "地图已存在时 TopBar toggle 绕过 GlobalSettings 关闭负责人。 ");
        Ensure(!GodotObject.IsInstanceValid(settings),
            "地图 toggle 后 GlobalSettings 实例仍然存活。 ");

        coordinator.Dispose();
        pageHost.Free();
        manager.Free();
    }

    private static void VerifyBattleReadOnlyMap()
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", 0xA002_0001UL), "无法创建战斗导航自检新局。");
        var battleNode = FindNextNode(manager, MapGraphNodeType.Battle);
        Ensure(manager.TryEnterBattle(battleNode, out var enterError), enterError);
        Ensure(manager.TryBeginActiveBattle(out var beginError), beginError);
        Ensure(manager.TryGetNodePageMapInteractivity(out bool interactive, out var mapError), mapError);
        Ensure(!interactive, "普通战斗中的地图不应可推进。");
        Ensure(!manager.TryRouteFromNodePage(null, out _), "空目标路由被错误接受。");
        Ensure(manager.ActiveNode?.NodeId == battleNode.NodeId, "拒绝路由后战斗节点上下文被改变。");
        manager.Free();
    }

    private static void VerifyServicePageMapAccess(MapGraphNodeType nodeType)
    {
        var manager = new GameManager();
        Ensure(manager.StartNewRun("wuzhu", nodeType == MapGraphNodeType.Lingmai ? 0xA002_0002UL : 0xA002_0003UL),
            "无法创建服务节点导航自检新局。");
        var target = FindAnyNode(manager, nodeType);
        SetRoutePositionBefore(manager, target);
        string enterError;
        bool entered = nodeType == MapGraphNodeType.Lingmai
            ? manager.TryEnterLingmai(target, out enterError)
            : manager.TryEnterShop(target, out enterError);
        Ensure(entered, enterError);
        Ensure(manager.TryGetNodePageMapInteractivity(out bool interactive, out var mapError), mapError);
        Ensure(interactive, $"{nodeType} 页面地图应允许选择下一节点。");
        Ensure(!manager.TryRouteFromNodePage(null, out _), "空目标路由被错误接受。");
        Ensure(manager.ActiveNode?.NodeId == target.NodeId, "拒绝路由后服务节点上下文被改变。");
        manager.Free();
    }

    private static MapNodeDefinition FindNextNode(GameManager manager, MapGraphNodeType nodeType)
    {
        foreach (var edge in manager.MapGraph.GetOutgoing(manager.CurrentMapNodeId))
        {
            var node = manager.MapGraph.GetNode(edge.ToNodeId);
            if (node?.NodeType == nodeType)
                return node;
        }
        throw new InvalidOperationException($"[NodePageNavigationSelfCheck] 起点后缺少 {nodeType} 节点。");
    }

    private static MapNodeDefinition FindAnyNode(GameManager manager, MapGraphNodeType nodeType)
    {
        foreach (var node in manager.MapGraph.AllNodes())
        {
            if (node?.NodeType == nodeType)
                return node;
        }
        throw new InvalidOperationException($"[NodePageNavigationSelfCheck] MapGraph 缺少 {nodeType} 节点。");
    }

    private static void SetRoutePositionBefore(GameManager manager, MapNodeDefinition target)
    {
        foreach (var source in manager.MapGraph.AllNodes())
        {
            foreach (var edge in manager.MapGraph.GetOutgoing(source.NodeId))
            {
                if (edge.ToNodeId != target.NodeId)
                    continue;

                manager.CurrentMapNodeId = source.NodeId;
                manager.CurrentMapLayer = source.LayerIndex;
                manager.CurrentMapIndex = source.IndexInLayer;
                return;
            }
        }
        throw new InvalidOperationException($"[NodePageNavigationSelfCheck] {target.NodeId} 缺少前置路线节点。");
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new InvalidOperationException($"[NodePageNavigationSelfCheck] {error}");
    }
}
