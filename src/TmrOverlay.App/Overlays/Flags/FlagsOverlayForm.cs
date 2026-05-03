using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Flags;

internal sealed class FlagsOverlayForm : PersistentOverlayForm
{
    private const int RefreshIntervalMilliseconds = 100;
    private const int BorderThickness = 10;
    private const int WsExTransparent = 0x00000020;

    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private long? _lastRefreshSequence;
    private Color? _borderColor;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    public FlagsOverlayForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            FlagsOverlayDefinition.Definition.DefaultWidth,
            FlagsOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _settings = settings;

        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        ShowInTaskbar = false;

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WsExTransparent;
            return createParams;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
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
            if (_borderColor is { } borderColor)
            {
                e.Graphics.SmoothingMode = SmoothingMode.None;
                using var pen = new Pen(borderColor, BorderThickness)
                {
                    Alignment = PenAlignment.Inset
                };
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "render");
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayFlagsPaint,
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
            LiveTelemetrySnapshot snapshot;
            var snapshotStarted = Stopwatch.GetTimestamp();
            var snapshotSucceeded = false;
            try
            {
                snapshot = _liveTelemetrySource.Snapshot();
                snapshotSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFlagsSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            var now = DateTimeOffset.UtcNow;
            var previousSequence = _lastRefreshSequence;
            SimpleTelemetryOverlayViewModel viewModel;
            var viewModelStarted = Stopwatch.GetTimestamp();
            var viewModelSucceeded = false;
            try
            {
                viewModel = FlagsOverlayViewModel.From(snapshot, now, unitSystem: "Metric");
                viewModelSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayFlagsViewModel,
                    viewModelStarted,
                    viewModelSucceeded);
            }

            var oldColor = _borderColor;
            _borderColor = SelectBorderColor(viewModel);
            _lastRefreshSequence = snapshot.Sequence;
            var uiChanged = !ColorEquals(oldColor, _borderColor);
            _performanceState.RecordOverlayRefreshDecision(
                FlagsOverlayDefinition.Definition.Id,
                now,
                previousSequence,
                snapshot.Sequence,
                snapshot.LastUpdatedAtUtc,
                applied: uiChanged);
            if (uiChanged)
            {
                Invalidate();
            }

            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "refresh");
            _borderColor = Color.FromArgb(236, 112, 99);
            Invalidate();
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayFlagsRefresh,
                started,
                succeeded);
        }
    }

    private Color? SelectBorderColor(SimpleTelemetryOverlayViewModel viewModel)
    {
        if (viewModel.Tone == SimpleTelemetryTone.Waiting)
        {
            return null;
        }

        var key = viewModel.Status.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var category = FlagCategoryFor(key);
        if (category is null || !IsCategoryEnabled(category.Value))
        {
            return null;
        }

        if (category == FlagCategory.Blue)
        {
            return Color.FromArgb(55, 162, 255);
        }

        return category.Value switch
        {
            FlagCategory.Green => Color.FromArgb(48, 214, 109),
            FlagCategory.Yellow => Color.FromArgb(255, 207, 74),
            FlagCategory.Critical => Color.FromArgb(236, 112, 99),
            FlagCategory.Finish => Color.White,
            _ => null
        };
    }

    private static FlagCategory? FlagCategoryFor(string key)
    {
        if (key.Contains("red", StringComparison.Ordinal)
            || key.Contains("black", StringComparison.Ordinal)
            || key.Contains("service", StringComparison.Ordinal)
            || key.Contains("repair", StringComparison.Ordinal)
            || key.Contains("disqualified", StringComparison.Ordinal)
            || key.Contains("driver flag", StringComparison.Ordinal)
            || key.Contains("scoring invalid", StringComparison.Ordinal)
            || key.Contains("unknown driver", StringComparison.Ordinal)
            || key.Contains("furled", StringComparison.Ordinal))
        {
            return FlagCategory.Critical;
        }

        if (key.Contains("yellow", StringComparison.Ordinal)
            || key.Contains("caution", StringComparison.Ordinal)
            || key.Contains("debris", StringComparison.Ordinal)
            || key.Contains("one lap to green", StringComparison.Ordinal)
            || key.Contains("random", StringComparison.Ordinal))
        {
            return FlagCategory.Yellow;
        }

        if (key.Contains("blue", StringComparison.Ordinal))
        {
            return FlagCategory.Blue;
        }

        if (key.Contains("checkered", StringComparison.Ordinal)
            || key.Contains("white", StringComparison.Ordinal)
            || key.Contains("countdown", StringComparison.Ordinal)
            || key.Contains("crossed", StringComparison.Ordinal)
            || key.Contains("ten to go", StringComparison.Ordinal)
            || key.Contains("five to go", StringComparison.Ordinal))
        {
            return FlagCategory.Finish;
        }

        if (key.Contains("green", StringComparison.Ordinal) || key.Contains("start go", StringComparison.Ordinal))
        {
            return FlagCategory.Green;
        }

        return null;
    }

    private bool IsCategoryEnabled(FlagCategory category)
    {
        return category switch
        {
            FlagCategory.Green => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowGreen, defaultValue: true),
            FlagCategory.Blue => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowBlue, defaultValue: true),
            FlagCategory.Yellow => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowYellow, defaultValue: true),
            FlagCategory.Critical => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowCritical, defaultValue: true),
            FlagCategory.Finish => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowFinish, defaultValue: true),
            _ => true
        };
    }

    private void ReportOverlayError(Exception exception, string stage)
    {
        var message = $"{stage}: {exception.GetType().Name} {exception.Message}";
        var now = DateTimeOffset.UtcNow;
        if (string.Equals(_lastLoggedError, message, StringComparison.Ordinal)
            && _lastLoggedErrorAtUtc is { } lastLogged
            && now - lastLogged < TimeSpan.FromSeconds(30))
        {
            return;
        }

        _lastLoggedError = message;
        _lastLoggedErrorAtUtc = now;
        _logger.LogWarning(exception, "Flags overlay {Stage} failed.", stage);
    }

    private static bool ColorEquals(Color? left, Color? right)
    {
        if (left.HasValue != right.HasValue)
        {
            return false;
        }

        return !left.HasValue || left.Value.ToArgb() == right.GetValueOrDefault().ToArgb();
    }

    private enum FlagCategory
    {
        Green,
        Blue,
        Yellow,
        Critical,
        Finish
    }
}
