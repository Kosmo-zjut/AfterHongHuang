using Godot;
using System;

/// <summary>Reusable presentation row for one logical audio channel.</summary>
public partial class VolumeRowController : HBoxContainer
{
    [Export] public AudioChannel Channel { get; set; }
    [Export] public string DisplayLabel { get; set; } = "";

    private Label _label;
    private Label _valueLabel;
    private HSlider _slider;
    private CheckBox _muteCheckBox;
    private bool _initialized;
    private bool _suppressChanges;

    /// <summary>Raised when the player submits a new linear percentage.</summary>
    public event Action<AudioChannel, int> VolumeChangeRequested;

    /// <summary>Raised when the player submits an explicit mute toggle.</summary>
    public event Action<AudioChannel, bool> MuteChangeRequested;

    public override void _Ready()
    {
        TryInitialize(out var error);
        if (!string.IsNullOrWhiteSpace(error))
            GD.PrintErr($"[设置] 音量行装配失败：{error}");
    }

    /// <summary>Validates and wires the editor-assembled row exactly once.</summary>
    public bool TryInitialize(out string error)
    {
        if (_initialized)
        {
            error = "";
            return true;
        }

        _label = GetNodeOrNull<Label>("Label");
        _valueLabel = GetNodeOrNull<Label>("%ValueLabel");
        _slider = GetNodeOrNull<HSlider>("%Slider");
        _muteCheckBox = GetNodeOrNull<CheckBox>("%MuteCheckBox");
        if (_label == null || _valueLabel == null || _slider == null || _muteCheckBox == null)
        {
            error = "缺少百分比、滑块或静音控件。";
            return false;
        }
        if (!Enum.IsDefined(Channel))
        {
            error = "音量行未分配有效音频层。";
            return false;
        }
        if (string.IsNullOrWhiteSpace(DisplayLabel))
        {
            error = "音量行未分配显示标签。";
            return false;
        }

        _label.Text = DisplayLabel;

        _slider.ValueChanged += OnSliderValueChanged;
        _muteCheckBox.Pressed += OnMutePressed;
        _initialized = true;
        error = "";
        return true;
    }

    /// <summary>Updates display without feeding the value back as a new player request.</summary>
    public void Refresh(AudioChannelSettings settings)
    {
        if (!_initialized && !TryInitialize(out _))
            return;

        _suppressChanges = true;
        _slider.SetValueNoSignal(settings.Percent);
        _valueLabel.Text = $"{settings.Percent}%";
        _muteCheckBox.ButtonPressed = settings.Muted;
        _suppressChanges = false;
    }

    /// <summary>Locks this row when the audio service cannot provide a valid snapshot.</summary>
    public void SetInteractable(bool enabled)
    {
        if (!_initialized && !TryInitialize(out _))
            return;

        _slider.Editable = enabled;
        _muteCheckBox.Disabled = !enabled;
        if (!enabled)
        {
            _suppressChanges = true;
            _slider.SetValueNoSignal(0);
            _valueLabel.Text = "—";
            _muteCheckBox.ButtonPressed = false;
            _suppressChanges = false;
        }
        Modulate = enabled ? Colors.White : new Color(1, 1, 1, 0.45f);
    }

    private void OnSliderValueChanged(double value)
    {
        if (_suppressChanges)
            return;

        int percent = Mathf.RoundToInt((float)Mathf.Clamp(value, 0.0, 100.0));
        VolumeChangeRequested?.Invoke(Channel, percent);
    }

    private void OnMutePressed()
    {
        if (_suppressChanges)
            return;

        MuteChangeRequested?.Invoke(Channel, _muteCheckBox.ButtonPressed);
    }
}
