namespace CrosshairOverlayTool;

internal sealed class HoldKeyTracker
{
    private const int EisuPseudoHoldDuplicateWindowMs = 20;
    private const int EisuPseudoHoldReleaseMarginMs = 55;
    private const int EisuPseudoHoldMinLeaseMs = 110;
    private const int EisuPseudoHoldMaxRepeatLeaseMs = 1000;

    private readonly HoldKeyBinding _binding;
    private readonly bool _usePseudoHoldForEisu;
    private readonly int _pseudoHoldSystemInitialLeaseMs;
    private readonly int _pseudoHoldSystemRepeatLeaseMs;
    private readonly Action<bool> _stateChangedCallback;
    private readonly Func<long> _getTickCount;

    private bool _pressed;
    private uint _activeScanCode;
    private uint _activeIdentity;
    private int _holdReleaseMissCount;
    private long _pseudoHoldExpiresAtTick;
    private int _pseudoHoldLeaseMs;
    private RepeatObservation _hookRepeat;
    private RepeatObservation _rawRepeat;

    public HoldKeyTracker(HoldKeyBinding binding, int eisuPseudoHoldInitialMarginMs, Action<bool> stateChangedCallback)
        : this(binding, eisuPseudoHoldInitialMarginMs, stateChangedCallback,
            () => Environment.TickCount64,
            System.Windows.Forms.SystemInformation.KeyboardDelay,
            System.Windows.Forms.SystemInformation.KeyboardSpeed)
    {
    }

    internal HoldKeyTracker(HoldKeyBinding binding, int eisuPseudoHoldInitialMarginMs,
        Action<bool> stateChangedCallback, Func<long> getTickCount, int keyboardDelay, int keyboardSpeed)
    {
        _binding = binding;
        _usePseudoHoldForEisu = _binding.VirtualKey == NativeMethods.VK_DBE_ALPHANUMERIC;
        // KeyboardSpeed ranges from about 2.5 to 30 repeats/sec; hardware can differ by 20%.
        var repeatsPerSecond = 2.5 + (Math.Clamp(keyboardSpeed, 0, 31) * 27.5 / 31);
        _pseudoHoldSystemRepeatLeaseMs = Math.Max(EisuPseudoHoldMinLeaseMs,
            (int)Math.Ceiling(1000 / repeatsPerSecond * 1.25) + EisuPseudoHoldReleaseMarginMs);
        _pseudoHoldSystemInitialLeaseMs = Math.Max(_pseudoHoldSystemRepeatLeaseMs,
            250 + Math.Clamp(keyboardDelay, 0, 3) * 250
            + Math.Clamp(eisuPseudoHoldInitialMarginMs, 0, 60) + EisuPseudoHoldReleaseMarginMs);
        _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
        _stateChangedCallback = stateChangedCallback;
        _getTickCount = getTickCount;
    }

    public bool IsPressed => _pressed;

    public bool UsesPseudoHoldForEisu => _usePseudoHoldForEisu;

    public void ProcessRawKeyboard(RawKeyboardEventArgs e)
    {
        if (_usePseudoHoldForEisu)
        {
            // Eisu may emit down/up pairs for each repeat. Neither input path's up
            // is a reliable physical release; only the keep-alive deadline releases it.
            if ((e.Flags & NativeMethods.RI_KEY_BREAK) == 0
                && MatchesPseudoHoldEvent(e.VirtualKey, e.KeyIdentity))
            {
                ProcessPseudoHoldDown(e.MakeCode, e.KeyIdentity, ref _rawRepeat);
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

        if (_usePseudoHoldForEisu)
        {
            var identity = CreateHookKeyIdentity(keyboardData);
            if (isKeyDown && MatchesPseudoHoldEvent(keyboardData.vkCode, identity))
            {
                ProcessPseudoHoldDown(keyboardData.scanCode, identity, ref _hookRepeat);
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

        if (isKeyUp && keyboardData.vkCode == _binding.VirtualKey)
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
            if (_getTickCount() >= _pseudoHoldExpiresAtTick)
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
                _hookRepeat = default;
                _rawRepeat = default;
                _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
                ExtendPseudoHoldLease();
            }
        }
        else
        {
            _pseudoHoldExpiresAtTick = 0;
            _hookRepeat = default;
            _rawRepeat = default;
            _pseudoHoldLeaseMs = _pseudoHoldSystemInitialLeaseMs;
        }

        _stateChangedCallback(isPressed);
    }

    private void ExtendPseudoHoldLease()
    {
        // A delayed duplicate or a new interval estimate must never shorten a lease.
        _pseudoHoldExpiresAtTick = Math.Max(_pseudoHoldExpiresAtTick, _getTickCount() + _pseudoHoldLeaseMs);
    }

    internal bool MatchesPseudoHoldEvent(uint virtualKey, uint identity)
    {
        return _usePseudoHoldForEisu && (virtualKey == _binding.VirtualKey
            // Raw Input may retain CapsLock while the IME-facing hook reports Eisu.
            // Only the physical key already identified as Eisu may renew the lease.
            || (_pressed && (virtualKey is NativeMethods.VK_PROCESSKEY or NativeMethods.VK_CAPITAL)
                && _activeIdentity != 0 && identity == _activeIdentity));
    }

    private void ProcessPseudoHoldDown(uint scanCode, uint identity, ref RepeatObservation observation)
    {
        if (!_pressed)
        {
            SetPressed(true, scanCode, identity);
        }
        else if (_activeIdentity == 0 && identity != 0)
        {
            _activeScanCode = scanCode;
            _activeIdentity = identity;
        }

        var now = _getTickCount();
        if (observation.LastTick is long lastTick)
        {
            var interval = now - lastTick;
            if (interval <= EisuPseudoHoldDuplicateWindowMs)
            {
                return;
            }

            // The first interval includes the initial typematic delay, not repeat speed.
            // Measure hook and Raw Input separately so their duplicate reports cannot
            // be mistaken for faster keyboard repeats.
            if (observation.HasRepeated)
            {
                observation.MaxRepeatIntervalMs = Math.Max(observation.MaxRepeatIntervalMs,
                    (int)Math.Min(interval, EisuPseudoHoldMaxRepeatLeaseMs - EisuPseudoHoldReleaseMarginMs));
            }

            observation.HasRepeated = true;
            _pseudoHoldLeaseMs = Math.Max(_pseudoHoldSystemRepeatLeaseMs,
                Math.Max(_hookRepeat.MaxRepeatIntervalMs, _rawRepeat.MaxRepeatIntervalMs)
                + EisuPseudoHoldReleaseMarginMs);
        }

        observation.LastTick = now;
        ExtendPseudoHoldLease();
    }

    private struct RepeatObservation
    {
        public long? LastTick;
        public bool HasRepeated;
        public int MaxRepeatIntervalMs;
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

    internal static uint CreateHookKeyIdentity(NativeMethods.KBDLLHOOKSTRUCT keyboardData)
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
