using Godot;

/// <summary>
/// 生产地图绘制工具。只消费 RunState.MapGraph，不读取旧静态路线表。
/// Battle 与 Map 共用同一份节点定义，但 Battle 的回调只读不推进。
/// </summary>
public static class MapRenderer
{
    public const float LayerSpacing = 220f;
    public const float NodeWidth = 190f;
    public const float NodeHeight = 76f;
    public const float NodeSpacingV = 104f;
    public const float MarginLeft = 40f;
    public const float CenterY = 420f;
    public const int FontSize = 13;

    public static bool IsNodeAccessible(MapNodeDefinition node)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.MapGraph == null || node == null)
            return false;
        bool completedServiceNode = gm.ActiveNodeResultSubmitted && gm.ActiveNode != null &&
            (gm.CurrentState == PlayerState.灵脉中 || gm.CurrentState == PlayerState.商店中 ||
             gm.CurrentState == PlayerState.事件中);
        bool activeServiceRoute = gm.ActiveNode != null && !gm.ActiveNodeResultSubmitted &&
            (gm.CurrentState == PlayerState.灵脉中 || gm.CurrentState == PlayerState.商店中);
        bool canExploreFromMap = gm.CurrentState == PlayerState.空闲 ||
                                  gm.CurrentState == PlayerState.战斗胜利结算 || completedServiceNode ||
                                  activeServiceRoute;
        if (!canExploreFromMap || !gm.DaoMarkSelected || !gm.MapNodesUnlocked)
            return false;
        if (gm.CurrentState == PlayerState.战斗胜利结算 &&
            (gm.ActiveBattle == null || !gm.ActiveBattle.ResultSubmitted))
            return false;
        if (node.NodeType == MapGraphNodeType.Start || gm.VisitedNodeIds.Contains(node.NodeId))
            return false;
        if (node.LayerIndex != gm.CurrentMapLayer + 1 ||
            string.IsNullOrEmpty(gm.CurrentMapNodeId))
            return false;

        foreach (var edge in gm.MapGraph.GetOutgoing(gm.CurrentMapNodeId))
        {
            if (edge.ToNodeId == node.NodeId)
                return true;
        }
        return false;
    }

    /// <summary>旧坐标接口已停用，调用方必须先从 RunState.MapGraph 取得节点。</summary>
    public static bool IsNodeAccessible(int layer, int nodeIndex)
    {
        var graph = GameManager.Instance?.MapGraph;
        if (graph == null || layer < 0 || layer >= graph.Layers.Count ||
            nodeIndex < 0 || nodeIndex >= graph.Layers[layer].Count)
            return false;
        return IsNodeAccessible(graph.Layers[layer][nodeIndex]);
    }

    /// <summary>构建地图按钮和连线；layer/index 只作为稳定 UI 坐标回调参数。</summary>
    public static void BuildInteractiveMap(Control parent, float offsetX, float offsetY,
        System.Action<int, int> onNodePressed)
    {
        var gm = GameManager.Instance;
        var graph = gm?.MapGraph;
        if (graph == null)
        {
            GD.PrintErr("[地图] 无法绘制：RunState 缺少生产 MapGraph。");
            return;
        }

        foreach (var graphNode in graph.AllNodes())
        {
            if (graphNode == null || string.IsNullOrWhiteSpace(graphNode.DisplayName))
            {
                GD.PrintErr("[地图] MapGraph 节点缺少显示名，拒绝绘制，避免把内部 ID 当玩家文案。");
                return;
            }
        }

        for (int layerIndex = 0; layerIndex < graph.Layers.Count - 1; layerIndex++)
        {
            var layer = graph.Layers[layerIndex];
            foreach (var node in layer)
            {
                Vector2 from = GetNodeCenter(node, offsetX, offsetY);
                foreach (var edge in graph.GetOutgoing(node.NodeId))
                {
                    var target = graph.GetNode(edge.ToNodeId);
                    if (target != null)
                        DrawConnectionLine(parent, from, GetNodeCenter(target, offsetX, offsetY));
                }
            }
        }

        for (int layerIndex = 0; layerIndex < graph.Layers.Count; layerIndex++)
        {
            foreach (var node in graph.Layers[layerIndex])
            {
                var btn = new Button
                {
                    Text = node.DisplayName,
                    Position = GetNodePosition(node, offsetX, offsetY),
                    Size = new Vector2(NodeWidth, NodeHeight),
                    TooltipText = GetTooltip(gm, node),
                };
                btn.AddThemeFontSizeOverride("font_size", FontSize);

                bool accessible = IsNodeAccessible(node);
                bool isCurrent = gm.CurrentMapNodeId == node.NodeId;
                ApplyStyle(btn, node, accessible, isCurrent, gm.VisitedNodeIds.Contains(node.NodeId));
                int capturedLayer = node.LayerIndex;
                int capturedIndex = node.LayerOrder;
                btn.Pressed += () => onNodePressed?.Invoke(capturedLayer, capturedIndex);
                parent.AddChild(btn);
            }
        }
    }

    public static Vector2 GetNodePosition(MapNodeDefinition node, float offsetX, float offsetY)
    {
        float totalHeight = (node == null ? 1 : 1) * 0f;
        var graph = GameManager.Instance?.MapGraph;
        if (graph != null && node != null)
            totalHeight = (graph.Layers[node.LayerIndex].Count - 1) * NodeSpacingV;
        float startY = CenterY - totalHeight / 2f + offsetY;
        return new Vector2(offsetX + node.LayerIndex * LayerSpacing,
            startY + node.StableOrder * NodeSpacingV);
    }

    public static Vector2 GetNodeCenter(MapNodeDefinition node, float offsetX, float offsetY)
    {
        return GetNodePosition(node, offsetX, offsetY) + new Vector2(NodeWidth / 2f, NodeHeight / 2f);
    }

    private static string GetTooltip(GameManager gm, MapNodeDefinition node)
    {
        if (node.NodeType == MapGraphNodeType.Start)
            return "当前起点";
        if (!gm.DaoMarkSelected)
            return "需先固化道痕";
        bool completedServiceNode = gm.ActiveNodeResultSubmitted && gm.ActiveNode != null &&
            (gm.CurrentState == PlayerState.灵脉中 || gm.CurrentState == PlayerState.商店中 ||
             gm.CurrentState == PlayerState.事件中);
        bool activeServiceRoute = gm.ActiveNode != null && !gm.ActiveNodeResultSubmitted &&
            (gm.CurrentState == PlayerState.灵脉中 || gm.CurrentState == PlayerState.商店中);
        if (gm.CurrentState != PlayerState.空闲 && gm.CurrentState != PlayerState.战斗胜利结算 &&
            !completedServiceNode && !activeServiceRoute)
            return "当前节点只读";
        return IsNodeAccessible(node) ? "点击进入节点" : "当前不可达";
    }

    private static void ApplyStyle(Button btn, MapNodeDefinition node, bool accessible,
        bool current, bool visited)
    {
        if (current)
        {
            btn.Disabled = true;
            btn.Modulate = new Color(1f, 1f, 0.4f, 1f);
            btn.Text += " ◄";
            return;
        }

        btn.Disabled = !accessible;
        if (visited || !accessible)
            btn.Modulate = new Color(0.35f, 0.35f, 0.35f, 1f);
        else if (node.NodeType == MapGraphNodeType.Boss)
            btn.Modulate = new Color(0.85f, 0.2f, 0.1f, 1f);
        else if (node.NodeType == MapGraphNodeType.Lingmai)
            btn.Modulate = new Color(0.3f, 0.7f, 0.4f, 1f);
        else if (node.NodeType == MapGraphNodeType.Shop)
            btn.Modulate = new Color(0.75f, 0.6f, 0.25f, 1f);
        else if (node.NodeType == MapGraphNodeType.Event)
            btn.Modulate = new Color(0.55f, 0.45f, 0.8f, 1f);
        else
            btn.Modulate = new Color(0.9f, 0.5f, 0.3f, 1f);
    }

    private static void DrawConnectionLine(Control parent, Vector2 from, Vector2 to)
    {
        Vector2 start = from + new Vector2(NodeWidth / 2f, 0);
        Vector2 end = to - new Vector2(NodeWidth / 2f, 0);
        float length = (end - start).Length();
        var line = new ColorRect
        {
            Color = new Color(0.4f, 0.35f, 0.25f, 0.5f),
            Position = start,
            Size = new Vector2(length, 2),
            PivotOffset = new Vector2(0, 1.5f),
            RotationDegrees = Mathf.RadToDeg(Mathf.Atan2(end.Y - start.Y, end.X - start.X)),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(line);
    }
}
