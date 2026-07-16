using Godot;

/// <summary>
/// Battle hand layout tuning profile. Defaults follow UI/UX v2.2.0 for a 1920x1080 viewport.
/// </summary>
[GlobalClass]
public partial class HandLayoutProfile : Resource
{
    [Export] public Vector2 CardSize { get; set; } = new(180f, 250f);
    [Export] public Rect2 HandAreaRect { get; set; } = new(new Vector2(300f, 760f), new Vector2(1320f, 280f));
    [Export] public Vector2 HandAnchor { get; set; } = new(960f, 910f);
    [Export] public Vector2 VirtualFanCenter { get; set; } = new(960f, 1680f);

    [ExportGroup("Angle Curve")]
    [Export] public float Angle1 { get; set; } = 0f;
    [Export] public float Angle2 { get; set; } = 9.5f;
    [Export] public float Angle3 { get; set; } = 16f;
    [Export] public float Angle5 { get; set; } = 28f;
    [Export] public float Angle7 { get; set; } = 40f;
    [Export] public float Angle10 { get; set; } = 54f;
    [Export] public float MaxAngle { get; set; } = 65f;
    [Export] public float MaxRotation { get; set; } = 16f;
    [Export] public float EdgeDropY { get; set; } = 74f;
    [Export] public float MinCardGap { get; set; } = 78f;
    [Export] public float MaxCardGap { get; set; } = 138f;

    [ExportGroup("Interaction")]
    [Export] public float ReflowDuration { get; set; } = 0.12f;
    [Export] public float HoverScale { get; set; } = 1.18f;
    [Export] public float HoverLiftY { get; set; } = -105f;
    [Export] public float SelectedScale { get; set; } = 1.20f;
    [Export] public float SelectedLiftY { get; set; } = -120f;
    [Export] public Vector2 HeldFollowOffset { get; set; } = new(0f, -90f);
    [Export] public float HandExitThresholdY { get; set; } = 740f;
    [Export] public float DragThreshold { get; set; } = 12f;

    [ExportGroup("Arrow")]
    [Export] public Vector2 ArrowStartOffset { get; set; } = new(0f, -110f);
    [Export] public float ArrowArcHeight { get; set; } = 260f;
    [Export] public float ArrowTargetPadding { get; set; } = 36f;

    [ExportGroup("Layering")]
    [Export] public int BaseZIndex { get; set; } = 5;
    [Export] public int RestZIndexStep { get; set; } = 2;
    [Export] public int HoverZIndex { get; set; } = 100;
    [Export] public int SelectedZIndex { get; set; } = 120;
    [Export] public int HeldZIndex { get; set; } = 130;

    [ExportGroup("Debug")]
    [Export] public bool DebugOverlayEnabled { get; set; } = false;
}
