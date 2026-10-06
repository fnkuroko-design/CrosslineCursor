namespace CrosshairOverlayTool;

// RegisterHotKey(MOD_NOREPEAT) is unreliable for Eisu on some IMEs. Use the same
// bounded pseudo-hold as the overlay, and consume each hold only once per shortcut.
internal sealed class EisuHotkeyTracker
{
    private readonly HoldKeyTracker _holdTracker;
    private bool _triggered;
    private uint? _keyIdentity;

    public EisuHotkeyTracker(int initialMarginMs)
        : this(initialMarginMs, () => Environment.TickCount64,
            System.Windows.Forms.SystemInformation.KeyboardDelay,
            System.Windows.Forms.SystemInformation.KeyboardSpeed)
    {
    }

    internal EisuHotkeyTracker(int initialMarginMs, Func<long> getTickCount, int keyboardDelay, int keyboardSpeed)
    {
        _holdTracker = new HoldKeyTracker(
            new HoldKeyBinding(NativeMethods.VK_DBE_ALPHANUMERIC, "英数"), initialMarginMs,
            pressed =>
            {
                if (!pressed)
                {
                    _triggered = false;
                    _keyIdentity = null;
                }
            },
            getTickCount, keyboardDelay, keyboardSpeed);
    }

    public bool IsPressed => _holdTracker.IsPressed;

    public bool ProcessKeyMessage(uint message, NativeMethods.KBDLLHOOKSTRUCT data, bool modifiersPressed)
    {
        var isDown = (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
            && (data.flags & NativeMethods.LLKHF_UP) == 0;
        if (isDown)
        {
            data.vkCode = NormalizePrimaryKey(data.vkCode, HoldKeyTracker.CreateHookKeyIdentity(data), modifiersPressed);
        }
        var matches = _holdTracker.MatchesPseudoHoldEvent(data.vkCode, HoldKeyTracker.CreateHookKeyIdentity(data));
        _holdTracker.ProcessKeyMessage(message, data);
        return TryTrigger(isDown && matches && modifiersPressed);
    }

    public bool ProcessRawKeyboard(RawKeyboardEventArgs data, bool modifiersPressed)
    {
        if ((data.Flags & NativeMethods.RI_KEY_BREAK) == 0)
        {
            data = data with { VirtualKey = (ushort)NormalizePrimaryKey(data.VirtualKey, data.KeyIdentity, modifiersPressed) };
        }

        var matches = _holdTracker.MatchesPseudoHoldEvent(data.VirtualKey, data.KeyIdentity);
        _holdTracker.ProcessRawKeyboard(data);
        return TryTrigger((data.Flags & NativeMethods.RI_KEY_BREAK) == 0 && matches && modifiersPressed);
    }

    public void ProcessTimerTick() => _holdTracker.ProcessTimerTick();

    private uint NormalizePrimaryKey(uint virtualKey, uint identity, bool modifiersPressed)
    {
        // Retain the existing PROCESSKEY-only shortcut fallback. Once a hold starts,
        // unrelated IME typing must not prolong it and leave the shortcut latched.
        if (virtualKey == NativeMethods.VK_PROCESSKEY
            && ((!_holdTracker.IsPressed && modifiersPressed) || _keyIdentity == identity))
        {
            virtualKey = NativeMethods.VK_DBE_ALPHANUMERIC;
        }

        if (virtualKey == NativeMethods.VK_DBE_ALPHANUMERIC)
        {
            _keyIdentity = identity;
        }

        return virtualKey;
    }

    private bool TryTrigger(bool matches)
    {
        if (!matches || _triggered)
        {
            return false;
        }

        _triggered = true;
        return true;
    }
}
