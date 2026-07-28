using Godot;
using System;

/// <summary>Focused startup proof for node-page map access and explicit rejected-route boundaries.</summary>
public static class NodePageNavigationSelfCheck
{
    public static void Run()
    {
        VerifyBattleReadOnlyMap();
        VerifyServicePageMapAccess(MapGraphNodeType.Lingmai);
        VerifyServicePageMapAccess(MapGraphNodeType.Shop);
        GD.Print("[NodePageNavigationSelfCheck] PASS Battle/Lingmai/Shop map access and rejected routes");
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
