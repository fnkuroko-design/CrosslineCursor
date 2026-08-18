namespace CrosshairOverlayTool;

internal sealed class HotkeyCaptureTextBox : TextBox
{
    private static readonly Keys DbeAlphanumericKey = unchecked((Keys)NativeMethods.VK_DBE_ALPHANUMERIC);

    public HotkeyCaptureTextBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        var keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Menu
            or Keys.LMenu
            or Keys.RMenu
            or Keys.ControlKey
            or Keys.LControlKey
            or Keys.RControlKey
            or Keys.ShiftKey
            or Keys.LShiftKey
            or Keys.RShiftKey
            || keyCode == DbeAlphanumericKey
            ? true
            : base.IsInputKey(keyData);
    }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == DbeAlphanumericKey)
        {
            e.IsInputKey = true;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (KeyBindingParser.TryCreateHotkeyBinding(keyData, out var binding))
        {
            Text = binding.DisplayText;
            SelectionStart = TextLength;
            return true;
        }

        if (KeyBindingParser.IsModifierOnlyKeyData(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Tab)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}
