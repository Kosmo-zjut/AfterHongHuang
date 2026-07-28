using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Presentation controller for the shared in-page settings overlay. Audio and display services own all
/// state changes; this class only forwards user intent and renders their result.
/// </summary>
public partial class SettingsDialogController : Control
{
    public event Action CloseRequestedByUser;

    private static readonly AudioChannel[] ExpectedChannels =
    {
        AudioChannel.Master,
        AudioChannel.Music,
        AudioChannel.Sfx,
    };

    private bool _initialized;
    private ColorRect _contentScrim;
    private OptionButton _resolutionOptions;
    private Label _resolutionStatusLabel;
    private Label _audioStatusLabel;
    private Button _closeButton;
    private VolumeRowController[] _volumeRows;

    /// <summary>Validates the scene assembly and wires controls exactly once before display.</summary>
    public bool TryInitialize(out string error)
    {
        if (_initialized)
        {
            error = "";
            return true;
        }

        _contentScrim = GetNodeOrNull<ColorRect>("%ContentScrim");
        _resolutionOptions = GetNodeOrNull<OptionButton>("%ResolutionOptions");
        _resolutionStatusLabel = GetNodeOrNull<Label>("%ResolutionStatus");
        _audioStatusLabel = GetNodeOrNull<Label>("%AudioStatus");
        _closeButton = GetNodeOrNull<Button>("%CloseButton");
        _volumeRows = new[]
        {
            GetNodeOrNull<VolumeRowController>("%MasterVolume"),
            GetNodeOrNull<VolumeRowController>("%MusicVolume"),
            GetNodeOrNull<VolumeRowController>("%SfxVolume"),
        };

        if (_contentScrim == null || _resolutionOptions == null || _resolutionStatusLabel == null ||
            _audioStatusLabel == null || _closeButton == null || _volumeRows[0] == null ||
            _volumeRows[1] == null || _volumeRows[2] == null)
        {
            error = "设置场景缺少必要的控件引用。";
            return false;
        }

        foreach (var row in _volumeRows)
        {
            if (!row.TryInitialize(out error))
                return false;
        }

        if (!TryValidateVolumeRows(out error))
            return false;

        foreach (var row in _volumeRows)
        {
            row.VolumeChangeRequested += OnVolumeChangeRequested;
            row.MuteChangeRequested += OnMuteChangeRequested;
        }

        PopulateResolutionOptions();
        _resolutionOptions.ItemSelected += OnResolutionSelected;
        _closeButton.Pressed += OnCloseRequested;

        if (AudioSettingsService.Instance != null)
            AudioSettingsService.Instance.SettingsChanged += OnAudioSettingsChanged;

        RefreshAudioState();
        _initialized = true;
        error = "";
        return true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo ||
            keyEvent.Keycode != Key.Escape)
            return;

        OnCloseRequested();
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        if (!_initialized)
            return;

        foreach (var row in _volumeRows)
        {
            row.VolumeChangeRequested -= OnVolumeChangeRequested;
            row.MuteChangeRequested -= OnMuteChangeRequested;
        }

        if (AudioSettingsService.Instance != null)
            AudioSettingsService.Instance.SettingsChanged -= OnAudioSettingsChanged;

        _resolutionOptions.ItemSelected -= OnResolutionSelected;
        _closeButton.Pressed -= OnCloseRequested;
        _initialized = false;
    }

    /// <summary>Sets the content barrier boundary supplied by the hosting page.</summary>
    public bool TrySetContentTopInset(float topInset, out string error)
    {
        if (_contentScrim == null || !GodotObject.IsInstanceValid(_contentScrim))
        {
            error = "设置遮罩缺少内容层引用。";
            return false;
        }

        if (!float.IsFinite(topInset) || topInset < 0.0f)
        {
            error = $"设置遮罩顶部边界无效：{topInset}。";
            return false;
        }

        _contentScrim.OffsetTop = topInset;
        error = "";
        return true;
    }

    private void PopulateResolutionOptions()
    {
        _resolutionOptions.Clear();
        Vector2I currentSize = DisplayServer.WindowGetSize();
        bool currentSupported = ResolutionSettings.TryFindIndex(currentSize, out int currentCatalogIndex);
        if (!currentSupported)
        {
            _resolutionOptions.AddItem($"当前分辨率（{currentSize.X} × {currentSize.Y}，不在列表）", -1);
            _resolutionOptions.SetItemDisabled(0, true);
        }

        for (int index = 0; index < ResolutionSettings.SupportedOptions.Count; index++)
            _resolutionOptions.AddItem(ResolutionSettings.SupportedOptions[index].Label, index);

        _resolutionOptions.Select(currentSupported ? currentCatalogIndex : 0);
        _resolutionStatusLabel.Text = currentSupported
            ? "应用后立即生效，并在下次启动时恢复。"
            : "当前窗口尺寸不在支持目录中；请选择一个受支持项后应用。";
    }

    private void OnAudioSettingsChanged(AudioSettingsSnapshot snapshot)
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            return;

        foreach (var row in _volumeRows)
            row.Refresh(snapshot.Get(row.Channel));
        ClearAudioError();
    }

    private void OnVolumeChangeRequested(AudioChannel channel, int percent)
    {
        var service = AudioSettingsService.Instance;
        string error = service == null ? "音频设置服务未初始化。" : "";
        if (service == null || !service.TrySetVolume(channel, percent, out error))
        {
            ShowAudioError(error);
            RefreshAudioState(clearError: false);
            return;
        }

        RefreshAudioState();
    }

    private void OnMuteChangeRequested(AudioChannel channel, bool muted)
    {
        var service = AudioSettingsService.Instance;
        string error = service == null ? "音频设置服务未初始化。" : "";
        if (service == null || !service.TrySetMuted(channel, muted, out error))
        {
            ShowAudioError(error);
            RefreshAudioState(clearError: false);
            return;
        }

        RefreshAudioState();
    }

    private void RefreshAudioState(bool clearError = true)
    {
        var service = AudioSettingsService.Instance;
        string error = service == null ? "音频设置服务未初始化。" : "";
        if (service == null || !service.TryGetSnapshot(out var snapshot, out error))
        {
            foreach (var row in _volumeRows)
                row.SetInteractable(false);
            ShowAudioError(error);
            return;
        }

        foreach (var row in _volumeRows)
        {
            row.SetInteractable(true);
            row.Refresh(snapshot.Get(row.Channel));
        }

        if (clearError)
            ClearAudioError();
    }

    private bool TryValidateVolumeRows(out string error)
    {
        var seenChannels = new HashSet<AudioChannel>();
        for (int index = 0; index < _volumeRows.Length; index++)
        {
            var row = _volumeRows[index];
            if (!seenChannels.Add(row.Channel))
            {
                error = $"音量行装配重复使用音频层：{row.Channel}。";
                return false;
            }

            if (row.Channel != ExpectedChannels[index])
            {
                error = $"音量行 {index + 1} 绑定通道错误：需要 {ExpectedChannels[index]}，实际 {row.Channel}。";
                return false;
            }
        }

        error = "";
        return true;
    }

    private void ShowAudioError(string error)
    {
        _audioStatusLabel.Text = $"声音设置未应用：{error}";
        _audioStatusLabel.Visible = true;
        GD.PrintErr($"[设置] {_audioStatusLabel.Text}");
    }

    private void ClearAudioError()
    {
        _audioStatusLabel.Text = "";
        _audioStatusLabel.Visible = false;
    }

    private void OnResolutionSelected(long optionIndex)
    {
        int catalogIndex = _resolutionOptions.GetItemId((int)optionIndex);
        if (catalogIndex < 0)
            return;

        if (!ResolutionSettings.TryApply(catalogIndex, out var appliedSize, out var error))
        {
            GD.PrintErr($"[设置] 分辨率未应用：{error}");
            _resolutionStatusLabel.Text = $"显示设置未应用：{error}";
            SelectCurrentResolution(DisplayServer.WindowGetSize());
            return;
        }

        _resolutionStatusLabel.Text = $"已应用：{appliedSize.X} × {appliedSize.Y}，下次启动仍保留。";
    }

    private void SelectCurrentResolution(Vector2I currentSize)
    {
        if (ResolutionSettings.TryFindIndex(currentSize, out int catalogIndex))
        {
            for (int itemIndex = 0; itemIndex < _resolutionOptions.ItemCount; itemIndex++)
            {
                if (_resolutionOptions.GetItemId(itemIndex) == catalogIndex)
                {
                    _resolutionOptions.Select(itemIndex);
                    return;
                }
            }
        }

        for (int itemIndex = 0; itemIndex < _resolutionOptions.ItemCount; itemIndex++)
        {
            if (_resolutionOptions.GetItemId(itemIndex) < 0)
            {
                _resolutionOptions.Select(itemIndex);
                return;
            }
        }

        GD.PrintErr($"[设置] 无法在分辨率控件中表示当前尺寸 {currentSize.X}×{currentSize.Y}。");
    }

    private void OnCloseRequested() => CloseRequestedByUser?.Invoke();
}
