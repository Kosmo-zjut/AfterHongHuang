using System;
using System.Collections.Generic;
using Godot;

public enum HandCardVisualState
{
    Rest,
    Hover,
    Selected,
    Held,
}

public readonly record struct HandCardLayout(
    int Index,
    int Count,
    Vector2 Center,
    Vector2 Position,
    Vector2 Size,
    float RotationDegrees,
    Vector2 Scale,
    int ZIndex,
    Vector2 ArrowOrigin,
    Rect2 Rect);

/// <summary>
/// Pure hand layout calculator. It has no gameplay dependency, so card counts can be tested without a battle.
/// </summary>
public static class HandLayoutCalculator
{
    public static HandCardLayout CalculateCardLayout(
        int handCount,
        int index,
        HandCardVisualState state,
        HandLayoutProfile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (handCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(handCount), "Hand count must be positive.");
        if (index < 0 || index >= handCount)
            throw new ArgumentOutOfRangeException(nameof(index), "Card index must be inside hand count.");

        Vector2 restCenter = CalculateRestCenter(handCount, index, profile);
        float restRotation = CalculateRestRotation(handCount, index, profile);
        int restZ = profile.BaseZIndex + index * profile.RestZIndexStep;

        Vector2 center = restCenter;
        float rotation = restRotation;
        Vector2 scale = Vector2.One;
        int zIndex = restZ;

        switch (state)
        {
            case HandCardVisualState.Hover:
                center += new Vector2(0f, profile.HoverLiftY);
                rotation = 0f;
                scale = Vector2.One * profile.HoverScale;
                zIndex = profile.HoverZIndex;
                break;
            case HandCardVisualState.Selected:
                center += new Vector2(0f, profile.SelectedLiftY);
                rotation = 0f;
                scale = Vector2.One * profile.SelectedScale;
                zIndex = profile.SelectedZIndex;
                break;
            case HandCardVisualState.Held:
                rotation = 0f;
                scale = Vector2.One * profile.SelectedScale;
                zIndex = profile.HeldZIndex;
                break;
        }

        Vector2 position = center - profile.CardSize * 0.5f;
        Rect2 rect = new(position, profile.CardSize);
        Vector2 arrowOrigin = center + profile.ArrowStartOffset;
        return new HandCardLayout(index, handCount, center, position, profile.CardSize, rotation, scale, zIndex, arrowOrigin, rect);
    }

    public static IReadOnlyList<HandCardLayout> CalculateRestLayouts(int handCount, HandLayoutProfile profile)
    {
        var layouts = new List<HandCardLayout>(Mathf.Max(handCount, 0));
        for (int i = 0; i < handCount; i++)
            layouts.Add(CalculateCardLayout(handCount, i, HandCardVisualState.Rest, profile));

        return layouts;
    }

    public static float GetTotalAngle(int handCount, HandLayoutProfile profile)
    {
        if (handCount <= 1) return profile.Angle1;
        if (handCount == 2) return profile.Angle2;
        if (handCount == 3) return profile.Angle3;
        if (handCount <= 5) return Mathf.Lerp(profile.Angle3, profile.Angle5, (handCount - 3f) / 2f);
        if (handCount <= 7) return Mathf.Lerp(profile.Angle5, profile.Angle7, (handCount - 5f) / 2f);
        if (handCount <= 10) return Mathf.Lerp(profile.Angle7, profile.Angle10, (handCount - 7f) / 3f);

        float extended = profile.Angle10 + (handCount - 10f) * 4f;
        return Mathf.Min(profile.MaxAngle, extended);
    }

    public static Vector2 GetArrowControlPoint(Vector2 start, Vector2 end, HandLayoutProfile profile)
    {
        Vector2 mid = (start + end) * 0.5f;
        return new Vector2(mid.X, Mathf.Min(start.Y, end.Y) - profile.ArrowArcHeight);
    }

    private static Vector2 CalculateRestCenter(int handCount, int index, HandLayoutProfile profile)
    {
        if (handCount <= 1)
            return profile.HandAnchor;

        float centerIndex = (handCount - 1) / 2f;
        float offset = index - centerIndex;
        float normalized = centerIndex <= 0f ? 0f : offset / centerIndex;
        float angleDeg = normalized * GetTotalAngle(handCount, profile) * 0.5f;
        Vector2 fanRadius = profile.HandAnchor - profile.VirtualFanCenter;
        Vector2 radialCenter = profile.VirtualFanCenter + fanRadius.Rotated(Mathf.DegToRad(angleDeg));
        float rawGap = EstimateAdjacentGap(handCount, fanRadius.Length(), GetTotalAngle(handCount, profile));
        float gapScale = rawGap <= 0f
            ? 1f
            : Mathf.Clamp(rawGap, profile.MinCardGap, profile.MaxCardGap) / rawGap;

        // X follows the virtual-circle fan, while Y uses the UX-provided edge drop so designers can tune arc emphasis directly.
        float x = profile.HandAnchor.X + (radialCenter.X - profile.HandAnchor.X) * gapScale;
        float dropY = profile.EdgeDropY * Mathf.Abs(normalized);
        return new Vector2(x, profile.HandAnchor.Y + dropY);
    }

    private static float CalculateRestRotation(int handCount, int index, HandLayoutProfile profile)
    {
        if (handCount <= 1)
            return 0f;

        float centerIndex = (handCount - 1) / 2f;
        float offset = index - centerIndex;
        float normalized = centerIndex <= 0f ? 0f : offset / centerIndex;
        float angleDeg = normalized * GetTotalAngle(handCount, profile) * 0.5f;
        return Mathf.Clamp(angleDeg, -profile.MaxRotation, profile.MaxRotation);
    }

    private static float EstimateAdjacentGap(int handCount, float radius, float totalAngleDeg)
    {
        if (handCount <= 1 || totalAngleDeg <= 0f)
            return 0f;

        float stepRad = Mathf.DegToRad(totalAngleDeg / (handCount - 1));
        return 2f * radius * Mathf.Sin(stepRad * 0.5f);
    }
}
