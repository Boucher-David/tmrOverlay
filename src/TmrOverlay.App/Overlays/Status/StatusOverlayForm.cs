using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Overlays.Status;

internal sealed class StatusOverlayForm : PersistentOverlayForm
{
    private readonly TelemetryCaptureState _state;
    private readonly AppEventRecorder _events;
    private readonly ILogger<StatusOverlayForm> _logger;
    private readonly Action _closeApplication;
    private readonly Panel _indicatorPanel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;
    private readonly Label _captureLabel;
    private readonly Label _healthLabel;
    private readonly CheckBox _rawCaptureCheckBox;
    private readonly Button _closeButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private bool _syncingRawCaptureCheckBox;

    public StatusOverlayForm(
        TelemetryCaptureState state,
        AppEventRecorder events,
        ILogger<StatusOverlayForm> logger,
        OverlaySettings settings,
        Action saveSettings,
        Action closeApplication)
        : base(
            settings,
            saveSettings,
            StatusOverlayDefinition.Definition.DefaultWidth,
            StatusOverlayDefinition.Definition.DefaultHeight)
    {
        _state = state;
        _events = events;
        _logger = logger;
        _closeApplication = closeApplication;

        BackColor = Color.FromArgb(26, 26, 26);
        Padding = new Padding(14, 12, 14, 12);

        _indicatorPanel = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(12, 12),
            BackColor = Color.FromArgb(140, 140, 140)
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
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(36, 10),
            Text = "TmrOverlay"
        };

        _rawCaptureCheckBox = new CheckBox
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 8.75f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(ClientSize.Width - 156, 10),
            Size = new Size(112, 22),
            TabStop = false,
            Text = "Raw capture",
            UseVisualStyleBackColor = false
        };
        _rawCaptureCheckBox.CheckedChanged += (_, _) => RawCaptureCheckBoxChanged();

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 36),
            Size = new Size(ClientSize.Width - 32, 22),
            Text = "Waiting for iRacing"
        };

        _detailLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 58),
            Size = new Size(ClientSize.Width - 32, 18),
            Text = "collector idle"
        };

        _captureLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(176, 196, 214),
            Font = new Font("Consolas", 8.75f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 80),
            Size = new Size(ClientSize.Width - 32, 18),
            Text = "capture: not started"
        };

        _healthLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(184, 184, 184),
            Font = new Font("Consolas", 8.75f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 102),
            Size = new Size(ClientSize.Width - 32, 34),
            Text = "health: waiting for telemetry"
        };

        _closeButton = new Button
        {
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(225, 225, 225),
            Location = new Point(ClientSize.Width - 36, 8),
            Size = new Size(26, 24),
            TabStop = false,
            Text = "X",
            UseVisualStyleBackColor = false
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Cursor = Cursors.Hand;
        _closeButton.Click += (_, _) => _closeApplication();

        Controls.Add(_indicatorPanel);
        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_detailLabel);
        Controls.Add(_captureLabel);
        Controls.Add(_healthLabel);
        Controls.Add(_rawCaptureCheckBox);
        Controls.Add(_closeButton);

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
            _closeButton.Dispose();
            _rawCaptureCheckBox.Dispose();
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
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var borderPen = new Pen(Color.FromArgb(72, 255, 255, 255));
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void RefreshOverlay()
    {
        var snapshot = _state.Snapshot();
        var health = CaptureHealth.From(snapshot);

        if (health.Level == CaptureHealthLevel.Error)
        {
            BackColor = Color.FromArgb(70, 18, 24);
            _indicatorPanel.BackColor = Color.FromArgb(245, 88, 88);
            _closeButton.BackColor = Color.FromArgb(105, 28, 36);
            _statusLabel.Text = health.StatusText;
        }
        else if (health.Level == CaptureHealthLevel.Warning)
        {
            BackColor = Color.FromArgb(64, 46, 14);
            _indicatorPanel.BackColor = Color.FromArgb(244, 180, 64);
            _closeButton.BackColor = Color.FromArgb(92, 64, 20);
            _statusLabel.Text = health.StatusText;
        }
        else if (snapshot.IsCapturing)
        {
            BackColor = Color.FromArgb(18, 46, 34);
            _indicatorPanel.BackColor = Color.FromArgb(80, 214, 124);
            _closeButton.BackColor = Color.FromArgb(28, 67, 49);
            _statusLabel.Text = health.StatusText;
        }
        else if (snapshot.IsConnected)
        {
            BackColor = Color.FromArgb(56, 44, 14);
            _indicatorPanel.BackColor = Color.FromArgb(242, 176, 64);
            _closeButton.BackColor = Color.FromArgb(87, 67, 22);
            _statusLabel.Text = health.StatusText;
        }
        else
        {
            BackColor = Color.FromArgb(26, 26, 26);
            _indicatorPanel.BackColor = Color.FromArgb(140, 140, 140);
            _closeButton.BackColor = Color.FromArgb(54, 54, 54);
            _statusLabel.Text = health.StatusText;
        }

        _detailLabel.Text = health.DetailText;
        _captureLabel.Text = health.CaptureText;
        _healthLabel.Text = health.MessageText;
        SyncRawCaptureCheckBox(snapshot);
        _indicatorPanel.Invalidate();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_statusLabel is null || _detailLabel is null || _captureLabel is null || _healthLabel is null || _rawCaptureCheckBox is null || _closeButton is null)
        {
            return;
        }

        _statusLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 22);
        _detailLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 18);
        _captureLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 18);
        _healthLabel.Size = new Size(Math.Max(220, ClientSize.Width - 32), 34);
        _rawCaptureCheckBox.Location = new Point(Math.Max(150, ClientSize.Width - 156), 10);
        _closeButton.Location = new Point(ClientSize.Width - 36, 8);
    }

    private void RawCaptureCheckBoxChanged()
    {
        if (_syncingRawCaptureCheckBox)
        {
            return;
        }

        try
        {
            var requested = _rawCaptureCheckBox.Checked;
            var accepted = _state.SetRawCaptureEnabled(requested);
            _events.Record("raw_capture_runtime_toggle", new Dictionary<string, string?>
            {
                ["requested"] = requested.ToString(),
                ["accepted"] = accepted.ToString()
            });

            if (accepted)
            {
                _logger.LogInformation("Runtime raw capture request changed to {RawCaptureEnabled}.", requested);
                return;
            }

            _logger.LogWarning("Runtime raw capture request to disable was rejected because capture is active.");
            SyncRawCaptureCheckBox(_state.Snapshot());
        }
        catch (Exception exception)
        {
            _state.RecordError($"Raw capture toggle failed: {exception.Message}");
            _events.Record("raw_capture_runtime_toggle_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name
            });
            _logger.LogError(exception, "Failed to update runtime raw capture request from the status overlay.");
            SyncRawCaptureCheckBox(_state.Snapshot());
        }
    }

    private void SyncRawCaptureCheckBox(TelemetryCaptureStatusSnapshot snapshot)
    {
        _syncingRawCaptureCheckBox = true;
        try
        {
            _rawCaptureCheckBox.Checked = snapshot.RawCaptureEnabled || snapshot.RawCaptureActive;
            _rawCaptureCheckBox.Enabled = !snapshot.RawCaptureActive;
            _rawCaptureCheckBox.Text = snapshot.RawCaptureActive ? "Raw active" : "Raw capture";
        }
        finally
        {
            _syncingRawCaptureCheckBox = false;
        }
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
                    CaptureHealthLevel.Warning,
                    "Waiting for iRacing",
                    "collector idle",
                    captureText,
                    Combine(appWarning, "health: sim not connected; no live telemetry source"));
            }

            if (!snapshot.IsCapturing)
            {
                return new CaptureHealth(
                    CaptureHealthLevel.Warning,
                    "Connected, waiting for telemetry",
                    "waiting for first telemetry frame",
                    captureText,
                    Combine(appWarning, "health: SDK connected but no live telemetry frame has started collection"));
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
