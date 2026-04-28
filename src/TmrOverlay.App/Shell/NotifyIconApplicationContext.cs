using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Overlays;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Shell;

internal sealed class NotifyIconApplicationContext : ApplicationContext
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly AppStorageOptions _storageOptions;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly AppEventRecorder _events;
    private readonly ILogger<NotifyIconApplicationContext> _logger;
    private readonly TelemetryCaptureOptions _options;
    private readonly TelemetryCaptureState _state;
    private readonly OverlayManager _overlayManager;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _rootItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public NotifyIconApplicationContext(
        IHostApplicationLifetime applicationLifetime,
        AppStorageOptions storageOptions,
        DiagnosticsBundleService diagnosticsBundleService,
        AppEventRecorder events,
        ILogger<NotifyIconApplicationContext> logger,
        TelemetryCaptureOptions options,
        TelemetryCaptureState state,
        OverlayManager overlayManager)
    {
        _applicationLifetime = applicationLifetime;
        _storageOptions = storageOptions;
        _diagnosticsBundleService = diagnosticsBundleService;
        _events = events;
        _logger = logger;
        _options = options;
        _state = state;
        _overlayManager = overlayManager;

        _statusItem = new ToolStripMenuItem("Waiting for iRacing")
        {
            Enabled = false
        };

        _captureItem = new ToolStripMenuItem("Open Latest Capture", null, (_, _) => OpenCapture())
        {
            Enabled = false
        };

        _rootItem = new ToolStripMenuItem("Open Capture Root", null, (_, _) => OpenDirectory(_options.ResolvedCaptureRoot));
        if (!_options.RawCaptureEnabled)
        {
            _rootItem.Text = "Open Raw Capture Root";
        }
        var logsItem = new ToolStripMenuItem("Open Logs", null, (_, _) => OpenDirectory(_storageOptions.LogsRoot));
        var settingsItem = new ToolStripMenuItem("Open Settings", null, (_, _) => _overlayManager.OpenSettingsOverlay());
        var diagnosticsItem = new ToolStripMenuItem("Create Diagnostics Bundle", null, (_, _) => CreateDiagnosticsBundle());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.AddRange(
        [
            _statusItem,
            new ToolStripSeparator(),
            _captureItem,
            _rootItem,
            logsItem,
            settingsItem,
            diagnosticsItem,
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
        _overlayManager.ShowStartupOverlays();

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
            _overlayManager.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RefreshMenu()
    {
        var snapshot = _state.Snapshot();
        _rootItem.Text = snapshot.RawCaptureEnabled ? "Open Capture Root" : "Open Raw Capture Root";

        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            _statusItem.Text = snapshot.RawCaptureEnabled ? "Capture error" : "Telemetry error";
            _captureItem.Enabled = !string.IsNullOrWhiteSpace(snapshot.CurrentCaptureDirectory ?? snapshot.LastCaptureDirectory);
            _captureItem.Text = snapshot.RawCaptureEnabled && snapshot.RawCaptureActive
                ? "Open Current Capture"
                : "Open Latest Raw Capture";
            return;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastWarning))
        {
            _statusItem.Text = snapshot.RawCaptureEnabled ? "Capture warning" : "Telemetry warning";
        }

        if (snapshot.IsCapturing)
        {
            if (!snapshot.RawCaptureEnabled)
            {
                _statusItem.Text = $"Analyzing live telemetry: {snapshot.FrameCount:N0} frames";
                _captureItem.Enabled = !string.IsNullOrWhiteSpace(snapshot.LastCaptureDirectory);
                _captureItem.Text = "Open Latest Raw Capture";
                return;
            }

            var statusPrefix = !string.IsNullOrWhiteSpace(snapshot.LastWarning)
                ? "Warning"
                : "Capturing";
            _statusItem.Text = $"{statusPrefix}: {snapshot.FrameCount:N0} queued / {snapshot.WrittenFrameCount:N0} written";
            _captureItem.Enabled = snapshot.RawCaptureActive && !string.IsNullOrWhiteSpace(snapshot.CurrentCaptureDirectory);
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
        _captureItem.Text = snapshot.RawCaptureEnabled ? "Open Latest Capture" : "Open Latest Raw Capture";
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

    private void CreateDiagnosticsBundle()
    {
        try
        {
            var bundlePath = _diagnosticsBundleService.CreateBundle();
            _events.Record("diagnostics_bundle_created", new Dictionary<string, string?>
            {
                ["bundlePath"] = bundlePath
            });
            OpenDirectory(Path.GetDirectoryName(bundlePath)!);
        }
        catch (Exception exception)
        {
            _events.Record("diagnostics_bundle_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name
            });
            _logger.LogError(exception, "Failed to create diagnostics bundle.");
        }
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
