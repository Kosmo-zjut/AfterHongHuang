using Godot;

/// <summary>战斗页独立 Debug 工具。Release 构建不会实例化，也不保留胜利旁路调用。</summary>
public partial class BattleDebugToolsController : Panel
{
#if DEBUG
    public System.Action OnInstantVictory;
#endif

    public override void _Ready()
    {
#if DEBUG
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        var button = GetNodeOrNull<Button>("InstantVictoryButton");
        if (button == null)
        {
            GD.PrintErr("[BattleDebug] Debug 工具场景缺少一键胜利按钮。");
            return;
        }
        button.Pressed += () => OnInstantVictory?.Invoke();
#else
        QueueFree();
#endif
    }
}
