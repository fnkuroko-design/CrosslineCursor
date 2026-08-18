namespace CrosshairOverlayTool;

internal sealed class HoldKeyTracker
{
    private const int EisuPseudoHoldDuplicateWindowMs = 20;
    private const int EisuPseudoHoldAdaptiveThresholdMs = 120;
    private const int EisuPseudoHoldReleaseMarginMs = 55;
    private const int EisuPseudoHoldMinLeaseMs = 110;

    private readonly HoldKeyBinding _binding;
    private readonly bool _usePseudoHoldForEisu;
    private readonly int _pseudoHoldSystemInitialLeaseMs;
    private readonly Action<bool> _stateChangedCallback;

    private bool _pressed;
    private uint _activeScanCode;
    private uint _activeIdentity;
    private int _holdReleaseMissCount;
    private long _pseudoHoldExpiresAtTick;
    private long _lastPseudoHoldKeepAliveTick;
    private int _pseudoHoldLeaseMs;

    public HoldKeyTracker(HoldKeyBinding binding, int eisuPseudoHoldInitialMarginMs, Action<bool> stateChangedCallback)
    {
        _binding = binding;
        _usePseudoHoldForEisu = _binding.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC;
        _pseudoHoldSystemInitialLeaseMs = GetInitialPseudoHoldLeaseMs(eisuPseudoHoldInitialMarginMs);
        _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
        _stateChangedCallback = stateChangedCallback;
    }

    public bool IsPressed => _pressed;

    public bool UsesPseudoHoldForEisu => _usePseudoHoldForEisu;

    public void ProcessRawKeyboard(RawKeyboardEventArgs e)
    {
        if (_usePseudoHoldForEisu
            && (e.Flags & NativeMethods.RI_KEY_BREAK) != NativeMethods.RI_KEY_BREAK
            && e.VirtualKey == _binding.VirtualKey)
        {
            RegisterPseudoHoldKeepAlive();
            if (!_pressed)
            {
                SetPressed(true, e.MakeCode, e.KeyIdentity);
            }
            else
            {
                ExtendPseudoHoldLease();
            }

            return;
        }

        if (!_pressed || (e.Flags & NativeMethods.RI_KEY_BREAK) != NativeMethods.RI_KEY_BREAK)
        {
            return;
        }

        if (_activeIdentity == 0 || e.KeyIdentity != _activeIdentity)
        {
            return;
        }

        SetPressed(false, 0, 0);
    }

    public void ProcessKeyMessage(uint message, NativeMethods.KBDLLHOOKSTRUCT keyboardData)
    {
        var isKeyUp = IsKeyUpEvent(message, keyboardData);
        var isKeyDown = !isKeyUp
            && (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN);

        if (_usePseudoHoldForEisu && isKeyDown && keyboardData.vkCode == _binding.VirtualKey)
        {
            RegisterPseudoHoldKeepAlive();
            if (!_pressed)
            {
                SetPressed(true, keyboardData.scanCode, CreateHookKeyIdentity(keyboardData));
            }
            else
            {
                ExtendPseudoHoldLease();
            }

            return;
        }

        if (!_pressed)
        {
            if (isKeyDown && keyboardData.vkCode == _binding.VirtualKey)
            {
                SetPressed(true, keyboardData.scanCode, CreateHookKeyIdentity(keyboardData));
            }

            return;
        }

        if (isKeyUp && MatchesReleaseEvent(keyboardData, _binding.VirtualKey, _activeScanCode))
        {
            SetPressed(false, 0, 0);
        }
    }

    public void ProcessTimerTick()
    {
        if (!_pressed)
        {
            return;
        }

        if (_usePseudoHoldForEisu)
        {
            if (Environment.TickCount64 >= _pseudoHoldExpiresAtTick)
            {
                SetPressed(false, 0, 0);
            }

            return;
        }

        if (IsCurrentlyDown())
        {
            _holdReleaseMissCount = 0;
            return;
        }

        _holdReleaseMissCount++;
        if (_holdReleaseMissCount < 2)
        {
            return;
        }

        SetPressed(false, 0, 0);
    }

    public void BridgeChordTransition()
    {
        if (!_pressed || !_usePseudoHoldForEisu)
        {
            return;
        }

        _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
        ExtendPseudoHoldLease();
    }

    public void KeepAliveExternal()
    {
        if (!_pressed || !_usePseudoHoldForEisu)
        {
            return;
        }

        ExtendPseudoHoldLease();
    }

    private void SetPressed(bool isPressed, uint activeScanCode, uint activeIdentity)
    {
        if (_pressed == isPressed
            && _activeScanCode == activeScanCode
            && _activeIdentity == activeIdentity)
        {
            return;
        }

        _pressed = isPressed;
        _activeScanCode = activeScanCode;
        _activeIdentity = activeIdentity;
        _holdReleaseMissCount = 0;

        if (isPressed)
        {
            if (_usePseudoHoldForEisu)
            {
                _lastPseudoHoldKeepAliveTick = 0;
                _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
                ExtendPseudoHoldLease();
            }
        }
        else
        {
            _pseudoHoldExpiresAtTick = 0;
            _lastPseudoHoldKeepAliveTick = 0;
            _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
        }

        _stateChangedCallback(isPressed);
    }

    private void ExtendPseudoHoldLease()
    {
        _pseudoHoldExpiresAtTick = Environment.TickCount64 + _pseudoHoldLeaseMs;
    }

    private void RegisterPseudoHoldKeepAlive()
    {
        var now = Environment.TickCount64;
        if (_lastPseudoHoldKeepAliveTick == 0)
        {
            _lastPseudoHoldKeepAliveTick = now;
            return;
        }

        var observedInterval = now - _lastPseudoHoldKeepAliveTick;
        if (observedInterval <= EisuPseudoHoldDuplicateWindowMs)
        {
            return;
        }

        if (observedInterval <= EisuPseudoHoldAdaptiveThresholdMs)
        {
            _pseudoHoldLeaseMs = (int)Math.Clamp(
                observedInterval + EisuPseudoHoldReleaseMarginMs,
                EisuPseudoHoldMinLeaseMs,
                _pseudoHoldSystemInitialLeaseMs);
        }

        _lastPseudoHoldKeepAliveTick = now;
    }

    private bool IsCurrentlyDown()
    {
        return _binding.VirtualKey switch
        {
            NativeMethods.VK_LCONTROL => IsAnyKeyPressed(NativeMethods.VK_LCONTROL, NativeMethods.VK_CONTROL),
            NativeMethods.VK_RCONTROL => IsAnyKeyPressed(NativeMethods.VK_RCONTROL, NativeMethods.VK_CONTROL),
            NativeMethods.VK_LMENU => IsAnyKeyPressed(NativeMethods.VK_LMENU, NativeMethods.VK_MENU),
            NativeMethods.VK_RMENU => IsAnyKeyPressed(NativeMethods.VK_RMENU, NativeMethods.VK_MENU),
            NativeMethods.VK_LSHIFT => IsAnyKeyPressed(NativeMethods.VK_LSHIFT, NativeMethods.VK_SHIFT),
            NativeMethods.VK_RSHIFT => IsAnyKeyPressed(NativeMethods.VK_RSHIFT, NativeMethods.VK_SHIFT),
            NativeMethods.VK_DBE_ALPHANUMERIC => IsAnyKeyPressed(NativeMethods.VK_DBE_ALPHANUMERIC),
            _ => IsAnyKeyPressed(_binding.VirtualKey)
        };
    }

    private static bool MatchesReleaseEvent(
        NativeMethods.KBDLLHOOKSTRUCT keyboardData,
        uint configuredVirtualKey,
        uint activeScanCode)
    {
        if (configuredVirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC)
        {
            return activeScanCode != 0 && keyboardData.scanCode == activeScanCode;
        }

        return keyboardData.vkCode == configuredVirtualKey;
    }

    private static bool IsKeyUpEvent(uint message, NativeMethods.KBDLLHOOKSTRUCT keyboardData)
    {
        return message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP
            || (keyboardData.flags & NativeMethods.LLKHF_UP) == NativeMethods.LLKHF_UP;
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

    private static int GetInitialPseudoHoldLeaseMs(int eisuPseudoHoldInitialMarginMs)
    {
        var keyboardDelay = System.Windows.Forms.SystemInformation.KeyboardDelay;
        return 250 + (keyboardDelay * 250) + eisuPseudoHoldInitialMarginMs;
    }

    private static uint CreateHookKeyIdentity(NativeMethods.KBDLLHOOKSTRUCT keyboardData)
    {
        var identity = keyboardData.scanCode & 0xFFFF;
        if ((keyboardData.flags & NativeMethods.LLKHF_EXTENDED) == NativeMethods.LLKHF_EXTENDED)
        {
            identity |= 1u << 16;
        }

        return identity;
    }
}

internal readonly record struct RawKeyboardEventArgs(
    ushort VirtualKey,
    ushort MakeCode,
    ushort Flags,
    uint KeyIdentity);
