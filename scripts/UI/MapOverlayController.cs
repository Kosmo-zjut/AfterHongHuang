using Godot;
using System;

/// <summary>
/// 节点页共享地图 overlay。地图数据和可达性仍由 MapRenderer/RunState 提供，
/// 本控件只负责固定左边界的揭示动画、横向视口和输入隔离。
/// </summary>
public partial class MapOverlayController : Control
{
    // Frozen fully-open rect: Rect2(64, 104, 1792, 872). It leaves substantially more
    // vertical than horizontal breathing room while keeping the left-to-right scroll reveal.
    private const float PanelLeft = 64f;
    private const float PanelTop = 104f;
    private const float PanelWidth = 1792f;
    private const float PanelHeight = 872f;
    private const float InputBlockerTop = 44f;
    private const float InputBlockerHeight = 1036f;
    private const float ViewTop = 58f;
    private const float ViewHeight = 760f;
    private const float DragThreshold = 8f;
    private const float WheelStep = 160f;
    // This Control owns input below TopBar without becoming a second opaque page. The visible
    // map is only the scroll/panel region; the current node page remains visible underneath.
    private static readonly Color ContentInputShieldColor = new(0f, 0f, 0f, 0f);

    private Panel _panel;
    private Control _inputBlocker;
    private ScrollContainer _viewport;
    private Control _mapContent;
    private bool _interactive;
    private bool _animating;
    private bool _closing;
    private bool _skipMapContentForSelfCheck;
    private bool _pointerDown;
    private bool _dragging;
    private bool _suppressNextNodeClick;
    private Vector2 _pointerStart;
    private float _scrollStart;
    private Action<MapNodeDefinition> _onNodePressed;
    private Action _onClosed;

    /// <summary>创建并打开一个共享地图 overlay。</summary>
    public static MapOverlayController Open(Node parent, bool interactive,
        Action<MapNodeDefinition> onNodePressed, Action onClosed = null, bool immediate = false)
    {
        if (parent == null)
        {
            GD.PrintErr("[地图Overlay] 缺少父节点，拒绝创建地图 overlay。");
            return null;
        }
        if (!OverlayCoordinator.TryPrepareMap(out var coordinatorError))
        {
            GD.PrintErr($"[地图Overlay] {coordinatorError}");
            return null;
        }

        var overlay = new MapOverlayController();
        overlay._interactive = interactive;
        overlay._onNodePressed = onNodePressed;
        overlay._onClosed = onClosed;
        parent.AddChild(overlay);
        OverlayCoordinator.RegisterMap(overlay);
        overlay.Build(immediate);
        return overlay;
    }

    /// <summary>
    /// Creates the real visual/input hierarchy without resolving RunState data. It exists only
    /// for the startup self-check, which runs before a playable run and MapGraph are available.
    /// </summary>
    internal static MapOverlayController OpenForSelfCheck(Control parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!OverlayCoordinator.TryPrepareMap(out var coordinatorError))
            throw new InvalidOperationException(coordinatorError);

        var overlay = new MapOverlayController
        {
            _interactive = false,
            _skipMapContentForSelfCheck = true,
        };
        parent.AddChild(overlay);
        OverlayCoordinator.RegisterMap(overlay);
        overlay.Build(immediate: true);
        return overlay;
    }

    /// <summary>关闭 overlay；关闭只释放地图层，不触碰底层节点页或 RunState。</summary>
    public void Close()
    {
        if (_closing || _panel == null)
            return;
        _closing = true;
        AnimateClose();
    }

    /// <summary>Used by mutual-exclusion changes and scene teardown; it does not alter the node page.</summary>
    public void CloseImmediately()
    {
        if (_closing)
            return;
        _closing = true;
        OverlayCoordinator.Unregister(this);
        QueueFree();
        _onClosed?.Invoke();
    }

    private void Build(bool immediate)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        // 根节点覆盖全屏但不占用 TopBar 输入。透明输入层只阻断内容区交互，
        // 不能把 Battle/Lingmai/Shop/奖励页面伪装成一张新的不透明底页。
        MouseFilter = MouseFilterEnum.Ignore;
        // The map deliberately sits above CardReward but stays below the TopBar input region.
        // CardReward is preserved as a sibling and becomes visible again when this cover closes.
        ZIndex = OverlayCoordinator.MapCoverZIndex;

        _inputBlocker = new ColorRect
        {
            Name = "ContentCover",
            Position = new Vector2(0, InputBlockerTop),
            Size = new Vector2(1920, InputBlockerHeight),
            Color = ContentInputShieldColor,
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(_inputBlocker);

        _panel = new Panel
        {
            Position = new Vector2(PanelLeft, PanelTop),
            Size = new Vector2(immediate ? PanelWidth : 0f, PanelHeight),
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(_panel);

        var background = new ColorRect
        {
            Color = new Color(0.12f, 0.09f, 0.05f, 0.97f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        _panel.AddChild(background);

        var title = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(0, 10),
            Size = new Vector2(PanelWidth, 40),
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        if (ActDefinition.TryGet(GameManager.Instance?.RunState?.ActId, out var act, out var actError))
            title.Text = act.DisplayTitle;
        else
        {
            title.Text = "地图上下文不可用";
            GD.PrintErr($"[地图Overlay] 页面上下文绑定失败：{actError}");
        }
        _panel.AddChild(title);

        var closeButton = new Button
        {
            Text = "收 卷",
            Position = new Vector2(PanelWidth - 140, 12),
            Size = new Vector2(100, 32),
        };
        closeButton.AddThemeFontSizeOverride("font_size", 13);
        closeButton.Pressed += Close;
        _panel.AddChild(closeButton);

        _viewport = new ScrollContainer
        {
            Position = new Vector2(0, ViewTop),
            Size = new Vector2(PanelWidth, ViewHeight),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = MouseFilterEnum.Stop,
        };
        _panel.AddChild(_viewport);

        var graph = GameManager.Instance?.MapGraph;
        if (_skipMapContentForSelfCheck)
        {
            // The startup self-check validates coverage ownership before any run creates a graph.
        }
        else if (graph == null)
        {
            ShowError("RunState 缺少生产 MapGraph，无法展示地图。");
        }
        else if (!ValidateGraphDisplayNames(graph, out var graphError))
        {
            ShowError(graphError);
        }
        else
        {
            float contentWidth = Mathf.Max(PanelWidth,
                MapRenderer.MarginLeft + Mathf.Max(0, graph.Layers.Count - 1) * MapRenderer.LayerSpacing +
                MapRenderer.NodeWidth + MapRenderer.MarginLeft);
            _mapContent = new Control
            {
                CustomMinimumSize = new Vector2(contentWidth, ViewHeight),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _viewport.AddChild(_mapContent);
            MapRenderer.BuildInteractiveMap(_mapContent, MapRenderer.MarginLeft, 0f,
                OnMapNodePressed);
            // 等 ScrollContainer 完成内容测量后居中整个地图内容；这只影响视口布局，
            // 不参与 MapScene 导航，也不依赖任何 deferred 入口标记。
            CenterMapContentAfterLayout();
        }

        var legend = new Label
        {
            Text = "● 当前位置    ● 可探索    ○ 已访问    ■ Boss    ◆ 道韵    ▣ 灵脉",
            Position = new Vector2(40, PanelHeight - 38),
            Size = new Vector2(1100, 24),
        };
        legend.AddThemeFontSizeOverride("font_size", 11);
        legend.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        _panel.AddChild(legend);

        if (!immediate)
            AnimateOpen();
    }

    private static bool ValidateGraphDisplayNames(MapGraph graph, out string error)
    {
        foreach (var node in graph.AllNodes())
        {
            if (node == null || string.IsNullOrWhiteSpace(node.DisplayName))
            {
                error = "MapGraph 节点缺少显示名，拒绝展示不完整地图。";
                GD.PrintErr($"[地图Overlay] {error}");
                return false;
            }
        }

        error = "";
        return true;
    }

    private async void CenterMapContentAfterLayout()
    {
        // BuildInteractiveMap 后 ScrollContainer 还可能尚未完成最小尺寸测量；
        // 等一帧再读取 scrollbar max，避免长地图首次打开无法居中。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInstanceValid(this) || _closing)
            return;

        if (_viewport == null || _mapContent == null)
        {
            GD.PrintErr("[地图Overlay] 缺少地图视口内容，无法执行居中定位。");
            return;
        }

        // 当前 RunState 节点可能位于边缘；以整张图为基准可避免卷轴打开时内容贴右/贴左。
        SetHorizontalScroll((_mapContent.CustomMinimumSize.X - PanelWidth) / 2f);
    }

    private void ShowError(string error)
    {
        GD.PrintErr($"[地图Overlay] {error}");
        var label = new Label
        {
            Text = error,
            Position = new Vector2(240, 360),
            Size = new Vector2(1120, 140),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.35f, 0.25f));
        _panel.AddChild(label);
    }

    private void OnMapNodePressed(int layer, int nodeIndex)
    {
        if (_suppressNextNodeClick)
        {
            _suppressNextNodeClick = false;
            return;
        }

        var graph = GameManager.Instance?.MapGraph;
        if (!_interactive || graph == null || layer < 0 || layer >= graph.Layers.Count ||
            nodeIndex < 0 || nodeIndex >= graph.Layers[layer].Count)
        {
            if (!_interactive)
                GD.Print("[地图Overlay] 当前页面地图只读，忽略节点点击。");
            else
                GD.PrintErr("[地图Overlay] 节点回调参数无效，拒绝进入节点。");
            return;
        }

        var node = graph.Layers[layer][nodeIndex];
        if (!MapRenderer.IsNodeAccessible(node))
        {
            GD.PrintErr($"[地图Overlay] 拒绝进入不可达节点：{node.NodeId}");
            return;
        }

        _onNodePressed?.Invoke(node);
    }

    public override void _Input(InputEvent @event)
    {
        if (_closing || _viewport == null)
            return;

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                if (_viewport.GetGlobalRect().HasPoint(mouseButton.Position))
                {
                    float delta = mouseButton.ButtonIndex == MouseButton.WheelUp ? -WheelStep : WheelStep;
                    SetHorizontalScroll(_viewport.GetHScrollBar().Value + delta);
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (mouseButton.ButtonIndex != MouseButton.Left)
                return;

            if (mouseButton.Pressed && _viewport.GetGlobalRect().HasPoint(mouseButton.Position))
            {
                _pointerDown = true;
                _dragging = false;
                _suppressNextNodeClick = false;
                _pointerStart = mouseButton.Position;
                _scrollStart = (float)_viewport.GetHScrollBar().Value;
            }
            else if (!mouseButton.Pressed && _pointerDown)
            {
                if (_dragging)
                    _suppressNextNodeClick = true;
                _pointerDown = false;
                _dragging = false;
            }
        }
        else if (@event is InputEventMouseMotion motion && _pointerDown)
        {
            float distance = motion.Position.DistanceTo(_pointerStart);
            if (!_dragging && distance >= DragThreshold)
                _dragging = true;
            if (_dragging)
            {
                float target = _scrollStart - (motion.Position.X - _pointerStart.X);
                SetHorizontalScroll(target);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// 将地图横向滚动限制在 ScrollContainer 当前可视范围内，避免滚轮/拖拽把内容拖出有效视口。
    /// </summary>
    private void SetHorizontalScroll(double value)
    {
        if (_viewport == null)
            return;

        var scrollBar = _viewport.GetHScrollBar();
        int min = Mathf.RoundToInt((float)scrollBar.MinValue);
        int max = Mathf.RoundToInt((float)scrollBar.MaxValue);
        int target = Mathf.Clamp(Mathf.RoundToInt((float)value), min, max);
        _viewport.SetHScroll(target);
    }

    private async void AnimateOpen()
    {
        _animating = true;
        const float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration && IsInstanceValid(_panel))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
            _panel.Size = new Vector2(PanelWidth * Ease(elapsed / duration), PanelHeight);
        }
        if (IsInstanceValid(_panel))
            _panel.Size = new Vector2(PanelWidth, PanelHeight);
        _animating = false;
    }

    private async void AnimateClose()
    {
        _animating = true;
        const float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration && IsInstanceValid(_panel))
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += (float)GetProcessDeltaTime();
            _panel.Size = new Vector2(PanelWidth * (1f - Ease(elapsed / duration)), PanelHeight);
        }
        QueueFree();
        OverlayCoordinator.Unregister(this);
        _onClosed?.Invoke();
    }

    private static float Ease(float value)
    {
        float t = Mathf.Clamp(value, 0f, 1f);
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }
}
