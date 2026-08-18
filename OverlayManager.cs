using Microsoft.Win32;

namespace CrosshairOverlayTool;

internal sealed class OverlayManager : IDisposable
{
    private readonly System.Windows.Forms.Timer _trackingTimer;
    private readonly int _lineThickness;
    private readonly int _opacity;
    private readonly Color _lineColor;

    private OverlayForm? _verticalOverlayForm;
    private OverlayForm? _horizontalOverlayForm;
    private Rectangle _virtualDesktopBounds;
    private Point _lastCursorPosition = new(int.MinValue, int.MinValue);
    private bool _disposed;

    public OverlayManager(int lineThickness, int opacity, Color lineColor)
    {
        _lineThickness = lineThickness;
        _opacity = opacity;
        _lineColor = lineColor;

        _trackingTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _trackingTimer.Tick += TrackingTimerOnTick;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void ShowOverlay()
    {
        ThrowIfDisposed();

        EnsureOverlayForms();
        SyncBoundsToVirtualDesktop();
        UpdateCursorPosition(force: true);

        ShowLineForm(_verticalOverlayForm!);
        ShowLineForm(_horizontalOverlayForm!);

        if (!_trackingTimer.Enabled)
        {
            _trackingTimer.Start();
        }
    }

    public void HideOverlay()
    {
        if (_disposed)
        {
            return;
        }

        _trackingTimer.Stop();
        _verticalOverlayForm?.Hide();
        _horizontalOverlayForm?.Hide();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _trackingTimer.Stop();
        _trackingTimer.Tick -= TrackingTimerOnTick;
        _trackingTimer.Dispose();
        _verticalOverlayForm?.Dispose();
        _horizontalOverlayForm?.Dispose();
    }

    private void TrackingTimerOnTick(object? sender, EventArgs e)
    {
        UpdateCursorPosition(force: false);
        EnsureOverlayFormsTopmost();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_verticalOverlayForm is null || _horizontalOverlayForm is null)
        {
            return;
        }

        SyncBoundsToVirtualDesktop();
        UpdateCursorPosition(force: true);
    }

    private void EnsureOverlayForms()
    {
        if (_verticalOverlayForm is not null && _horizontalOverlayForm is not null)
        {
            return;
        }

        _verticalOverlayForm = new OverlayForm(OverlayOrientation.Vertical);
        _horizontalOverlayForm = new OverlayForm(OverlayOrientation.Horizontal);
        _verticalOverlayForm.ApplyAppearance(_lineThickness, _opacity, _lineColor);
        _horizontalOverlayForm.ApplyAppearance(_lineThickness, _opacity, _lineColor);
    }

    private void SyncBoundsToVirtualDesktop()
    {
        _virtualDesktopBounds = GetVirtualDesktopBounds();
    }

    private void UpdateCursorPosition(bool force)
    {
        if (_verticalOverlayForm is null || _horizontalOverlayForm is null || !NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        var cursorPosition = point.ToPoint();
        if (!force && cursorPosition == _lastCursorPosition)
        {
            return;
        }

        _lastCursorPosition = cursorPosition;
        _verticalOverlayForm.UpdateLineBounds(_virtualDesktopBounds, cursorPosition);
        _horizontalOverlayForm.UpdateLineBounds(_virtualDesktopBounds, cursorPosition);
    }

    private static void ShowLineForm(OverlayForm overlayForm)
    {
        if (!overlayForm.Visible)
        {
            overlayForm.Show();
        }

        NativeMethods.ShowWindow(overlayForm.Handle, NativeMethods.SW_SHOWNOACTIVATE);
        MoveLineFormToTopmost(overlayForm);
    }

    private void EnsureOverlayFormsTopmost()
    {
        // 他プロセスが後から最前面ウィンドウを表示しても、表示中のオーバーレイを
        // 最前面グループの先頭へ戻す。カーソルが止まっていても追跡タイマーで実行する。
        if (_verticalOverlayForm?.Visible == true)
        {
            MoveLineFormToTopmost(_verticalOverlayForm);
        }

        if (_horizontalOverlayForm?.Visible == true)
        {
            MoveLineFormToTopmost(_horizontalOverlayForm);
        }
    }

    private static void MoveLineFormToTopmost(OverlayForm overlayForm)
    {
        NativeMethods.SetWindowPos(
            overlayForm.Handle,
            NativeMethods.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        // HWND_TOPMOST で最前面グループへ戻した後、同グループ内でも先頭へ上げる。
        // 一般の最前面ウィンドウが後から表示された場合に、相対的な Z オーダーを回復する。
        NativeMethods.SetWindowPos(
            overlayForm.Handle,
            NativeMethods.HWND_TOP,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private static Rectangle GetVirtualDesktopBounds()
    {
        var x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
        {
            return Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1, 1);
        }

        return new Rectangle(x, y, width, height);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
