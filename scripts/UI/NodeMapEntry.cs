using System;
using Godot;

/// <summary>
/// Reusable local continue entry for node pages. Layout and theme live in its scene;
/// maps and CardReward overlays intentionally cover this control while TopBar remains global.
/// </summary>
public partial class NodeMapEntry : Button
{
    private const string ScenePath = "res://scenes/UI/NodeMapEntry.tscn";

    private Action _toggleMapOverlay;

    /// <summary>
    /// Instantiates the shared scene. It intentionally accepts only overlay operations,
    /// so this entry cannot submit node results, alter rewards, or route scenes itself.
    /// </summary>
    public static NodeMapEntry Add(Control parent, Action toggleMapOverlay)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(toggleMapOverlay);

        var packedScene = ResourceLoader.Load<PackedScene>(ScenePath);
        if (packedScene == null)
            throw new InvalidOperationException($"无法加载共享节点地图入口场景：{ScenePath}");

        var entry = packedScene.Instantiate<NodeMapEntry>();
        entry.Configure(toggleMapOverlay);
        parent.AddChild(entry);
        return entry;
    }

    /// <summary>Binds only the host page's existing overlay toggle.</summary>
    public void Configure(Action toggleMapOverlay)
    {
        _toggleMapOverlay = toggleMapOverlay ?? throw new ArgumentNullException(nameof(toggleMapOverlay));
        Pressed += OnPressed;
    }

    private void OnPressed()
    {
        _toggleMapOverlay();
    }
}
