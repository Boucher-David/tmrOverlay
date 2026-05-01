using System.Drawing;
using System.Drawing.Drawing2D;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Events;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Status;

internal sealed class StatusOverlayForm : PersistentOverlayForm
{
    private readonly TelemetryCaptureState _state;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly AppEventRecorder _events;
    private readonly OverlaySettings _settings;
    private readonly Panel _indicatorPanel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;
    private readonly Label _activityLabel;
    private readonly Label _captureLabel;
    private readonly Label _healthLabel;
    private readonly Button _rawCaptureButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public StatusOverlayForm(
        TelemetryCaptureState state,
        ILiveTelemetrySource liveTelemetrySource,
        AppEventRecorder events,
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
        _liveTelemetrySource = liveTelemetrySource;
        _events = events;
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

        _rawCaptureButton = new Button
        {
            BackColor = OverlayTheme.Colors.ButtonBackground,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = OverlayTheme.Font(fontFamily, 8.25f, FontStyle.Bold),
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(ClientSize.Width - 116, 8),
            Size = new Size(100, 26),
            TabStop = false,
            Text = "Capture",
            UseVisualStyleBackColor = false
        };
        _rawCaptureButton.FlatAppearance.BorderSize = 1;
        _rawCaptureButton.FlatAppearance.BorderColor = OverlayTheme.Colors.WindowBorder;
        _rawCaptureButton.Click += (_, _) => ToggleRawCapture();

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
            Location = new Point(16, 104),
            Size = new Size(ClientSize.Width - 32, 18),
            Text = "capture: not started"
        };

        _activityLabel = new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.NeutralBackground,
            ForeColor = OverlayTheme.Colors.TextMuted,
            Font = OverlayTheme.Font(fontFamily, 8f, FontStyle.Bold),
            Location = new Point(16, 80),
            Size = new Size(150, 20),
            Text = "IDLE",
            TextAlign = ContentAlignment.MiddleCenter
        };

        _healthLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(fontFamily, 8.75f),
            Location = new Point(16, 126),
            Size = new Size(ClientSize.Width - 32, 34),
            Text = "health: waiting for telemetry"
        };

        Controls.Add(_indicatorPanel);
        Controls.Add(_titleLabel);
        Controls.Add(_rawCaptureButton);
        Controls.Add(_statusLabel);
        Controls.Add(_detailLabel);
        Controls.Add(_activityLabel);
        Controls.Add(_captureLabel);
        Controls.Add(_healthLabel);

        RegisterDragSurfaces(
            _indicatorPanel,
            _titleLabel,
            _statusLabel,
            _detailLabel,
            _activityLabel,
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
            _activityLabel.Dispose();
            _captureLabel.Dispose();
            _healthLabel.Dispose();
            _rawCaptureButton.Dispose();
            _indicatorPanel.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void RefreshOverlay()
    {
        var snapshot = _state.Snapshot();
        var live = _liveTelemetrySource.Snapshot();
        var health = CaptureHealth.From(snapshot, live);

        if (health.Level == CaptureHealthLevel.Error)
        {
            BackColor = OverlayTheme.Colors.ErrorBackground;
            _indicatorPanel.BackColor = OverlayTheme.Colors.ErrorIndicator;
            _statusLabel.Text = health.StatusText;
        }
        else if (health.Level == CaptureHealthLevel.Warning)
        {
            BackColor = OverlayTheme.Colors.WarningBackground;
            _indicatorPanel.BackColor = OverlayTheme.Colors.WarningIndicator;
            _statusLabel.Text = health.StatusText;
        }
        else if (snapshot.RawCaptureStopRequested || snapshot.IsCaptureSynthesisPending || snapshot.IsCaptureSynthesisRunning || snapshot.IsHistoryFinalizing)
        {
            BackColor = OverlayTheme.Colors.InfoBackground;
            _indicatorPanel.BackColor = OverlayTheme.Colors.InfoText;
            _statusLabel.Text = health.StatusText;
        }
        else if (snapshot.IsCapturing)
        {
            BackColor = OverlayTheme.Colors.SuccessStrongBackground;
            _indicatorPanel.BackColor = OverlayTheme.Colors.SuccessIndicator;
            _statusLabel.Text = health.StatusText;
        }
        else if (snapshot.IsConnected)
        {
            BackColor = OverlayTheme.Colors.WarningBackground;
            _indicatorPanel.BackColor = OverlayTheme.Colors.WarningIndicator;
            _statusLabel.Text = health.StatusText;
        }
        else
        {
            BackColor = OverlayTheme.Colors.NeutralBackground;
            _indicatorPanel.BackColor = OverlayTheme.Colors.NeutralIndicator;
            _statusLabel.Text = health.StatusText;
        }

        _detailLabel.Text = health.DetailText;
        _activityLabel.Text = health.Activity.Text;
        _activityLabel.BackColor = health.Activity.BackColor;
        _activityLabel.ForeColor = health.Activity.ForeColor;
        _captureLabel.Text = health.CaptureText;
        _healthLabel.Text = health.MessageText;
        SyncRawCaptureButton(snapshot);
        _captureLabel.Visible = _settings.GetBooleanOption(OverlayOptionKeys.StatusCaptureDetails, defaultValue: true);
        _healthLabel.Visible = _settings.GetBooleanOption(OverlayOptionKeys.StatusHealthDetails, defaultValue: true);
        _indicatorPanel.Invalidate();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_statusLabel is null || _detailLabel is null || _captureLabel is null || _healthLabel is null)
        {
            return;
        }

        _statusLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 22);
        _rawCaptureButton.Location = new Point(Math.Max(16, ClientSize.Width - 116), 8);
        _detailLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 18);
        _activityLabel.Size = new Size(150, 20);
        _captureLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 18);
        _healthLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 34);
    }

    private void ToggleRawCapture()
    {
        var snapshot = _state.Snapshot();
        var requested = !(snapshot.RawCaptureEnabled || snapshot.RawCaptureActive);
        var accepted = _state.SetRawCaptureEnabled(requested);
        _events.Record("raw_capture_runtime_toggle", new Dictionary<string, string?>
        {
            ["requested"] = requested.ToString(),
            ["accepted"] = accepted.ToString(),
            ["source"] = "status_overlay",
            ["rawCaptureActive"] = snapshot.RawCaptureActive.ToString(),
            ["rawCaptureStopRequested"] = snapshot.RawCaptureStopRequested.ToString()
        });
        SyncRawCaptureButton(_state.Snapshot());
    }

    private void SyncRawCaptureButton(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (snapshot.RawCaptureStopRequested)
        {
            _rawCaptureButton.Text = "Stopping";
            _rawCaptureButton.Enabled = false;
            _rawCaptureButton.BackColor = OverlayTheme.Colors.InfoBackground;
            _rawCaptureButton.ForeColor = OverlayTheme.Colors.InfoText;
            return;
        }

        _rawCaptureButton.Enabled = true;
        if (snapshot.RawCaptureActive)
        {
            _rawCaptureButton.Text = "Stop raw";
            _rawCaptureButton.BackColor = OverlayTheme.Colors.WarningStrongBackground;
            _rawCaptureButton.ForeColor = OverlayTheme.Colors.WarningText;
            return;
        }

        if (snapshot.RawCaptureEnabled)
        {
            _rawCaptureButton.Text = "Cancel raw";
            _rawCaptureButton.BackColor = OverlayTheme.Colors.InfoBackground;
            _rawCaptureButton.ForeColor = OverlayTheme.Colors.InfoText;
            return;
        }

        _rawCaptureButton.Text = "Capture";
        _rawCaptureButton.BackColor = OverlayTheme.Colors.ButtonBackground;
        _rawCaptureButton.ForeColor = OverlayTheme.Colors.TextControl;
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
        ActivityBadge Activity,
        string CaptureText,
        string MessageText)
    {
        public static CaptureHealth From(TelemetryCaptureStatusSnapshot snapshot, LiveTelemetrySnapshot live)
        {
            var now = DateTimeOffset.UtcNow;
            var activity = ActivityBadge.From(snapshot);
            var capturePath = snapshot.CurrentCaptureDirectory ?? snapshot.LastCaptureDirectory ?? snapshot.CaptureRoot;
            var rawWriting = snapshot.RawCaptureActive;
            var captureText = snapshot.RawCaptureEnabled || snapshot.RawCaptureActive
                ? $"raw: {CompactPath(capturePath)}"
                : "raw: disabled; history ready";
            var frameAge = AgeSeconds(snapshot.LastFrameCapturedAtUtc, now);
            var diskAge = AgeSeconds(snapshot.LastDiskWriteAtUtc, now);
            var bytes = FormatBytes(snapshot.TelemetryFileBytes);
            var detail = rawWriting
                ? $"queued {snapshot.FrameCount,7:N0}  written {snapshot.WrittenFrameCount,7:N0}  drops {snapshot.DroppedFrameCount,4:N0}  file {bytes}  write {FormatMilliseconds(snapshot.LastCaptureWriteElapsedMilliseconds)}/{FormatMilliseconds(snapshot.MaxCaptureWriteElapsedMilliseconds)}"
                : $"frames {snapshot.FrameCount,7:N0}  {CompactLiveMode(live)}  raw off";

            if (!string.IsNullOrWhiteSpace(snapshot.LastError))
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Capture error",
                    detail,
                    activity,
                    captureText,
                    $"error: {Trim(snapshot.LastError)}");
            }

            var appWarning = string.IsNullOrWhiteSpace(snapshot.AppWarning)
                ? null
                : $"warning: {Trim(snapshot.AppWarning)}";

            if (snapshot.RawCaptureStopRequested && snapshot.RawCaptureActive)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Ok,
                    "Stopping raw capture",
                    "closing raw writer; live analysis continues",
                    activity,
                    captureText,
                    "health: raw capture will wait for synthesis after iRacing closes");
            }

            if (snapshot.IsCaptureSynthesisPending)
            {
                var pendingAge = AgeSeconds(snapshot.CaptureSynthesisPendingSinceUtc, now);
                var reason = string.IsNullOrWhiteSpace(snapshot.CaptureSynthesisPendingReason)
                    ? "iRacing still running"
                    : Trim(snapshot.CaptureSynthesisPendingReason);
                return new CaptureHealth(
                    CaptureHealthLevel.Ok,
                    "Waiting to synthesize",
                    "iRacing still running",
                    activity,
                    captureText,
                    $"health: compact capture synthesis will start after iRacing closes; waiting {FormatDuration(pendingAge)} ({reason})");
            }

            if (snapshot.IsCaptureSynthesisRunning)
            {
                var synthesisAge = AgeSeconds(snapshot.CaptureSynthesisStartedAtUtc, now);
                return new CaptureHealth(
                    CaptureHealthLevel.Ok,
                    "Synthesizing capture",
                    "writing compact telemetry summary",
                    activity,
                    captureText,
                    Combine(
                        LastCaptureSynthesisText(snapshot, now),
                        $"health: please wait; writing compact capture synthesis for {FormatAge(synthesisAge)}"));
            }

            if (snapshot.IsHistoryFinalizing)
            {
                var savingAge = AgeSeconds(snapshot.HistoryFinalizationStartedAtUtc, now);
                return new CaptureHealth(
                    CaptureHealthLevel.Ok,
                    "Saving session history",
                    "finalizing compact session summary",
                    activity,
                    captureText,
                    Combine(
                        LastHistoryText(snapshot, now),
                        $"health: writing compact session data for {FormatAge(savingAge)}"));
            }

            if (!snapshot.IsConnected)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Waiting for iRacing",
                    "collector idle",
                    activity,
                    captureText,
                    Combine(appWarning, LastHistoryText(snapshot, now) ?? "health: sim not connected; no live telemetry source"));
            }

            if (!snapshot.IsCapturing)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Connected, waiting for telemetry",
                    "waiting for first telemetry frame",
                    activity,
                    captureText,
                    Combine(appWarning, "health: SDK connected but no live telemetry frame has started collection"));
            }

            if (rawWriting && snapshot.FrameCount > 0 && snapshot.WrittenFrameCount == 0)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Frames queued, not written",
                    detail,
                    activity,
                    captureText,
                    "error: telemetry frames arrived but disk writer has not confirmed writes");
            }

            if (rawWriting && snapshot.WrittenFrameCount > snapshot.FrameCount + 2)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Capture counters inconsistent",
                    detail,
                    activity,
                    captureText,
                    "warning: written frame count is ahead of queued frame count");
            }

            if (snapshot.DroppedFrameCount > 0)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Collecting with dropped frames",
                    detail,
                    activity,
                    captureText,
                    "warning: capture queue overflowed; disk may be too slow");
            }

            if (frameAge is not null && frameAge > 5)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Telemetry frames stalled",
                    detail,
                    activity,
                    captureText,
                    $"error: no SDK frame for {frameAge:N0}s; sim may be paused/disconnected");
            }

            if (rawWriting && diskAge is not null && diskAge > 5)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Error,
                    "Disk writes stalled",
                    detail,
                    activity,
                    captureText,
                    $"error: no telemetry.bin write confirmation for {diskAge:N0}s");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.LastWarning))
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Collecting with warning",
                    detail,
                    activity,
                    captureText,
                    $"warning: {Trim(snapshot.LastWarning)}");
            }

            if (appWarning is not null)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Build may be stale",
                    detail,
                    activity,
                    captureText,
                    appWarning);
            }

            var healthMessage = rawWriting
                ? $"health: live frames ok; last frame {FormatAge(frameAge)}, disk {FormatAge(diskAge)}"
                : $"health: live analysis ok; last frame {FormatAge(frameAge)}";
            return new CaptureHealth(
                CaptureHealthLevel.Ok,
                OkStatusText(snapshot, live),
                detail,
                activity,
                captureText,
                Combine(DescribeLiveMode(live), healthMessage));
        }

        private static string? LastHistoryText(TelemetryCaptureStatusSnapshot snapshot, DateTimeOffset now)
        {
            if (snapshot.LastHistorySavedAtUtc is null)
            {
                return null;
            }

            var label = string.IsNullOrWhiteSpace(snapshot.LastHistorySummaryLabel)
                ? "session history"
                : Trim(snapshot.LastHistorySummaryLabel);
            return $"history: saved {label} {FormatAge(AgeSeconds(snapshot.LastHistorySavedAtUtc, now))}";
        }

        private static string? LastCaptureSynthesisText(TelemetryCaptureStatusSnapshot snapshot, DateTimeOffset now)
        {
            if (snapshot.LastCaptureSynthesisSavedAtUtc is null)
            {
                return null;
            }

            var path = string.IsNullOrWhiteSpace(snapshot.LastCaptureSynthesisPath)
                ? "capture-synthesis.json"
                : CompactPath(snapshot.LastCaptureSynthesisPath);
            var elapsed = snapshot.LastCaptureSynthesisElapsedMilliseconds is { } milliseconds
                ? $"{milliseconds:N0} ms"
                : "n/a";
            var frames = snapshot.LastCaptureSynthesisTotalFrameRecords is { } totalFrames
                ? $"{totalFrames:N0} frames"
                : "n/a frames";
            var stride = snapshot.LastCaptureSynthesisSampleStride is { } sampleStride && sampleStride > 1
                ? $" stride {sampleStride:N0}"
                : string.Empty;
            var cpu = snapshot.LastCaptureSynthesisProcessCpuMilliseconds is { } cpuMilliseconds
                ? $" cpu {cpuMilliseconds:N0} ms"
                : string.Empty;
            return $"synthesis: saved {path} ({FormatBytes(snapshot.LastCaptureSynthesisBytes)}) in {elapsed};{cpu} {frames}{stride} {FormatAge(AgeSeconds(snapshot.LastCaptureSynthesisSavedAtUtc, now))}";
        }

        private static string OkStatusText(TelemetryCaptureStatusSnapshot snapshot, LiveTelemetrySnapshot live)
        {
            if (live.IsSpectatingFocusedCar)
            {
                return "Spectating focus telemetry";
            }

            if (live.IsSpectating)
            {
                return "Spectating team telemetry";
            }

            if (live.IsLocalDriverInCar)
            {
                return snapshot.RawCaptureActive ? "Collecting raw telemetry" : "Analyzing live telemetry";
            }

            return "Analyzing session telemetry";
        }

        private static string CompactLiveMode(LiveTelemetrySnapshot live)
        {
            if (live.IsLocalDriverInCar)
            {
                return "driving";
            }

            if (live.FocusCar.HasData && live.FocusCar.CarIdx is { } focusCarIdx)
            {
                return live.FocusCar.IsTeamCar
                    ? $"watching team #{focusCarIdx}"
                    : $"watching #{focusCarIdx}";
            }

            return "local idle";
        }

        private static string DescribeLiveMode(LiveTelemetrySnapshot live)
        {
            if (live.IsLocalDriverInCar)
            {
                return "mode: local driver in car";
            }

            if (live.FocusCar.HasData && live.FocusCar.CarIdx is { } focusCarIdx)
            {
                var position = FormatPosition(live.FocusCar.ClassPosition ?? live.FocusCar.OverallPosition);
                var suffix = position is null ? string.Empty : $" P{position}";
                return live.FocusCar.IsTeamCar
                    ? $"mode: spectating team #{focusCarIdx}{suffix}; local fuel/side telemetry unavailable"
                    : $"mode: spectating focus #{focusCarIdx}{suffix}; local fuel/side telemetry unavailable";
            }

            return live.IsCollecting
                ? "mode: local driver idle; waiting for in-car scalar telemetry"
                : "mode: waiting";
        }

        private static string? FormatPosition(int? position)
        {
            return position is { } value && value > 0
                ? value.ToString("0")
                : null;
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

        private static string FormatDuration(double? seconds)
        {
            return seconds is null ? "n/a" : $"{seconds.Value:N1}s";
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

        private static string FormatMilliseconds(long? milliseconds)
        {
            return milliseconds is null ? "n/a" : $"{milliseconds.Value:N0}ms";
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

        public sealed record ActivityBadge(string Text, Color BackColor, Color ForeColor)
        {
            public static ActivityBadge From(TelemetryCaptureStatusSnapshot snapshot)
            {
                if (snapshot.IsCaptureSynthesisPending)
                {
                    return new ActivityBadge("WAITING SIM EXIT", OverlayTheme.Colors.InfoBackground, OverlayTheme.Colors.InfoText);
                }

                if (snapshot.IsHistoryFinalizing)
                {
                    return new ActivityBadge("SAVING HISTORY", OverlayTheme.Colors.InfoBackground, OverlayTheme.Colors.InfoText);
                }

                if (snapshot.IsCaptureSynthesisRunning)
                {
                    return new ActivityBadge("SYNTHESIZING", OverlayTheme.Colors.InfoBackground, OverlayTheme.Colors.InfoText);
                }

                if (snapshot.RawCaptureStopRequested)
                {
                    return new ActivityBadge("STOPPING RAW", OverlayTheme.Colors.InfoBackground, OverlayTheme.Colors.InfoText);
                }

                if (snapshot.IsCapturing && snapshot.RawCaptureActive)
                {
                    return new ActivityBadge("RAW WRITES", OverlayTheme.Colors.SuccessBackground, OverlayTheme.Colors.SuccessText);
                }

                if (snapshot.IsCapturing)
                {
                    return new ActivityBadge("SESSION HISTORY", OverlayTheme.Colors.SuccessBackground, OverlayTheme.Colors.SuccessText);
                }

                if (snapshot.IsConnected)
                {
                    return new ActivityBadge("CONNECTED", OverlayTheme.Colors.WarningStrongBackground, OverlayTheme.Colors.WarningText);
                }

                return new ActivityBadge("IDLE", OverlayTheme.Colors.NeutralBackground, OverlayTheme.Colors.TextMuted);
            }
        }
    }
}
