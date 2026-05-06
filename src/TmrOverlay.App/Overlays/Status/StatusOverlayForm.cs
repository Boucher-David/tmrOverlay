using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Overlays.Status;

internal sealed class StatusOverlayForm : PersistentOverlayForm
{
    private readonly TelemetryCaptureState _state;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly Panel _indicatorPanel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;
    private readonly Label _captureLabel;
    private readonly Label _healthLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private long? _lastRefreshFrameCount;

    public StatusOverlayForm(
        TelemetryCaptureState state,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            StatusOverlayDefinition.Definition.DefaultWidth,
            StatusOverlayDefinition.Definition.DefaultHeight)
    {
        _state = state;
        _performanceState = performanceState;
        _settings = settings;

        BackColor = OverlayTheme.Colors.NeutralBackground;
        Padding = new Padding(14, 12, 14, 12);

        _indicatorPanel = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(12, 12),
            BackColor = OverlayTheme.Colors.NeutralIndicator
        };
        _indicatorPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_indicatorPanel.BackColor);
            e.Graphics.FillEllipse(brush, 0, 0, _indicatorPanel.Width - 1, _indicatorPanel.Height - 1);
        };

        _titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(fontFamily, 11f, FontStyle.Bold),
            Location = new Point(36, 10),
            Text = "TmrOverlay"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextControl,
            Font = OverlayTheme.Font(fontFamily, 10f),
            Location = new Point(16, 36),
            Size = new Size(ClientSize.Width - 32, 22),
            Text = "Waiting for iRacing"
        };

        _detailLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(fontFamily, 9f),
            Location = new Point(16, 58),
            Size = new Size(ClientSize.Width - 32, 18),
            Text = "collector idle"
        };

        _captureLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.InfoText,
            Font = OverlayTheme.Font(fontFamily, 8.75f),
            Location = new Point(16, 80),
            Size = new Size(ClientSize.Width - 32, 18),
            Text = "capture: not started"
        };

        _healthLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(fontFamily, 8.75f),
            Location = new Point(16, 102),
            Size = new Size(ClientSize.Width - 32, 34),
            Text = "health: waiting for telemetry"
        };

        Controls.Add(_indicatorPanel);
        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_detailLabel);
        Controls.Add(_captureLabel);
        Controls.Add(_healthLabel);

        RegisterDragSurfaces(
            _indicatorPanel,
            _titleLabel,
            _statusLabel,
            _detailLabel,
            _captureLabel,
            _healthLabel);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _titleLabel.Dispose();
            _statusLabel.Dispose();
            _detailLabel.Dispose();
            _captureLabel.Dispose();
            _healthLabel.Dispose();
            _indicatorPanel.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayStatusPaint,
                started,
                succeeded);
        }
    }

    private void RefreshOverlay()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            TelemetryCaptureStatusSnapshot snapshot;
            var snapshotStarted = Stopwatch.GetTimestamp();
            var snapshotSucceeded = false;
            try
            {
                snapshot = _state.Snapshot();
                snapshotSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayStatusSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            CaptureHealth health;
            var healthStarted = Stopwatch.GetTimestamp();
            var healthSucceeded = false;
            try
            {
                health = CaptureHealth.From(snapshot);
                healthSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayStatusHealth,
                    healthStarted,
                    healthSucceeded);
            }

            var now = DateTimeOffset.UtcNow;
            var previousFrameCount = _lastRefreshFrameCount;
            var applyStarted = Stopwatch.GetTimestamp();
            var applySucceeded = false;
            var uiChanged = false;
            try
            {
                Color nextBackColor;
                Color nextIndicatorColor;
                if (health.Level == CaptureHealthLevel.Error)
                {
                    nextBackColor = OverlayTheme.Colors.ErrorBackground;
                    nextIndicatorColor = OverlayTheme.Colors.ErrorIndicator;
                }
                else if (health.Level == CaptureHealthLevel.Warning)
                {
                    nextBackColor = OverlayTheme.Colors.WarningBackground;
                    nextIndicatorColor = OverlayTheme.Colors.WarningIndicator;
                }
                else if (snapshot.IsCapturing)
                {
                    nextBackColor = OverlayTheme.Colors.SuccessStrongBackground;
                    nextIndicatorColor = OverlayTheme.Colors.SuccessIndicator;
                }
                else if (snapshot.IsConnected)
                {
                    nextBackColor = OverlayTheme.Colors.InfoBackground;
                    nextIndicatorColor = OverlayTheme.Colors.NeutralIndicator;
                }
                else
                {
                    nextBackColor = OverlayTheme.Colors.NeutralBackground;
                    nextIndicatorColor = OverlayTheme.Colors.NeutralIndicator;
                }

                uiChanged |= SetBackColorIfChanged(this, nextBackColor);
                var indicatorChanged = SetBackColorIfChanged(_indicatorPanel, nextIndicatorColor);
                uiChanged |= indicatorChanged;
                uiChanged |= SetTextIfChanged(_statusLabel, health.StatusText);
                uiChanged |= SetTextIfChanged(_detailLabel, health.DetailText);
                uiChanged |= SetTextIfChanged(_captureLabel, health.CaptureText);
                uiChanged |= SetTextIfChanged(_healthLabel, health.MessageText);
                uiChanged |= SetVisibleIfChanged(
                    _captureLabel,
                    _settings.GetBooleanOption(OverlayOptionKeys.StatusCaptureDetails, defaultValue: true));
                uiChanged |= SetVisibleIfChanged(
                    _healthLabel,
                    _settings.GetBooleanOption(OverlayOptionKeys.StatusHealthDetails, defaultValue: true));
                if (indicatorChanged)
                {
                    _indicatorPanel.Invalidate();
                }

                if (uiChanged)
                {
                    Invalidate();
                }

                applySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayStatusApplyUi,
                    applyStarted,
                    applySucceeded);
            }

            _lastRefreshFrameCount = snapshot.FrameCount;
            _performanceState.RecordOverlayRefreshDecision(
                StatusOverlayDefinition.Definition.Id,
                now,
                previousFrameCount,
                snapshot.FrameCount,
                snapshot.LastFrameCapturedAtUtc,
                applied: uiChanged);
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayStatusRefresh,
                started,
                succeeded);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_statusLabel is null || _detailLabel is null || _captureLabel is null || _healthLabel is null)
        {
            return;
        }

        _statusLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 22);
        _detailLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 18);
        _captureLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 18);
        _healthLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 34);
    }

    private static bool SetTextIfChanged(Label label, string? value)
    {
        var text = value ?? string.Empty;
        if (string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        label.Text = text;
        return true;
    }

    private static bool SetVisibleIfChanged(Control control, bool visible)
    {
        if (control.Visible == visible)
        {
            return false;
        }

        control.Visible = visible;
        return true;
    }

    private static bool SetBackColorIfChanged(Control control, Color color)
    {
        if (control.BackColor == color)
        {
            return false;
        }

        control.BackColor = color;
        return true;
    }

    private enum CaptureHealthLevel
    {
        Ok,
        Warning,
        Error
    }

    private sealed record CaptureHealth(
        CaptureHealthLevel Level,
        string StatusText,
        string DetailText,
        string CaptureText,
        string MessageText)
    {
        public static CaptureHealth From(TelemetryCaptureStatusSnapshot snapshot)
        {
            var now = DateTimeOffset.UtcNow;
            var capturePath = snapshot.CurrentCaptureDirectory ?? snapshot.LastCaptureDirectory ?? snapshot.CaptureRoot;
            var captureText = snapshot.RawCaptureEnabled
                ? $"raw: {CompactPath(capturePath)}"
                : "raw: disabled; history ready";
            var frameAge = AgeSeconds(snapshot.LastFrameCapturedAtUtc, now);
            var diskAge = AgeSeconds(snapshot.LastDiskWriteAtUtc, now);
            var bytes = FormatBytes(snapshot.TelemetryFileBytes);
            var detail = snapshot.RawCaptureEnabled
                ? $"queued {snapshot.FrameCount,7:N0}  written {snapshot.WrittenFrameCount,7:N0}  drops {snapshot.DroppedFrameCount,4:N0}  file {bytes}"
                : $"frames {snapshot.FrameCount,7:N0}  history on  raw off";

            if (!string.IsNullOrWhiteSpace(snapshot.LastError))
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Capture error",
                    detail,
                    captureText,
                    $"error: {Trim(snapshot.LastError)}");
            }

            var appWarning = string.IsNullOrWhiteSpace(snapshot.AppWarning)
                ? null
                : $"warning: {Trim(snapshot.AppWarning)}";

            if (!snapshot.IsConnected)
            {
                return new CaptureHealth(
                    appWarning is null ? CaptureHealthLevel.Ok : CaptureHealthLevel.Warning,
                    "Waiting for iRacing",
                    "collector idle",
                    captureText,
                    Combine(appWarning, "health: waiting is expected before iRacing is running"));
            }

            if (!snapshot.IsCapturing)
            {
                return new CaptureHealth(
                    appWarning is null ? CaptureHealthLevel.Ok : CaptureHealthLevel.Warning,
                    "Connected, waiting for telemetry",
                    "waiting for first telemetry frame",
                    captureText,
                    Combine(appWarning, "health: live telemetry starts after session data arrives"));
            }

            if (snapshot.RawCaptureEnabled && snapshot.FrameCount > 0 && snapshot.WrittenFrameCount == 0)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Frames queued, not written",
                    detail,
                    captureText,
                    "error: telemetry frames arrived but disk writer has not confirmed writes");
            }

            if (snapshot.RawCaptureEnabled && snapshot.WrittenFrameCount > snapshot.FrameCount + 2)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Capture counters inconsistent",
                    detail,
                    captureText,
                    "warning: written frame count is ahead of queued frame count");
            }

            if (snapshot.DroppedFrameCount > 0)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Collecting with dropped frames",
                    detail,
                    captureText,
                    "warning: capture queue overflowed; disk may be too slow");
            }

            if (frameAge is not null && frameAge > 5)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Telemetry frames stalled",
                    detail,
                    captureText,
                    $"error: no SDK frame for {frameAge:N0}s; sim may be paused/disconnected");
            }

            if (snapshot.RawCaptureEnabled && diskAge is not null && diskAge > 5)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Disk writes stalled",
                    detail,
                    captureText,
                    $"error: no telemetry.bin write confirmation for {diskAge:N0}s");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.LastWarning))
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Collecting with warning",
                    detail,
                    captureText,
                    $"warning: {Trim(snapshot.LastWarning)}");
            }

            if (appWarning is not null)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Build may be stale",
                    detail,
                    captureText,
                    appWarning);
            }

            var healthMessage = snapshot.RawCaptureEnabled
                ? $"health: live frames ok; last frame {FormatAge(frameAge)}, disk {FormatAge(diskAge)}"
                : $"health: live analysis ok; last frame {FormatAge(frameAge)}";
            return new CaptureHealth(
                CaptureHealthLevel.Ok,
                snapshot.RawCaptureEnabled ? "Collecting raw telemetry" : "Analyzing live telemetry",
                detail,
                captureText,
                healthMessage);
        }

        private static double? AgeSeconds(DateTimeOffset? timestampUtc, DateTimeOffset now)
        {
            return timestampUtc is null
                ? null
                : Math.Max(0d, (now - timestampUtc.Value).TotalSeconds);
        }

        private static string FormatAge(double? seconds)
        {
            return seconds is null ? "n/a" : $"{seconds.Value:N1}s ago";
        }

        private static string FormatBytes(long? bytes)
        {
            if (bytes is null)
            {
                return "n/a";
            }

            if (bytes < 1024)
            {
                return $"{bytes:N0} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes.Value / 1024d:N1} KB";
            }

            return $"{bytes.Value / 1024d / 1024d:N1} MB";
        }

        private static string CompactPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "not resolved";
            }

            var normalized = path.Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 3)
            {
                return normalized;
            }

            return $".../{string.Join('/', segments.TakeLast(3))}";
        }

        private static string Trim(string value)
        {
            const int maxLength = 96;
            var normalized = value.ReplaceLineEndings(" ");
            return normalized.Length <= maxLength
                ? normalized
                : normalized[..(maxLength - 1)] + "...";
        }

        private static string Combine(string? first, string second)
        {
            return string.IsNullOrWhiteSpace(first)
                ? second
                : $"{first} | {second}";
        }
    }
}
