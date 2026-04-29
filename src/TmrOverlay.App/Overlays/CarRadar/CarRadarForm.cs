using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.CarRadar;

internal sealed class CarRadarForm : PersistentOverlayForm
{
    private static readonly Color TransparentColor = Color.Fuchsia;
    private const double RadarRangeSeconds = 7d;
    private const double RadarRangeLaps = 0.02d;
    private const double SnapshotStaleSeconds = 1.5d;
    private const double FadeInSeconds = 0.25d;
    private const double FadeOutSeconds = 0.85d;
    private const double MinimumVisibleAlpha = 0.02d;
    private const float MulticlassWarningArcStartDegrees = 62.5f;
    private const float MulticlassWarningArcSweepDegrees = 55f;

    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<CarRadarForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly OverlaySettings _settings;
    private readonly string _fontFamily;
    private readonly Dictionary<int, RadarCarVisual> _carVisuals = [];
    private LiveProximitySnapshot _proximity = LiveProximitySnapshot.Unavailable;
    private DateTimeOffset? _lastRefreshAtUtc;
    private double _radarAlpha;
    private double _leftSideAlpha;
    private double _rightSideAlpha;
    private string? _overlayError;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    private bool ShowMulticlassWarning =>
        _settings.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true);

    public CarRadarForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<CarRadarForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            CarRadarOverlayDefinition.Definition.DefaultWidth,
            CarRadarOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _settings = settings;
        _fontFamily = fontFamily;
        BackColor = TransparentColor;
        TransparencyKey = TransparentColor;
        Padding = Padding.Empty;

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 100
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
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!ShouldPaintRadar())
        {
            return;
        }

        try
        {
            if (_overlayError is not null)
            {
                DrawError(e.Graphics);
                return;
            }

            DrawRadar(e.Graphics);
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "render");
            DrawError(e.Graphics);
        }
    }

    private void RefreshOverlay()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var elapsedSeconds = _lastRefreshAtUtc is { } lastRefresh
                ? Math.Clamp((now - lastRefresh).TotalSeconds, 0d, 0.5d)
                : FadeInSeconds;
            _lastRefreshAtUtc = now;

            var snapshot = _liveTelemetrySource.Snapshot();
            _proximity = IsFresh(snapshot, now) ? snapshot.Proximity : LiveProximitySnapshot.Unavailable;
            _overlayError = null;
            UpdateFadeState(now, elapsedSeconds);
            Invalidate();
            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "refresh");
            Invalidate();
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayRadarRefresh,
                Stopwatch.GetElapsedTime(started),
                succeeded);
        }
    }

    private bool ShouldPaintRadar()
    {
        return _overlayError is not null
            || _radarAlpha > MinimumVisibleAlpha
            || _leftSideAlpha > MinimumVisibleAlpha
            || _rightSideAlpha > MinimumVisibleAlpha
            || _carVisuals.Values.Any(visual => visual.Alpha > MinimumVisibleAlpha);
    }

    private bool HasCurrentRadarSignal()
    {
        return _overlayError is not null
            || _proximity.HasCarLeft
            || _proximity.HasCarRight
            || CurrentRadarCars().Any()
            || CurrentMulticlassApproach() is not null;
    }

    private static bool IsFresh(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        if (!snapshot.IsConnected || !snapshot.IsCollecting || snapshot.LastUpdatedAtUtc is not { } lastUpdated)
        {
            return false;
        }

        var ageSeconds = (now - lastUpdated).TotalSeconds;
        return ageSeconds >= 0d && ageSeconds <= SnapshotStaleSeconds;
    }

    private void UpdateFadeState(DateTimeOffset now, double elapsedSeconds)
    {
        UpdateRadarAlpha(HasCurrentRadarSignal(), elapsedSeconds);
        UpdateSideWarningAlphas(elapsedSeconds);
        UpdateCarVisuals(now, elapsedSeconds);
    }

    private void UpdateRadarAlpha(bool hasCurrentSignal, double elapsedSeconds)
    {
        var target = hasCurrentSignal ? 1d : 0d;
        var duration = target > _radarAlpha ? FadeInSeconds : FadeOutSeconds;
        _radarAlpha = MoveToward(_radarAlpha, target, elapsedSeconds / duration);
    }

    private void UpdateSideWarningAlphas(double elapsedSeconds)
    {
        _leftSideAlpha = MoveTowardSideAlpha(_leftSideAlpha, _proximity.HasCarLeft, elapsedSeconds);
        _rightSideAlpha = MoveTowardSideAlpha(_rightSideAlpha, _proximity.HasCarRight, elapsedSeconds);
    }

    private static double MoveTowardSideAlpha(double current, bool visible, double elapsedSeconds)
    {
        var target = visible ? 1d : 0d;
        var duration = target > current ? FadeInSeconds : FadeOutSeconds;
        return MoveToward(current, target, elapsedSeconds / duration);
    }

    private void UpdateCarVisuals(DateTimeOffset now, double elapsedSeconds)
    {
        var currentCars = CurrentRadarCars()
            .GroupBy(car => car.CarIdx)
            .Select(group => group.MinBy(car => Math.Abs(RangeRatio(car)))!)
            .ToDictionary(car => car.CarIdx);

        foreach (var car in currentCars.Values)
        {
            if (!_carVisuals.TryGetValue(car.CarIdx, out var visual))
            {
                visual = new RadarCarVisual(car);
                _carVisuals[car.CarIdx] = visual;
            }

            visual.Car = car;
            visual.LastSeenAtUtc = now;
            visual.Alpha = MoveToward(visual.Alpha, 1d, elapsedSeconds / FadeInSeconds);
        }

        foreach (var visual in _carVisuals.Values)
        {
            if (currentCars.ContainsKey(visual.Car.CarIdx))
            {
                continue;
            }

            visual.Alpha = MoveToward(visual.Alpha, 0d, elapsedSeconds / FadeOutSeconds);
        }

        foreach (var carIdx in _carVisuals
            .Where(pair => pair.Value.Alpha <= MinimumVisibleAlpha && (now - pair.Value.LastSeenAtUtc).TotalSeconds > FadeOutSeconds)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _carVisuals.Remove(carIdx);
        }
    }

    private IEnumerable<LiveProximityCar> CurrentRadarCars()
    {
        return _proximity.NearbyCars.Where(IsInRadarRange);
    }

    private LiveMulticlassApproach? CurrentMulticlassApproach()
    {
        return ShowMulticlassWarning ? _proximity.StrongestMulticlassApproach : null;
    }

    private static double MoveToward(double current, double target, double delta)
    {
        if (delta <= 0d)
        {
            return current;
        }

        return current < target
            ? Math.Min(target, current + delta)
            : Math.Max(target, current - delta);
    }

    private void DrawRadar(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = Math.Min(ClientSize.Width, ClientSize.Height) - 8;
        var bounds = new RectangleF(
            (ClientSize.Width - diameter) / 2f,
            (ClientSize.Height - diameter) / 2f,
            diameter,
            diameter);

        using var circleBrush = new SolidBrush(WithAlpha(82, 12, 18, 22));
        using var circlePen = new Pen(WithAlpha(120, 255, 255, 255), 1.2f);
        graphics.FillEllipse(circleBrush, bounds);
        graphics.DrawEllipse(circlePen, bounds);

        DrawMulticlassApproachWarning(graphics, bounds);
        DrawDistanceRings(graphics, bounds);
        DrawNearbyCars(graphics, bounds);
        DrawSideWarningCars(graphics, bounds);
        DrawPlayerCar(graphics, bounds);
    }

    private void DrawError(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var diameter = Math.Min(ClientSize.Width, ClientSize.Height) - 8;
        var bounds = new RectangleF(
            (ClientSize.Width - diameter) / 2f,
            (ClientSize.Height - diameter) / 2f,
            diameter,
            diameter);

        using var circleBrush = new SolidBrush(Color.FromArgb(150, 32, 14, 18));
        using var circlePen = new Pen(Color.FromArgb(210, 236, 112, 99), 1.4f);
        graphics.FillEllipse(circleBrush, bounds);
        graphics.DrawEllipse(circlePen, bounds);

        using var titleFont = OverlayTheme.Font(_fontFamily, 10f, FontStyle.Bold);
        using var detailFont = OverlayTheme.Font(_fontFamily, 7.5f);
        using var titleBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        using var detailBrush = new SolidBrush(OverlayTheme.Colors.TextSecondary);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.DrawString("radar error", titleFont, titleBrush, bounds, format);
        var detailBounds = new RectangleF(bounds.Left + 28f, bounds.Top + bounds.Height / 2f + 14f, bounds.Width - 56f, 28f);
        graphics.DrawString(TrimError(_overlayError), detailFont, detailBrush, detailBounds, format);
    }

    private void ReportOverlayError(Exception exception, string operation)
    {
        _overlayError = $"{operation}: {exception.Message}";
        var now = DateTimeOffset.UtcNow;
        if (!string.Equals(_lastLoggedError, _overlayError, StringComparison.Ordinal)
            || _lastLoggedErrorAtUtc is null
            || now - _lastLoggedErrorAtUtc.Value > TimeSpan.FromSeconds(30))
        {
            _lastLoggedError = _overlayError;
            _lastLoggedErrorAtUtc = now;
            _logger.LogError(exception, "Car radar overlay {Operation} failed.", operation);
        }
    }

    private void DrawDistanceRings(Graphics graphics, RectangleF bounds)
    {
        using var ringPen = new Pen(WithAlpha(40, 255, 255, 255), 1f);
        for (var index = 1; index <= 2; index++)
        {
            var inset = bounds.Width * index / 6f;
            graphics.DrawEllipse(
                ringPen,
                bounds.X + inset,
                bounds.Y + inset,
                bounds.Width - inset * 2f,
                bounds.Height - inset * 2f);
        }
    }

    private void DrawPlayerCar(Graphics graphics, RectangleF bounds)
    {
        var centerX = bounds.X + bounds.Width / 2f;
        var centerY = bounds.Y + bounds.Height / 2f;
        var carRect = new RectangleF(centerX - 12f, centerY - 24f, 24f, 48f);
        using var brush = new SolidBrush(WithAlpha(240, 255, 255, 255));
        using var pen = new Pen(WithAlpha(230, 20, 24, 28));
        graphics.FillRoundedRectangle(brush, carRect, 4f);
        graphics.DrawRoundedRectangle(pen, carRect, 4f);
    }

    private void DrawMulticlassApproachWarning(Graphics graphics, RectangleF bounds)
    {
        if (CurrentMulticlassApproach() is not { } approach)
        {
            return;
        }

        var urgency = Math.Clamp(approach.Urgency, 0d, 1d);
        var alpha = (int)Math.Round(120d + urgency * 110d);
        var warningColor = WithAlpha(alpha, 236, 112, 99);
        var arcBounds = RectangleF.Inflate(bounds, -4f, -4f);
        using var arcPen = new Pen(warningColor, 5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawArc(arcPen, arcBounds, MulticlassWarningArcStartDegrees, MulticlassWarningArcSweepDegrees);

        using var font = OverlayTheme.Font(_fontFamily, 9f, FontStyle.Bold);
        using var textBrush = new SolidBrush(WithAlpha(alpha, 255, 225, 220));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        var labelBounds = new RectangleF(
            bounds.Left + 28f,
            bounds.Bottom - 48f,
            bounds.Width - 56f,
            18f);
        graphics.DrawString(FormatMulticlassWarning(approach), font, textBrush, labelBounds, format);
    }

    private void DrawNearbyCars(Graphics graphics, RectangleF bounds)
    {
        var centerX = bounds.X + bounds.Width / 2f;
        var centerY = bounds.Y + bounds.Height / 2f;
        var usableRadius = bounds.Width / 2f - 34f;
        var visibleCars = _carVisuals.Values
            .Where(visual => visual.Alpha > MinimumVisibleAlpha)
            .OrderBy(visual => Math.Abs(RangeRatio(visual.Car)))
            .ToArray();

        for (var index = 0; index < visibleCars.Length; index++)
        {
            var visual = visibleCars[index];
            var car = visual.Car;
            var ratio = RangeRatio(car);
            var closeness = 1d - Math.Clamp(Math.Abs(ratio), 0d, 1d);
            var laneOffset = LateralOffset(car, index, visibleCars.Length);
            var x = centerX + laneOffset;
            var y = centerY - (float)(ratio * usableRadius);
            var carRect = new RectangleF(x - 10f, y - 18f, 20f, 36f);
            var color = ProximityColor(closeness, visual.Alpha * _radarAlpha);

            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.FromArgb(ScaleAlpha(Math.Min(255, color.A + 36), _radarAlpha), 255, 255, 255), 1f);
            graphics.FillRoundedRectangle(brush, carRect, 4f);
            graphics.DrawRoundedRectangle(pen, carRect, 4f);
        }
    }

    private void DrawSideWarningCars(Graphics graphics, RectangleF bounds)
    {
        if (_leftSideAlpha <= MinimumVisibleAlpha && _rightSideAlpha <= MinimumVisibleAlpha)
        {
            return;
        }

        var centerX = bounds.X + bounds.Width / 2f;
        var centerY = bounds.Y + bounds.Height / 2f;
        if (_leftSideAlpha > MinimumVisibleAlpha)
        {
            DrawWarningCar(graphics, centerX - 42f, centerY, _leftSideAlpha * _radarAlpha);
        }

        if (_rightSideAlpha > MinimumVisibleAlpha)
        {
            DrawWarningCar(graphics, centerX + 42f, centerY, _rightSideAlpha * _radarAlpha);
        }
    }

    private static void DrawWarningCar(Graphics graphics, float x, float y, double alphaMultiplier)
    {
        var carRect = new RectangleF(x - 10f, y - 18f, 20f, 36f);
        using var brush = new SolidBrush(Color.FromArgb(ScaleAlpha(238, alphaMultiplier), 236, 112, 99));
        using var pen = new Pen(Color.FromArgb(ScaleAlpha(245, alphaMultiplier), 255, 255, 255), 1f);
        graphics.FillRoundedRectangle(brush, carRect, 4f);
        graphics.DrawRoundedRectangle(pen, carRect, 4f);
    }

    private float LateralOffset(LiveProximityCar car, int index, int visibleCount)
    {
        if (_proximity.HasCarLeft && car == _proximity.NearestBehind)
        {
            return -42f;
        }

        if (_proximity.HasCarRight && car == _proximity.NearestBehind)
        {
            return 42f;
        }

        if (_proximity.HasCarLeft && car == _proximity.NearestAhead)
        {
            return -42f;
        }

        if (_proximity.HasCarRight && car == _proximity.NearestAhead)
        {
            return 42f;
        }

        var lane = ((car.CarIdx + index) % 3) - 1;
        if (visibleCount <= 1)
        {
            lane = 0;
        }

        return lane * 32f;
    }

    private Color WithAlpha(int alpha, int red, int green, int blue)
    {
        return Color.FromArgb(ScaleAlpha(alpha, _radarAlpha), red, green, blue);
    }

    private static Color ProximityColor(double closeness, double visualAlpha)
    {
        var normalized = Math.Clamp(closeness, 0d, 1d);
        var redMix = SmoothStep(0.45d, 1d, normalized);
        var alpha = ScaleAlpha(
            (int)Math.Round(Math.Pow(normalized, 0.8d) * 238d),
            visualAlpha);
        return Color.FromArgb(
            alpha,
            Lerp(246, 236, redMix),
            Lerp(184, 112, redMix),
            Lerp(88, 99, redMix));
    }

    private static int ScaleAlpha(int alpha, double multiplier)
    {
        return (int)Math.Round(Math.Clamp(alpha * multiplier, 0d, 255d));
    }

    private static int Lerp(int start, int end, double ratio)
    {
        return (int)Math.Round(start + (end - start) * ratio);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var ratio = Math.Clamp((value - edge0) / (edge1 - edge0), 0d, 1d);
        return ratio * ratio * (3d - 2d * ratio);
    }

    private static double RangeRatio(LiveProximityCar car)
    {
        return Math.Clamp(
            car.RelativeSeconds is { } seconds
                ? seconds / RadarRangeSeconds
                : car.RelativeLaps / RadarRangeLaps,
            -1d,
            1d);
    }

    private static bool IsInRadarRange(LiveProximityCar car)
    {
        return car.RelativeSeconds is { } seconds
            ? Math.Abs(seconds) <= RadarRangeSeconds
            : Math.Abs(car.RelativeLaps) <= RadarRangeLaps;
    }

    private static string FormatMulticlassWarning(LiveMulticlassApproach approach)
    {
        return approach.RelativeSeconds is { } seconds
            ? FormattableString.Invariant($"multiclass {Math.Abs(seconds):0.0} seconds")
            : "multiclass approaching";
    }

    private static string TrimError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "see logs";
        }

        return message.Length <= 46 ? message : string.Concat(message.AsSpan(0, 43), "...");
    }

    private sealed class RadarCarVisual
    {
        public RadarCarVisual(LiveProximityCar car)
        {
            Car = car;
            LastSeenAtUtc = DateTimeOffset.UtcNow;
        }

        public LiveProximityCar Car { get; set; }

        public double Alpha { get; set; }

        public DateTimeOffset LastSeenAtUtc { get; set; }
    }
}

internal static class GraphicsRadarExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = RoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        using var path = RoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}
