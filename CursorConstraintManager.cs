namespace CrosshairOverlayTool;

internal sealed class CursorConstraintManager : IDisposable
{
    private const int DecisionThresholdPixels = 6;
    private const int ConstraintThicknessPixels = 1;
    private const int PollIntervalMs = 16;

    private readonly System.Windows.Forms.Timer _decisionTimer;

    private ConstraintAxis _axis;
    private Rectangle _virtualDesktopBounds;
    private Point _anchorPoint;
    private bool _isActive;
    private bool _clipApplied;
    private bool _disposed;

    public CursorConstraintManager()
    {
        _decisionTimer = new System.Windows.Forms.Timer
        {
            Interval = PollIntervalMs
        };
        _decisionTimer.Tick += OnDecisionTimerTick;
    }

    public void SetAxisLock(bool active)
    {
        if (_disposed)
        {
            return;
        }

        if (active)
        {
            Activate();
            return;
        }

        ReleaseLock();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseLock();
        _decisionTimer.Tick -= OnDecisionTimerTick;
        _decisionTimer.Dispose();
    }

    private void Activate()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        _axis = ConstraintAxis.Pending;
        _virtualDesktopBounds = GetVirtualDesktopBounds();
        _anchorPoint = GetCurrentCursorPoint();
        ClearCursorClip();
        _decisionTimer.Start();
    }

    private void ReleaseLock()
    {
        _isActive = false;
        _axis = ConstraintAxis.None;
        _decisionTimer.Stop();
        ClearCursorClip();
    }

    private void OnDecisionTimerTick(object? sender, EventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        var currentPoint = GetCurrentCursorPoint();
        if (_axis != ConstraintAxis.Pending)
        {
            _decisionTimer.Stop();
            return;
        }

        var dx = Math.Abs(currentPoint.X - _anchorPoint.X);
        var dy = Math.Abs(currentPoint.Y - _anchorPoint.Y);
        if (Math.Max(dx, dy) < DecisionThresholdPixels)
        {
            return;
        }

        _axis = dx >= dy ? ConstraintAxis.Horizontal : ConstraintAxis.Vertical;
        ApplyClip(currentPoint);
        _decisionTimer.Stop();
    }

    private void ApplyClip(Point currentPoint)
    {
        var rect = _axis switch
        {
            ConstraintAxis.Horizontal => new NativeMethods.RECT
            {
                Left = _virtualDesktopBounds.Left,
                Top = _anchorPoint.Y,
                Right = _virtualDesktopBounds.Right,
                Bottom = Math.Min(_virtualDesktopBounds.Bottom, _anchorPoint.Y + ConstraintThicknessPixels)
            },
            ConstraintAxis.Vertical => new NativeMethods.RECT
            {
                Left = _anchorPoint.X,
                Top = _virtualDesktopBounds.Top,
                Right = Math.Min(_virtualDesktopBounds.Right, _anchorPoint.X + ConstraintThicknessPixels),
                Bottom = _virtualDesktopBounds.Bottom
            },
            _ => default
        };

        if (_axis == ConstraintAxis.Horizontal)
        {
            NativeMethods.SetCursorPos(currentPoint.X, _anchorPoint.Y);
        }
        else if (_axis == ConstraintAxis.Vertical)
        {
            NativeMethods.SetCursorPos(_anchorPoint.X, currentPoint.Y);
        }

        if (rect.Right <= rect.Left)
        {
            rect.Right = rect.Left + ConstraintThicknessPixels;
        }

        if (rect.Bottom <= rect.Top)
        {
            rect.Bottom = rect.Top + ConstraintThicknessPixels;
        }

        if (NativeMethods.ClipCursor(ref rect))
        {
            _clipApplied = true;
        }
    }

    private void ClearCursorClip()
    {
        if (!_clipApplied)
        {
            return;
        }

        NativeMethods.ClipCursor(IntPtr.Zero);
        _clipApplied = false;
    }

    private Point GetCurrentCursorPoint()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return _anchorPoint;
        }

        return ClampToVirtualDesktop(point.ToPoint());
    }

    private Point ClampToVirtualDesktop(Point point)
    {
        var maxX = _virtualDesktopBounds.Right - 1;
        var maxY = _virtualDesktopBounds.Bottom - 1;
        return new Point(
            Math.Clamp(point.X, _virtualDesktopBounds.Left, maxX),
            Math.Clamp(point.Y, _virtualDesktopBounds.Top, maxY));
    }

    private static Rectangle GetVirtualDesktopBounds()
    {
        return new Rectangle(
            NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN),
            NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
    }

    private enum ConstraintAxis
    {
        None,
        Pending,
        Horizontal,
        Vertical
    }
}
