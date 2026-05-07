using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

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

            AppDiagnosticsStatusModel health;
            var healthStarted = Stopwatch.GetTimestamp();
            var healthSucceeded = false;
            try
            {
                health = AppDiagnosticsStatusModel.From(snapshot);
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
                if (health.Severity == AppDiagnosticsSeverity.Error)
                {
                    nextBackColor = OverlayTheme.Colors.ErrorBackground;
                    nextIndicatorColor = OverlayTheme.Colors.ErrorIndicator;
                }
                else if (health.Severity == AppDiagnosticsSeverity.Warning)
                {
                    nextBackColor = OverlayTheme.Colors.WarningBackground;
                    nextIndicatorColor = OverlayTheme.Colors.WarningIndicator;
                }
                else if (health.Severity == AppDiagnosticsSeverity.Success)
                {
                    nextBackColor = OverlayTheme.Colors.SuccessStrongBackground;
                    nextIndicatorColor = OverlayTheme.Colors.SuccessIndicator;
                }
                else if (health.Severity == AppDiagnosticsSeverity.Info)
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

}
