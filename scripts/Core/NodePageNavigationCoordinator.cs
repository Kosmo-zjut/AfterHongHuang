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
        if (!TryPrepareMapToggle(IsOverlayAlive, () => _mapOverlay.Close(), out bool closedExisting,
                out error))
        {
            Report(error);
            return false;
        }

        if (closedExisting)
            return true;

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

    /// <summary>
    /// Runs the common ordering for a map toggle: close GlobalSettings first, then close an
    /// existing map or leave the caller to open one. The delegates keep the ordering proof
    /// testable without constructing a second production navigation implementation.
    /// </summary>
    private static bool TryPrepareMapToggle(Func<bool> isOverlayAlive, Action closeOverlay,
        out bool closedExisting, out string error)
    {
        closedExisting = false;
        error = "";
        if (isOverlayAlive == null || closeOverlay == null)
        {
            error = "地图 toggle 缺少有效的 overlay 生命周期操作。";
            return false;
        }

        if (!OverlayCoordinator.TryPrepareMap(out error))
            return false;

        if (isOverlayAlive())
        {
            closeOverlay();
            closedExisting = true;
        }

        return true;
    }

    /// <summary>供同程序集自检复用生产 toggle 顺序，不改变真实页面路由。</summary>
    internal static bool TryPrepareMapToggleForSelfCheck(Func<bool> isOverlayAlive, Action closeOverlay,
        out bool closedExisting, out string error) =>
        TryPrepareMapToggle(isOverlayAlive, closeOverlay, out closedExisting, out error);

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
