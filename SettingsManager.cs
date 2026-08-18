using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CrosshairOverlayTool;

internal sealed class SettingsManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public SettingsManager()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrosshairOverlayTool");
        _settingsPath = Path.Combine(_settingsDirectory, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        var defaults = CreateSanitized(new AppSettings());

        if (!File.Exists(_settingsPath))
        {
            TrySave(defaults);
            return defaults;
        }

        try
        {
            using var stream = File.OpenRead(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(stream, SerializerOptions);
            var sanitized = CreateSanitized(loaded ?? new AppSettings());

            if (loaded is null || NeedsRewrite(loaded, sanitized))
            {
                TrySave(sanitized);
            }

            return sanitized;
        }
        catch (JsonException)
        {
            BackupInvalidSettingsFile();
            TrySave(defaults);
            return defaults;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex}");
            return defaults;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex}");
            return defaults;
        }
    }

    public bool TrySave(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var sanitized = CreateSanitized(settings);
            var json = JsonSerializer.Serialize(sanitized, SerializerOptions);
            File.WriteAllText(_settingsPath, json);
            return true;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex}");
            return false;
        }
    }

    private static AppSettings CreateSanitized(AppSettings settings)
    {
        var holdKey = string.IsNullOrWhiteSpace(settings.HoldKey)
            ? KeyBindingParser.DefaultHoldKeyText
            : settings.HoldKey.Trim();

        return new AppSettings
        {
            EnableHoldMode = settings.EnableHoldMode,
            EnableToggleMode = settings.EnableToggleMode,
            HoldKey = holdKey,
            ToggleHotkey = string.IsNullOrWhiteSpace(settings.ToggleHotkey)
                ? KeyBindingParser.DefaultToggleHotkeyText
                : settings.ToggleHotkey.Trim(),
            HorizontalLockToggleHotkey = string.IsNullOrWhiteSpace(settings.HorizontalLockToggleHotkey)
                ? AppSettings.DefaultHorizontalLockToggleHotkeyText
                : settings.HorizontalLockToggleHotkey.Trim(),
            LineThickness = Math.Clamp(settings.LineThickness, 1, 20),
            Opacity = Math.Clamp(settings.Opacity, 1, 255),
            LineColor = string.IsNullOrWhiteSpace(settings.LineColor)
                ? "#FF3B30"
                : settings.LineColor.Trim(),
            RunOnStartup = settings.RunOnStartup,
            EisuPseudoHoldInitialMarginMs = Math.Clamp(
                settings.EisuPseudoHoldInitialMarginMs,
                AppSettings.MinEisuPseudoHoldInitialMarginMs,
                AppSettings.MaxEisuPseudoHoldInitialMarginMs),
            EnableHorizontalLock = settings.EnableHorizontalLock,
            HorizontalLockTriggerMode = AppSettings.HorizontalLockTriggerModeSameHoldKey,
            HorizontalLockHoldKey = holdKey
        };
    }

    private static bool NeedsRewrite(AppSettings original, AppSettings sanitized)
    {
        return original.EnableHoldMode != sanitized.EnableHoldMode
            || original.EnableToggleMode != sanitized.EnableToggleMode
            || !StringEquals(original.HoldKey, sanitized.HoldKey)
            || !StringEquals(original.ToggleHotkey, sanitized.ToggleHotkey)
            || !StringEquals(original.HorizontalLockToggleHotkey, sanitized.HorizontalLockToggleHotkey)
            || original.LineThickness != sanitized.LineThickness
            || original.Opacity != sanitized.Opacity
            || !StringEquals(original.LineColor, sanitized.LineColor)
            || original.RunOnStartup != sanitized.RunOnStartup
            || original.EisuPseudoHoldInitialMarginMs != sanitized.EisuPseudoHoldInitialMarginMs
            || original.EnableHorizontalLock != sanitized.EnableHorizontalLock
            || !StringEquals(original.HorizontalLockTriggerMode, sanitized.HorizontalLockTriggerMode)
            || !StringEquals(original.HorizontalLockHoldKey, sanitized.HorizontalLockHoldKey);
    }
    private void BackupInvalidSettingsFile()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var backupPath = Path.Combine(
                _settingsDirectory,
                $"settings.invalid.{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(_settingsPath, backupPath, overwrite: true);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to backup invalid settings: {ex}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Failed to backup invalid settings: {ex}");
        }
    }

    private static bool StringEquals(string? left, string? right)
    {
        return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
    }
}
