using Godot;
using System;

/// <summary>Logical audio layers exposed by the shared settings page.</summary>
public enum AudioChannel
{
    Master = 0,
    Music = 1,
    Sfx = 2,
}

/// <summary>Immutable percentage and explicit mute state for one audio layer.</summary>
public readonly struct AudioChannelSettings
{
    public AudioChannelSettings(int percent, bool muted)
    {
        Percent = percent;
        Muted = muted;
    }

    public int Percent { get; }
    public bool Muted { get; }
}

/// <summary>Immutable snapshot shared by the settings presentation and persistence boundary.</summary>
public sealed class AudioSettingsSnapshot
{
    public AudioSettingsSnapshot(AudioChannelSettings master, AudioChannelSettings music,
        AudioChannelSettings sfx)
    {
        Master = master;
        Music = music;
        Sfx = sfx;
    }

    public AudioChannelSettings Master { get; }
    public AudioChannelSettings Music { get; }
    public AudioChannelSettings Sfx { get; }

    public AudioChannelSettings Get(AudioChannel channel) => channel switch
    {
        AudioChannel.Master => Master,
        AudioChannel.Music => Music,
        AudioChannel.Sfx => Sfx,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "未知音频层。"),
    };
}

/// <summary>
/// Application-lifetime owner for the three audio buses and the audio section of the shared
/// settings file. It owns no playback node or music state machine; title music remains local to
/// Title.tscn and only selects the Music bus.
/// </summary>
public partial class AudioSettingsService : Node
{
    private const string SettingsPath = "user://afterhonghuang-settings.cfg";
    private const string AudioSection = "audio";
    private const float SilentVolumeDb = -80.0f;

    private readonly int[] _busIndices = { -1, -1, -1 };
    private AudioSettingsSnapshot _snapshot;

    public static AudioSettingsService Instance { get; private set; }

    public bool IsAvailable { get; private set; }
    public string LastError { get; private set; } = "";

    /// <summary>Raised only after all three buses, memory and the config file commit together.</summary>
    public event Action<AudioSettingsSnapshot> SettingsChanged;

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            GD.PrintErr("[音频设置] 检测到重复 AudioSettingsService Autoload，拒绝替换现有实例。");
            return;
        }

        Instance = this;
    }

    public override void _Ready()
    {
        TryInitialize();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Loads buses and persisted values exactly once during Autoload startup.</summary>
    public bool TryInitialize()
    {
        if (IsAvailable)
            return true;

        if (!TryResolveBuses(out var error))
            return Fail(error);

        if (!TryLoadSnapshot(out var snapshot, out error))
            return Fail(error);

        if (!TryApplyToBuses(snapshot, out error))
            return Fail(error);

        _snapshot = snapshot;
        IsAvailable = true;
        LastError = "";
        return true;
    }

    /// <summary>Returns the last committed snapshot without exposing mutable service state.</summary>
    public bool TryGetSnapshot(out AudioSettingsSnapshot snapshot, out string error)
    {
        if (!IsAvailable || _snapshot == null)
        {
            snapshot = null;
            error = string.IsNullOrWhiteSpace(LastError) ? "音频设置服务不可用。" : LastError;
            return false;
        }

        snapshot = _snapshot;
        error = "";
        return true;
    }

    /// <summary>Applies one percentage and persists it while preserving the display section.</summary>
    public bool TrySetVolume(AudioChannel channel, int percent, out string error)
    {
        error = "";
        if (!TryValidateRequest(channel, percent, out error))
            return false;
        if (!TryGetSnapshot(out var current, out error))
            return false;

        var currentChannel = current.Get(channel);
        bool muted = percent > 0 ? false : currentChannel.Muted;
        var next = BuildSnapshot(current, channel, new AudioChannelSettings(percent, muted));
        return TryCommit(next, out error);
    }

    /// <summary>Applies one explicit mute state and persists it while preserving its percentage.</summary>
    public bool TrySetMuted(AudioChannel channel, bool muted, out string error)
    {
        error = "";
        if (!Enum.IsDefined(channel))
        {
            error = $"未知音频层：{channel}。";
            return false;
        }
        if (!TryGetSnapshot(out var current, out error))
            return false;

        var currentChannel = current.Get(channel);
        var next = BuildSnapshot(current, channel,
            new AudioChannelSettings(currentChannel.Percent, muted));
        return TryCommit(next, out error);
    }

    private bool TryCommit(AudioSettingsSnapshot next, out string error)
    {
        error = "";
        if (!TryLoadConfig(out var config, out error))
            return false;

        var oldBusStates = CaptureBusStates();
        if (!TryApplyToBuses(next, out error))
        {
            RestoreBusStates(oldBusStates);
            return false;
        }

        WriteSnapshot(config, next);
        if (config.Save(SettingsPath) != Error.Ok)
        {
            RestoreBusStates(oldBusStates);
            error = "声音设置保存失败，已恢复本次变更前的实际音量。";
            GD.PrintErr($"[音频设置] {error}");
            return false;
        }

        _snapshot = next;
        LastError = "";
        SettingsChanged?.Invoke(next);
        return true;
    }

    private bool TryResolveBuses(out string error)
    {
        error = "";
        _busIndices[(int)AudioChannel.Master] = AudioServer.GetBusIndex("Master");
        _busIndices[(int)AudioChannel.Music] = AudioServer.GetBusIndex("Music");
        _busIndices[(int)AudioChannel.Sfx] = AudioServer.GetBusIndex("SFX");

        foreach (AudioChannel channel in Enum.GetValues<AudioChannel>())
        {
            if (_busIndices[(int)channel] < 0)
            {
                error = $"缺少必要音频总线：{channel}。";
                return false;
            }
        }

        return true;
    }

    private bool TryLoadSnapshot(out AudioSettingsSnapshot snapshot, out string error)
    {
        snapshot = null;
        error = "";
        if (!TryLoadConfig(out var config, out error))
            return false;

        string[] keys =
        {
            "master_percent", "master_muted", "music_percent", "music_muted", "sfx_percent", "sfx_muted",
        };
        bool hasAudioValues = false;
        foreach (string key in keys)
            hasAudioValues |= config.HasSectionKey(AudioSection, key);

        if (!hasAudioValues)
        {
            snapshot = new AudioSettingsSnapshot(
                new AudioChannelSettings(80, false),
                new AudioChannelSettings(65, false),
                new AudioChannelSettings(80, false));
            return true;
        }

        foreach (string key in keys)
        {
            if (!config.HasSectionKey(AudioSection, key))
            {
                error = $"声音配置缺少字段 audio/{key}。";
                return false;
            }
        }

        if (!TryReadPercent(config, "master_percent", out int masterPercent, out error) ||
            !TryReadPercent(config, "music_percent", out int musicPercent, out error) ||
            !TryReadPercent(config, "sfx_percent", out int sfxPercent, out error) ||
            !TryReadBool(config, "master_muted", out bool masterMuted, out error) ||
            !TryReadBool(config, "music_muted", out bool musicMuted, out error) ||
            !TryReadBool(config, "sfx_muted", out bool sfxMuted, out error))
            return false;

        snapshot = new AudioSettingsSnapshot(
            new AudioChannelSettings(masterPercent, masterMuted),
            new AudioChannelSettings(musicPercent, musicMuted),
            new AudioChannelSettings(sfxPercent, sfxMuted));
        return true;
    }

    private bool TryLoadConfig(out ConfigFile config, out string error)
    {
        config = new ConfigFile();
        Error result = config.Load(SettingsPath);
        if (result == Error.Ok || result == Error.FileNotFound)
        {
            error = "";
            return true;
        }

        error = $"读取声音设置失败：{result}。";
        GD.PrintErr($"[音频设置] {error}");
        return false;
    }

    private static bool TryReadPercent(ConfigFile config, string key, out int percent, out string error)
    {
        Variant value = config.GetValue(AudioSection, key);
        if (value.VariantType != Variant.Type.Int)
        {
            percent = 0;
            error = $"声音配置 audio/{key} 类型无效。";
            return false;
        }

        percent = value.AsInt32();
        if (percent < 0 || percent > 100)
        {
            error = $"声音配置 audio/{key} 超出 0~100 范围。";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryReadBool(ConfigFile config, string key, out bool value, out string error)
    {
        Variant variant = config.GetValue(AudioSection, key);
        if (variant.VariantType != Variant.Type.Bool)
        {
            value = false;
            error = $"声音配置 audio/{key} 类型无效。";
            return false;
        }

        value = variant.AsBool();
        error = "";
        return true;
    }

    private static void WriteSnapshot(ConfigFile config, AudioSettingsSnapshot snapshot)
    {
        config.SetValue(AudioSection, "master_percent", snapshot.Master.Percent);
        config.SetValue(AudioSection, "master_muted", snapshot.Master.Muted);
        config.SetValue(AudioSection, "music_percent", snapshot.Music.Percent);
        config.SetValue(AudioSection, "music_muted", snapshot.Music.Muted);
        config.SetValue(AudioSection, "sfx_percent", snapshot.Sfx.Percent);
        config.SetValue(AudioSection, "sfx_muted", snapshot.Sfx.Muted);
    }

    private bool TryApplyToBuses(AudioSettingsSnapshot snapshot, out string error)
    {
        error = "";
        try
        {
            ApplyChannel(AudioChannel.Master, snapshot.Master);
            ApplyChannel(AudioChannel.Music, snapshot.Music);
            ApplyChannel(AudioChannel.Sfx, snapshot.Sfx);
            return true;
        }
        catch (Exception exception)
        {
            error = $"应用音频总线失败：{exception.Message}";
            GD.PrintErr($"[音频设置] {error}");
            return false;
        }
    }

    private void ApplyChannel(AudioChannel channel, AudioChannelSettings settings)
    {
        int busIndex = _busIndices[(int)channel];
        float volumeDb = settings.Percent == 0
            ? SilentVolumeDb
            : Mathf.LinearToDb(settings.Percent / 100.0f);
        AudioServer.SetBusVolumeDb(busIndex, volumeDb);
        AudioServer.SetBusMute(busIndex, settings.Muted || settings.Percent == 0);
    }

    private BusState[] CaptureBusStates()
    {
        var states = new BusState[_busIndices.Length];
        foreach (AudioChannel channel in Enum.GetValues<AudioChannel>())
        {
            int index = _busIndices[(int)channel];
            states[(int)channel] = new BusState(AudioServer.GetBusVolumeDb(index), AudioServer.IsBusMute(index));
        }

        return states;
    }

    private void RestoreBusStates(BusState[] states)
    {
        foreach (AudioChannel channel in Enum.GetValues<AudioChannel>())
        {
            int index = _busIndices[(int)channel];
            AudioServer.SetBusVolumeDb(index, states[(int)channel].VolumeDb);
            AudioServer.SetBusMute(index, states[(int)channel].Muted);
        }
    }

    private static bool TryValidateRequest(AudioChannel channel, int percent, out string error)
    {
        if (!Enum.IsDefined(channel))
        {
            error = $"未知音频层：{channel}。";
            return false;
        }
        if (percent < 0 || percent > 100)
        {
            error = $"音量百分比必须在 0~100：{percent}。";
            return false;
        }

        error = "";
        return true;
    }

    private static AudioSettingsSnapshot BuildSnapshot(AudioSettingsSnapshot current, AudioChannel channel,
        AudioChannelSettings replacement)
    {
        return channel switch
        {
            AudioChannel.Master => new AudioSettingsSnapshot(replacement, current.Music, current.Sfx),
            AudioChannel.Music => new AudioSettingsSnapshot(current.Master, replacement, current.Sfx),
            AudioChannel.Sfx => new AudioSettingsSnapshot(current.Master, current.Music, replacement),
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "未知音频层。"),
        };
    }

    private bool Fail(string error)
    {
        IsAvailable = false;
        LastError = error;
        GD.PrintErr($"[音频设置] {error}");
        return false;
    }

    private readonly struct BusState
    {
        public BusState(float volumeDb, bool muted)
        {
            VolumeDb = volumeDb;
            Muted = muted;
        }

        public float VolumeDb { get; }
        public bool Muted { get; }
    }
}
