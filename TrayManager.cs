using System.Drawing.Drawing2D;

namespace CrosshairOverlayTool;

internal sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _togglePinnedMenuItem;
    private readonly ToolStripMenuItem _toggleHorizontalLockMenuItem;
    private readonly ToolStripMenuItem _settingsMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;
    private readonly Icon _trayIcon;

    private bool _disposed;

    public TrayManager()
    {
        _statusMenuItem = new ToolStripMenuItem("状態: 非表示")
        {
            Enabled = false
        };

        _togglePinnedMenuItem = new ToolStripMenuItem("固定表示");
        _togglePinnedMenuItem.Click += OnTogglePinnedMenuItemClick;

        _toggleHorizontalLockMenuItem = new ToolStripMenuItem("ホールド中の軸拘束");
        _toggleHorizontalLockMenuItem.Click += OnToggleHorizontalLockMenuItemClick;

        _settingsMenuItem = new ToolStripMenuItem("設定...");
        _settingsMenuItem.Click += OnSettingsMenuItemClick;

        _exitMenuItem = new ToolStripMenuItem("終了");
        _exitMenuItem.Click += OnExitMenuItemClick;

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.AddRange(
            [
                _statusMenuItem,
                _togglePinnedMenuItem,
                _toggleHorizontalLockMenuItem,
                new ToolStripSeparator(),
                _settingsMenuItem,
                new ToolStripSeparator(),
                _exitMenuItem
            ]);

        _trayIcon = TrayIconFactory.Create();
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Crosshair Overlay Tool",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
        _notifyIcon.MouseDoubleClick += OnNotifyIconMouseDoubleClick;
    }

    public event EventHandler? TogglePinnedRequested;

    public event EventHandler? ToggleHorizontalLockRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void UpdateState(TrayState state)
    {
        if (_disposed)
        {
            return;
        }

        _statusMenuItem.Text = BuildStatusText(state);

        _togglePinnedMenuItem.Enabled = state.ToggleModeEnabled;
        _togglePinnedMenuItem.Checked = state.ToggleStateOn;
        _togglePinnedMenuItem.Text = state.ToggleModeEnabled
            ? "固定表示"
            : "固定表示 (トグルモード無効)";

        _toggleHorizontalLockMenuItem.Checked = state.HorizontalLockEnabled;
        _toggleHorizontalLockMenuItem.Text = state.HoldModeEnabled
            ? "ホールド中の軸拘束"
            : "ホールド中の軸拘束 (ホールドモード無効)";

        _notifyIcon.Text = state.OverlayVisible
            ? "Crosshair Overlay Tool - 表示中"
            : "Crosshair Overlay Tool - 待機中";
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.MouseDoubleClick -= OnNotifyIconMouseDoubleClick;
        _notifyIcon.Dispose();

        _togglePinnedMenuItem.Click -= OnTogglePinnedMenuItemClick;
        _toggleHorizontalLockMenuItem.Click -= OnToggleHorizontalLockMenuItemClick;
        _settingsMenuItem.Click -= OnSettingsMenuItemClick;
        _exitMenuItem.Click -= OnExitMenuItemClick;
        _contextMenu.Dispose();
        _trayIcon.Dispose();
    }

    private static string BuildStatusText(TrayState state)
    {
        if (!state.OverlayVisible)
        {
            return "状態: 非表示";
        }

        if (state.ToggleStateOn)
        {
            return "状態: 表示中 (固定)";
        }

        if (state.HoldModeEnabled && state.HoldKeyPressed)
        {
            return state.HorizontalLockActive
                ? "状態: 表示中 (ホールド / 軸拘束)"
                : "状態: 表示中 (ホールド)";
        }

        return "状態: 表示中";
    }

    private void OnNotifyIconMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_togglePinnedMenuItem.Enabled)
        {
            return;
        }

        TogglePinnedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTogglePinnedMenuItemClick(object? sender, EventArgs e)
    {
        TogglePinnedRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnToggleHorizontalLockMenuItemClick(object? sender, EventArgs e)
    {
        ToggleHorizontalLockRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSettingsMenuItemClick(object? sender, EventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitMenuItemClick(object? sender, EventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}

internal readonly record struct TrayState(
    bool OverlayVisible,
    bool ToggleModeEnabled,
    bool ToggleStateOn,
    bool HoldModeEnabled,
    bool HoldKeyPressed,
    bool HorizontalLockEnabled,
    bool HorizontalLockActive);

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var ringPen = new Pen(Color.FromArgb(210, 35, 35, 35), 2.5f);
        using var linePen = new Pen(Color.FromArgb(255, 255, 59, 48), 2.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var centerBrush = new SolidBrush(Color.FromArgb(255, 255, 59, 48));

        graphics.DrawEllipse(ringPen, 5, 5, 22, 22);
        graphics.DrawLine(linePen, 16, 3, 16, 12);
        graphics.DrawLine(linePen, 16, 20, 16, 29);
        graphics.DrawLine(linePen, 3, 16, 12, 16);
        graphics.DrawLine(linePen, 20, 16, 29, 16);
        graphics.FillEllipse(centerBrush, 13, 13, 6, 6);

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var createdIcon = Icon.FromHandle(iconHandle);
            return (Icon)createdIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }
}
