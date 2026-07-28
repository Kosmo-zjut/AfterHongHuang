using System;
using Godot;

/// <summary>
/// Owns one node page's shared map overlay for the lifetime of that page. UI controllers only
/// request a toggle; GameManager remains the single owner of map interactivity and node routing.
/// </summary>
public sealed class NodePageNavigationCoordinator : IDisposable
{
    private readonly Node _pageHost;
    private readonly GameManager _gameManager;
    private readonly Action<string> _reportError;
    private readonly Action _onRouteSucceeded;
    private MapOverlayController _mapOverlay;

    public NodePageNavigationCoordinator(Node pageHost, GameManager gameManager, Action<string> reportError,
        Action onRouteSucceeded = null)
    {
        _pageHost = pageHost ?? throw new ArgumentNullException(nameof(pageHost));
        _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
        _reportError = reportError;
        _onRouteSucceeded = onRouteSucceeded;
    }

    /// <summary>
    /// Opens or closes the current page's map. Closing only frees the overlay, so the exact node
    /// page and optional reward child remain alive. Only the overlay node callback may request a route.
    /// </summary>
    public bool TryToggleMap(out string error)
    {
        error = "";
        if (IsOverlayAlive())
        {
            _mapOverlay.Close();
            return true;
        }

        if (!_gameManager.TryGetNodePageMapInteractivity(out bool interactive, out error))
        {
            Report(error);
            return false;
        }

        _mapOverlay = MapOverlayController.Open(_pageHost, interactive,
            interactive ? OnMapNodeSelected : null, OnMapClosed);
        if (_mapOverlay != null)
            return true;

        error = "地图 overlay 创建失败。";
        Report(error);
        return false;
    }

    /// <summary>Ensures scene teardown unregisters a still-open overlay without changing RunState.</summary>
    public void Dispose()
    {
        if (!IsOverlayAlive())
            return;

        _mapOverlay.CloseImmediately();
        _mapOverlay = null;
    }

    private void OnMapNodeSelected(MapNodeDefinition target)
    {
        if (!_gameManager.TryRouteFromNodePage(target, out var error))
        {
            Report(error);
            return;
        }

        // Route success is the only path that removes this page's overlay. A rejected target leaves
        // both the map and underlying node page intact for another legal selection or manual close.
        _onRouteSucceeded?.Invoke();
        if (IsOverlayAlive())
            _mapOverlay.CloseImmediately();
        _mapOverlay = null;
    }

    private void OnMapClosed()
    {
        _mapOverlay = null;
    }

    private bool IsOverlayAlive() => _mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay);

    private void Report(string error)
    {
        GD.PrintErr($"[节点页导航] {error}");
        _reportError?.Invoke(error);
    }
}
