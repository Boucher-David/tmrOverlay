using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.History;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.DesignV2;

internal enum DesignV2LiveOverlayKind
{
    Standings,
    FuelCalculator,
    Relative,
    TrackMap,
    StreamChat,
    Flags,
    SessionWeather,
    PitService,
    InputState,
    CarRadar,
    GapToLeader
}

internal sealed class DesignV2LiveOverlayForm : PersistentOverlayForm
{
    private const int RefreshIntervalMilliseconds = 250;
    private const int PaddingSize = 16;
    private const int HeaderHeight = 38;
    private const int FooterHeight = 32;
    private const int BodyGap = 12;
    private const int RowHeight = 30;
    private const int RowGap = 5;
    private const int ColumnGap = 8;
    private const int MinimumColumnWidth = 24;
    private const int MetricLabelWidth = 124;

    private static readonly Color Surface = Color.FromArgb(242, 9, 14, 32);
    private static readonly Color SurfaceInset = Color.FromArgb(230, 13, 21, 44);
    private static readonly Color SurfaceRaised = Color.FromArgb(235, 18, 31, 60);
    private static readonly Color TitleBar = Color.FromArgb(248, 8, 10, 28);
    private static readonly Color Border = Color.FromArgb(210, 40, 72, 108);
    private static readonly Color BorderMuted = Color.FromArgb(150, 32, 54, 84);
    private static readonly Color TextPrimary = Color.FromArgb(255, 247, 255);
    private static readonly Color TextSecondary = Color.FromArgb(208, 230, 255);
    private static readonly Color TextMuted = Color.FromArgb(140, 174, 212);
    private static readonly Color Cyan = Color.FromArgb(0, 232, 255);
    private static readonly Color Magenta = Color.FromArgb(255, 42, 167);
    private static readonly Color Amber = Color.FromArgb(255, 209, 91);
    private static readonly Color Green = Color.FromArgb(98, 255, 159);
    private static readonly Color Orange = Color.FromArgb(255, 125, 73);
    private static readonly Color Error = Color.FromArgb(255, 98, 116);

    private readonly DesignV2LiveOverlayKind _kind;
    private readonly OverlayDefinition _definition;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly AppPerformanceState _performanceState;
    private readonly ILogger _logger;
    private readonly OverlaySettings _settings;
    private readonly string _fontFamily;
    private readonly string _unitSystem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly List<double> _gapPoints = [];
    private readonly List<DesignV2InputPoint> _inputTrace = [];
    private DesignV2OverlayModel _model;
    private HistoricalComboIdentity? _cachedHistoryCombo;
    private SessionHistoryLookupResult? _cachedHistory;
    private DateTimeOffset _cachedHistoryAtUtc;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;

    public DesignV2LiveOverlayForm(
        DesignV2LiveOverlayKind kind,
        OverlayDefinition definition,
        ILiveTelemetrySource liveTelemetrySource,
        SessionHistoryQueryService historyQueryService,
        AppPerformanceState performanceState,
        ILogger logger,
        OverlaySettings settings,
        string fontFamily,
        string unitSystem,
        Action saveSettings)
        : base(settings, saveSettings, definition.DefaultWidth, definition.DefaultHeight)
    {
        _kind = kind;
        _definition = definition;
        _liveTelemetrySource = liveTelemetrySource;
        _historyQueryService = historyQueryService;
        _performanceState = performanceState;
        _logger = logger;
        _settings = settings;
        _fontFamily = fontFamily;
        _unitSystem = unitSystem;
        _model = WaitingModel(TitleFor(kind), "waiting");

        BackColor = Color.Black;
        Padding = Padding.Empty;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        _refreshTimer.Tick += (_, _) =>
        {
            _performanceState.RecordOverlayTimerTick(
                _definition.Id,
                RefreshIntervalMilliseconds,
                Visible,
                !Visible || Opacity <= 0.001d);
            RefreshOverlay();
        };
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
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            DrawOverlay(e.Graphics, ClientRectangle, _model);
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation($"overlay.{_definition.Id}.design_v2.paint", started, succeeded);
        }
    }

    private void RefreshOverlay()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var snapshot = _liveTelemetrySource.Snapshot();
            _model = BuildModel(snapshot, DateTimeOffset.UtcNow);
            Invalidate();
            succeeded = true;
        }
        catch (Exception exception)
        {
            ReportOverlayError(exception);
            _model = new DesignV2OverlayModel(
                TitleFor(_kind),
                "overlay error",
                Trim(exception.Message, 96),
                DesignV2Evidence.Error,
                new DesignV2MetricRowsBody([new DesignV2MetricRow("Error", Trim(exception.Message, 120), DesignV2Evidence.Error)]));
            Invalidate();
        }
        finally
        {
            _performanceState.RecordOperation($"overlay.{_definition.Id}.design_v2.refresh", started, succeeded);
        }
    }

    private DesignV2OverlayModel BuildModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        return _kind switch
        {
            DesignV2LiveOverlayKind.Standings => BuildStandingsModel(snapshot, now),
            DesignV2LiveOverlayKind.Relative => BuildRelativeModel(snapshot, now),
            DesignV2LiveOverlayKind.FuelCalculator => BuildFuelModel(snapshot),
            DesignV2LiveOverlayKind.SessionWeather => FromSimple(SessionWeatherOverlayViewModel.From(snapshot, now, _unitSystem)),
            DesignV2LiveOverlayKind.PitService => FromSimple(PitServiceOverlayViewModel.From(snapshot, now, _unitSystem)),
            DesignV2LiveOverlayKind.InputState => BuildInputModel(snapshot, now),
            DesignV2LiveOverlayKind.Flags => FromSimple(FlagsOverlayViewModel.From(snapshot, now, _unitSystem)),
            DesignV2LiveOverlayKind.CarRadar => BuildRadarModel(snapshot, now),
            DesignV2LiveOverlayKind.GapToLeader => BuildGapModel(snapshot, now),
            DesignV2LiveOverlayKind.TrackMap => BuildTrackMapModel(snapshot, now),
            DesignV2LiveOverlayKind.StreamChat => BuildStreamChatModel(),
            _ => WaitingModel(TitleFor(_kind), "waiting")
        };
    }

    private DesignV2OverlayModel BuildStandingsModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var showClassSeparators = OverlayContentColumnSettings.Standings.Blocks is { Count: > 0 } blocks
            && OverlayContentColumnSettings.BlockEnabled(_settings, blocks[0]);
        var otherRows = OverlayContentColumnSettings.Standings.Blocks is { Count: > 0 } otherBlocks
            ? OverlayContentColumnSettings.BlockCount(_settings, otherBlocks[0])
            : 2;
        var visibleRows = Math.Max(1, (ClientSize.Height - HeaderHeight - FooterHeight - BodyGap - 36) / (RowHeight + RowGap));
        var viewModel = StandingsOverlayViewModel.From(
            snapshot,
            now,
            maximumRows: Math.Clamp(visibleRows, 1, 20),
            otherClassRowsPerClass: otherRows,
            showClassSeparators: showClassSeparators);
        var columns = OverlayContentColumnSettings.VisibleColumnsFor(_settings, OverlayContentColumnSettings.Standings)
            .Select(column => new DesignV2Column(column.Label, column.Width, AlignmentFor(column.Alignment)))
            .ToArray();
        var rows = viewModel.Rows.Select(row => new DesignV2TableRow(
            ValuesForStandingsRow(row),
            row.IsReference,
            row.IsClassHeader,
            row.IsPartial ? DesignV2Evidence.Partial : DesignV2Evidence.Measured,
            row.CarClassColorHex,
            row.IsClassHeader ? row.Driver : string.Empty,
            row.IsClassHeader ? ClassHeaderDetail(row) : string.Empty)).ToArray();
        return new DesignV2OverlayModel(
            "Standings",
            viewModel.Status,
            viewModel.Source,
            rows.Length == 0 ? DesignV2Evidence.Unavailable : DesignV2Evidence.Measured,
            new DesignV2TableBody(columns, rows));
    }

    private IReadOnlyList<string> ValuesForStandingsRow(StandingsOverlayRowViewModel row)
    {
        var valuesByKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OverlayContentColumnSettings.DataClassPosition] = row.ClassPosition,
            [OverlayContentColumnSettings.DataCarNumber] = row.CarNumber,
            [OverlayContentColumnSettings.DataDriver] = row.Driver,
            [OverlayContentColumnSettings.DataGap] = row.Gap,
            [OverlayContentColumnSettings.DataInterval] = row.Interval,
            [OverlayContentColumnSettings.DataPit] = row.Pit
        };
        return OverlayContentColumnSettings.VisibleColumnsFor(_settings, OverlayContentColumnSettings.Standings)
            .Select(column => valuesByKey.TryGetValue(column.DataKey, out var value) ? value : string.Empty)
            .ToArray();
    }

    private static string ClassHeaderDetail(StandingsOverlayRowViewModel row)
    {
        var parts = new[] { row.Gap, row.Interval }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return string.Join(" | ", parts);
    }

    private DesignV2OverlayModel BuildRelativeModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            _settings.GetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, 5, 0, 8),
            _settings.GetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, 5, 0, 8));
        var columns = OverlayContentColumnSettings.VisibleColumnsFor(_settings, OverlayContentColumnSettings.Relative)
            .Select(column => new DesignV2Column(column.Label, column.Width, AlignmentFor(column.Alignment)))
            .ToArray();
        var rows = viewModel.Rows.Select(row => new DesignV2TableRow(
            ValuesForRelativeRow(row),
            row.IsReference,
            IsClassHeader: false,
            row.IsPartial ? DesignV2Evidence.Partial : DesignV2Evidence.Measured,
            row.ClassColorHex)).ToArray();
        return new DesignV2OverlayModel(
            "Relative",
            viewModel.Status,
            viewModel.Source,
            rows.Length == 0 ? DesignV2Evidence.Unavailable : DesignV2Evidence.Live,
            new DesignV2TableBody(columns, rows));
    }

    private IReadOnlyList<string> ValuesForRelativeRow(RelativeOverlayRowViewModel row)
    {
        var valuesByKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OverlayContentColumnSettings.DataRelativePosition] = row.Position,
            [OverlayContentColumnSettings.DataDriver] = row.Driver,
            [OverlayContentColumnSettings.DataGap] = row.Gap,
            [OverlayContentColumnSettings.DataPit] = row.IsPit ? "PIT" : string.Empty
        };
        return OverlayContentColumnSettings.VisibleColumnsFor(_settings, OverlayContentColumnSettings.Relative)
            .Select(column => valuesByKey.TryGetValue(column.DataKey, out var value) ? value : string.Empty)
            .ToArray();
    }

    private DesignV2OverlayModel BuildFuelModel(LiveTelemetrySnapshot snapshot)
    {
        var history = LookupHistory(snapshot.Combo);
        var strategy = FuelStrategyCalculator.From(snapshot, history);
        var viewModel = FuelCalculatorViewModel.From(
            strategy,
            history,
            _settings.GetBooleanOption(OverlayOptionKeys.FuelAdvice, defaultValue: true),
            _unitSystem,
            maximumRows: Math.Max(1, (ClientSize.Height - HeaderHeight - FooterHeight - BodyGap - 34) / (RowHeight + RowGap)));
        var rows = new List<DesignV2MetricRow>
        {
            new("Plan", viewModel.Overview, DesignV2Evidence.Modeled)
        };
        rows.AddRange(viewModel.Rows.Select(row => new DesignV2MetricRow(
            row.Label,
            string.IsNullOrWhiteSpace(row.Advice) ? row.Value : $"{row.Value} | {row.Advice}",
            DesignV2Evidence.Modeled)));
        return new DesignV2OverlayModel(
            "Fuel Calculator",
            viewModel.Status,
            viewModel.Source,
            DesignV2Evidence.Modeled,
            new DesignV2MetricRowsBody(rows));
    }

    private DesignV2OverlayModel BuildInputModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.Models.Inputs.HasData)
        {
            _inputTrace.Add(new DesignV2InputPoint(
                snapshot.Models.Inputs.Throttle,
                snapshot.Models.Inputs.Brake,
                snapshot.Models.Inputs.Clutch,
                snapshot.Models.Inputs.BrakeAbsActive == true));
            if (_inputTrace.Count > 120)
            {
                _inputTrace.RemoveRange(0, _inputTrace.Count - 120);
            }
        }

        var viewModel = InputStateOverlayViewModel.From(snapshot, now, _unitSystem);
        return new DesignV2OverlayModel(
            "Inputs",
            viewModel.Status,
            viewModel.Source,
            EvidenceFor(viewModel.Tone),
            new DesignV2InputsBody(viewModel.Rows, _inputTrace.ToArray()));
    }

    private DesignV2OverlayModel BuildRadarModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        if (!availability.IsAvailable)
        {
            return WaitingModel("Car Radar", availability.StatusText);
        }

        var proximity = snapshot.Proximity;
        var status = proximity.HasCarLeft && proximity.HasCarRight
            ? "cars both sides"
            : proximity.HasCarLeft
                ? "car left"
                : proximity.HasCarRight
                    ? "car right"
                    : proximity.StrongestMulticlassApproach is not null
                        ? "class traffic"
                        : "clear";
        return new DesignV2OverlayModel(
            "Car Radar",
            status,
            proximity.HasData ? "source: spatial telemetry" : "source: waiting",
            proximity.HasData ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable,
            new DesignV2RadarBody(
                proximity.HasCarLeft,
                proximity.HasCarRight,
                proximity.NearbyCars.Count,
                proximity.StrongestMulticlassApproach is not null));
    }

    private DesignV2OverlayModel BuildGapModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var gap = TmrOverlay.App.Overlays.GapToLeader.GapToLeaderLiveModelAdapter.Select(snapshot);
        if (gap.HasData && gap.ClassLeaderGap.Seconds is { } seconds && IsFinite(seconds))
        {
            _gapPoints.Add(seconds);
            if (_gapPoints.Count > 120)
            {
                _gapPoints.RemoveRange(0, _gapPoints.Count - 120);
            }
        }

        if (!availability.IsAvailable)
        {
            return new DesignV2OverlayModel(
                "Focused Gap Trend",
                availability.StatusText,
                "source: waiting",
                DesignV2Evidence.Unavailable,
                new DesignV2GraphBody(_gapPoints.ToArray()));
        }

        var status = gap.HasData
            ? $"{FormatPosition(gap.ReferenceClassPosition)} {FormatGap(gap.ClassLeaderGap)}"
            : "waiting";
        var footer = gap.HasData
            ? $"source: live gap telemetry | cars {gap.ClassCars.Count}"
            : "source: waiting";
        return new DesignV2OverlayModel(
            "Focused Gap Trend",
            status,
            footer,
            gap.HasData ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable,
            new DesignV2GraphBody(_gapPoints.ToArray()));
    }

    private DesignV2OverlayModel BuildTrackMapModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var trackMap = snapshot.Models.TrackMap;
        var race = snapshot.Models.RaceEvents;
        var rows = new[]
        {
            new DesignV2MetricRow("Track", snapshot.Combo.TrackKey, DesignV2Evidence.Measured),
            new DesignV2MetricRow("Lap", race.HasData ? $"{race.LapCompleted + 1}" : "--", DesignV2Evidence.Live),
            new DesignV2MetricRow("Progress", race.HasData ? $"{race.LapDistPct * 100d:0.0}%" : "--", DesignV2Evidence.Live),
            new DesignV2MetricRow("Sectors", trackMap.Sectors.Count > 0 ? $"{trackMap.Sectors.Count}" : "--", DesignV2Evidence.Measured)
        };
        return new DesignV2OverlayModel(
            "Track Map",
            availability.IsAvailable ? "live map" : availability.StatusText,
            trackMap.HasLiveTiming ? "source: live timing + track model" : "source: track model",
            trackMap.HasLiveTiming ? DesignV2Evidence.Live : DesignV2Evidence.Partial,
            new DesignV2MetricRowsBody(rows));
    }

    private DesignV2OverlayModel BuildStreamChatModel()
    {
        var provider = _settings.GetStringOption(OverlayOptionKeys.StreamChatProvider, "none").Trim().ToLowerInvariant();
        DesignV2MetricRow[] rows = provider switch
        {
            "twitch" => new[]
            {
                new DesignV2MetricRow(
                    "Twitch",
                    string.IsNullOrWhiteSpace(_settings.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel))
                        ? "add channel in settings"
                        : $"#{_settings.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel)}",
                    DesignV2Evidence.Partial)
            },
            "streamlabs" => new[]
            {
                new DesignV2MetricRow(
                    "Streamlabs",
                    string.IsNullOrWhiteSpace(_settings.GetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl))
                        ? "add URL in settings"
                        : "browser source configured",
                    DesignV2Evidence.Partial)
            },
            _ => new[]
            {
                new DesignV2MetricRow("Provider", "choose Twitch or Streamlabs", DesignV2Evidence.Unavailable)
            }
        };
        return new DesignV2OverlayModel(
            "Stream Chat",
            provider == "none" || string.IsNullOrWhiteSpace(provider) ? "not configured" : provider,
            "source: stream chat settings",
            provider == "none" || string.IsNullOrWhiteSpace(provider) ? DesignV2Evidence.Unavailable : DesignV2Evidence.Live,
            new DesignV2MetricRowsBody(rows));
    }

    private DesignV2OverlayModel FromSimple(SimpleTelemetryOverlayViewModel viewModel)
    {
        return new DesignV2OverlayModel(
            viewModel.Title,
            viewModel.Status,
            viewModel.Source,
            EvidenceFor(viewModel.Tone),
            new DesignV2MetricRowsBody(viewModel.Rows.Select(row => new DesignV2MetricRow(
                row.Label,
                row.Value,
                EvidenceFor(row.Tone))).ToArray()));
    }

    private SessionHistoryLookupResult LookupHistory(HistoricalComboIdentity combo)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedHistory is not null
            && _cachedHistoryCombo is not null
            && string.Equals(_cachedHistoryCombo.CarKey, combo.CarKey, StringComparison.Ordinal)
            && string.Equals(_cachedHistoryCombo.TrackKey, combo.TrackKey, StringComparison.Ordinal)
            && string.Equals(_cachedHistoryCombo.SessionKey, combo.SessionKey, StringComparison.Ordinal)
            && now - _cachedHistoryAtUtc <= TimeSpan.FromSeconds(30))
        {
            return _cachedHistory;
        }

        _cachedHistory = _historyQueryService.Lookup(combo);
        _cachedHistoryCombo = combo;
        _cachedHistoryAtUtc = now;
        return _cachedHistory;
    }

    private void DrawOverlay(Graphics graphics, Rectangle bounds, DesignV2OverlayModel model)
    {
        var outer = RectangleF.Inflate(bounds, -0.5f, -0.5f);
        FillRounded(graphics, outer, 8, Surface, Border);
        var header = new RectangleF(outer.Left + 1, outer.Top + 1, outer.Width - 2, HeaderHeight);
        FillRounded(graphics, header, 7, TitleBar, null);
        FillRounded(graphics, new RectangleF(outer.Left, outer.Top + 7, 3, Math.Max(1, outer.Height - 14)), 2, EvidenceColor(model.Evidence), null);
        using (var accent = new SolidBrush(Cyan))
        {
            graphics.FillRectangle(accent, outer.Left, header.Bottom - 1, outer.Width, 2);
        }

        using var titleFont = FontOf(14, FontStyle.Bold);
        using var statusFont = FontOf(11, FontStyle.Bold);
        DrawText(graphics, model.Title, titleFont, TextPrimary, new RectangleF(outer.Left + 14, header.Top + 10, Math.Min(230, outer.Width * 0.55f), 18));
        DrawText(graphics, model.Status, statusFont, EvidenceColor(model.Evidence), new RectangleF(outer.Left + 230, header.Top + 10, outer.Width - 244, 18), ContentAlignment.MiddleRight);

        var body = new RectangleF(
            outer.Left + PaddingSize,
            header.Bottom + BodyGap,
            outer.Width - PaddingSize * 2,
            Math.Max(1, outer.Height - HeaderHeight - FooterHeight - BodyGap - 1));
        DrawBody(graphics, body, model.Body);

        using var footerFont = FontOf(9.5f);
        DrawText(graphics, model.Footer, footerFont, TextMuted, new RectangleF(outer.Left + 14, outer.Bottom - 24, outer.Width - 28, 14));
    }

    private void DrawBody(Graphics graphics, RectangleF rect, DesignV2Body body)
    {
        switch (body)
        {
            case DesignV2TableBody table:
                DrawTable(graphics, rect, table);
                break;
            case DesignV2MetricRowsBody metrics:
                DrawMetricRows(graphics, rect, metrics.Rows);
                break;
            case DesignV2GraphBody graph:
                DrawGraph(graphics, rect, graph.Points);
                break;
            case DesignV2InputsBody inputs:
                DrawInputs(graphics, rect, inputs);
                break;
            case DesignV2RadarBody radar:
                DrawRadar(graphics, rect, radar);
                break;
        }
    }

    private void DrawTable(Graphics graphics, RectangleF rect, DesignV2TableBody table)
    {
        FillRounded(graphics, rect, 5, SurfaceInset, BorderMuted);
        if (table.Columns.Count == 0)
        {
            return;
        }

        var configuredWidth = table.Columns.Sum(column => Math.Max(MinimumColumnWidth, column.Width));
        var availableWidth = Math.Max(1f, rect.Width - 20 - Math.Max(0, table.Columns.Count - 1) * ColumnGap);
        var fit = Math.Min(1f, availableWidth / Math.Max(1, configuredWidth));
        var x = rect.Left + 10;
        using var headerFont = FontOf(9.5f, FontStyle.Bold);
        foreach (var column in table.Columns)
        {
            var width = Math.Max(MinimumColumnWidth, column.Width) * fit;
            DrawText(graphics, column.Label, headerFont, TextMuted, new RectangleF(x, rect.Top + 8, width, 14), column.Alignment);
            x += width + ColumnGap;
        }

        var maximumRows = Math.Max(1, (int)((rect.Height - RowHeight) / (RowHeight + RowGap)));
        using var rowFont = FontOf(10.5f);
        using var rowBoldFont = FontOf(10.5f, FontStyle.Bold);
        using var classHeaderFont = FontOf(9.8f, FontStyle.Bold);
        using var classDetailFont = FontOf(9.2f, FontStyle.Bold);
        var y = rect.Top + 30;
        var drawnRows = 0;
        foreach (var row in table.Rows)
        {
            if (drawnRows >= maximumRows)
            {
                break;
            }

            if (row.IsClassHeader)
            {
                if (drawnRows > 0)
                {
                    y += 7;
                }

                var headerRect = new RectangleF(rect.Left + 8, y, rect.Width - 16, 24);
                if (headerRect.Bottom > rect.Bottom)
                {
                    break;
                }

                var headerFill = TryParseHexColor(row.ClassColorHex, out var headerColor)
                    ? Blend(SurfaceRaised, headerColor, 4, 2)
                    : Blend(SurfaceRaised, Cyan, 5, 1);
                FillRounded(graphics, headerRect, 5, headerFill, Color.FromArgb(90, BorderMuted));
                if (TryParseHexColor(row.ClassColorHex, out var accent))
                {
                    FillRounded(graphics, new RectangleF(headerRect.Left, headerRect.Top, 3, headerRect.Height), 2, accent, null);
                }

                DrawText(
                    graphics,
                    string.IsNullOrWhiteSpace(row.ClassHeaderTitle) ? "Class" : row.ClassHeaderTitle,
                    classHeaderFont,
                    TextPrimary,
                    new RectangleF(headerRect.Left + 10, headerRect.Top + 5, headerRect.Width * 0.58f, 14));
                DrawText(
                    graphics,
                    row.ClassHeaderDetail,
                    classDetailFont,
                    TextSecondary,
                    new RectangleF(headerRect.Left + headerRect.Width * 0.58f, headerRect.Top + 5, headerRect.Width * 0.42f - 10, 14),
                    ContentAlignment.MiddleRight);
                y += headerRect.Height + RowGap;
                drawnRows++;
                continue;
            }

            var rowRect = new RectangleF(rect.Left + 8, y, rect.Width - 16, RowHeight);
            if (rowRect.Bottom > rect.Bottom)
            {
                break;
            }

            var fill = row.IsReference
                ? Blend(SurfaceRaised, Cyan, 10, 1)
                : SurfaceRaised;
            FillRounded(graphics, rowRect, 5, fill, Color.FromArgb(90, BorderMuted));

            x = rowRect.Left + 8;
            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var column = table.Columns[columnIndex];
                var width = Math.Max(MinimumColumnWidth, column.Width) * fit;
                var value = columnIndex < row.Values.Count ? row.Values[columnIndex] : string.Empty;
                DrawText(
                    graphics,
                    value,
                    row.IsReference || columnIndex == 0 ? rowBoldFont : rowFont,
                    row.IsReference ? TextPrimary : TextSecondary,
                    new RectangleF(x, rowRect.Top + 7, width, 16),
                    column.Alignment);
                x += width + ColumnGap;
            }

            y += RowHeight + RowGap;
            drawnRows++;
        }
    }

    private void DrawMetricRows(Graphics graphics, RectangleF rect, IReadOnlyList<DesignV2MetricRow> rows)
    {
        if (rows.Count == 0)
        {
            FillRounded(graphics, rect, 5, SurfaceInset, BorderMuted);
            using var waitingFont = FontOf(11, FontStyle.Bold);
            DrawText(graphics, "waiting", waitingFont, TextMuted, RectangleF.Inflate(rect, -12, -10));
            return;
        }

        using var labelFont = FontOf(9.8f, FontStyle.Bold);
        using var valueFont = FontOf(10.5f, FontStyle.Bold);
        var maximumRows = Math.Max(1, (int)(rect.Height / (RowHeight + RowGap)));
        foreach (var (row, index) in rows.Take(maximumRows).Select((row, index) => (row, index)))
        {
            var rowRect = new RectangleF(rect.Left, rect.Top + index * (RowHeight + RowGap), rect.Width, RowHeight);
            FillRounded(graphics, rowRect, 5, SurfaceRaised, Color.FromArgb(90, BorderMuted));
            DrawText(graphics, row.Label, labelFont, TextMuted, new RectangleF(rowRect.Left + 10, rowRect.Top + 7, MetricLabelWidth, 16));
            DrawText(graphics, row.Value, valueFont, EvidenceColor(row.Evidence), new RectangleF(rowRect.Left + MetricLabelWidth + 12, rowRect.Top + 7, rowRect.Width - MetricLabelWidth - 22, 16), ContentAlignment.MiddleRight);
        }
    }

    private void DrawGraph(Graphics graphics, RectangleF rect, IReadOnlyList<double> points)
    {
        FillRounded(graphics, rect, 5, SurfaceInset, BorderMuted);
        if (points.Count < 2)
        {
            using var waitingFont = FontOf(11, FontStyle.Bold);
            DrawText(graphics, "waiting for trend", waitingFont, TextMuted, RectangleF.Inflate(rect, -12, -10));
            return;
        }

        var min = points.Min();
        var max = points.Max();
        var span = Math.Max(1d, max - min);
        var plot = RectangleF.Inflate(rect, -16, -18);
        plot.X += 46;
        plot.Width -= 50;
        using var gridPen = new Pen(Color.FromArgb(80, TextMuted), 1);
        for (var index = 1; index < 4; index++)
        {
            var y = plot.Top + index * plot.Height / 4f;
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
        }

        using var linePen = new Pen(Cyan, 2f);
        using var path = new GraphicsPath();
        for (var index = 0; index < points.Count; index++)
        {
            var progress = index / (float)Math.Max(1, points.Count - 1);
            var normalized = (float)((points[index] - min) / span);
            var point = new PointF(plot.Left + progress * plot.Width, plot.Bottom - normalized * plot.Height);
            if (index == 0)
            {
                path.StartFigure();
                path.AddLine(point, point);
            }
            else
            {
                path.AddLine(path.GetLastPoint(), point);
            }
        }

        graphics.DrawPath(linePen, path);
    }

    private void DrawInputs(Graphics graphics, RectangleF rect, DesignV2InputsBody body)
    {
        var topRows = body.Rows.Take(3).Select(row => new DesignV2MetricRow(row.Label, row.Value, EvidenceFor(row.Tone))).ToArray();
        var metricsRect = new RectangleF(rect.Left, rect.Top, rect.Width, Math.Min(rect.Height, 3 * (RowHeight + RowGap)));
        DrawMetricRows(graphics, metricsRect, topRows);
        var railTop = metricsRect.Bottom + 10;
        var railHeight = Math.Max(12, rect.Bottom - railTop);
        FillRounded(graphics, new RectangleF(rect.Left, railTop, rect.Width, railHeight), 5, SurfaceInset, BorderMuted);
        if (body.Trace.Count == 0)
        {
            return;
        }

        var latest = body.Trace[^1];
        DrawInputBar(graphics, "T", latest.Throttle, Green, new RectangleF(rect.Left + 14, railTop + 12, rect.Width - 28, 10));
        DrawInputBar(graphics, "B", latest.Brake, latest.BrakeAbsActive ? Amber : Magenta, new RectangleF(rect.Left + 14, railTop + 34, rect.Width - 28, 10));
        DrawInputBar(graphics, "C", latest.Clutch, Cyan, new RectangleF(rect.Left + 14, railTop + 56, rect.Width - 28, 10));
    }

    private void DrawInputBar(Graphics graphics, string label, double? value, Color color, RectangleF rect)
    {
        using var font = FontOf(8.5f, FontStyle.Bold);
        DrawText(graphics, label, font, TextMuted, new RectangleF(rect.Left, rect.Top - 3, 18, 14));
        var bar = new RectangleF(rect.Left + 22, rect.Top, rect.Width - 22, rect.Height);
        FillRounded(graphics, bar, 4, Color.FromArgb(85, TextMuted), null);
        var fill = new RectangleF(bar.Left, bar.Top, bar.Width * (float)Math.Clamp(value ?? 0d, 0d, 1d), bar.Height);
        FillRounded(graphics, fill, 4, color, null);
    }

    private void DrawRadar(Graphics graphics, RectangleF rect, DesignV2RadarBody radar)
    {
        FillRounded(graphics, rect, 5, SurfaceInset, BorderMuted);
        var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
        var radius = Math.Min(rect.Width, rect.Height) * 0.36f;
        using var ringPen = new Pen(Color.FromArgb(110, Cyan), 1.5f);
        graphics.DrawEllipse(ringPen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
        FillRounded(graphics, new RectangleF(center.X - 12, center.Y - 24, 24, 48), 4, Cyan, null);
        if (radar.HasLeft)
        {
            FillRounded(graphics, new RectangleF(center.X - radius - 10, center.Y - 18, 20, 36), 4, Magenta, null);
        }
        if (radar.HasRight)
        {
            FillRounded(graphics, new RectangleF(center.X + radius - 10, center.Y - 18, 20, 36), 4, Amber, null);
        }
        using var font = FontOf(10.5f, FontStyle.Bold);
        var text = radar.MulticlassWarning ? $"class traffic | cars {radar.NearbyCount}" : $"nearby cars {radar.NearbyCount}";
        DrawText(graphics, text, font, radar.MulticlassWarning ? Amber : TextMuted, new RectangleF(rect.Left + 12, rect.Bottom - 28, rect.Width - 24, 16), ContentAlignment.MiddleCenter);
    }

    private void ReportOverlayError(Exception exception)
    {
        var now = DateTimeOffset.UtcNow;
        var message = exception.Message;
        if (!string.Equals(_lastLoggedError, message, StringComparison.Ordinal)
            || _lastLoggedErrorAtUtc is null
            || now - _lastLoggedErrorAtUtc > TimeSpan.FromSeconds(30))
        {
            _logger.LogError(exception, "Design V2 overlay {OverlayId} failed.", _definition.Id);
            _lastLoggedError = message;
            _lastLoggedErrorAtUtc = now;
        }
    }

    private static DesignV2OverlayModel WaitingModel(string title, string status)
    {
        return new DesignV2OverlayModel(
            title,
            status,
            "source: waiting",
            DesignV2Evidence.Unavailable,
            new DesignV2MetricRowsBody([]));
    }

    private static string TitleFor(DesignV2LiveOverlayKind kind)
    {
        return kind switch
        {
            DesignV2LiveOverlayKind.Standings => "Standings",
            DesignV2LiveOverlayKind.FuelCalculator => "Fuel Calculator",
            DesignV2LiveOverlayKind.Relative => "Relative",
            DesignV2LiveOverlayKind.TrackMap => "Track Map",
            DesignV2LiveOverlayKind.StreamChat => "Stream Chat",
            DesignV2LiveOverlayKind.Flags => "Flags",
            DesignV2LiveOverlayKind.SessionWeather => "Session / Weather",
            DesignV2LiveOverlayKind.PitService => "Pit Service",
            DesignV2LiveOverlayKind.InputState => "Inputs",
            DesignV2LiveOverlayKind.CarRadar => "Car Radar",
            DesignV2LiveOverlayKind.GapToLeader => "Focused Gap Trend",
            _ => "Overlay"
        };
    }

    private static ContentAlignment AlignmentFor(OverlayContentColumnAlignment alignment)
    {
        return alignment switch
        {
            OverlayContentColumnAlignment.Center => ContentAlignment.MiddleCenter,
            OverlayContentColumnAlignment.Right => ContentAlignment.MiddleRight,
            _ => ContentAlignment.MiddleLeft
        };
    }

    private static DesignV2Evidence EvidenceFor(SimpleTelemetryTone tone)
    {
        return tone switch
        {
            SimpleTelemetryTone.Success => DesignV2Evidence.Live,
            SimpleTelemetryTone.Info => DesignV2Evidence.Measured,
            SimpleTelemetryTone.Warning => DesignV2Evidence.Partial,
            SimpleTelemetryTone.Error => DesignV2Evidence.Error,
            SimpleTelemetryTone.Waiting => DesignV2Evidence.Unavailable,
            _ => DesignV2Evidence.Live
        };
    }

    private static Color EvidenceColor(DesignV2Evidence evidence)
    {
        return evidence switch
        {
            DesignV2Evidence.Live => Green,
            DesignV2Evidence.Measured => Cyan,
            DesignV2Evidence.Modeled => Magenta,
            DesignV2Evidence.Partial => Amber,
            DesignV2Evidence.Error => Error,
            _ => TextMuted
        };
    }

    private Font FontOf(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font(string.IsNullOrWhiteSpace(_fontFamily) ? "Segoe UI" : _fontFamily, size, style, GraphicsUnit.Point);
    }

    private static void DrawText(Graphics graphics, string text, Font font, Color color, RectangleF bounds, ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            Alignment = alignment switch
            {
                ContentAlignment.MiddleCenter => StringAlignment.Center,
                ContentAlignment.MiddleRight => StringAlignment.Far,
                _ => StringAlignment.Near
            },
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(text, font, brush, bounds, format);
    }

    private static void FillRounded(Graphics graphics, RectangleF rect, float radius, Color fill, Color? stroke)
    {
        using var path = RoundedPath(rect, radius);
        using var brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);
        if (stroke is { } strokeColor)
        {
            using var pen = new Pen(strokeColor, 1f);
            graphics.DrawPath(pen, path);
        }
    }

    private static GraphicsPath RoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(0, radius * 2);
        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Blend(Color panel, Color accent, int panelWeight, int accentWeight)
    {
        var total = Math.Max(1, panelWeight + accentWeight);
        return Color.FromArgb(
            Math.Max(panel.A, accent.A),
            (panel.R * panelWeight + accent.R * accentWeight) / total,
            (panel.G * panelWeight + accent.G * accentWeight) / total,
            (panel.B * panelWeight + accent.B * accentWeight) / total);
    }

    private static bool TryParseHexColor(string? value, out Color color)
    {
        color = Color.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().TrimStart('#');
        if (trimmed.Length != 6 || !int.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return false;
        }

        color = Color.FromArgb((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
        return true;
    }

    private static string FormatPosition(int? position)
    {
        return position?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "--";
    }

    private static string FormatGap(LiveGapValue gap)
    {
        if (gap.Seconds is { } seconds && IsFinite(seconds))
        {
            return seconds < 60d
                ? $"{seconds:+0.0;-0.0;0.0}s"
                : $"+{Math.Floor(seconds / 60d):0}:{seconds % 60d:00.0}";
        }

        if (gap.Laps is { } laps && IsFinite(laps))
        {
            return $"{laps:+0.00;-0.00;0.00} lap";
        }

        return "--";
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string Trim(string value, int maximumLength)
    {
        return value.Length <= maximumLength ? value : value[..Math.Max(0, maximumLength - 1)] + "...";
    }
}

internal enum DesignV2Evidence
{
    Live,
    Measured,
    Modeled,
    Partial,
    Unavailable,
    Error
}

internal sealed record DesignV2OverlayModel(
    string Title,
    string Status,
    string Footer,
    DesignV2Evidence Evidence,
    DesignV2Body Body);

internal abstract record DesignV2Body;

internal sealed record DesignV2TableBody(
    IReadOnlyList<DesignV2Column> Columns,
    IReadOnlyList<DesignV2TableRow> Rows) : DesignV2Body;

internal sealed record DesignV2MetricRowsBody(
    IReadOnlyList<DesignV2MetricRow> Rows) : DesignV2Body;

internal sealed record DesignV2GraphBody(
    IReadOnlyList<double> Points) : DesignV2Body;

internal sealed record DesignV2InputsBody(
    IReadOnlyList<SimpleTelemetryRowViewModel> Rows,
    IReadOnlyList<DesignV2InputPoint> Trace) : DesignV2Body;

internal sealed record DesignV2RadarBody(
    bool HasLeft,
    bool HasRight,
    int NearbyCount,
    bool MulticlassWarning) : DesignV2Body;

internal sealed record DesignV2Column(
    string Label,
    int Width,
    ContentAlignment Alignment);

internal sealed record DesignV2TableRow(
    IReadOnlyList<string> Values,
    bool IsReference,
    bool IsClassHeader,
    DesignV2Evidence Evidence,
    string? ClassColorHex,
    string ClassHeaderTitle = "",
    string ClassHeaderDetail = "");

internal sealed record DesignV2MetricRow(
    string Label,
    string Value,
    DesignV2Evidence Evidence);

internal sealed record DesignV2InputPoint(
    double? Throttle,
    double? Brake,
    double? Clutch,
    bool BrakeAbsActive);
