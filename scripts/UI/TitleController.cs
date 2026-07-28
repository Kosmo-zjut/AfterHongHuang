using Godot;

/// <summary>
/// Title scene presentation controller. Scene references are explicitly assembled in Title.tscn
/// so this controller does not depend on the title hierarchy or scene-path literals.
/// </summary>
public partial class TitleController : Control
{
    private const string VersionSettingKey = "application/config/version";

    [Export] public PackedScene CharacterSelectScene { get; set; }
    [Export] public PackedScene SettingsDialogScene { get; set; }

    private Button _startButton;
    private Button _settingsButton;
    private Button _quitButton;
    private Label _versionLabel;
    private VideoStreamPlayer _backgroundVideo;
    private bool _backgroundVideoFinishedConnected;

    public override void _Ready()
    {
        // Native window operations must wait until the first visible scene has joined the tree.
        ResolutionSettings.ApplyPersistedAtStartup();

        if (!TryValidateConfiguration(out var error))
        {
            GD.PrintErr($"[标题] 场景装配无效：{error}");
            SetButtonsDisabled(true);
            return;
        }

        _versionLabel.Text = ProjectSettings.GetSetting(VersionSettingKey).AsString();
        _startButton.Pressed += OnStartPressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;
        ConnectBackgroundVideoFinished();
    }

    public override void _ExitTree()
    {
        if (_backgroundVideo != null && _backgroundVideoFinishedConnected)
        {
            _backgroundVideo.Finished -= OnBackgroundVideoFinished;
            _backgroundVideoFinishedConnected = false;
        }
    }

    /// <summary>Checks the editor-wired dependencies before title actions become available.</summary>
    internal bool TryValidateConfiguration(out string error)
    {
        _startButton = GetNodeOrNull<Button>("%StartButton");
        _settingsButton = GetNodeOrNull<Button>("%SettingsButton");
        _quitButton = GetNodeOrNull<Button>("%QuitButton");
        _versionLabel = GetNodeOrNull<Label>("%VersionLabel");
        _backgroundVideo = GetNodeOrNull<VideoStreamPlayer>("%TitleVideo");
        if (_startButton == null || _settingsButton == null || _quitButton == null || _versionLabel == null)
        {
            error = "缺少标题页按钮或版本标签引用。";
            return false;
        }

        if (_backgroundVideo == null || _backgroundVideo.Stream == null)
        {
            error = "标题背景视频节点或 VideoStream 资源未装配。";
            GD.PrintErr($"[标题] {error}");
            return false;
        }

        if (CharacterSelectScene == null)
        {
            error = "缺少角色选择 PackedScene 引用。";
            return false;
        }

        if (SettingsDialogScene == null)
        {
            error = "缺少共享设置弹窗 PackedScene 引用。";
            return false;
        }

        if (!ProjectSettings.HasSetting(VersionSettingKey) || string.IsNullOrWhiteSpace(ProjectSettings.GetSetting(VersionSettingKey).AsString()))
        {
            error = "ProjectSettings 未配置 application/config/version。";
            return false;
        }

        error = "";
        return true;
    }

    private void OnStartPressed()
    {
        Error result = GetTree().ChangeSceneToPacked(CharacterSelectScene);
        if (result != Error.Ok)
            GD.PrintErr($"[标题] 角色选择场景切换失败：{result}");
    }

    private void OnSettingsPressed()
    {
        SettingsHelper.Show(this, SettingsDialogScene);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void ConnectBackgroundVideoFinished()
    {
        if (_backgroundVideoFinishedConnected)
            return;

        _backgroundVideo.Finished += OnBackgroundVideoFinished;
        _backgroundVideoFinishedConnected = true;
    }

    private void OnBackgroundVideoFinished()
    {
        if (!GodotObject.IsInstanceValid(_backgroundVideo) || !_backgroundVideo.IsInsideTree())
            return;

        _backgroundVideo.StreamPosition = 0.0f;
        _backgroundVideo.Play();
    }

    private void SetButtonsDisabled(bool disabled)
    {
        if (_startButton != null) _startButton.Disabled = disabled;
        if (_settingsButton != null) _settingsButton.Disabled = disabled;
        if (_quitButton != null) _quitButton.Disabled = disabled;
    }
}
