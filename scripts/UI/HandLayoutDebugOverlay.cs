using System.Collections.Generic;
using Godot;

/// <summary>
/// Debug-only overlay for tuning hand layout points. It renders nothing while disabled.
/// </summary>
public partial class HandLayoutDebugOverlay : Control
{
    private HandLayoutProfile _profile;
    private IReadOnlyList<HandCardLayout> _layouts = new List<HandCardLayout>();
    private bool _enabled;
    private Vector2? _hoverPoint;
    private Vector2? _heldPoint;
    private Vector2? _arrowStart;
    private Vector2? _arrowControl;
    private Vector2? _arrowTarget;

    public void SetSnapshot(
        HandLayoutProfile profile,
        IReadOnlyList<HandCardLayout> layouts,
        bool enabled,
        Vector2? hoverPoint = null,
        Vector2? heldPoint = null,
        Vector2? arrowStart = null,
        Vector2? arrowControl = null,
        Vector2? arrowTarget = null)
    {
        _profile = profile;
        _layouts = layouts ?? new List<HandCardLayout>();
        _enabled = enabled;
        _hoverPoint = hoverPoint;
        _heldPoint = heldPoint;
        _arrowStart = arrowStart;
        _arrowControl = arrowControl;
        _arrowTarget = arrowTarget;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_enabled || _profile == null) return;

        DrawRect(_profile.HandAreaRect, new Color(0.2f, 0.9f, 1f, 0.16f), true);
        DrawRect(_profile.HandAreaRect, new Color(0.2f, 0.9f, 1f, 0.78f), false, 2f);
        DrawMarker(_profile.VirtualFanCenter, new Color(0f, 1f, 1f, 0.9f), "FanCenter", true);
        DrawLine(new Vector2(_profile.HandAnchor.X, _profile.HandAreaRect.Position.Y - 40f),
            new Vector2(_profile.HandAnchor.X, _profile.HandAreaRect.End.Y + 40f),
            new Color(0.2f, 0.9f, 1f, 0.45f), 1f);
        DrawMarker(_profile.HandAnchor, new Color(0.1f, 0.75f, 1f, 0.9f), "Anchor");

        foreach (var layout in _layouts)
        {
            DrawCircle(layout.Center, 5f, new Color(0.2f, 1f, 0.35f, 0.95f));
            DrawString(ThemeDB.FallbackFont, layout.Center + new Vector2(8f, -8f), layout.Index.ToString(),
                HorizontalAlignment.Left, -1f, 14, Colors.White);
            DrawRect(layout.Rect, new Color(0.2f, 1f, 0.35f, 0.65f), false, 1.5f);
        }

        DrawLine(new Vector2(0f, _profile.HandExitThresholdY), new Vector2(1920f, _profile.HandExitThresholdY),
            new Color(1f, 0.1f, 0.1f, 0.55f), 2f);

        if (_hoverPoint.HasValue)
            DrawMarker(_hoverPoint.Value, new Color(1f, 0.95f, 0.15f, 0.95f), "Hover");
        if (_heldPoint.HasValue)
            DrawMarker(_heldPoint.Value, new Color(1f, 0.55f, 0.1f, 0.95f), "Held");
        if (_arrowStart.HasValue)
            DrawMarker(_arrowStart.Value, new Color(1f, 0.15f, 0.1f, 0.95f), "ArrowStart");
        if (_arrowControl.HasValue)
        {
            DrawMarker(_arrowControl.Value, new Color(0.75f, 0.25f, 1f, 0.95f), "ArrowCtrl");
            if (_arrowStart.HasValue)
                DrawDashedLine(_arrowStart.Value, _arrowControl.Value, new Color(0.75f, 0.25f, 1f, 0.45f), 1.5f);
            if (_arrowTarget.HasValue)
                DrawDashedLine(_arrowControl.Value, _arrowTarget.Value, new Color(0.75f, 0.25f, 1f, 0.45f), 1.5f);
        }
        if (_arrowTarget.HasValue)
            DrawMarker(_arrowTarget.Value, new Color(1f, 0.15f, 0.1f, 0.95f), "ArrowTarget");
    }

    private void DrawMarker(Vector2 point, Color color, string label, bool cross = false)
    {
        if (cross)
        {
            DrawLine(point + new Vector2(-12f, 0f), point + new Vector2(12f, 0f), color, 2f);
            DrawLine(point + new Vector2(0f, -12f), point + new Vector2(0f, 12f), color, 2f);
        }
        else
        {
            DrawCircle(point, 6f, color);
        }

        DrawString(ThemeDB.FallbackFont, point + new Vector2(10f, -10f), label,
            HorizontalAlignment.Left, -1f, 14, Colors.White);
    }
}
