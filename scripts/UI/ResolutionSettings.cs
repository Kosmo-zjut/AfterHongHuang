using Godot;
using System;
using System.Collections.Generic;

/// <summary>One authoritative resolution catalog and application service for the settings UI.</summary>
public static class ResolutionSettings
{
    private const string SettingsPath = "user://afterhonghuang-settings.cfg";
    private const string SettingsSection = "display";
    private const string WidthKey = "width";
    private const string HeightKey = "height";

    /// <summary>Supported window sizes. The 1920x1080 design viewport is retained and scales with the window.</summary>
    public static readonly IReadOnlyList<ResolutionOption> SupportedOptions = new[]
    {
        new ResolutionOption(1920, 1080, "1920 × 1080（推荐）"),
        new ResolutionOption(1600, 900, "1600 × 900"),
        new ResolutionOption(1280, 720, "1280 × 720"),
    };

    /// <summary>Loads and applies the persisted setting once at application startup when one exists.</summary>
    public static void ApplyPersistedAtStartup()
    {
        var config = new ConfigFile();
        if (config.Load(SettingsPath) != Error.Ok ||
            !config.HasSectionKey(SettingsSection, WidthKey) ||
            !config.HasSectionKey(SettingsSection, HeightKey))
            return;

        Variant widthValue = config.GetValue(SettingsSection, WidthKey);
        Variant heightValue = config.GetValue(SettingsSection, HeightKey);
        if (widthValue.VariantType != Variant.Type.Int || heightValue.VariantType != Variant.Type.Int)
        {
            GD.PrintErr("[设置] 已保存的分辨率配置类型无效，保留工程默认值。");
            return;
        }

        int width = widthValue.AsInt32();
        int height = heightValue.AsInt32();
        if (!TryFindIndex(new Vector2I(width, height), out int index))
        {
            GD.PrintErr($"[设置] 已保存的分辨率 {width}×{height} 不受支持，保留工程默认值。");
            return;
        }

        if (!TryApply(index, out _, out var error))
            GD.PrintErr($"[设置] 无法应用已保存的分辨率：{error}");
    }

    /// <summary>Applies and persists one catalog entry. A failed OS application never reports success.</summary>
    public static bool TryApply(int index, out Vector2I appliedSize, out string error)
    {
        Vector2I previousSize = DisplayServer.WindowGetSize();
        appliedSize = previousSize;
        error = "";
        if (index < 0 || index >= SupportedOptions.Count)
        {
            error = $"不支持的分辨率索引：{index}";
            GD.PrintErr($"[设置] {error}");
            return false;
        }
        if (DisplayServer.GetName().Equals("headless", StringComparison.OrdinalIgnoreCase))
        {
            error = "当前为 headless 显示服务，无法应用窗口分辨率。";
            GD.PrintErr($"[设置] {error}");
            return false;
        }

        var option = SupportedOptions[index];
        var desired = new Vector2I(option.Width, option.Height);
        DisplayServer.WindowSetSize(desired);
        appliedSize = DisplayServer.WindowGetSize();
        if (appliedSize != desired)
        {
            string restoreError = RestoreWindowSize(previousSize);
            appliedSize = DisplayServer.WindowGetSize();
            error = $"窗口管理器未接受 {option.Width}×{option.Height}，实际为 {appliedSize.X}×{appliedSize.Y}。{restoreError}";
            GD.PrintErr($"[设置] {error}");
            return false;
        }

        var config = new ConfigFile();
        config.SetValue(SettingsSection, WidthKey, option.Width);
        config.SetValue(SettingsSection, HeightKey, option.Height);
        if (config.Save(SettingsPath) != Error.Ok)
        {
            string restoreError = RestoreWindowSize(previousSize);
            appliedSize = DisplayServer.WindowGetSize();
            error = $"保存用户设置文件失败，窗口已恢复至 {appliedSize.X}×{appliedSize.Y}。{restoreError}";
            GD.PrintErr($"[设置] {error}");
            return false;
        }

        GD.Print($"[设置] 分辨率已应用并保存：{option.Width}×{option.Height}。");
        return true;
    }

    private static string RestoreWindowSize(Vector2I previousSize)
    {
        DisplayServer.WindowSetSize(previousSize);
        Vector2I restoredSize = DisplayServer.WindowGetSize();
        return restoredSize == previousSize
            ? ""
            : $" 尝试恢复至 {previousSize.X}×{previousSize.Y} 失败，实际为 {restoredSize.X}×{restoredSize.Y}。";
    }

    /// <summary>Resolves the current window size to one catalog item when possible.</summary>
    public static bool TryFindIndex(Vector2I size, out int index)
    {
        for (int i = 0; i < SupportedOptions.Count; i++)
        {
            var option = SupportedOptions[i];
            if (option.Width == size.X && option.Height == size.Y)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }
}

/// <summary>Catalog entry used by both the settings UI and the persistent application service.</summary>
public readonly struct ResolutionOption
{
    public ResolutionOption(int width, int height, string label)
    {
        Width = width;
        Height = height;
        Label = label;
    }

    public int Width { get; }
    public int Height { get; }
    public string Label { get; }
}
