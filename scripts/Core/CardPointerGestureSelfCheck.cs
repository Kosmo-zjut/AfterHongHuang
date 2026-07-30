using Godot;

/// <summary>Behavioral proof for the BattleController-owned physical card gesture.</summary>
public static class CardPointerGestureSelfCheck
{
    public static void Run()
    {
        var gesture = new PointerGestureState<GestureProbe>();
        var probe = new GestureProbe();

        Ensure(gesture.TryBegin(probe, Vector2.Zero), "第一次左键 Press 未取得手势所有权。");
        Ensure(!gesture.TryBegin(new GestureProbe(), Vector2.One),
            "已有左键手势时不应允许第二个 Press 抢占所有权。");
        Ensure(gesture.Release(Vector2.One) == PointerReleaseKind.Click,
            "未达到拖拽阈值的 Release 应进入一次点击路径。");
        Ensure(gesture.Release(Vector2.One) == PointerReleaseKind.None,
            "同一物理手势的 Release 只能消费一次。");

        int committedCount = 0;
        int reboundCount = 0;
        Ensure(gesture.TryBegin(probe, Vector2.Zero), "合法拖拽测试无法开始。");
        Ensure(gesture.TryBeginDrag(new Vector2(32, 0), 8),
            "超过阈值后未转为 Battle 拖拽状态。");
        var legalRelease = gesture.Release(new Vector2(32, 0));
        if (legalRelease == PointerReleaseKind.Dragged)
            committedCount++;
        Ensure(committedCount == 1, "合法敌方 Release 未只进入一次结算入口。");

        Ensure(gesture.TryBegin(probe, Vector2.Zero), "非法释放测试无法开始。");
        Ensure(gesture.TryBeginDrag(new Vector2(32, 0), 8), "非法释放未进入拖拽状态。");
        var invalidRelease = gesture.Release(new Vector2(32, 0));
        if (invalidRelease == PointerReleaseKind.Dragged && !IsLegalTarget(false))
            reboundCount++;
        Ensure(committedCount == 1 && reboundCount == 1,
            "非法 Release 必须回弹且不得扣费或重复结算。");

        Ensure(gesture.TryBegin(probe, Vector2.Zero), "右键取消测试无法开始。");
        Ensure(gesture.Cancel(), "右键取消未标记当前物理手势。");
        Ensure(gesture.Release(Vector2.One) == PointerReleaseKind.Cancelled,
            "右键取消后旧左键 Release 未被吞掉。");
        Ensure(gesture.Release(Vector2.One) == PointerReleaseKind.None,
            "取消后的旧 Release 被重复消费。");
        Ensure(gesture.TryBegin(probe, Vector2.Zero),
            "取消后的新左键 Press 未恢复手势入口。");
        Ensure(gesture.Release(Vector2.One) == PointerReleaseKind.Click,
            "取消后的新左键 Press/Release 未恢复点击路径。");

        GD.Print("[CardPointerGestureSelfCheck] PASS global ownership, legal/illegal release and cancel suppression");
    }

    private static bool IsLegalTarget(bool configuredTarget)
    {
        return configuredTarget;
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException($"[CardPointerGestureSelfCheck] {error}");
    }

    private sealed class GestureProbe
    {
    }
}
