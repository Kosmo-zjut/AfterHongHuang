using Godot;

/// <summary>
/// Coordinates one shared SettingsDialog instance. The dialog scene owns presentation and input;
/// this helper only owns its cross-page lifetime and submits the optional in-game abandon intent.
/// </summary>
public static class SettingsHelper
{
    private static SettingsDialogController _currentDialog;

    /// <summary>Shows or toggles the one shared settings scene without creating duplicates.</summary>
    public static void Show(Node parent, PackedScene settingsDialogScene)
    {
        if (!TryShow(parent, settingsDialogScene, out var error))
            GD.PrintErr($"[设置] 打开失败：{error}");
    }

    /// <summary>Internal result-bearing entry used by the focused settings self-check.</summary>
    internal static bool TryShow(Node parent, PackedScene settingsDialogScene, out string error)
    {
        error = "";
        if (_currentDialog != null && GodotObject.IsInstanceValid(_currentDialog))
        {
            CloseCurrentDialog();
            return true;
        }
        _currentDialog = null;

        if (parent == null || !GodotObject.IsInstanceValid(parent))
        {
            error = "缺少有效父节点。";
            return false;
        }

        if (settingsDialogScene == null)
        {
            error = "缺少共享设置弹窗 PackedScene。";
            return false;
        }

        SettingsDialogController dialog;
        try
        {
            dialog = settingsDialogScene.Instantiate<SettingsDialogController>();
        }
        catch (System.Exception exception)
        {
            error = $"设置场景实例化失败：{exception.Message}";
            return false;
        }

        if (dialog == null)
        {
            error = "设置场景实例化返回空节点。";
            return false;
        }

        // Exported node references resolve as the scene joins the tree. Attach before validating
        // the editor assembly so SettingsDialogController never observes null child references.
        parent.AddChild(dialog);
        if (!dialog.TryInitialize(out error))
        {
            dialog.QueueFree();
            return false;
        }

        if (!dialog.TrySetInGameMode(parent is not TitleController, out error))
        {
            dialog.QueueFree();
            return false;
        }

        _currentDialog = dialog;
        dialog.CloseRequestedByUser += CloseCurrentDialog;
        dialog.AbandonRunRequested += OnAbandonRunRequested;
        dialog.TreeExited += OnDialogTreeExited;
        if (!OverlayCoordinator.TryPrepareGlobalSettings(dialog, CloseCurrentDialog, out error))
        {
            dialog.CloseRequestedByUser -= CloseCurrentDialog;
            dialog.AbandonRunRequested -= OnAbandonRunRequested;
            dialog.TreeExited -= OnDialogTreeExited;
            _currentDialog = null;
            dialog.QueueFree();
            return false;
        }
        PresentDialog(dialog);
        return true;
    }

    /// <summary>Closes the current dialog and releases its shared overlay registration.</summary>
    public static void CloseCurrentDialog()
    {
        var dialog = _currentDialog;
        _currentDialog = null;
        if (dialog == null || !GodotObject.IsInstanceValid(dialog))
            return;

        dialog.CloseRequestedByUser -= CloseCurrentDialog;
        dialog.AbandonRunRequested -= OnAbandonRunRequested;
        dialog.TreeExited -= OnDialogTreeExited;
        OverlayCoordinator.Unregister(dialog);
        dialog.QueueFree();
    }

    private static void OnAbandonRunRequested()
    {
        var dialog = _currentDialog;
        var manager = GameManager.Instance;
        if (manager == null || !GodotObject.IsInstanceValid(manager))
        {
            dialog?.ShowOperationError("游戏状态管理器不可用，无法返回主菜单。 ");
            return;
        }

        if (!manager.TryAbandonRunToTitle(out var error))
        {
            dialog?.ShowOperationError(error);
            return;
        }

        // Scene teardown will release the dialog. Do not close it first: the core route owns the
        // abandon transition, and a route failure must leave the current settings page visible.
    }

    private static void OnDialogTreeExited()
    {
        if (_currentDialog != null && GodotObject.IsInstanceValid(_currentDialog))
            OverlayCoordinator.Unregister(_currentDialog);
        _currentDialog = null;
    }

    private static void PresentDialog(SettingsDialogController dialog)
    {
        dialog.Show();
    }
}
