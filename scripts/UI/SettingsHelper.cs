using Godot;

/// <summary>
/// Coordinates one shared SettingsDialog instance. The dialog scene owns presentation and input;
/// this helper only owns its cross-page lifetime and resolves the host's TopBar inset.
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

        if (!dialog.TrySetContentTopInset(ResolveTopBarInset(parent), out error))
        {
            dialog.QueueFree();
            return false;
        }

        if (!OverlayCoordinator.TryPrepareUtilityOverlay("设置", out error))
        {
            dialog.QueueFree();
            return false;
        }

        _currentDialog = dialog;
        dialog.CloseRequestedByUser += CloseCurrentDialog;
        dialog.TreeExited += OnDialogTreeExited;
        OverlayCoordinator.RegisterSettings(dialog);
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
        dialog.TreeExited -= OnDialogTreeExited;
        OverlayCoordinator.Unregister(dialog);
        dialog.QueueFree();
    }

    private static float ResolveTopBarInset(Node parent)
    {
        float inset = 0.0f;
        foreach (Node child in parent.GetChildren())
        {
            if (child is not TopBar topBar || !GodotObject.IsInstanceValid(topBar) || !topBar.Visible)
                continue;

            float currentHeight = topBar.Size.Y;
            float minimumHeight = topBar.CustomMinimumSize.Y;
            float combinedMinimumHeight = topBar.GetCombinedMinimumSize().Y;
            inset = Mathf.Max(inset, currentHeight);
            inset = Mathf.Max(inset, minimumHeight);
            inset = Mathf.Max(inset, combinedMinimumHeight);
        }

        return inset;
    }

    private static void OnDialogTreeExited()
    {
        _currentDialog = null;
    }

    private static void PresentDialog(SettingsDialogController dialog)
    {
        dialog.Show();
    }
}
