using Godot;

/// <summary>
/// 场景0：标题界面。
/// 全屏插画背景 + 4个按钮 + 版本号。
/// </summary>
public partial class TitleController : Control
{
    private Button _startBtn;
    private Button _timelineBtn;
    private Button _settingsBtn;
    private Button _quitBtn;
    private Label _versionLabel;

    public override void _Ready()
    {
        // Window operations must run after a visible scene has entered the tree. Applying a saved
        // resolution from the Autoload _EnterTree path can race native window initialization.
        ResolutionSettings.ApplyPersistedAtStartup();
        _startBtn = GetNode<Button>("VBoxContainer/StartBtn");
        _timelineBtn = GetNode<Button>("VBoxContainer/TimelineBtn");
        _settingsBtn = GetNode<Button>("VBoxContainer/SettingsBtn");
        _quitBtn = GetNode<Button>("VBoxContainer/QuitBtn");
        _versionLabel = GetNode<Label>("VersionLabel");

        _versionLabel.Text = "v0.0.1-alpha";

        _startBtn.Pressed += OnStartPressed;
        _timelineBtn.Pressed += OnTimelinePressed;
        _settingsBtn.Pressed += OnSettingsPressed;
        _quitBtn.Pressed += OnQuitPressed;
    }

    private void OnStartPressed()
    {
        if (!GameManager.Instance.ChangeSceneToFile(
                "res://scenes/CharacterSelect/CharacterSelect.tscn", out var error))
            GD.PrintErr($"[标题] 角色选择场景路由失败：{error}");
    }

    private void OnTimelinePressed()
    {
        ShowPlaceholder("时间线", "剧情模块\n修炼中，暂未开放");
    }

    private void OnSettingsPressed()
    {
        SettingsHelper.Show(this);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void ShowPlaceholder(string title, string msg)
    {
        // 简单弹窗
        var dialog = new AcceptDialog();
        dialog.Title = title;
        dialog.DialogText = msg;
        dialog.Size = new Vector2I(300, 150);
        AddChild(dialog);
        dialog.PopupCentered();
    }
}
