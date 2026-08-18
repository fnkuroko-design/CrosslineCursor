using System.Text.Json.Serialization;

namespace CrosshairOverlayTool;

internal sealed class AppSettings
{
    public const int DefaultEisuPseudoHoldInitialMarginMs = 0;
    public const int MinEisuPseudoHoldInitialMarginMs = 0;
    public const int MaxEisuPseudoHoldInitialMarginMs = 60;
    public const string DefaultHorizontalLockToggleHotkeyText = "Ctrl+Alt+Shift+L";
    public const string HorizontalLockTriggerModeSameHoldKey = "same_hold_key";
    public const string HorizontalLockTriggerModeSeparateHoldKey = "separate_hold_key";
    public const string DefaultHorizontalLockTriggerMode = HorizontalLockTriggerModeSameHoldKey;
    public const string DefaultHorizontalLockHoldKey = "RightCtrl";

    [JsonPropertyName("enable_hold_mode")]
    public bool EnableHoldMode { get; set; } = true;

    [JsonPropertyName("enable_toggle_mode")]
    public bool EnableToggleMode { get; set; } = true;

    [JsonPropertyName("hold_key")]
    public string HoldKey { get; set; } = KeyBindingParser.DefaultHoldKeyText;

    [JsonPropertyName("toggle_hotkey")]
    public string ToggleHotkey { get; set; } = KeyBindingParser.DefaultToggleHotkeyText;

    [JsonPropertyName("horizontal_lock_toggle_hotkey")]
    public string HorizontalLockToggleHotkey { get; set; } = DefaultHorizontalLockToggleHotkeyText;

    [JsonPropertyName("line_thickness")]
    public int LineThickness { get; set; } = 2;

    [JsonPropertyName("opacity")]
    public int Opacity { get; set; } = 160;

    [JsonPropertyName("line_color")]
    public string LineColor { get; set; } = "#FF3B30";

    [JsonPropertyName("run_on_startup")]
    public bool RunOnStartup { get; set; }

    [JsonPropertyName("eisu_pseudo_hold_initial_margin_ms")]
    public int EisuPseudoHoldInitialMarginMs { get; set; } = DefaultEisuPseudoHoldInitialMarginMs;

    [JsonPropertyName("enable_horizontal_lock")]
    public bool EnableHorizontalLock { get; set; }

    [JsonPropertyName("horizontal_lock_trigger_mode")]
    public string HorizontalLockTriggerMode { get; set; } = DefaultHorizontalLockTriggerMode;

    [JsonPropertyName("horizontal_lock_hold_key")]
    public string HorizontalLockHoldKey { get; set; } = DefaultHorizontalLockHoldKey;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            EnableHoldMode = EnableHoldMode,
            EnableToggleMode = EnableToggleMode,
            HoldKey = HoldKey,
            ToggleHotkey = ToggleHotkey,
            HorizontalLockToggleHotkey = HorizontalLockToggleHotkey,
            LineThickness = LineThickness,
            Opacity = Opacity,
            LineColor = LineColor,
            RunOnStartup = RunOnStartup,
            EisuPseudoHoldInitialMarginMs = EisuPseudoHoldInitialMarginMs,
            EnableHorizontalLock = EnableHorizontalLock,
            HorizontalLockTriggerMode = HorizontalLockTriggerMode,
            HorizontalLockHoldKey = HorizontalLockHoldKey
        };
    }

    public void CopyFrom(AppSettings other)
    {
        EnableHoldMode = other.EnableHoldMode;
        EnableToggleMode = other.EnableToggleMode;
        HoldKey = other.HoldKey;
        ToggleHotkey = other.ToggleHotkey;
        HorizontalLockToggleHotkey = other.HorizontalLockToggleHotkey;
        LineThickness = other.LineThickness;
        Opacity = other.Opacity;
        LineColor = other.LineColor;
        RunOnStartup = other.RunOnStartup;
        EisuPseudoHoldInitialMarginMs = other.EisuPseudoHoldInitialMarginMs;
        EnableHorizontalLock = other.EnableHorizontalLock;
        HorizontalLockTriggerMode = other.HorizontalLockTriggerMode;
        HorizontalLockHoldKey = other.HorizontalLockHoldKey;
    }
}
