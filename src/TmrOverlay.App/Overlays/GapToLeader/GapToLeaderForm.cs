using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.GapToLeader;

internal sealed class GapToLeaderForm : PersistentOverlayForm
{
    private static readonly TimeSpan TrendWindow = TimeSpan.FromHours(4);
    private const int AxisLabelWidth = 64;
    private const int MaxTrendPointsPerCar = 36_000;
    private const int MaxWeatherPoints = 36_000;
    private const int MaxDriverChangeMarkers = 64;
    private const double StickyVisibilityMinimumSeconds = 120d;
    private const double StickyVisibilityLaps = 1.5d;
    private const double EntryTailSeconds = 300d;
    private const double EntryFadeSeconds = 45d;
    private const double MissingSegmentGapSeconds = 10d;
    private const double MissingTelemetryGraceSeconds = 5d;

    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<GapToLeaderForm> _logger;
    private readonly AppPerformanceState _performanceState;
    private readonly OverlaySettings _settings;
    private readonly string _fontFamily;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _sourceLabel;
    // Overlay-local render buffer only. The gap overlay never persists this trace.
    private readonly Dictionary<int, List<GapTrendPoint>> _series = [];
    private readonly List<WeatherTrendPoint> _weather = [];
    private readonly List<DriverChangeMarker> _driverChangeMarkers = [];
    private readonly List<LeaderChangeMarker> _leaderChangeMarkers = [];
    private readonly Dictionary<int, CarRenderState> _carRenderStates = [];
    private readonly Dictionary<int, DriverIdentity> _driverIdentities = [];
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private long _lastSequence;
    private LiveLeaderGapSnapshot _gap = LiveLeaderGapSnapshot.Unavailable;
    private DateTimeOffset? _latestPointAtUtc;
    private double? _latestAxisSeconds;
    private double? _trendStartAxisSeconds;
    private double? _lapReferenceSeconds;
    private int? _lastDriversSoFar;
    private int? _lastClassLeaderCarIdx;
    private string? _overlayError;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    public GapToLeaderForm(
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<GapToLeaderForm> logger,
        AppPerformanceState performanceState,
        OverlaySettings settings,
        string fontFamily,
        Action saveSettings)
        : base(
            settings,
            saveSettings,
            GapToLeaderOverlayDefinition.Definition.DefaultWidth,
            GapToLeaderOverlayDefinition.Definition.DefaultHeight)
    {
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
        _performanceState = performanceState;
        _settings = settings;
        _fontFamily = fontFamily;

        BackColor = OverlayTheme.Colors.WindowBackground;
        Padding = new Padding(12);

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(_fontFamily, 11f, FontStyle.Bold),
            Location = new Point(14, 10),
            Size = new Size(210, 24),
            Text = "Class Gap Trend"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSubtle,
            Font = OverlayTheme.Font(_fontFamily, 9f),
            Location = new Point(224, 11),
            Size = new Size(ClientSize.Width - 238, 22),
            Text = "waiting",
            TextAlign = ContentAlignment.MiddleRight
        };

        _sourceLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextMuted,
            Font = OverlayTheme.Font(_fontFamily, 8.5f),
            Location = new Point(14, ClientSize.Height - 28),
            Size = new Size(ClientSize.Width - 28, 18),
            Text = "source: waiting",
            TextAlign = ContentAlignment.MiddleLeft
        };

        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_sourceLabel);

        RegisterDragSurfaces(_titleLabel, _statusLabel, _sourceLabel);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_statusLabel is null || _sourceLabel is null)
        {
            return;
        }

        _statusLabel.Location = new Point(224, 11);
        _statusLabel.Size = new Size(Math.Max(120, ClientSize.Width - 238), 22);
        _sourceLabel.Location = new Point(14, ClientSize.Height - 28);
        _sourceLabel.Size = new Size(Math.Max(260, ClientSize.Width - 28), 18);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _titleLabel.Dispose();
            _statusLabel.Dispose();
            _sourceLabel.Dispose();
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

            var graphBounds = new Rectangle(
                14,
                42,
                Math.Max(280, ClientSize.Width - 28),
                Math.Max(120, ClientSize.Height - 76));
            using var graphBrush = new SolidBrush(OverlayTheme.Colors.PanelBackground);
            e.Graphics.FillRectangle(graphBrush, graphBounds);
            e.Graphics.DrawRectangle(borderPen, graphBounds);

            if (_overlayError is not null)
            {
                DrawError(e.Graphics, graphBounds);
                succeeded = true;
                return;
            }

            var drawStarted = Stopwatch.GetTimestamp();
            var drawSucceeded = false;
            try
            {
                DrawGraph(e.Graphics, graphBounds);
                drawSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlayGapDrawGraph,
                    drawStarted,
                    drawSucceeded);
            }

            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "render");
            DrawError(
                e.Graphics,
                new Rectangle(
                    14,
                    42,
                    Math.Max(280, ClientSize.Width - 28),
                    Math.Max(120, ClientSize.Height - 76)));
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayGapPaint,
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
                    AppPerformanceMetricIds.OverlayGapSnapshot,
                    snapshotStarted,
                    snapshotSucceeded);
            }

            _gap = snapshot.LeaderGap;
            _lapReferenceSeconds = SelectLapReferenceSeconds(snapshot);
            if (snapshot.Sequence != _lastSequence)
            {
                _lastSequence = snapshot.Sequence;
                var recordStarted = Stopwatch.GetTimestamp();
                var recordSucceeded = false;
                try
                {
                    RecordGapSnapshot(snapshot);
                    recordSucceeded = true;
                }
                finally
                {
                    _performanceState.RecordOperation(
                        AppPerformanceMetricIds.OverlayGapRecordSnapshot,
                        recordStarted,
                        recordSucceeded);
                }
            }

            _overlayError = null;
            ApplyStatusColor(_gap);
            var selectedSeriesCount = 0;
            if (_gap.HasData)
            {
                var selectStarted = Stopwatch.GetTimestamp();
                var selectSucceeded = false;
                try
                {
                    selectedSeriesCount = SelectChartSeries().Count;
                    selectSucceeded = true;
                }
                finally
                {
                    _performanceState.RecordOperation(
                        AppPerformanceMetricIds.OverlayGapSelectSeries,
                        selectStarted,
                        selectSucceeded);
                }
            }

            _statusLabel.Text = _gap.HasData
                ? $"C{FormatPosition(_gap.TeamClassPosition)} {FormatGap(_gap.ClassLeaderGap)}"
                : "waiting";
            _sourceLabel.Text = _gap.HasData
                ? $"{FormatTrendWindow(TrendWindow)} class trend | cars {selectedSeriesCount}"
                : "source: waiting";
            Invalidate();
            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception, "refresh");
            ApplyErrorStatusColor();
            _statusLabel.Text = "graph error";
            _sourceLabel.Text = TrimError(_overlayError);
            Invalidate();
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayGapRefresh,
                started,
                succeeded);
        }
    }

    private void RecordGapSnapshot(LiveTelemetrySnapshot snapshot)
    {
        var timestamp = snapshot.LatestSample?.CapturedAtUtc
            ?? snapshot.LastUpdatedAtUtc
            ?? DateTimeOffset.UtcNow;
        var axisSeconds = SelectAxisSeconds(timestamp, snapshot.LatestSample?.SessionTime);
        _latestPointAtUtc = timestamp;
        _latestAxisSeconds = axisSeconds;
        if (_trendStartAxisSeconds is null || axisSeconds < _trendStartAxisSeconds.Value)
        {
            _trendStartAxisSeconds = axisSeconds;
        }

        RecordWeatherSnapshot(snapshot, axisSeconds);
        RecordDriverChangeMarkers(snapshot, timestamp, axisSeconds);
        RecordLeaderChange(snapshot, timestamp, axisSeconds);

        foreach (var car in snapshot.LeaderGap.ClassCars)
        {
            var gapSeconds = ChartGapSeconds(car);
            if (gapSeconds is null)
            {
                continue;
            }

            if (!_series.TryGetValue(car.CarIdx, out var points))
            {
                points = [];
                _series[car.CarIdx] = points;
            }

            var startsSegment = points.Count == 0 || axisSeconds - points[^1].AxisSeconds > MissingSegmentGapSeconds;
            if (points.Count > 0 && points[^1].TimestampUtc == timestamp)
            {
                points[^1] = new GapTrendPoint(timestamp, axisSeconds, gapSeconds.Value, car.IsTeamCar, car.IsClassLeader, car.ClassPosition, points[^1].StartsSegment);
            }
            else
            {
                points.Add(new GapTrendPoint(timestamp, axisSeconds, gapSeconds.Value, car.IsTeamCar, car.IsClassLeader, car.ClassPosition, startsSegment));
            }

            if (points.Count > MaxTrendPointsPerCar)
            {
                points.RemoveRange(0, points.Count - MaxTrendPointsPerCar);
            }
        }

        UpdateCarRenderStates(snapshot, axisSeconds);
        PruneOldPoints(axisSeconds);
    }

    private void DrawGraph(Graphics graphics, Rectangle graphBounds)
    {
        Rectangle plotBounds;
        Rectangle axisBounds;
        IReadOnlyList<ChartSeriesSelection> selectedSeries;
        TrendDomain domain;
        double maxGapSeconds;
        var prepareStarted = Stopwatch.GetTimestamp();
        var prepareSucceeded = false;
        try
        {
            var innerBounds = Rectangle.Inflate(graphBounds, -12, -14);
            innerBounds.Y += 10;
            innerBounds.Height -= 12;
            plotBounds = new Rectangle(
                innerBounds.Left + AxisLabelWidth,
                innerBounds.Top,
                Math.Max(40, innerBounds.Width - AxisLabelWidth),
                innerBounds.Height);
            axisBounds = new Rectangle(innerBounds.Left, innerBounds.Top, AxisLabelWidth - 8, innerBounds.Height);

            selectedSeries = SelectChartSeries();
            domain = SelectTimeDomain(selectedSeries);
            maxGapSeconds = SelectMaxGapSeconds(selectedSeries, domain.StartSeconds, domain.EndSeconds);
            prepareSucceeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayGapDrawPrepare,
                prepareStarted,
                prepareSucceeded);
        }

        var staticStarted = Stopwatch.GetTimestamp();
        var staticSucceeded = false;
        try
        {
            DrawWeatherBands(graphics, plotBounds, domain);
            DrawGridLines(graphics, plotBounds, axisBounds, domain, maxGapSeconds, _lapReferenceSeconds);
            DrawLeaderChangeMarkers(graphics, plotBounds, domain);

            using var leaderPen = new Pen(Color.FromArgb(235, 255, 255, 255), 1.6f);
            graphics.DrawLine(leaderPen, plotBounds.Left, plotBounds.Top, plotBounds.Right, plotBounds.Top);
            staticSucceeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayGapDrawStatic,
                staticStarted,
                staticSucceeded);
        }

        var endpointLabels = new List<EndpointLabel>();
        var seriesStarted = Stopwatch.GetTimestamp();
        var seriesSucceeded = false;
        try
        {
            foreach (var selection in selectedSeries
                .OrderBy(selection => selection.State.IsClassLeader)
                .ThenBy(selection => selection.State.IsTeamCar))
            {
                var state = selection.State;
                if (!_series.TryGetValue(state.CarIdx, out var points))
                {
                    continue;
                }

                var visiblePoints = points
                    .Where(point => point.AxisSeconds >= selection.DrawStartSeconds && point.AxisSeconds >= domain.StartSeconds && point.AxisSeconds <= domain.EndSeconds)
                    .ToArray();
                if (visiblePoints.Length == 0)
                {
                    continue;
                }

                var color = WithAlpha(SeriesColor(state), selection.Alpha * SeriesAlphaMultiplier(state));
                using var pen = new Pen(color, state.IsTeamCar ? 2.8f : state.IsClassLeader ? 1.8f : 1.25f);
                if (selection.IsStale || selection.IsStickyExit)
                {
                    pen.DashStyle = DashStyle.Dash;
                }

                DrawSeriesSegments(graphics, visiblePoints, pen, color, state.IsTeamCar, domain, maxGapSeconds, plotBounds);
                AddPositionLabel(endpointLabels, state, visiblePoints[^1], color, domain, maxGapSeconds, plotBounds);
                if (selection.IsStale)
                {
                    DrawTerminalMarker(graphics, visiblePoints[^1], color, domain, maxGapSeconds, plotBounds);
                }
            }

            seriesSucceeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayGapDrawSeries,
                seriesStarted,
                seriesSucceeded);
        }

        var labelsStarted = Stopwatch.GetTimestamp();
        var labelsSucceeded = false;
        try
        {
            DrawPositionLabels(graphics, endpointLabels, plotBounds);
            DrawDriverChangeMarkers(graphics, plotBounds, domain, maxGapSeconds);
            DrawScaleLabels(graphics, plotBounds, axisBounds, maxGapSeconds);
            labelsSucceeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlayGapDrawLabels,
                labelsStarted,
                labelsSucceeded);
        }
    }

    private void RecordWeatherSnapshot(LiveTelemetrySnapshot snapshot, double axisSeconds)
    {
        var condition = SelectWeatherCondition(snapshot);
        if (_weather.Count > 0 && Math.Abs(_weather[^1].AxisSeconds - axisSeconds) < 0.001d)
        {
            _weather[^1] = new WeatherTrendPoint(axisSeconds, condition);
        }
        else
        {
            _weather.Add(new WeatherTrendPoint(axisSeconds, condition));
        }

        if (_weather.Count > MaxWeatherPoints)
        {
            _weather.RemoveRange(0, _weather.Count - MaxWeatherPoints);
        }
    }

    private void RecordDriverChangeMarkers(LiveTelemetrySnapshot snapshot, DateTimeOffset timestamp, double axisSeconds)
    {
        RecordExplicitTeamDriverChangeMarker(snapshot, timestamp, axisSeconds);
        RecordSessionInfoDriverChangeMarkers(snapshot, timestamp, axisSeconds);
    }

    private void RecordExplicitTeamDriverChangeMarker(LiveTelemetrySnapshot snapshot, DateTimeOffset timestamp, double axisSeconds)
    {
        if (snapshot.LatestSample?.DriversSoFar is not { } driversSoFar || driversSoFar <= 0)
        {
            return;
        }

        if (_lastDriversSoFar is { } previousDrivers)
        {
            if (driversSoFar < previousDrivers)
            {
                _lastDriversSoFar = driversSoFar;
                return;
            }

            if (driversSoFar > previousDrivers
                && snapshot.LeaderGap.ClassCars.FirstOrDefault(car => car.IsTeamCar) is { } team
                && ChartGapSeconds(team) is { } gapSeconds)
            {
                AddDriverChangeMarker(new DriverChangeMarker(
                    timestamp,
                    axisSeconds,
                    team.CarIdx,
                    gapSeconds,
                    true,
                    $"D{driversSoFar}",
                    DriverChangeMarkerSource.TeamControl));
            }
        }

        _lastDriversSoFar = driversSoFar;
    }

    private void RecordSessionInfoDriverChangeMarkers(LiveTelemetrySnapshot snapshot, DateTimeOffset timestamp, double axisSeconds)
    {
        if (snapshot.Context.Drivers.Count == 0)
        {
            return;
        }

        foreach (var driver in snapshot.Context.Drivers)
        {
            if (ToDriverIdentity(driver) is not { } identity)
            {
                continue;
            }

            if (_driverIdentities.TryGetValue(identity.CarIdx, out var previous)
                && !previous.HasSameDriver(identity)
                && snapshot.LeaderGap.ClassCars.FirstOrDefault(car => car.CarIdx == identity.CarIdx) is { } car
                && ChartGapSeconds(car) is { } gapSeconds)
            {
                AddDriverChangeMarker(new DriverChangeMarker(
                    timestamp,
                    axisSeconds,
                    identity.CarIdx,
                    gapSeconds,
                    car.IsTeamCar,
                    identity.ShortLabel,
                    DriverChangeMarkerSource.SessionInfo));
            }

            _driverIdentities[identity.CarIdx] = identity;
        }
    }

    private void AddDriverChangeMarker(DriverChangeMarker marker)
    {
        if (_driverChangeMarkers.Any(existing =>
                existing.CarIdx == marker.CarIdx
                && Math.Abs(existing.AxisSeconds - marker.AxisSeconds) < 5d))
        {
            return;
        }

        _driverChangeMarkers.Add(marker);
        if (_driverChangeMarkers.Count > MaxDriverChangeMarkers)
        {
            _driverChangeMarkers.RemoveRange(0, _driverChangeMarkers.Count - MaxDriverChangeMarkers);
        }
    }

    private void RecordLeaderChange(LiveTelemetrySnapshot snapshot, DateTimeOffset timestamp, double axisSeconds)
    {
        if (snapshot.LeaderGap.ClassLeaderCarIdx is not { } leaderCarIdx)
        {
            return;
        }

        if (_lastClassLeaderCarIdx is { } previousLeader && previousLeader != leaderCarIdx)
        {
            _leaderChangeMarkers.Add(new LeaderChangeMarker(timestamp, axisSeconds, previousLeader, leaderCarIdx));
        }

        _lastClassLeaderCarIdx = leaderCarIdx;
    }

    private void UpdateCarRenderStates(LiveTelemetrySnapshot snapshot, double axisSeconds)
    {
        var desiredCarIds = SelectDesiredCarIds(snapshot.LeaderGap.ClassCars);
        foreach (var car in snapshot.LeaderGap.ClassCars)
        {
            if (ChartGapSeconds(car) is not { } gapSeconds)
            {
                continue;
            }

            if (!_carRenderStates.TryGetValue(car.CarIdx, out var state))
            {
                state = new CarRenderState(car.CarIdx);
                _carRenderStates[car.CarIdx] = state;
            }

            var wasVisible = ShouldKeepVisible(state, axisSeconds);
            state.LastSeenAxisSeconds = axisSeconds;
            state.LastGapSeconds = gapSeconds;
            state.IsTeamCar = car.IsTeamCar;
            state.IsClassLeader = car.IsClassLeader;
            state.ClassPosition = car.ClassPosition;
            state.DeltaSecondsToTeam = car.DeltaSecondsToTeam;
            state.IsCurrentlyDesired = desiredCarIds.Contains(car.CarIdx);
            if (state.IsCurrentlyDesired)
            {
                if (!wasVisible)
                {
                    state.VisibleSinceAxisSeconds = axisSeconds;
                }

                state.LastDesiredAxisSeconds = axisSeconds;
            }
        }

        foreach (var state in _carRenderStates.Values)
        {
            if (!desiredCarIds.Contains(state.CarIdx))
            {
                state.IsCurrentlyDesired = false;
            }
        }
    }

    private HashSet<int> SelectDesiredCarIds(IReadOnlyList<LiveClassGapCar> cars)
    {
        var selected = new HashSet<int>();
        foreach (var car in cars.Where(car => car.IsClassLeader || car.IsTeamCar))
        {
            selected.Add(car.CarIdx);
        }

        foreach (var car in cars
            .Where(car => !car.IsTeamCar
                && !car.IsClassLeader
                && car.DeltaSecondsToTeam is not null
                && car.DeltaSecondsToTeam.Value < 0d)
            .OrderByDescending(car => car.DeltaSecondsToTeam!.Value)
            .Take(_settings.GetIntegerOption(OverlayOptionKeys.GapCarsAhead, defaultValue: 5, minimum: 0, maximum: 12)))
        {
            selected.Add(car.CarIdx);
        }

        foreach (var car in cars
            .Where(car => !car.IsTeamCar
                && !car.IsClassLeader
                && car.DeltaSecondsToTeam is not null
                && car.DeltaSecondsToTeam.Value > 0d)
            .OrderBy(car => car.DeltaSecondsToTeam!.Value)
            .Take(_settings.GetIntegerOption(OverlayOptionKeys.GapCarsBehind, defaultValue: 5, minimum: 0, maximum: 12)))
        {
            selected.Add(car.CarIdx);
        }

        return selected;
    }

    private bool ShouldKeepVisible(CarRenderState state, double axisSeconds)
    {
        return state.LastDesiredAxisSeconds is { } lastDesired
            && axisSeconds - lastDesired <= StickyVisibilitySeconds();
    }

    private double StickyVisibilitySeconds()
    {
        return Math.Max(
            StickyVisibilityMinimumSeconds,
            _lapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * StickyVisibilityLaps
                : 0d);
    }

    private IReadOnlyList<ChartSeriesSelection> SelectChartSeries()
    {
        var now = _latestAxisSeconds ?? SelectAxisSeconds(_latestPointAtUtc ?? DateTimeOffset.UtcNow, null);
        return _carRenderStates.Values
            .Where(state => ShouldKeepVisible(state, now))
            .Select(state => ToSelection(state, now))
            .OrderBy(selection => selection.State.GapSortValue)
            .ToArray();
    }

    private ChartSeriesSelection ToSelection(CarRenderState state, double now)
    {
        var lastDesired = state.LastDesiredAxisSeconds ?? now;
        var visibleSince = state.VisibleSinceAxisSeconds ?? lastDesired;
        var isStickyExit = !state.IsCurrentlyDesired;
        var isStale = now - state.LastSeenAxisSeconds > MissingTelemetryGraceSeconds;
        var stickySeconds = StickyVisibilitySeconds();
        var exitAlpha = isStickyExit
            ? 1d - Math.Clamp((now - lastDesired) / Math.Max(1d, stickySeconds), 0d, 1d)
            : 1d;
        var entryAlpha = Math.Clamp((now - visibleSince) / EntryFadeSeconds, 0d, 1d);
        var alpha = Math.Clamp(Math.Min(exitAlpha, 0.35d + entryAlpha * 0.65d), 0.18d, 1d);
        var drawStartSeconds = now - visibleSince <= EntryFadeSeconds
            ? Math.Max(0d, visibleSince - EntryTailSeconds)
            : double.NegativeInfinity;
        return new ChartSeriesSelection(
            state,
            alpha,
            isStickyExit,
            isStale,
            drawStartSeconds);
    }

    private TrendDomain SelectTimeDomain(IReadOnlyList<ChartSeriesSelection> selectedSeries)
    {
        var latest = _latestAxisSeconds ?? SelectAxisSeconds(_latestPointAtUtc ?? DateTimeOffset.UtcNow, null);
        var anchor = _trendStartAxisSeconds ?? FirstVisibleAxisSeconds(selectedSeries) ?? latest;
        var start = latest - anchor > TrendWindow.TotalSeconds
            ? latest - TrendWindow.TotalSeconds
            : anchor;
        return new TrendDomain(start, start + TrendWindow.TotalSeconds);
    }

    private double? FirstVisibleAxisSeconds(IReadOnlyList<ChartSeriesSelection> selectedSeries)
    {
        double? firstVisiblePoint = null;
        foreach (var selection in selectedSeries)
        {
            if (!_series.TryGetValue(selection.State.CarIdx, out var points))
            {
                continue;
            }

            foreach (var point in points.Where(point => point.AxisSeconds >= selection.DrawStartSeconds))
            {
                if (firstVisiblePoint is null || point.AxisSeconds < firstVisiblePoint.Value)
                {
                    firstVisiblePoint = point.AxisSeconds;
                }
            }
        }

        return firstVisiblePoint;
    }

    private double SelectMaxGapSeconds(
        IReadOnlyList<ChartSeriesSelection> selectedSeries,
        double startSeconds,
        double endSeconds)
    {
        var maxGap = selectedSeries
            .Where(selection => _series.ContainsKey(selection.State.CarIdx))
            .SelectMany(selection => _series[selection.State.CarIdx].Where(point => point.AxisSeconds >= selection.DrawStartSeconds))
            .Where(point => point.AxisSeconds >= startSeconds && point.AxisSeconds <= endSeconds)
            .Select(point => point.GapSeconds)
            .DefaultIfEmpty(1d)
            .Max();
        return NiceCeiling(Math.Max(1d, maxGap));
    }

    private static PointF ToGraphPoint(
        GapTrendPoint point,
        double startSeconds,
        double endSeconds,
        double maxGapSeconds,
        Rectangle plotBounds)
    {
        var totalSeconds = Math.Max(1d, endSeconds - startSeconds);
        var xRatio = Math.Clamp((point.AxisSeconds - startSeconds) / totalSeconds, 0d, 1d);
        var yRatio = Math.Clamp(point.GapSeconds / maxGapSeconds, 0d, 1d);
        return new PointF(
            plotBounds.Left + (float)(xRatio * plotBounds.Width),
            plotBounds.Top + (float)(yRatio * plotBounds.Height));
    }

    private static PointF ToGraphPoint(
        DriverChangeMarker marker,
        double startSeconds,
        double endSeconds,
        double maxGapSeconds,
        Rectangle plotBounds)
    {
        var totalSeconds = Math.Max(1d, endSeconds - startSeconds);
        var xRatio = Math.Clamp((marker.AxisSeconds - startSeconds) / totalSeconds, 0d, 1d);
        var yRatio = Math.Clamp(marker.GapSeconds / maxGapSeconds, 0d, 1d);
        return new PointF(
            plotBounds.Left + (float)(xRatio * plotBounds.Width),
            plotBounds.Top + (float)(yRatio * plotBounds.Height));
    }

    private static float AxisSecondsToX(double axisSeconds, TrendDomain domain, Rectangle plotBounds)
    {
        var totalSeconds = Math.Max(1d, domain.EndSeconds - domain.StartSeconds);
        var ratio = Math.Clamp((axisSeconds - domain.StartSeconds) / totalSeconds, 0d, 1d);
        return plotBounds.Left + (float)(ratio * plotBounds.Width);
    }

    private static void DrawPoint(Graphics graphics, PointF point, Color color, float radius)
    {
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, point.X - radius, point.Y - radius, radius * 2f, radius * 2f);
    }

    private static void DrawSeriesSegments(
        Graphics graphics,
        IReadOnlyList<GapTrendPoint> points,
        Pen pen,
        Color color,
        bool isTeamCar,
        TrendDomain domain,
        double maxGapSeconds,
        Rectangle plotBounds)
    {
        var segment = new List<PointF>();
        foreach (var point in points)
        {
            if (point.StartsSegment && segment.Count > 0)
            {
                DrawSegment(graphics, segment, pen, color, isTeamCar);
                segment.Clear();
            }

            segment.Add(ToGraphPoint(point, domain.StartSeconds, domain.EndSeconds, maxGapSeconds, plotBounds));
        }

        DrawSegment(graphics, segment, pen, color, isTeamCar);
    }

    private static void DrawSegment(Graphics graphics, IReadOnlyList<PointF> segment, Pen pen, Color color, bool isTeamCar)
    {
        if (segment.Count == 0)
        {
            return;
        }

        if (segment.Count == 1)
        {
            DrawPoint(graphics, segment[0], color, isTeamCar ? 4f : 3f);
            return;
        }

        graphics.DrawLines(pen, segment.ToArray());
        DrawPoint(graphics, segment[^1], color, isTeamCar ? 4.5f : 3f);
    }

    private static void AddPositionLabel(
        List<EndpointLabel> labels,
        CarRenderState state,
        GapTrendPoint point,
        Color color,
        TrendDomain domain,
        double maxGapSeconds,
        Rectangle plotBounds)
    {
        if (state.ClassPosition is not { } position || position <= 0)
        {
            return;
        }

        var graphPoint = ToGraphPoint(point, domain.StartSeconds, domain.EndSeconds, maxGapSeconds, plotBounds);
        labels.Add(new EndpointLabel(
            $"P{position}",
            graphPoint,
            color,
            state.IsTeamCar,
            state.IsClassLeader));
    }

    private void DrawPositionLabels(Graphics graphics, IReadOnlyList<EndpointLabel> labels, Rectangle plotBounds)
    {
        if (labels.Count == 0)
        {
            return;
        }

        const float labelHeight = 13f;
        const float labelGap = 1f;
        var ordered = labels
            .OrderBy(label => label.Point.Y)
            .ThenByDescending(label => label.IsTeamCar)
            .ThenByDescending(label => label.IsClassLeader)
            .Select(label => new PositionedEndpointLabel(label, label.Point.Y - labelHeight / 2f))
            .ToArray();

        var minY = plotBounds.Top + 1f;
        var maxY = plotBounds.Bottom - labelHeight - 1f;
        for (var index = 0; index < ordered.Length; index++)
        {
            var y = Math.Clamp(ordered[index].Y, minY, maxY);
            if (index > 0)
            {
                y = Math.Max(y, ordered[index - 1].Y + labelHeight + labelGap);
            }

            ordered[index] = ordered[index] with { Y = y };
        }

        if (ordered[^1].Y > maxY)
        {
            var shift = ordered[^1].Y - maxY;
            for (var index = 0; index < ordered.Length; index++)
            {
                ordered[index] = ordered[index] with { Y = Math.Max(minY, ordered[index].Y - shift) };
            }
        }

        foreach (var positioned in ordered)
        {
            DrawPositionLabel(graphics, positioned.Label, positioned.Y, plotBounds);
        }
    }

    private void DrawPositionLabel(
        Graphics graphics,
        EndpointLabel label,
        float y,
        Rectangle plotBounds)
    {
        const float labelHeight = 13f;
        using var font = new Font(_fontFamily, label.IsTeamCar ? 7.5f : 7f, FontStyle.Regular, GraphicsUnit.Point);
        var textSize = graphics.MeasureString(label.Text, font);
        var x = Math.Min(plotBounds.Right - textSize.Width - 2f, label.Point.X + 6f);
        var labelBounds = new RectangleF(x - 2f, y, textSize.Width + 4f, labelHeight);

        if (Math.Abs(y + 6.5f - label.Point.Y) > 3f)
        {
            using var connectorPen = new Pen(WithAlpha(label.Color, 0.32d), 1f);
            graphics.DrawLine(connectorPen, label.Point.X + 3f, label.Point.Y, labelBounds.Left, y + 6.5f);
        }

        using var backgroundBrush = new SolidBrush(Color.FromArgb(label.IsTeamCar ? 188 : 150, 18, 30, 42));
        using var textBrush = new SolidBrush(WithAlpha(label.Color, label.IsTeamCar ? 1d : 0.78d));
        graphics.FillRectangle(backgroundBrush, labelBounds);
        graphics.DrawString(label.Text, font, textBrush, x, y - 1f);
    }

    private static void DrawTerminalMarker(
        Graphics graphics,
        GapTrendPoint point,
        Color color,
        TrendDomain domain,
        double maxGapSeconds,
        Rectangle plotBounds)
    {
        var graphPoint = ToGraphPoint(point, domain.StartSeconds, domain.EndSeconds, maxGapSeconds, plotBounds);
        using var pen = new Pen(color, 1.2f);
        graphics.DrawLine(pen, graphPoint.X - 4f, graphPoint.Y - 4f, graphPoint.X + 4f, graphPoint.Y + 4f);
        graphics.DrawLine(pen, graphPoint.X - 4f, graphPoint.Y + 4f, graphPoint.X + 4f, graphPoint.Y - 4f);
    }

    private void DrawLeaderChangeMarkers(Graphics graphics, Rectangle plotBounds, TrendDomain domain)
    {
        var markers = _leaderChangeMarkers
            .Where(marker => marker.AxisSeconds >= domain.StartSeconds && marker.AxisSeconds <= domain.EndSeconds)
            .ToArray();
        if (markers.Length == 0)
        {
            return;
        }

        using var markerPen = new Pen(Color.FromArgb(115, 255, 255, 255), 1f)
        {
            DashStyle = DashStyle.Dot
        };
        using var labelFont = new Font(_fontFamily, 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(150, 218, 226, 230));

        foreach (var marker in markers)
        {
            var x = AxisSecondsToX(marker.AxisSeconds, domain, plotBounds);
            graphics.DrawLine(markerPen, x, plotBounds.Top, x, plotBounds.Bottom);
            graphics.DrawString("leader", labelFont, labelBrush, x + 4f, plotBounds.Top + 4f);
        }
    }

    private void DrawWeatherBands(Graphics graphics, Rectangle plotBounds, TrendDomain domain)
    {
        if (_weather.Count == 0)
        {
            return;
        }

        var points = _weather
            .Where(point => point.AxisSeconds <= domain.EndSeconds)
            .OrderBy(point => point.AxisSeconds)
            .ToArray();
        if (points.Length == 0)
        {
            return;
        }

        var startIndex = Array.FindLastIndex(points, point => point.AxisSeconds <= domain.StartSeconds);
        var firstIndex = startIndex >= 0 ? startIndex : Array.FindIndex(points, point => point.AxisSeconds >= domain.StartSeconds);
        if (firstIndex < 0)
        {
            return;
        }

        for (var index = firstIndex; index < points.Length; index++)
        {
            var point = points[index];
            var segmentStart = Math.Max(domain.StartSeconds, point.AxisSeconds);
            var segmentEnd = index + 1 < points.Length
                ? Math.Min(domain.EndSeconds, points[index + 1].AxisSeconds)
                : domain.EndSeconds;
            if (segmentEnd <= domain.StartSeconds || segmentEnd <= segmentStart)
            {
                continue;
            }

            if (WeatherBandColor(point.Condition) is not { } color)
            {
                continue;
            }

            var left = AxisSecondsToX(segmentStart, domain, plotBounds);
            var right = AxisSecondsToX(segmentEnd, domain, plotBounds);
            var width = Math.Max(1f, right - left);
            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, left, plotBounds.Top, width, plotBounds.Height);

            if (point.Condition == WeatherCondition.DeclaredWet)
            {
                using var stripBrush = new SolidBrush(Color.FromArgb(44, 94, 190, 255));
                graphics.FillRectangle(stripBrush, left, plotBounds.Top, width, 4f);
            }
        }
    }

    private void DrawDriverChangeMarkers(Graphics graphics, Rectangle plotBounds, TrendDomain domain, double maxGapSeconds)
    {
        var markers = _driverChangeMarkers
            .Where(marker => marker.AxisSeconds >= domain.StartSeconds && marker.AxisSeconds <= domain.EndSeconds)
            .ToArray();
        if (markers.Length == 0)
        {
            return;
        }

        using var tickPen = new Pen(Color.FromArgb(205, 255, 255, 255), 1.2f);
        using var markerFill = new SolidBrush(Color.FromArgb(18, 30, 42));
        using var teamMarkerPen = new Pen(Color.FromArgb(112, 224, 146), 1.8f);
        using var otherMarkerPen = new Pen(Color.FromArgb(220, 235, 245, 255), 1.4f);
        using var labelFont = new Font(_fontFamily, 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(190, 205, 218, 228));

        foreach (var marker in markers)
        {
            var point = ToGraphPoint(marker, domain.StartSeconds, domain.EndSeconds, maxGapSeconds, plotBounds);
            graphics.DrawLine(tickPen, point.X, point.Y - 9f, point.X, point.Y + 9f);
            graphics.FillEllipse(markerFill, point.X - 4.5f, point.Y - 4.5f, 9f, 9f);
            graphics.DrawEllipse(marker.IsTeamCar ? teamMarkerPen : otherMarkerPen, point.X - 4.5f, point.Y - 4.5f, 9f, 9f);
            graphics.DrawString(marker.Label, labelFont, labelBrush, point.X + 6f, point.Y - 16f);
        }
    }

    private void DrawGridLines(
        Graphics graphics,
        Rectangle plotBounds,
        Rectangle axisBounds,
        TrendDomain domain,
        double maxGapSeconds,
        double? lapReferenceSeconds)
    {
        DrawLapIntervalLines(graphics, plotBounds, domain, lapReferenceSeconds);

        using var gridPen = new Pen(Color.FromArgb(34, 255, 255, 255), 1f);
        using var gridFont = new Font(_fontFamily, 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var gridBrush = new SolidBrush(Color.FromArgb(120, 138, 152, 160));
        using var labelFormat = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        var step = NiceGridStep(maxGapSeconds / 4d);
        for (var value = step; value < maxGapSeconds; value += step)
        {
            var y = GapToY(value, maxGapSeconds, plotBounds);
            graphics.DrawLine(gridPen, plotBounds.Left, y, plotBounds.Right, y);
            DrawAxisLabel(graphics, FormatAxisSeconds(value), gridFont, gridBrush, axisBounds, y, labelFormat);
        }

        if (lapReferenceSeconds is not { } lapSeconds || lapSeconds < 20d || maxGapSeconds < lapSeconds * 0.85d)
        {
            return;
        }

        using var lapPen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.25f);
        using var lapBrush = new SolidBrush(Color.FromArgb(205, 255, 255, 255));
        for (var lap = 1; lap * lapSeconds < maxGapSeconds; lap++)
        {
            var y = GapToY(lap * lapSeconds, maxGapSeconds, plotBounds);
            graphics.DrawLine(lapPen, plotBounds.Left, y, plotBounds.Right, y);
            DrawAxisLabel(graphics, $"+{lap} lap", gridFont, lapBrush, axisBounds, y, labelFormat);
        }
    }

    private void DrawLapIntervalLines(
        Graphics graphics,
        Rectangle plotBounds,
        TrendDomain domain,
        double? lapReferenceSeconds)
    {
        if (lapReferenceSeconds is not { } lapSeconds || lapSeconds < 20d)
        {
            return;
        }

        var intervalSeconds = lapSeconds * 5d;
        var durationSeconds = domain.EndSeconds - domain.StartSeconds;
        if (durationSeconds < intervalSeconds * 0.75d)
        {
            return;
        }

        using var linePen = new Pen(Color.FromArgb(34, 255, 255, 255), 1f);
        using var labelFont = new Font(_fontFamily, 7f, FontStyle.Regular, GraphicsUnit.Point);
        using var labelBrush = new SolidBrush(Color.FromArgb(128, 138, 152, 160));
        using var labelFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter
        };

        for (var elapsed = intervalSeconds; elapsed < durationSeconds; elapsed += intervalSeconds)
        {
            var x = plotBounds.Left + (float)(elapsed / durationSeconds * plotBounds.Width);
            graphics.DrawLine(linePen, x, plotBounds.Top, x, plotBounds.Bottom);
            graphics.DrawString(
                $"{elapsed / lapSeconds:0}L",
                labelFont,
                labelBrush,
                new RectangleF(x - 18f, plotBounds.Bottom + 1f, 36f, 12f),
                labelFormat);
        }
    }

    private void DrawScaleLabels(Graphics graphics, Rectangle plotBounds, Rectangle axisBounds, double maxGapSeconds)
    {
        using var font = new Font(_fontFamily, 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var brush = new SolidBrush(Color.FromArgb(138, 152, 160));
        using var labelFormat = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        DrawAxisLabel(graphics, "leader", font, brush, axisBounds, plotBounds.Top, labelFormat);
        DrawAxisLabel(graphics, FormatAxisSeconds(maxGapSeconds), font, brush, axisBounds, plotBounds.Bottom, labelFormat);

    }

    private static void DrawAxisLabel(
        Graphics graphics,
        string text,
        Font font,
        Brush brush,
        Rectangle axisBounds,
        float y,
        StringFormat labelFormat)
    {
        graphics.DrawString(
            text,
            font,
            brush,
            new RectangleF(axisBounds.Left, y - 8f, axisBounds.Width, 16f),
            labelFormat);
    }

    private void ApplyStatusColor(LiveLeaderGapSnapshot gap)
    {
        if (!gap.HasData)
        {
            BackColor = OverlayTheme.Colors.WindowBackground;
            _statusLabel.ForeColor = OverlayTheme.Colors.TextSubtle;
            return;
        }

        BackColor = OverlayTheme.Colors.InfoBackground;
        _statusLabel.ForeColor = OverlayTheme.Colors.InfoText;
    }

    private void ApplyErrorStatusColor()
    {
        BackColor = OverlayTheme.Colors.ErrorGraphBackground;
        _statusLabel.ForeColor = OverlayTheme.Colors.ErrorText;
    }

    private void DrawError(Graphics graphics, Rectangle graphBounds)
    {
        using var overlayBrush = new SolidBrush(Color.FromArgb(150, 42, 18, 22));
        graphics.FillRectangle(overlayBrush, graphBounds);

        using var borderPen = new Pen(Color.FromArgb(180, 236, 112, 99), 1f);
        graphics.DrawRectangle(borderPen, graphBounds);

        using var titleFont = new Font(_fontFamily, 10f, FontStyle.Bold, GraphicsUnit.Point);
        using var detailFont = new Font(_fontFamily, 8f, FontStyle.Regular, GraphicsUnit.Point);
        using var titleBrush = new SolidBrush(Color.FromArgb(238, 255, 225, 220));
        using var detailBrush = new SolidBrush(Color.FromArgb(205, 255, 225, 220));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.DrawString("gap graph error", titleFont, titleBrush, graphBounds, format);
        var detailBounds = new RectangleF(graphBounds.Left + 24f, graphBounds.Top + graphBounds.Height / 2f + 14f, graphBounds.Width - 48f, 28f);
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
            _logger.LogError(exception, "Gap-to-leader overlay {Operation} failed.", operation);
        }
    }

    private void PruneOldPoints(double latestAxisSeconds)
    {
        var cutoff = latestAxisSeconds - TrendWindow.TotalSeconds;
        foreach (var carIdx in _series.Keys.ToArray())
        {
            _series[carIdx].RemoveAll(point => point.AxisSeconds < cutoff);
            if (_series[carIdx].Count == 0)
            {
                _series.Remove(carIdx);
            }
        }

        _weather.RemoveAll(point => point.AxisSeconds < cutoff);
        _driverChangeMarkers.RemoveAll(marker => marker.AxisSeconds < cutoff);
        _leaderChangeMarkers.RemoveAll(marker => marker.AxisSeconds < cutoff);
        foreach (var carIdx in _carRenderStates.Keys.ToArray())
        {
            if (!_series.ContainsKey(carIdx)
                && _carRenderStates[carIdx].LastDesiredAxisSeconds is { } lastDesired
                && latestAxisSeconds - lastDesired > StickyVisibilitySeconds())
            {
                _carRenderStates.Remove(carIdx);
            }
        }
    }

    private static Color SeriesColor(CarRenderState car)
    {
        if (car.IsClassLeader)
        {
            return Color.FromArgb(235, 255, 255, 255);
        }

        if (car.IsTeamCar)
        {
            return Color.FromArgb(112, 224, 146);
        }

        return car.DeltaSecondsToTeam is not null && car.DeltaSecondsToTeam.Value < 0d
            ? Color.FromArgb(140, 190, 245)
            : Color.FromArgb(246, 184, 88);
    }

    private static double SeriesAlphaMultiplier(CarRenderState car)
    {
        return car.IsClassLeader || car.IsTeamCar
            ? 1d
            : 0.48d;
    }

    private static Color WithAlpha(Color color, double alpha)
    {
        return Color.FromArgb(
            (int)Math.Clamp(color.A * alpha, 0d, 255d),
            color.R,
            color.G,
            color.B);
    }

    private static double? ChartGapSeconds(LiveClassGapCar car)
    {
        return car.GapSecondsToClassLeader
            ?? (car.GapLapsToClassLeader is { } laps ? laps * 60d : null);
    }

    private static WeatherCondition SelectWeatherCondition(LiveTelemetrySnapshot snapshot)
    {
        if (snapshot.LatestSample is not { } sample)
        {
            return WeatherCondition.Unknown;
        }

        if (sample.WeatherDeclaredWet)
        {
            return WeatherCondition.DeclaredWet;
        }

        return sample.TrackWetness switch
        {
            >= 4 => WeatherCondition.Wet,
            >= 2 => WeatherCondition.Damp,
            >= 0 => WeatherCondition.Dry,
            _ => WeatherCondition.Unknown
        };
    }

    private static Color? WeatherBandColor(WeatherCondition condition)
    {
        return condition switch
        {
            WeatherCondition.Damp => Color.FromArgb(12, 75, 170, 205),
            WeatherCondition.Wet => Color.FromArgb(20, 70, 135, 230),
            WeatherCondition.DeclaredWet => Color.FromArgb(28, 78, 142, 238),
            _ => null
        };
    }

    private static DriverIdentity? ToDriverIdentity(HistoricalSessionDriver driver)
    {
        if (driver.CarIdx is not { } carIdx || driver.IsSpectator == true)
        {
            return null;
        }

        var key = driver.UserId is { } userId && userId > 0
            ? FormattableString.Invariant($"id:{userId}")
            : !string.IsNullOrWhiteSpace(driver.UserName)
                ? $"name:{driver.UserName.Trim().ToUpperInvariant()}"
                : null;
        if (key is null)
        {
            return null;
        }

        return new DriverIdentity(
            carIdx,
            key,
            SelectDriverLabel(driver));
    }

    private static string SelectDriverLabel(HistoricalSessionDriver driver)
    {
        foreach (var value in new[] { driver.Initials, driver.AbbrevName, driver.UserName })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Length <= 3
                    ? value.Trim()
                    : value.Trim()[..Math.Min(3, value.Trim().Length)].ToUpperInvariant();
            }
        }

        return "DR";
    }

    private static double SelectAxisSeconds(DateTimeOffset timestampUtc, double? sessionTimeSeconds)
    {
        return sessionTimeSeconds is { } sessionTime
            && sessionTime >= 0d
            && !double.IsNaN(sessionTime)
            && !double.IsInfinity(sessionTime)
            ? sessionTime
            : timestampUtc.ToUnixTimeMilliseconds() / 1000d;
    }

    private static double? SelectLapReferenceSeconds(LiveTelemetrySnapshot snapshot)
    {
        if (IsValidLapReference(snapshot.Fuel.LapTimeSeconds))
        {
            return snapshot.Fuel.LapTimeSeconds;
        }

        if (IsValidLapReference(snapshot.Context.Car.DriverCarEstLapTimeSeconds))
        {
            return snapshot.Context.Car.DriverCarEstLapTimeSeconds;
        }

        return IsValidLapReference(snapshot.Context.Car.CarClassEstLapTimeSeconds)
            ? snapshot.Context.Car.CarClassEstLapTimeSeconds
            : null;
    }

    private static bool IsValidLapReference(double? seconds)
    {
        return seconds is { } value && value is > 20d and < 1800d && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static float GapToY(double gapSeconds, double maxGapSeconds, Rectangle plotBounds)
    {
        return plotBounds.Top + (float)(Math.Clamp(gapSeconds / maxGapSeconds, 0d, 1d) * plotBounds.Height);
    }

    private static double NiceCeiling(double value)
    {
        if (value <= 1d)
        {
            return 1d;
        }

        var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        foreach (var step in new[] { 1d, 1.5d, 2d, 3d, 5d, 7.5d, 10d })
        {
            if (normalized <= step)
            {
                return step * magnitude;
            }
        }

        return 10d * magnitude;
    }

    private static double NiceGridStep(double value)
    {
        if (value <= 0.25d)
        {
            return 0.25d;
        }

        var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        foreach (var step in new[] { 1d, 2d, 2.5d, 5d, 10d })
        {
            if (normalized <= step)
            {
                return step * magnitude;
            }
        }

        return 10d * magnitude;
    }

    private static string FormatAxisSeconds(double seconds)
    {
        if (seconds < 10d)
        {
            return FormattableString.Invariant($"+{seconds:0.#}s");
        }

        return FormatGapSeconds(seconds);
    }

    private static string FormatTrendWindow(TimeSpan trendWindow)
    {
        return trendWindow.TotalHours >= 1d
            ? FormattableString.Invariant($"{trendWindow.TotalHours:0.#}h")
            : FormattableString.Invariant($"{trendWindow.TotalMinutes:0}m");
    }

    private static string FormatPosition(int? position)
    {
        return position is { } value && value > 0
            ? value.ToString("0")
            : "--";
    }

    private static string FormatGap(LiveGapValue gap)
    {
        if (!gap.HasData)
        {
            return "--";
        }

        if (gap.IsLeader)
        {
            return "leader";
        }

        return gap.Seconds is { } seconds
            ? FormatGapSeconds(seconds)
            : gap.Laps is { } laps
                ? FormattableString.Invariant($"+{laps:0.00} lap")
                : "--";
    }

    private static string FormatGapSeconds(double seconds)
    {
        return seconds >= 60d
            ? FormattableString.Invariant($"+{Math.Floor(seconds / 60d):0}:{seconds % 60d:00.0}")
            : FormattableString.Invariant($"+{seconds:0.0}s");
    }

    private static string TrimError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "see logs";
        }

        return message.Length <= 72 ? message : string.Concat(message.AsSpan(0, 69), "...");
    }

    private sealed record GapTrendPoint(
        DateTimeOffset TimestampUtc,
        double AxisSeconds,
        double GapSeconds,
        bool IsTeamCar,
        bool IsClassLeader,
        int? ClassPosition,
        bool StartsSegment);

    private sealed record WeatherTrendPoint(double AxisSeconds, WeatherCondition Condition);

    private sealed record LeaderChangeMarker(
        DateTimeOffset TimestampUtc,
        double AxisSeconds,
        int PreviousLeaderCarIdx,
        int NewLeaderCarIdx);

    private sealed record DriverChangeMarker(
        DateTimeOffset TimestampUtc,
        double AxisSeconds,
        int CarIdx,
        double GapSeconds,
        bool IsTeamCar,
        string Label,
        DriverChangeMarkerSource Source);

    private sealed record DriverIdentity(
        int CarIdx,
        string DriverKey,
        string ShortLabel)
    {
        public bool HasSameDriver(DriverIdentity other)
        {
            return string.Equals(DriverKey, other.DriverKey, StringComparison.Ordinal);
        }
    }

    private sealed record ChartSeriesSelection(
        CarRenderState State,
        double Alpha,
        bool IsStickyExit,
        bool IsStale,
        double DrawStartSeconds);

    private sealed record EndpointLabel(
        string Text,
        PointF Point,
        Color Color,
        bool IsTeamCar,
        bool IsClassLeader);

    private sealed record PositionedEndpointLabel(EndpointLabel Label, float Y);

    private sealed class CarRenderState(int carIdx)
    {
        public int CarIdx { get; } = carIdx;

        public double LastSeenAxisSeconds { get; set; }

        public double LastGapSeconds { get; set; }

        public double? LastDesiredAxisSeconds { get; set; }

        public double? VisibleSinceAxisSeconds { get; set; }

        public bool IsCurrentlyDesired { get; set; }

        public bool IsTeamCar { get; set; }

        public bool IsClassLeader { get; set; }

        public int? ClassPosition { get; set; }

        public double? DeltaSecondsToTeam { get; set; }

        public double GapSortValue => LastGapSeconds;
    }

    private sealed record TrendDomain(double StartSeconds, double EndSeconds);

    private enum WeatherCondition
    {
        Unknown,
        Dry,
        Damp,
        Wet,
        DeclaredWet
    }

    private enum DriverChangeMarkerSource
    {
        TeamControl,
        SessionInfo
    }
}
