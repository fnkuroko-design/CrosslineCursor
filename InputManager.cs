using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CrosshairOverlayTool;

internal sealed class InputManager : IDisposable
{
    private const int ToggleHotkeyId = 1;
    private const int HorizontalLockToggleHotkeyId = 2;
    private const int HoldKeyStateMessage = NativeMethods.WM_APP + 1;
    private const int ToggleHotkeyMessage = NativeMethods.WM_APP + 2;
    private const int LockHoldKeyStateMessage = NativeMethods.WM_APP + 3;
    private const int EmergencyUnlockMessage = NativeMethods.WM_APP + 4;
    private const int HorizontalLockToggleHotkeyMessage = NativeMethods.WM_APP + 5;
    private const uint ToggleHotkeyDebounceMs = 150;

    private readonly bool _toggleModeEnabled;
    private readonly HotkeyBinding _toggleHotkey;
    private readonly bool _toggleHotkeyHandledByHook;
    private readonly HotkeyBinding _horizontalLockToggleHotkey;
    private readonly bool _horizontalLockToggleHotkeyHandledByHook;
    private readonly bool _keyboardHookRequired;
    private readonly bool _horizontalLockEnabled;
    private readonly System.Windows.Forms.Timer _holdStateTimer;
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly HoldKeyTracker? _primaryHoldTracker;
    private readonly HoldKeyTracker? _lockHoldTracker;
    private readonly bool _rawKeyboardRequired;

    private IntPtr _keyboardHook = IntPtr.Zero;
    private uint _lastToggleHotkeyScanCode;
    private long _lastToggleHotkeyTick;
    private uint _lastHorizontalLockToggleHotkeyScanCode;
    private long _lastHorizontalLockToggleHotkeyTick;
    private bool _disposed;

    public InputManager(
        bool enableHoldMode,
        HoldKeyBinding holdKey,
        bool enableToggleMode,
        HotkeyBinding toggleHotkey,
        HotkeyBinding horizontalLockToggleHotkey,
        int eisuPseudoHoldInitialMarginMs,
        bool enableHorizontalLock,
        bool useSeparateLockHoldKey,
        HoldKeyBinding lockHoldKey)
    {
        _toggleModeEnabled = enableToggleMode;
        _toggleHotkey = toggleHotkey;
        _horizontalLockToggleHotkey = horizontalLockToggleHotkey;
        _horizontalLockEnabled = enableHoldMode;
        _toggleHotkeyHandledByHook = _toggleModeEnabled && _toggleHotkey.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC;
        _horizontalLockToggleHotkeyHandledByHook =
            _horizontalLockEnabled && _horizontalLockToggleHotkey.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC;

        _holdStateTimer = new System.Windows.Forms.Timer { Interval = 20 };
        _holdStateTimer.Tick += OnHoldStateTimerTick;
        _keyboardProc = KeyboardHookCallback;
        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;
        _hotkeyWindow.HoldKeyStateReceived += OnHoldKeyStateReceived;
        _hotkeyWindow.LockHoldKeyStateReceived += OnLockHoldKeyStateReceived;
        _hotkeyWindow.ToggleHotkeyReceived += OnToggleHotkeyReceived;
        _hotkeyWindow.HorizontalLockToggleHotkeyReceived += OnHorizontalLockToggleHotkeyReceived;
        _hotkeyWindow.EmergencyUnlockReceived += OnEmergencyUnlockReceived;
        _hotkeyWindow.RawKeyboardReceived += OnRawKeyboardReceived;

        if (enableHoldMode)
        {
            _primaryHoldTracker = new HoldKeyTracker(
                holdKey,
                eisuPseudoHoldInitialMarginMs,
                isPressed => PostStateMessage(HoldKeyStateMessage, isPressed));
        }

        if (_horizontalLockEnabled && useSeparateLockHoldKey)
        {
            _lockHoldTracker = new HoldKeyTracker(
                lockHoldKey,
                eisuPseudoHoldInitialMarginMs,
                isPressed =>
                {
                    if (_primaryHoldTracker is not null
                        && _primaryHoldTracker.UsesPseudoHoldForEisu
                        && _primaryHoldTracker.IsPressed)
                    {
                        _primaryHoldTracker.BridgeChordTransition();
                    }

                    PostStateMessage(LockHoldKeyStateMessage, isPressed);
                });
        }

        _rawKeyboardRequired =
            _primaryHoldTracker is not null
            || _lockHoldTracker is not null
            || _toggleHotkeyHandledByHook
            || _horizontalLockToggleHotkeyHandledByHook;

        if (_rawKeyboardRequired)
        {
            _hotkeyWindow.TryRegisterRawKeyboardInput();
        }

        _keyboardHookRequired =
            _primaryHoldTracker is not null
            || _lockHoldTracker is not null
            || _toggleHotkeyHandledByHook
            || _horizontalLockToggleHotkeyHandledByHook
            || _horizontalLockEnabled;

        if (_keyboardHookRequired)
        {
            TryInstallKeyboardHook();
        }

        if (_toggleModeEnabled && !_toggleHotkeyHandledByHook)
        {
            TryRegisterToggleHotkey();
        }

        if (_horizontalLockEnabled && !_horizontalLockToggleHotkeyHandledByHook)
        {
            TryRegisterHorizontalLockToggleHotkey();
        }
    }

    public event EventHandler<bool>? HoldKeyStateChanged;

    public event EventHandler<bool>? LockHoldKeyStateChanged;

    public event EventHandler? ToggleHotkeyPressed;

    public event EventHandler? HorizontalLockToggleHotkeyPressed;

    public event EventHandler? EmergencyUnlockRequested;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_toggleModeEnabled && !_toggleHotkeyHandledByHook)
        {
            NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, ToggleHotkeyId);
        }

        NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, HorizontalLockToggleHotkeyId);

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        _holdStateTimer.Stop();
        _holdStateTimer.Tick -= OnHoldStateTimerTick;
        _holdStateTimer.Dispose();
        _hotkeyWindow.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyWindow.HoldKeyStateReceived -= OnHoldKeyStateReceived;
        _hotkeyWindow.LockHoldKeyStateReceived -= OnLockHoldKeyStateReceived;
        _hotkeyWindow.ToggleHotkeyReceived -= OnToggleHotkeyReceived;
        _hotkeyWindow.HorizontalLockToggleHotkeyReceived -= OnHorizontalLockToggleHotkeyReceived;
        _hotkeyWindow.EmergencyUnlockReceived -= OnEmergencyUnlockReceived;
        _hotkeyWindow.RawKeyboardReceived -= OnRawKeyboardReceived;
        _hotkeyWindow.Dispose();
    }

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (_disposed)
        {
            return;
        }

        if (hotkeyId == ToggleHotkeyId)
        {
            ToggleHotkeyPressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (hotkeyId == HorizontalLockToggleHotkeyId)
        {
            HorizontalLockToggleHotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnHoldKeyStateReceived(object? sender, bool isPressed)
    {
        if (_disposed)
        {
            return;
        }

        HoldKeyStateChanged?.Invoke(this, isPressed);
    }

    private void OnLockHoldKeyStateReceived(object? sender, bool isPressed)
    {
        if (_disposed)
        {
            return;
        }

        LockHoldKeyStateChanged?.Invoke(this, isPressed);
    }

    private void OnToggleHotkeyReceived(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        ToggleHotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private void OnHorizontalLockToggleHotkeyReceived(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        HorizontalLockToggleHotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private void OnEmergencyUnlockReceived(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        EmergencyUnlockRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRawKeyboardReceived(object? sender, RawKeyboardEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _primaryHoldTracker?.ProcessRawKeyboard(e);
        _lockHoldTracker?.ProcessRawKeyboard(e);
        ProcessToggleHotkeyRawInput(e);
        ProcessHorizontalLockToggleHotkeyRawInput(e);
        EnsureHoldTimerRunningIfNeeded();
    }

    private void OnHoldStateTimerTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            _holdStateTimer.Stop();
            return;
        }

        if (_primaryHoldTracker is not null
            && _lockHoldTracker is not null
            && _primaryHoldTracker.UsesPseudoHoldForEisu
            && _primaryHoldTracker.IsPressed
            && _lockHoldTracker.IsPressed)
        {
            _primaryHoldTracker.KeepAliveExternal();
        }

        _primaryHoldTracker?.ProcessTimerTick();
        _lockHoldTracker?.ProcessTimerTick();

        if (!IsAnyHoldTrackerPressed())
        {
            _holdStateTimer.Stop();
        }
    }

    private void TryInstallKeyboardHook()
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            var moduleName = currentProcess.MainModule?.ModuleName;
            var moduleHandle = NativeMethods.GetModuleHandle(moduleName);
            _keyboardHook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _keyboardProc,
                moduleHandle,
                0);

            if (_keyboardHook == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Failed to install keyboard hook: {ex}");
        }
    }

    private void TryRegisterToggleHotkey()
    {
        if (NativeMethods.RegisterHotKey(
            _hotkeyWindow.Handle,
            ToggleHotkeyId,
            _toggleHotkey.Modifiers,
            _toggleHotkey.VirtualKey))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        Debug.WriteLine($"Failed to register toggle hotkey. Win32Error={error}");
    }

    private void TryRegisterHorizontalLockToggleHotkey()
    {
        if (NativeMethods.RegisterHotKey(
            _hotkeyWindow.Handle,
            HorizontalLockToggleHotkeyId,
            _horizontalLockToggleHotkey.Modifiers,
            _horizontalLockToggleHotkey.VirtualKey))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        Debug.WriteLine($"Failed to register horizontal lock toggle hotkey. Win32Error={error}");
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _keyboardHookRequired)
        {
            var message = unchecked((uint)wParam.ToInt64());
            if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN
                or NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
            {
                var keyboardData = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                _primaryHoldTracker?.ProcessKeyMessage(message, keyboardData);
                _lockHoldTracker?.ProcessKeyMessage(message, keyboardData);
                ProcessToggleHotkeyMessage(message, keyboardData);
                ProcessHorizontalLockToggleHotkeyMessage(message, keyboardData);
                ProcessEmergencyUnlockMessage(message, keyboardData);
                EnsureHoldTimerRunningIfNeeded();
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void ProcessToggleHotkeyMessage(uint message, NativeMethods.KBDLLHOOKSTRUCT keyboardData)
    {
        if (!_toggleHotkeyHandledByHook)
        {
            return;
        }

        var isKeyDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        if (!isKeyDown || !IsHotkeyPrimaryKeyEvent(keyboardData, _toggleHotkey) || !AreRequiredModifiersPressed(_toggleHotkey.Modifiers))
        {
            return;
        }

        TryPostToggleHotkeyMessage(keyboardData.scanCode);
    }

    private void ProcessHorizontalLockToggleHotkeyMessage(uint message, NativeMethods.KBDLLHOOKSTRUCT keyboardData)
    {
        if (!_horizontalLockToggleHotkeyHandledByHook)
        {
            return;
        }

        var isKeyDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        if (!isKeyDown
            || !IsHotkeyPrimaryKeyEvent(keyboardData, _horizontalLockToggleHotkey)
            || !AreRequiredModifiersPressed(_horizontalLockToggleHotkey.Modifiers))
        {
            return;
        }

        TryPostHorizontalLockToggleHotkeyMessage(keyboardData.scanCode);
    }

    private void ProcessEmergencyUnlockMessage(uint message, NativeMethods.KBDLLHOOKSTRUCT keyboardData)
    {
        if (!_horizontalLockEnabled)
        {
            return;
        }

        if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN
            && keyboardData.vkCode == (uint)Keys.Escape)
        {
            NativeMethods.PostMessage(_hotkeyWindow.Handle, EmergencyUnlockMessage, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static bool IsHotkeyPrimaryKeyEvent(NativeMethods.KBDLLHOOKSTRUCT keyboardData, HotkeyBinding hotkeyBinding)
    {
        return keyboardData.vkCode == hotkeyBinding.VirtualKey
            || (hotkeyBinding.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC
                && keyboardData.vkCode == NativeMethods.VK_PROCESSKEY);
    }

    private void ProcessToggleHotkeyRawInput(RawKeyboardEventArgs e)
    {
        if (!_toggleHotkeyHandledByHook
            || (e.Flags & NativeMethods.RI_KEY_BREAK) == NativeMethods.RI_KEY_BREAK
            || !AreRequiredModifiersPressed(_toggleHotkey.Modifiers))
        {
            return;
        }

        if (e.VirtualKey != _toggleHotkey.VirtualKey
            && !(_toggleHotkey.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC && e.VirtualKey == NativeMethods.VK_PROCESSKEY))
        {
            return;
        }

        TryPostToggleHotkeyMessage(e.MakeCode);
    }

    private void ProcessHorizontalLockToggleHotkeyRawInput(RawKeyboardEventArgs e)
    {
        if (!_horizontalLockToggleHotkeyHandledByHook
            || (e.Flags & NativeMethods.RI_KEY_BREAK) == NativeMethods.RI_KEY_BREAK
            || !AreRequiredModifiersPressed(_horizontalLockToggleHotkey.Modifiers))
        {
            return;
        }

        if (e.VirtualKey != _horizontalLockToggleHotkey.VirtualKey
            && !(_horizontalLockToggleHotkey.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC && e.VirtualKey == NativeMethods.VK_PROCESSKEY))
        {
            return;
        }

        TryPostHorizontalLockToggleHotkeyMessage(e.MakeCode);
    }

    private static bool AreRequiredModifiersPressed(uint modifiers)
    {
        return (!HasModifier(modifiers, NativeMethods.MOD_CONTROL) || IsAnyKeyPressed(NativeMethods.VK_LCONTROL, NativeMethods.VK_RCONTROL, NativeMethods.VK_CONTROL))
            && (!HasModifier(modifiers, NativeMethods.MOD_ALT) || IsAnyKeyPressed(NativeMethods.VK_LMENU, NativeMethods.VK_RMENU, NativeMethods.VK_MENU))
            && (!HasModifier(modifiers, NativeMethods.MOD_SHIFT) || IsAnyKeyPressed(NativeMethods.VK_LSHIFT, NativeMethods.VK_RSHIFT, NativeMethods.VK_SHIFT))
            && (!HasModifier(modifiers, NativeMethods.MOD_WIN) || IsAnyKeyPressed(NativeMethods.VK_LWIN, NativeMethods.VK_RWIN));
    }

    private static bool HasModifier(uint modifiers, uint flag)
    {
        return (modifiers & flag) == flag;
    }

    private static bool IsAnyKeyPressed(params uint[] virtualKeys)
    {
        foreach (var virtualKey in virtualKeys)
        {
            if ((NativeMethods.GetAsyncKeyState((int)virtualKey) & 0x8000) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAnyHoldTrackerPressed()
    {
        return (_primaryHoldTracker?.IsPressed ?? false)
            || (_lockHoldTracker?.IsPressed ?? false);
    }

    private void EnsureHoldTimerRunningIfNeeded()
    {
        if (IsAnyHoldTrackerPressed() && !_holdStateTimer.Enabled)
        {
            _holdStateTimer.Start();
        }
    }

    private void TryPostToggleHotkeyMessage(uint scanCode)
    {
        var now = Environment.TickCount64;
        var sameScanCode = _lastToggleHotkeyScanCode != 0 && scanCode == _lastToggleHotkeyScanCode;
        var withinDebounce = sameScanCode && now - _lastToggleHotkeyTick < ToggleHotkeyDebounceMs;
        if (withinDebounce)
        {
            return;
        }

        _lastToggleHotkeyScanCode = scanCode;
        _lastToggleHotkeyTick = now;
        NativeMethods.PostMessage(_hotkeyWindow.Handle, ToggleHotkeyMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private void TryPostHorizontalLockToggleHotkeyMessage(uint scanCode)
    {
        var now = Environment.TickCount64;
        var sameScanCode = _lastHorizontalLockToggleHotkeyScanCode != 0
            && scanCode == _lastHorizontalLockToggleHotkeyScanCode;
        var withinDebounce = sameScanCode && now - _lastHorizontalLockToggleHotkeyTick < ToggleHotkeyDebounceMs;
        if (withinDebounce)
        {
            return;
        }

        _lastHorizontalLockToggleHotkeyScanCode = scanCode;
        _lastHorizontalLockToggleHotkeyTick = now;
        NativeMethods.PostMessage(_hotkeyWindow.Handle, HorizontalLockToggleHotkeyMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private void PostStateMessage(int message, bool isPressed)
    {
        NativeMethods.PostMessage(
            _hotkeyWindow.Handle,
            message,
            isPressed ? new IntPtr(1) : IntPtr.Zero,
            IntPtr.Zero);
    }

    private static uint CreateRawKeyIdentity(NativeMethods.RAWKEYBOARD keyboardData)
    {
        uint identity = (uint)(keyboardData.MakeCode & 0xFFFF);
        if ((keyboardData.Flags & NativeMethods.RI_KEY_E0) == NativeMethods.RI_KEY_E0)
        {
            identity |= 1u << 16;
        }

        if ((keyboardData.Flags & NativeMethods.RI_KEY_E1) == NativeMethods.RI_KEY_E1)
        {
            identity |= 1u << 17;
        }

        return identity;
    }

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public HotkeyWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "CrosshairOverlayTool.HotkeyWindow",
                ExStyle = NativeMethods.WS_EX_TOOLWINDOW
            });
        }

        public event EventHandler<int>? HotkeyPressed;

        public event EventHandler<bool>? HoldKeyStateReceived;

        public event EventHandler<bool>? LockHoldKeyStateReceived;

        public event EventHandler? ToggleHotkeyReceived;

        public event EventHandler? HorizontalLockToggleHotkeyReceived;

        public event EventHandler? EmergencyUnlockReceived;

        public event EventHandler<RawKeyboardEventArgs>? RawKeyboardReceived;

        public void TryRegisterRawKeyboardInput()
        {
            var devices = new[]
            {
                new NativeMethods.RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,
                    usUsage = 0x06,
                    dwFlags = NativeMethods.RIDEV_INPUTSINK,
                    hwndTarget = Handle
                }
            };

            NativeMethods.RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                HotkeyPressed?.Invoke(this, m.WParam.ToInt32());
                return;
            }

            if (m.Msg == HoldKeyStateMessage)
            {
                HoldKeyStateReceived?.Invoke(this, m.WParam != IntPtr.Zero);
                return;
            }

            if (m.Msg == LockHoldKeyStateMessage)
            {
                LockHoldKeyStateReceived?.Invoke(this, m.WParam != IntPtr.Zero);
                return;
            }

            if (m.Msg == ToggleHotkeyMessage)
            {
                ToggleHotkeyReceived?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (m.Msg == HorizontalLockToggleHotkeyMessage)
            {
                HorizontalLockToggleHotkeyReceived?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (m.Msg == EmergencyUnlockMessage)
            {
                EmergencyUnlockReceived?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (m.Msg == NativeMethods.WM_INPUT && TryReadRawKeyboard(m.LParam, out var keyboardData))
            {
                RawKeyboardReceived?.Invoke(
                    this,
                    new RawKeyboardEventArgs(
                        keyboardData.VKey,
                        keyboardData.MakeCode,
                        keyboardData.Flags,
                        CreateRawKeyIdentity(keyboardData)));
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }

        private static bool TryReadRawKeyboard(IntPtr rawInputHandle, out NativeMethods.RAWKEYBOARD keyboardData)
        {
            keyboardData = default;

            uint dataSize = 0;
            var headerSize = (uint)Marshal.SizeOf<NativeMethods.RAWINPUTHEADER>();
            var sizeResult = NativeMethods.GetRawInputData(rawInputHandle, NativeMethods.RID_INPUT, IntPtr.Zero, ref dataSize, headerSize);
            if (sizeResult != 0 || dataSize == 0)
            {
                return false;
            }

            var dataBuffer = Marshal.AllocHGlobal((int)dataSize);
            try
            {
                var readResult = NativeMethods.GetRawInputData(rawInputHandle, NativeMethods.RID_INPUT, dataBuffer, ref dataSize, headerSize);
                if (readResult == uint.MaxValue)
                {
                    return false;
                }

                var rawInput = Marshal.PtrToStructure<NativeMethods.RAWINPUT>(dataBuffer);
                if (rawInput.header.dwType != NativeMethods.RIM_TYPEKEYBOARD)
                {
                    return false;
                }

                keyboardData = rawInput.data.Keyboard;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(dataBuffer);
            }
        }
    }
}
