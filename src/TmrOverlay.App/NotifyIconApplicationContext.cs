using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Hosting;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App;

internal sealed class NotifyIconApplicationContext : ApplicationContext
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly TelemetryCaptureOptions _options;
    private readonly TelemetryCaptureState _state;
    private readonly NotifyIcon _notifyIcon;
    private readonly StatusOverlayForm _overlayForm;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _rootItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public NotifyIconApplicationContext(
        IHostApplicationLifetime applicationLifetime,
        TelemetryCaptureOptions options,
        TelemetryCaptureState state)
    {
        _applicationLifetime = applicationLifetime;
        _options = options;
        _state = state;
        _overlayForm = new StatusOverlayForm(state, ExitApplication);

        _statusItem = new ToolStripMenuItem("Waiting for iRacing")
        {
            Enabled = false
        };

        _captureItem = new ToolStripMenuItem("Open Latest Capture", null, (_, _) => OpenCapture())
        {
            Enabled = false
        };

        _rootItem = new ToolStripMenuItem("Open Capture Root", null, (_, _) => OpenDirectory(_options.ResolvedCaptureRoot));
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.AddRange(
        [
            _statusItem,
            new ToolStripSeparator(),
            _captureItem,
            _rootItem,
            new ToolStripSeparator(),
            exitItem
        ]);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Icon = SystemIcons.Application,
            Text = "TmrOverlay",
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenCapture();
        _overlayForm.Show();

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _refreshTimer.Tick += (_, _) => RefreshMenu();
        _refreshTimer.Start();

        RefreshMenu();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _overlayForm.Close();
            _overlayForm.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RefreshMenu()
    {
        var snapshot = _state.Snapshot();

        if (snapshot.IsCapturing)
        {
            _statusItem.Text = $"Capturing {snapshot.FrameCount:N0} frames";
            _captureItem.Enabled = !string.IsNullOrWhiteSpace(snapshot.CurrentCaptureDirectory);
            _captureItem.Text = "Open Current Capture";
            return;
        }

        if (snapshot.IsConnected)
        {
            _statusItem.Text = "Connected to iRacing";
        }
        else
        {
            _statusItem.Text = "Waiting for iRacing";
        }

        _captureItem.Enabled = !string.IsNullOrWhiteSpace(snapshot.LastCaptureDirectory);
        _captureItem.Text = "Open Latest Capture";
    }

    private void OpenCapture()
    {
        var snapshot = _state.Snapshot();
        var path = snapshot.CurrentCaptureDirectory ?? snapshot.LastCaptureDirectory ?? _options.ResolvedCaptureRoot;
        OpenDirectory(path);
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        _applicationLifetime.StopApplication();
        ExitThread();
    }

    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
