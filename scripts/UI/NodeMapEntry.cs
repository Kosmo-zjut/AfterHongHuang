using System;
using Godot;

/// <summary>
/// Reusable external map entry for node pages. Layout and theme live in its scene;
/// this class only binds the page's existing map-overlay toggle and reflects its state.
/// </summary>
public partial class NodeMapEntry : Button
{
    private const string ScenePath = "res://scenes/UI/NodeMapEntry.tscn";

    private Action _toggleMapOverlay;
    private Func<bool> _isMapOverlayOpen;
    private bool? _lastOverlayOpen;

    /// <summary>
    /// Instantiates the shared scene. It intentionally accepts only overlay operations,
    /// so this entry cannot submit node results, alter rewards, or route scenes itself.
    /// </summary>
    public static NodeMapEntry Add(Control parent, Action toggleMapOverlay, Func<bool> isMapOverlayOpen)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(toggleMapOverlay);
        ArgumentNullException.ThrowIfNull(isMapOverlayOpen);

        var packedScene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (packedScene == null)
            throw new InvalidOperationException($"无法加载共享节点地图入口场景：{ScenePath}");

        var entry = packedScene.Instantiate<NodeMapEntry>();
        entry.Configure(toggleMapOverlay, isMapOverlayOpen);
        parent.AddChild(entry);
        return entry;
    }

    /// <summary>Binds only the host page's existing overlay toggle and visible-state query.</summary>
    public void Configure(Action toggleMapOverlay, Func<bool> isMapOverlayOpen)
    {
        _toggleMapOverlay = toggleMapOverlay ?? throw new ArgumentNullException(nameof(toggleMapOverlay));
        _isMapOverlayOpen = isMapOverlayOpen ?? throw new ArgumentNullException(nameof(isMapOverlayOpen));
        ZIndex = OverlayCoordinator.GlobalOperationZIndex;
        ZAsRelative = false;
        Pressed += OnPressed;
        RefreshTooltip();
    }

    public override void _Process(double delta)
    {
        RefreshTooltip();
    }

    private void OnPressed()
    {
        _toggleMapOverlay();
        CallDeferred(nameof(RefreshTooltip));
    }

    private void RefreshTooltip()
    {
        if (_isMapOverlayOpen == null)
            return;

        bool isOpen = _isMapOverlayOpen();
        if (_lastOverlayOpen == isOpen)
            return;

        _lastOverlayOpen = isOpen;
        TooltipText = isOpen ? "收起地图卷轴" : "打开地图卷轴";
    }
}
