namespace CrosshairOverlayTool;

internal sealed class OverlayForm : Form
{
    private readonly OverlayOrientation _orientation;
    private int _lineThickness = 2;

    public OverlayForm(OverlayOrientation orientation)
    {
        _orientation = orientation;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.Style = NativeMethods.WS_POPUP;
            createParams.ExStyle |= NativeMethods.WS_EX_TOPMOST
                | NativeMethods.WS_EX_TRANSPARENT
                | NativeMethods.WS_EX_TOOLWINDOW
                | NativeMethods.WS_EX_LAYERED
                | NativeMethods.WS_EX_NOACTIVATE;
            return createParams;
        }
    }

    public void ApplyAppearance(int lineThickness, int opacity, Color lineColor)
    {
        _lineThickness = Math.Clamp(lineThickness, 1, 20);
        BackColor = lineColor;
        Opacity = Math.Clamp(opacity, 1, 255) / 255d;
    }

    public void UpdateLineBounds(Rectangle virtualDesktopBounds, Point cursorPosition)
    {
        if (virtualDesktopBounds.Width <= 0 || virtualDesktopBounds.Height <= 0)
        {
            return;
        }

        Bounds = _orientation == OverlayOrientation.Vertical
            ? CreateVerticalBounds(virtualDesktopBounds, cursorPosition.X)
            : CreateHorizontalBounds(virtualDesktopBounds, cursorPosition.Y);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_NCHITTEST)
        {
            m.Result = (IntPtr)NativeMethods.HTTRANSPARENT;
            return;
        }

        base.WndProc(ref m);
    }

    private Rectangle CreateVerticalBounds(Rectangle virtualDesktopBounds, int cursorX)
    {
        var width = Math.Min(_lineThickness, virtualDesktopBounds.Width);
        var x = ClampCenteredStart(cursorX, width, virtualDesktopBounds.Left, virtualDesktopBounds.Right);
        return new Rectangle(x, virtualDesktopBounds.Top, width, virtualDesktopBounds.Height);
    }

    private Rectangle CreateHorizontalBounds(Rectangle virtualDesktopBounds, int cursorY)
    {
        var height = Math.Min(_lineThickness, virtualDesktopBounds.Height);
        var y = ClampCenteredStart(cursorY, height, virtualDesktopBounds.Top, virtualDesktopBounds.Bottom);
        return new Rectangle(virtualDesktopBounds.Left, y, virtualDesktopBounds.Width, height);
    }

    private static int ClampCenteredStart(int cursorPosition, int thickness, int min, int maxExclusive)
    {
        var start = cursorPosition - (thickness / 2);
        var maxStart = Math.Max(min, maxExclusive - thickness);
        return Math.Clamp(start, min, maxStart);
    }
}

internal enum OverlayOrientation
{
    Vertical,
    Horizontal
}
