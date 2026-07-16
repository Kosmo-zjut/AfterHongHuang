using Godot;

/// <summary>
/// 将已经由 GameManager 激活的节点上下文路由到对应底层场景。
/// 本类不修改 RunState；状态迁移必须先由 GameManager 的事务入口完成。
/// </summary>
public static class NodeSceneRouter
{
    /// <summary>按节点类型进入已激活的底层节点页。</summary>
    public static bool TryGoTo(GameManager manager, MapNodeDefinition node, out string error)
    {
        error = "";
        if (manager == null || node == null)
        {
            error = "节点场景路由缺少 GameManager 或 MapNodeDefinition。";
            GD.PrintErr($"[节点路由] {error}");
            return false;
        }

        if (TryRouteActiveNode(manager, node, out error))
            return true;

        if (!manager.AbortActiveNodeEntry(out var rollbackError))
        {
            error = $"{error}；路由失败后的节点回滚也失败：{rollbackError}";
            GD.PrintErr($"[节点路由] {error}");
        }
        else
        {
            GD.PrintErr($"[节点路由] {node.NodeId} 路由失败，已清理未开始节点：{error}");
        }
        return false;
    }

    /// <summary>
    /// Routes an already active node without mutating state on failure. Completed-node transitions
    /// call this inside GameManager's transaction so the original result page can be restored.
    /// </summary>
    public static bool TryRouteActiveNode(GameManager manager, MapNodeDefinition node, out string error)
    {
        error = "";
        if (manager == null || node == null)
        {
            error = "节点场景路由缺少 GameManager 或 MapNodeDefinition。";
            GD.PrintErr($"[节点路由] {error}");
            return false;
        }

        switch (node.NodeType)
        {
            case MapGraphNodeType.Battle:
            case MapGraphNodeType.Boss:
                return TryChangeScene(manager, node, "res://scenes/Battle/Battle.tscn", out error);
            case MapGraphNodeType.Lingmai:
                return TryChangeScene(manager, node, "res://scenes/Lingmai/Lingmai.tscn", out error);
            case MapGraphNodeType.Shop:
                return TryChangeScene(manager, node, "res://scenes/Shop/Shop.tscn", out error);
            case MapGraphNodeType.Event:
                return TryChangeScene(manager, node, "res://scenes/Event/Event.tscn", out error);
            default:
                error = $"不支持的节点场景类型：{node.NodeType}";
                GD.PrintErr($"[节点路由] {error}");
                return false;
        }
    }

    private static bool TryChangeScene(GameManager manager, MapNodeDefinition node,
        string scenePath, out string error)
    {
        return manager.ChangeSceneToFile(scenePath, out error);
    }
}
