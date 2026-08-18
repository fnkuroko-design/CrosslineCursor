using System.Globalization;

namespace CrosshairOverlayTool;

internal static class KeyBindingParser
{
    private static readonly Keys DbeAlphanumericKey = unchecked((Keys)NativeMethods.VK_DBE_ALPHANUMERIC);

    public const string DefaultHoldKeyText = "LeftAlt";
    public const string DefaultToggleHotkeyText = "Ctrl+Alt+Shift+Q";
    public static IReadOnlyList<string> SupportedHoldKeyNames { get; } =
        ["LeftAlt", "RightAlt", "LeftCtrl", "RightCtrl", "LeftShift", "RightShift", "CapsLock", "英数"];

    private static readonly Dictionary<string, HoldKeyBinding> HoldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LeftAlt"] = new(NativeMethods.VK_LMENU, "LeftAlt"),
        ["LAlt"] = new(NativeMethods.VK_LMENU, "LeftAlt"),
        ["RightAlt"] = new(NativeMethods.VK_RMENU, "RightAlt"),
        ["RAlt"] = new(NativeMethods.VK_RMENU, "RightAlt"),
        ["LeftCtrl"] = new(NativeMethods.VK_LCONTROL, "LeftCtrl"),
        ["LCtrl"] = new(NativeMethods.VK_LCONTROL, "LeftCtrl"),
        ["RightCtrl"] = new(NativeMethods.VK_RCONTROL, "RightCtrl"),
        ["RCtrl"] = new(NativeMethods.VK_RCONTROL, "RightCtrl"),
        ["LeftShift"] = new(NativeMethods.VK_LSHIFT, "LeftShift"),
        ["LShift"] = new(NativeMethods.VK_LSHIFT, "LeftShift"),
        ["RightShift"] = new(NativeMethods.VK_RSHIFT, "RightShift"),
        ["RShift"] = new(NativeMethods.VK_RSHIFT, "RightShift"),
        ["CapsLock"] = new(NativeMethods.VK_CAPITAL, "CapsLock"),
        ["Caps"] = new(NativeMethods.VK_CAPITAL, "CapsLock"),
        ["英数"] = new(NativeMethods.VK_DBE_ALPHANUMERIC, "英数"),
        ["Eisu"] = new(NativeMethods.VK_DBE_ALPHANUMERIC, "英数")
    };

    private static readonly Dictionary<string, uint> ModifierTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = NativeMethods.MOD_CONTROL,
        ["Control"] = NativeMethods.MOD_CONTROL,
        ["Alt"] = NativeMethods.MOD_ALT,
        ["Shift"] = NativeMethods.MOD_SHIFT,
        ["Win"] = NativeMethods.MOD_WIN,
        ["Windows"] = NativeMethods.MOD_WIN
    };

    public static HoldKeyBinding ResolveHoldKey(string? text, out bool usedFallback)
    {
        usedFallback = !TryParseHoldKey(text, out var binding);
        return usedFallback ? HoldKeys[DefaultHoldKeyText] : binding;
    }

    public static HotkeyBinding ResolveToggleHotkey(string? text, out bool usedFallback)
    {
        usedFallback = !TryParseHotkey(text, out var binding);
        return usedFallback ? CreateDefaultHotkey() : binding;
    }

    public static bool TryParseHoldKey(string? text, out HoldKeyBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(text) && HoldKeys.TryGetValue(text.Trim(), out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    public static bool TryParseHotkey(string? text, out HotkeyBinding binding)
    {
        binding = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        Keys? primaryKey = null;
        var normalizedTokens = new List<string>();

        foreach (var token in tokens)
        {
            if (ModifierTokens.TryGetValue(token, out var modifier))
            {
                modifiers |= modifier;
                normalizedTokens.Add(NormalizeModifierToken(token));
                continue;
            }

            if (primaryKey is not null || !TryParsePrimaryKey(token, out var parsedKey))
            {
                return false;
            }

            primaryKey = parsedKey;
            normalizedTokens.Add(FormatPrimaryKey(parsedKey));
        }

        if (primaryKey is null)
        {
            return false;
        }

        var effectiveModifiers = modifiers & ~NativeMethods.MOD_NOREPEAT;
        if (effectiveModifiers == 0)
        {
            return false;
        }

        binding = new HotkeyBinding(modifiers, (uint)primaryKey.Value, string.Join("+", normalizedTokens));
        return true;
    }

    public static bool TryCreateHotkeyBinding(Keys keyData, out HotkeyBinding binding)
    {
        binding = default;

        var normalizedKeyCode = NormalizeKeyCode(keyData & Keys.KeyCode);
        if (normalizedKeyCode == Keys.None || IsModifierKey(normalizedKeyCode))
        {
            return false;
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        var normalizedTokens = new List<string>();

        if ((keyData & Keys.Control) == Keys.Control)
        {
            modifiers |= NativeMethods.MOD_CONTROL;
            normalizedTokens.Add("Ctrl");
        }

        if ((keyData & Keys.Alt) == Keys.Alt)
        {
            modifiers |= NativeMethods.MOD_ALT;
            normalizedTokens.Add("Alt");
        }

        if ((keyData & Keys.Shift) == Keys.Shift)
        {
            modifiers |= NativeMethods.MOD_SHIFT;
            normalizedTokens.Add("Shift");
        }

        var effectiveModifiers = modifiers & ~NativeMethods.MOD_NOREPEAT;
        if (effectiveModifiers == 0)
        {
            return false;
        }

        normalizedTokens.Add(FormatPrimaryKey(normalizedKeyCode));
        binding = new HotkeyBinding(modifiers, (uint)normalizedKeyCode, string.Join("+", normalizedTokens));
        return true;
    }

    public static bool IsModifierOnlyKeyData(Keys keyData)
    {
        var normalizedKeyCode = NormalizeKeyCode(keyData & Keys.KeyCode);
        return normalizedKeyCode == Keys.None || IsModifierKey(normalizedKeyCode);
    }

    private static HotkeyBinding CreateDefaultHotkey()
    {
        return new HotkeyBinding(
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            (uint)Keys.Q,
            DefaultToggleHotkeyText);
    }

    private static string NormalizeModifierToken(string token)
    {
        if (token.Equals("Control", StringComparison.OrdinalIgnoreCase))
        {
            return "Ctrl";
        }

        if (token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            return "Win";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(token.ToLowerInvariant());
    }

    private static bool TryParsePrimaryKey(string token, out Keys key)
    {
        if (token.Length == 1)
        {
            var ch = token[0];
            if (char.IsLetter(ch))
            {
                key = Keys.A + (char.ToUpperInvariant(ch) - 'A');
                return true;
            }

            if (char.IsDigit(ch))
            {
                key = Keys.D0 + (ch - '0');
                return true;
            }
        }

        switch (token.ToUpperInvariant())
        {
            case "ESC":
                key = Keys.Escape;
                return true;
            case "PGUP":
                key = Keys.PageUp;
                return true;
            case "PGDN":
                key = Keys.PageDown;
                return true;
            case "SPACE":
                key = Keys.Space;
                return true;
        }

        if (token.Equals("英数", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Eisu", StringComparison.OrdinalIgnoreCase))
        {
            key = DbeAlphanumericKey;
            return true;
        }

        if (Enum.TryParse(token, ignoreCase: true, out key))
        {
            key &= Keys.KeyCode;
            return key != Keys.None;
        }

        return false;
    }

    private static string FormatPrimaryKey(Keys key)
    {
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            return ((char)('0' + ((int)key - (int)Keys.D0))).ToString();
        }

        if ((int)key == (int)NativeMethods.VK_DBE_ALPHANUMERIC)
        {
            return "英数";
        }

        return key switch
        {
            Keys.Escape => "Esc",
            Keys.PageUp or Keys.Prior => "PgUp",
            Keys.PageDown or Keys.Next => "PgDn",
            Keys.Return or Keys.Enter => "Enter",
            Keys.Capital or Keys.CapsLock => "CapsLock",
            _ => key.ToString()
        };
    }

    private static Keys NormalizeKeyCode(Keys keyCode)
    {
        var normalized = keyCode & Keys.KeyCode;
        return (int)normalized == (int)NativeMethods.VK_DBE_ALPHANUMERIC
            ? DbeAlphanumericKey
            : normalized;
    }

    private static bool IsModifierKey(Keys keyCode)
    {
        return keyCode is Keys.ControlKey
            or Keys.LControlKey
            or Keys.RControlKey
            or Keys.ShiftKey
            or Keys.LShiftKey
            or Keys.RShiftKey
            or Keys.Menu
            or Keys.LMenu
            or Keys.RMenu
            or Keys.LWin
            or Keys.RWin;
    }
}

internal readonly record struct HoldKeyBinding(uint VirtualKey, string DisplayName);

internal readonly record struct HotkeyBinding(uint Modifiers, uint VirtualKey, string DisplayText);
