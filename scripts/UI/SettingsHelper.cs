using Godot;

/// <summary>
/// 设置弹窗工具类。MapController 和 BattleController 共用。
/// </summary>
public static class SettingsHelper
{
    private static AcceptDialog _currentDialog;
    /// <summary>显示设置弹窗</summary>
    public static void Show(Node parent)
    {
        if (_currentDialog != null && GodotObject.IsInstanceValid(_currentDialog))
        {
            CloseCurrentDialog();
            return;
        }

        if (parent == null)
        {
            GD.PrintErr("[设置] 缺少父节点，拒绝创建设置弹窗。");
            return;
        }
        if (!OverlayCoordinator.TryPrepareUtilityOverlay("设置", out var coordinatorError))
        {
            GD.PrintErr($"[设置] {coordinatorError}");
            return;
        }

        var dialog = new AcceptDialog();
        _currentDialog = dialog;
        OverlayCoordinator.RegisterSettings(dialog);
        dialog.Title = "设置";
        dialog.Size = new Vector2I(340, 340);
        dialog.Exclusive = true;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        vbox.SetPosition(new Vector2(16, 12));
        vbox.CustomMinimumSize = new Vector2(300, 0);

        // ---- 分辨率 ----
        var resLabel = new Label();
        resLabel.Text = "画面分辨率";
        resLabel.AddThemeFontSizeOverride("font_size", 15);
        resLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.5f));
        vbox.AddChild(resLabel);

        var resOption = new OptionButton();
        resOption.CustomMinimumSize = new Vector2(280, 32);
        Vector2I currentSize = DisplayServer.WindowGetSize();
        bool currentSupported = ResolutionSettings.TryFindIndex(currentSize, out int currentCatalogIndex);
        if (!currentSupported)
        {
            resOption.AddItem($"当前分辨率（{currentSize.X} × {currentSize.Y}，不在列表）", -1);
            resOption.SetItemDisabled(0, true);
        }
        for (int i = 0; i < ResolutionSettings.SupportedOptions.Count; i++)
        {
            var option = ResolutionSettings.SupportedOptions[i];
            resOption.AddItem(option.Label, i);
        }
        resOption.Select(currentSupported ? currentCatalogIndex : 0);
        vbox.AddChild(resOption);

        var resolutionStatus = new Label();
        resolutionStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        resolutionStatus.CustomMinimumSize = new Vector2(280, 40);
        resolutionStatus.AddThemeFontSizeOverride("font_size", 12);
        resolutionStatus.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        resolutionStatus.Text = currentSupported
            ? "应用后立即生效，并在下次启动时恢复。"
            : "当前窗口尺寸不在支持目录中；请选择一个受支持项后应用。";
        resOption.ItemSelected += (itemIndex) =>
        {
            int catalogIndex = resOption.GetItemId((int)itemIndex);
            if (catalogIndex < 0)
                return;
            ApplyResolution(catalogIndex, resOption, resolutionStatus);
        };
        vbox.AddChild(resolutionStatus);

        // ---- 分隔线 ----
        vbox.AddChild(MakeHSeparator());

        // ---- 返回标题 ----
        var returnBtn = MakeActionButton("返回标题界面");
        returnBtn.Pressed += () =>
        {
            CloseCurrentDialog();
            GameManager.Instance.GoToTitle();
        };
        vbox.AddChild(returnBtn);

        // ---- 退出游戏 ----
        var quitBtn = MakeActionButton("退出游戏", new Color(0.8f, 0.25f, 0.2f));
        quitBtn.Pressed += () =>
        {
            CloseCurrentDialog();
            parent.GetTree().Quit();
        };
        vbox.AddChild(quitBtn);

        dialog.AddChild(vbox);
        dialog.Confirmed += CloseCurrentDialog;
        dialog.Canceled += CloseCurrentDialog;
        dialog.CloseRequested += CloseCurrentDialog;
        parent.AddChild(dialog);
        dialog.PopupCentered();
    }

    /// <summary>Closes the settings modal and releases the shared overlay slot.</summary>
    public static void CloseCurrentDialog()
    {
        if (_currentDialog != null && GodotObject.IsInstanceValid(_currentDialog))
        {
            OverlayCoordinator.Unregister(_currentDialog);
            _currentDialog.QueueFree();
        }
        _currentDialog = null;
    }

    private static void ApplyResolution(int index, OptionButton optionButton, Label statusLabel)
    {
        if (!ResolutionSettings.TryApply(index, out var appliedSize, out var error))
        {
            GD.PrintErr($"[设置] 分辨率未应用：{error}");
            statusLabel.Text = $"未应用：{error}";
            statusLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.38f, 0.3f));
            SelectCurrentResolution(optionButton, DisplayServer.WindowGetSize());
            return;
        }
        GD.Print($"[设置] 分辨率已切换至 {appliedSize.X}×{appliedSize.Y}");
        statusLabel.Text = $"已应用：{appliedSize.X} × {appliedSize.Y}，下次启动仍保留。";
        statusLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.82f, 0.55f));
    }

    private static void SelectCurrentResolution(OptionButton optionButton, Vector2I size)
    {
        if (ResolutionSettings.TryFindIndex(size, out int catalogIndex))
        {
            for (int i = 0; i < optionButton.ItemCount; i++)
            {
                if (optionButton.GetItemId(i) == catalogIndex)
                {
                    optionButton.Select(i);
                    return;
                }
            }
        }

        for (int i = 0; i < optionButton.ItemCount; i++)
        {
            if (optionButton.GetItemId(i) < 0)
            {
                optionButton.Select(i);
                return;
            }
        }

        GD.PrintErr($"[设置] 现有 OptionButton 没有可表示当前尺寸 {size.X}×{size.Y} 的项目。");
    }

    private static Button MakeActionButton(string text, Color? color = null)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(280, 36);
        btn.AddThemeFontSizeOverride("font_size", 14);
        if (color.HasValue)
            btn.AddThemeColorOverride("font_color", color.Value);
        return btn;
    }

    private static Control MakeHSeparator()
    {
        var sep = new ColorRect();
        sep.Color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        sep.CustomMinimumSize = new Vector2(280, 1);
        return sep;
    }
}
