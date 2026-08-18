namespace CrosshairOverlayTool;

internal sealed class AppController : ApplicationContext
{
    private readonly SettingsManager _settingsManager;
    private readonly StartupRegistrationManager _startupRegistrationManager;
    private readonly AppSettings _settings;
    private readonly TrayManager _trayManager;
    private OverlayManager? _overlayManager;
    private CursorConstraintManager? _cursorConstraintManager;
    private InputManager? _inputManager;

    private bool _holdKeyPressed;
    private bool _toggleStateOn;
    private bool _overlayVisible;
    private bool _horizontalLockActive;
    private bool _horizontalLockSuppressedUntilHoldRelease;
    private bool _disposed;

    public AppController()
    {
        _settingsManager = new SettingsManager();
        _startupRegistrationManager = new StartupRegistrationManager("CrosshairOverlayTool");
        _settings = _settingsManager.Load();
        _trayManager = new TrayManager();

        _trayManager.TogglePinnedRequested += OnTrayTogglePinnedRequested;
        _trayManager.ToggleHorizontalLockRequested += OnTrayToggleHorizontalLockRequested;
        _trayManager.SettingsRequested += OnTraySettingsRequested;
        _trayManager.ExitRequested += OnTrayExitRequested;

        ApplySettings(_settings, showDialogs: false);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            _trayManager.TogglePinnedRequested -= OnTrayTogglePinnedRequested;
            _trayManager.ToggleHorizontalLockRequested -= OnTrayToggleHorizontalLockRequested;
            _trayManager.SettingsRequested -= OnTraySettingsRequested;
            _trayManager.ExitRequested -= OnTrayExitRequested;
            DisposeManagers();
            _trayManager.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    private void OnHoldKeyStateChanged(object? sender, bool isPressed)
    {
        _holdKeyPressed = isPressed;
        if (!isPressed)
        {
            _horizontalLockSuppressedUntilHoldRelease = false;
        }

        RecomputeState();
    }

    private void OnToggleHotkeyPressed(object? sender, EventArgs e)
    {
        _toggleStateOn = !_toggleStateOn;
        RecomputeState();
    }

    private void OnHorizontalLockToggleHotkeyPressed(object? sender, EventArgs e)
    {
        ToggleHorizontalLockEnabled();
    }

    private void OnEmergencyUnlockRequested(object? sender, EventArgs e)
    {
        if (!_horizontalLockActive)
        {
            return;
        }

        _horizontalLockSuppressedUntilHoldRelease = true;
        RecomputeState();
    }

    private void OnTrayTogglePinnedRequested(object? sender, EventArgs e)
    {
        if (!_settings.EnableToggleMode)
        {
            _trayManager.ShowNotification("Crosshair Overlay Tool", "トグルモードが無効です。", ToolTipIcon.Warning);
            return;
        }

        _toggleStateOn = !_toggleStateOn;
        RecomputeState();
    }

    private void OnTraySettingsRequested(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_settings);
        if (settingsForm.ShowDialog() != DialogResult.OK || settingsForm.ResultSettings is null)
        {
            return;
        }

        ApplySettings(settingsForm.ResultSettings, showDialogs: true);
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        ExitThread();
    }

    private void OnTrayToggleHorizontalLockRequested(object? sender, EventArgs e)
    {
        ToggleHorizontalLockEnabled();
    }

    private void RecomputeState(bool forceApply = false)
    {
        var holdShow = _settings.EnableHoldMode && _holdKeyPressed;
        var toggleShow = _settings.EnableToggleMode && _toggleStateOn;
        var shouldShow = holdShow || toggleShow;
        if (!holdShow)
        {
            _horizontalLockSuppressedUntilHoldRelease = false;
        }

        var shouldLock = ShouldEnableHorizontalLock(holdShow);

        if (!forceApply && shouldShow == _overlayVisible && shouldLock == _horizontalLockActive)
        {
            UpdateTrayState();
            return;
        }

        if (forceApply || shouldShow != _overlayVisible)
        {
            _overlayVisible = shouldShow;

            if (shouldShow)
            {
                _overlayManager?.ShowOverlay();
            }
            else
            {
                _overlayManager?.HideOverlay();
            }
        }

        if (forceApply || shouldLock != _horizontalLockActive)
        {
            _horizontalLockActive = shouldLock;
            _cursorConstraintManager?.SetAxisLock(shouldLock);
        }

        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        _trayManager.UpdateState(
            new TrayState(
                _overlayVisible,
                _settings.EnableToggleMode,
                _toggleStateOn,
                _settings.EnableHoldMode,
                _holdKeyPressed,
                _settings.EnableHorizontalLock,
                _horizontalLockActive));
    }

    private bool ApplySettings(AppSettings newSettings, bool showDialogs)
    {
        var workingCopy = newSettings.Clone();
        var previousRunOnStartup = _settings.RunOnStartup;
        var settingsChanged = NormalizeInputSettings(workingCopy);
        var lineColor = ResolveLineColor(workingCopy, ref settingsChanged);

        var startupSyncFailed = false;
        if (workingCopy.RunOnStartup != previousRunOnStartup
            && !_startupRegistrationManager.Sync(workingCopy.RunOnStartup))
        {
            startupSyncFailed = true;
            workingCopy.RunOnStartup = previousRunOnStartup;
        }

        var holdKeyBinding = KeyBindingParser.ResolveHoldKey(workingCopy.HoldKey, out _);
        var toggleHotkeyBinding = KeyBindingParser.ResolveToggleHotkey(workingCopy.ToggleHotkey, out _);
        var horizontalLockToggleHotkeyBinding = KeyBindingParser.ResolveToggleHotkey(workingCopy.HorizontalLockToggleHotkey, out _);
        var nextOverlayManager = new OverlayManager(workingCopy.LineThickness, workingCopy.Opacity, lineColor);
        var nextCursorConstraintManager = new CursorConstraintManager();
        var nextInputManager = new InputManager(
            workingCopy.EnableHoldMode,
            holdKeyBinding,
            workingCopy.EnableToggleMode,
            toggleHotkeyBinding,
            horizontalLockToggleHotkeyBinding,
            workingCopy.EisuPseudoHoldInitialMarginMs,
            workingCopy.EnableHorizontalLock,
            false,
            holdKeyBinding);

        nextInputManager.HoldKeyStateChanged += OnHoldKeyStateChanged;
        nextInputManager.ToggleHotkeyPressed += OnToggleHotkeyPressed;
        nextInputManager.HorizontalLockToggleHotkeyPressed += OnHorizontalLockToggleHotkeyPressed;
        nextInputManager.EmergencyUnlockRequested += OnEmergencyUnlockRequested;

        DisposeManagers();
        _overlayManager = nextOverlayManager;
        _cursorConstraintManager = nextCursorConstraintManager;
        _inputManager = nextInputManager;
        _settings.CopyFrom(workingCopy);

        if (!_settings.EnableHoldMode)
        {
            _holdKeyPressed = false;
            _horizontalLockSuppressedUntilHoldRelease = false;
        }

        if (!_settings.EnableToggleMode)
        {
            _toggleStateOn = false;
        }

        var saveSucceeded = _settingsManager.TrySave(_settings);
        RecomputeState(forceApply: true);

        if (startupSyncFailed)
        {
            _trayManager.ShowNotification("Crosshair Overlay Tool", "スタートアップ設定の更新に失敗しました。", ToolTipIcon.Warning);
            if (showDialogs)
            {
                MessageBox.Show(
                    "スタートアップ設定の更新に失敗したため、その項目は変更されませんでした。",
                    "Crosshair Overlay Tool",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        else if (previousRunOnStartup != _settings.RunOnStartup)
        {
            _trayManager.ShowNotification(
                "Crosshair Overlay Tool",
                _settings.RunOnStartup ? "スタートアップに登録しました。" : "スタートアップ登録を解除しました。");
        }

        if (!saveSucceeded)
        {
            _trayManager.ShowNotification("Crosshair Overlay Tool", "設定ファイルの保存に失敗しました。", ToolTipIcon.Error);
            if (showDialogs)
            {
                MessageBox.Show(
                    "設定は反映されましたが、settings.json の保存に失敗しました。",
                    "Crosshair Overlay Tool",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        return saveSucceeded && !startupSyncFailed;
    }

    private void DisposeManagers()
    {
        if (_inputManager is not null)
        {
            _inputManager.HoldKeyStateChanged -= OnHoldKeyStateChanged;
            _inputManager.ToggleHotkeyPressed -= OnToggleHotkeyPressed;
            _inputManager.HorizontalLockToggleHotkeyPressed -= OnHorizontalLockToggleHotkeyPressed;
            _inputManager.EmergencyUnlockRequested -= OnEmergencyUnlockRequested;
            _inputManager.Dispose();
            _inputManager = null;
        }

        _cursorConstraintManager?.Dispose();
        _cursorConstraintManager = null;
        _overlayManager?.Dispose();
        _overlayManager = null;
    }

    private static bool NormalizeInputSettings(AppSettings settings)
    {
        var settingsChanged = false;

        var holdKey = KeyBindingParser.ResolveHoldKey(settings.HoldKey, out var holdKeyFallback);
        if (holdKeyFallback || !string.Equals(settings.HoldKey, holdKey.DisplayName, StringComparison.Ordinal))
        {
            settings.HoldKey = holdKey.DisplayName;
            settingsChanged = true;
        }

        var toggleHotkey = KeyBindingParser.ResolveToggleHotkey(settings.ToggleHotkey, out var hotkeyFallback);
        if (hotkeyFallback
            || !string.Equals(settings.ToggleHotkey, toggleHotkey.DisplayText, StringComparison.Ordinal))
        {
            settings.ToggleHotkey = toggleHotkey.DisplayText;
            settingsChanged = true;
        }

        var horizontalLockToggleHotkey = KeyBindingParser.ResolveToggleHotkey(
            settings.HorizontalLockToggleHotkey,
            out var horizontalLockHotkeyFallback);
        if (horizontalLockHotkeyFallback
            || !string.Equals(settings.HorizontalLockToggleHotkey, horizontalLockToggleHotkey.DisplayText, StringComparison.Ordinal))
        {
            settings.HorizontalLockToggleHotkey = horizontalLockToggleHotkey.DisplayText;
            settingsChanged = true;
        }

        if (string.Equals(settings.HorizontalLockToggleHotkey, settings.ToggleHotkey, StringComparison.Ordinal))
        {
            settings.HorizontalLockToggleHotkey = ResolveDistinctHorizontalLockToggleHotkey(settings.ToggleHotkey);
            settingsChanged = true;
        }

        if (!string.Equals(settings.HorizontalLockTriggerMode, AppSettings.HorizontalLockTriggerModeSameHoldKey, StringComparison.Ordinal))
        {
            settings.HorizontalLockTriggerMode = AppSettings.HorizontalLockTriggerModeSameHoldKey;
            settingsChanged = true;
        }

        if (!string.Equals(settings.HorizontalLockHoldKey, settings.HoldKey, StringComparison.Ordinal))
        {
            settings.HorizontalLockHoldKey = settings.HoldKey;
            settingsChanged = true;
        }

        return settingsChanged;
    }

    private static Color ResolveLineColor(AppSettings settings, ref bool settingsChanged)
    {
        if (ColorUtilities.TryParseHtmlColor(settings.LineColor, out var color))
        {
            return color;
        }

        settingsChanged = true;
        settings.LineColor = ColorUtilities.DefaultLineColorHex;
        return ColorTranslator.FromHtml(ColorUtilities.DefaultLineColorHex);
    }

    private bool ShouldEnableHorizontalLock(bool holdShow)
    {
        return _settings.EnableHorizontalLock && holdShow && !_horizontalLockSuppressedUntilHoldRelease;
    }

    private void ToggleHorizontalLockEnabled()
    {
        _settings.EnableHorizontalLock = !_settings.EnableHorizontalLock;
        _horizontalLockSuppressedUntilHoldRelease = false;
        var saved = _settingsManager.TrySave(_settings);
        RecomputeState(forceApply: true);

        _trayManager.ShowNotification(
            "Crosshair Overlay Tool",
            _settings.EnableHorizontalLock
                ? $"軸拘束を有効にしました。({_settings.HorizontalLockToggleHotkey})"
                : $"軸拘束を無効にしました。({_settings.HorizontalLockToggleHotkey})");

        if (!saved)
        {
            _trayManager.ShowNotification("Crosshair Overlay Tool", "設定ファイルの保存に失敗しました。", ToolTipIcon.Warning);
        }
    }

    private static string ResolveDistinctHorizontalLockToggleHotkey(string toggleHotkey)
    {
        if (!string.Equals(toggleHotkey, AppSettings.DefaultHorizontalLockToggleHotkeyText, StringComparison.Ordinal))
        {
            return AppSettings.DefaultHorizontalLockToggleHotkeyText;
        }

        return KeyBindingParser.DefaultToggleHotkeyText;
    }
}
