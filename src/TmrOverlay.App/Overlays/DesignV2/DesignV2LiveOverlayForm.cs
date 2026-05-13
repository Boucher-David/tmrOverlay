using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.WebSockets;
using System.Text;
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
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;
using TmrOverlay.Core.TrackMaps;

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
    private const float FlagOuterPadding = 8f;
    private const float FlagCellGap = 8f;
    private const float TrackSectorBoundaryTickLength = 17f;
    private const float TrackPitLineWidth = 2.2f;
    private const double TrackMapReloadIntervalSeconds = 10d;
    private const int MaximumChatMessages = 64;
    private const int VisibleChatMessageBudget = 36;
    private const int GapMaxTrendPointsPerCar = 36_000;
    private const int GapMaxWeatherPoints = 36_000;
    private const int GapMaxDriverChangeMarkers = 64;
    private const double GapTrendWindowSeconds = 4d * 60d * 60d;
    private const double GapMinimumTrendDomainSeconds = 120d;
    private const double GapMinimumTrendDomainLaps = 1.5d;
    private const double GapTrendRightPaddingSeconds = 20d;
    private const double GapTrendRightPaddingLaps = 0.15d;
    private const double GapMissingSegmentSeconds = 10d;
    private const double GapMissingTelemetryGraceSeconds = 5d;
    private const double GapEntryTailSeconds = 300d;
    private const double GapEntryFadeSeconds = 45d;
    private const double GapDefaultLapReferenceSeconds = 60d;
    private const double GapFilteredRangeMinimumSeconds = 15d;
    private const double GapFilteredRangeMaximumSeconds = 90d;
    private const double GapFilteredRangeLaps = 0.5d;
    private const double GapFocusScaleMinimumReferenceGapSeconds = 90d;
    private const double GapFocusScaleMinimumReferenceGapLaps = 0.5d;
    private const double GapFocusScaleMinimumRangeSeconds = 20d;
    private const double GapFocusScaleMinimumRangeLaps = 0.1d;
    private const double GapFocusScalePaddingMultiplier = 1.18d;
    private const double GapFocusScaleTriggerRatio = 3d;
    private const double GapSameLapReferenceBoundaryLaps = 0.95d;
    private const double GapMetricDeadbandMinimumSeconds = 0.25d;
    private const double GapMetricDeadbandLapFraction = 0.0025d;
    private const double GapThreatMinimumGainSeconds = 0.5d;
    private const double GapThreatGainLapFraction = 0.005d;
    private const double GapFuelStintResetMinimumLiters = 5d;
    private const float GapEndpointLabelLaneWidth = 38f;
    private const float GapEndpointLabelPinThreshold = 4f;
    private const float GapEndpointLabelHeight = 13f;
    private const float GapEndpointLabelGap = 1f;
    private const float GapMetricsTableWidth = 184f;
    private const float GapMetricsTableGap = 10f;
    private const float GapMetricsMinimumPlotWidth = 300f;
    private const float GapThreatBadgeHeight = 16f;
    private const float GapFocusScaleReferenceRatio = 0.56f;
    private const float GapFocusScaleTopPadding = 18f;
    private const float GapFocusScaleBottomPadding = 8f;
    private const double FocusedCarLengthMeters = 4.746d;
    private const double RadarRangeMeters = FocusedCarLengthMeters * 6d;

    private static readonly Uri TwitchChatUri = new("wss://irc-ws.chat.twitch.tv:443");
    private static readonly Color TransparentColor = Color.FromArgb(1, 2, 3);
    private static Color Surface => OverlayTheme.DesignV2.Surface;
    private static Color SurfaceInset => OverlayTheme.DesignV2.SurfaceInset;
    private static Color SurfaceRaised => OverlayTheme.DesignV2.SurfaceRaised;
    private static Color TitleBar => OverlayTheme.DesignV2.TitleBar;
    private static Color Border => OverlayTheme.DesignV2.Border;
    private static Color BorderMuted => OverlayTheme.DesignV2.BorderMuted;
    private static Color TextPrimary => OverlayTheme.DesignV2.TextPrimary;
    private static Color TextSecondary => OverlayTheme.DesignV2.TextSecondary;
    private static Color TextMuted => OverlayTheme.DesignV2.TextMuted;
    private static Color Cyan => OverlayTheme.DesignV2.Cyan;
    private static Color Magenta => OverlayTheme.DesignV2.Magenta;
    private static Color Amber => OverlayTheme.DesignV2.Amber;
    private static Color Green => OverlayTheme.DesignV2.Green;
    private static Color Orange => OverlayTheme.DesignV2.Orange;
    private static Color Error => OverlayTheme.DesignV2.Error;
    private static Color OneLapAheadText => Color.FromArgb(255, 155, 164);
    private static Color MultipleLapsAheadText => Error;
    private static Color OneLapBehindText => Color.FromArgb(150, 210, 255);
    private static Color MultipleLapsBehindText => Color.FromArgb(82, 158, 255);
    private static Color TrackInterior => OverlayTheme.DesignV2.TrackInterior;
    private static Color TrackHalo => OverlayTheme.DesignV2.TrackHalo;
    private static Color TrackLine => OverlayTheme.DesignV2.TrackLine;
    private static Color TrackMarkerBorder => OverlayTheme.DesignV2.TrackMarkerBorder;
    private static Color PitLineColor => OverlayTheme.DesignV2.PitLine;
    private static Color StartFinishBoundaryColor => OverlayTheme.DesignV2.StartFinishBoundary;
    private static Color StartFinishBoundaryShadowColor => OverlayTheme.DesignV2.StartFinishBoundaryShadow;
    private static Color PersonalBestSectorColor => OverlayTheme.DesignV2.PersonalBestSector;
    private static Color BestLapSectorColor => OverlayTheme.DesignV2.BestLapSector;
    private static Color FlagPoleColor => OverlayTheme.DesignV2.FlagPole;
    private static Color FlagPoleShadowColor => OverlayTheme.DesignV2.FlagPoleShadow;

    private readonly DesignV2LiveOverlayKind _kind;
    private readonly OverlayDefinition _definition;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly TrackMapStore _trackMapStore;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly AppPerformanceState _performanceState;
    private readonly ILogger _logger;
    private readonly OverlaySettings _settings;
    private readonly string _fontFamily;
    private readonly string _unitSystem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly List<double> _gapPoints = [];
    private readonly Dictionary<int, List<DesignV2GapTrendPoint>> _gapSeries = [];
    private readonly List<DesignV2GapWeatherPoint> _gapWeather = [];
    private readonly List<DesignV2GapDriverChangeMarker> _gapDriverChangeMarkers = [];
    private readonly List<DesignV2GapLeaderChangeMarker> _gapLeaderChangeMarkers = [];
    private readonly Dictionary<int, DesignV2GapCarRenderState> _gapCarRenderStates = [];
    private readonly Dictionary<int, DesignV2GapDriverIdentity> _gapDriverIdentities = [];
    private readonly List<DesignV2InputPoint> _inputTrace = [];
    private readonly List<StreamChatMessage> _chatMessages = [];
    private readonly Dictionary<int, double> _smoothedTrackMarkerProgress = new();
    private readonly Button? _closeButton;
    private DesignV2OverlayModel _model;
    private TrackMapDocument? _trackMap;
    private string? _trackMapIdentityKey;
    private HistoricalComboIdentity? _cachedHistoryCombo;
    private SessionHistoryLookupResult? _cachedHistory;
    private DateTimeOffset _cachedHistoryAtUtc;
    private string? _lastLoggedError;
    private DateTimeOffset? _lastLoggedErrorAtUtc;
    private CancellationTokenSource? _chatConnectionCancellation;
    private Task? _chatConnectionTask;
    private string _streamChatStatus = "waiting for chat source";
    private string? _activeStreamChatSettingsKey;
    private string? _lastChatLoggedError;
    private DateTimeOffset? _lastTrackMarkerSmoothingAtUtc;
    private DateTimeOffset _nextTrackMapReloadAtUtc = DateTimeOffset.MinValue;
    private long? _lastGapSequence;
    private double? _latestGapAxisSeconds;
    private double? _gapTrendStartAxisSeconds;
    private double? _lastGapLapReferenceSeconds;
    private double? _currentGapFuelStintStartAxisSeconds;
    private double? _lastGapFuelLevelLiters;
    private DesignV2GapReferenceContext? _lastGapReferenceContext;
    private int? _lastGapDriversSoFar;
    private int? _lastGapClassLeaderCarIdx;
    private bool _settingsPreviewVisible;
    private bool _flagsManagedEnabled = true;
    private bool _flagsSettingsOverlayActive;
    private bool _chatConnectedAnnounced;
    private bool _disposed;

    public DesignV2LiveOverlayForm(
        DesignV2LiveOverlayKind kind,
        OverlayDefinition definition,
        ILiveTelemetrySource liveTelemetrySource,
        TrackMapStore trackMapStore,
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
        _trackMapStore = trackMapStore;
        _historyQueryService = historyQueryService;
        _performanceState = performanceState;
        _logger = logger;
        _settings = settings;
        _fontFamily = fontFamily;
        _unitSystem = unitSystem;
        _model = WaitingModel(TitleFor(kind), "waiting");

        BackColor = UsesTransparentBackground(kind) ? TransparentColor : Color.Black;
        if (UsesTransparentBackground(kind))
        {
            TransparencyKey = TransparentColor;
        }
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

        if (_kind == DesignV2LiveOverlayKind.StreamChat)
        {
            _closeButton = CreateStreamChatCloseButton();
            Controls.Add(_closeButton);
            LayoutStreamChatCloseButton();
        }
    }

    public void SetSettingsPreviewVisible(bool previewVisible)
    {
        if (_settingsPreviewVisible == previewVisible)
        {
            return;
        }

        _settingsPreviewVisible = previewVisible;
        Invalidate();
    }

    public override bool IsIntrinsicallyInputTransparentOverlay => IsInputTransparentKind(_kind);

    public bool IsInputTransparentOverlay => IsIntrinsicallyInputTransparentOverlay;

    public string DiagnosticKind => KindName(_kind);

    public string DiagnosticBodyKind => BodyName(_model.Body);

    public void SetFlagsManagedState(bool managedEnabled, bool settingsOverlayActive)
    {
        _flagsManagedEnabled = managedEnabled;
        _flagsSettingsOverlayActive = settingsOverlayActive;
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _closeButton?.Dispose();
            StopChatConnection();
        }

        base.Dispose(disposing);
    }

    protected override Size GetPersistedOverlaySize()
    {
        if (_kind == DesignV2LiveOverlayKind.Standings)
        {
            return new Size(
                _settings.Width > 0 ? _settings.Width : _definition.DefaultWidth,
                _settings.Height > 0 ? _settings.Height : _definition.DefaultHeight);
        }

        return base.GetPersistedOverlaySize();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutStreamChatCloseButton();
    }

    protected override bool ShouldReceiveInputWhileTransparent(Point clientPoint)
    {
        return IsStreamChatCloseButtonHit(clientPoint)
            || IsStreamChatDragHit(clientPoint, ClientSize);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && IsStreamChatCloseButtonHit(e.Location))
        {
            DisableOverlayAndClose();
            return;
        }

        base.OnMouseUp(e);
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
            if (!DrawCustomOverlay(e.Graphics, ClientRectangle, _model))
            {
                DrawOverlay(e.Graphics, ClientRectangle, _model);
            }
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
        var model = _kind switch
        {
            DesignV2LiveOverlayKind.Standings => BuildStandingsModel(snapshot, now),
            DesignV2LiveOverlayKind.Relative => BuildRelativeModel(snapshot, now),
            DesignV2LiveOverlayKind.FuelCalculator => BuildFuelModel(snapshot, now),
            DesignV2LiveOverlayKind.SessionWeather => FromSimple(SessionWeatherOverlayViewModel.From(snapshot, now, _unitSystem)),
            DesignV2LiveOverlayKind.PitService => FromSimple(PitServiceOverlayViewModel.From(snapshot, now, _unitSystem)),
            DesignV2LiveOverlayKind.InputState => BuildInputModel(snapshot, now),
            DesignV2LiveOverlayKind.Flags => BuildFlagsModel(snapshot, now),
            DesignV2LiveOverlayKind.CarRadar => BuildRadarModel(snapshot, now),
            DesignV2LiveOverlayKind.GapToLeader => BuildGapModel(snapshot, now),
            DesignV2LiveOverlayKind.TrackMap => BuildTrackMapModel(snapshot, now),
            DesignV2LiveOverlayKind.StreamChat => BuildStreamChatModel(),
            _ => WaitingModel(TitleFor(_kind), "waiting")
        };
        return ApplyChromeSettings(model, snapshot);
    }

    private DesignV2OverlayModel ApplyChromeSettings(DesignV2OverlayModel model, LiveTelemetrySnapshot snapshot)
    {
        var headerText = BuildHeaderText(_settings, snapshot, model.Status);
        var showFooter = ShowFooterForSettings(_kind, _settings, snapshot);
        return model with
        {
            HeaderText = headerText,
            ShowFooter = showFooter
        };
    }

    internal static string BuildHeaderText(OverlaySettings settings, LiveTelemetrySnapshot snapshot, string status)
    {
        var parts = new List<string>(2);
        if (OverlayChromeSettings.ShowHeaderStatus(settings, snapshot) && !string.IsNullOrWhiteSpace(status))
        {
            parts.Add(status);
        }

        if (OverlayChromeSettings.ShowHeaderTimeRemaining(settings, snapshot))
        {
            var timeRemaining = OverlayHeaderTimeFormatter.FormatTimeRemaining(snapshot);
            if (!string.IsNullOrWhiteSpace(timeRemaining))
            {
                parts.Add(timeRemaining);
            }
        }

        return string.Join(" | ", parts);
    }

    internal static bool ShowFooterForSettings(DesignV2LiveOverlayKind kind, OverlaySettings settings, LiveTelemetrySnapshot snapshot)
    {
        return OverlayChromeSettings.ShowFooterSource(settings, snapshot)
            && (kind != DesignV2LiveOverlayKind.FuelCalculator
                || settings.GetBooleanOption(OverlayOptionKeys.FuelSource, defaultValue: true));
    }

    private DesignV2OverlayModel BuildStandingsModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var showClassSeparators = OverlayContentColumnSettings.Standings.Blocks is { Count: > 0 } blocks
            && OverlayContentColumnSettings.BlockEnabled(_settings, blocks[0]);
        var otherRows = OverlayContentColumnSettings.Standings.Blocks is { Count: > 0 } otherBlocks
            ? OverlayContentColumnSettings.BlockCount(_settings, otherBlocks[0])
            : 2;
        var visibleRows = StandingsVisibleRowsForHeight(ClientSize.Height);
        if (snapshot.Models.Scoring.HasData)
        {
            var requiredRows = StandingsOverlayViewModel.ExpandRowBudgetForClassGroups(
                snapshot.Models.Scoring.ClassGroups,
                visibleRows,
                otherRows,
                showClassSeparators);
            if (EnsureClientHeightForStandingsRows(requiredRows))
            {
                visibleRows = StandingsVisibleRowsForHeight(ClientSize.Height);
            }
        }

        var viewModel = StandingsOverlayViewModel.From(
            snapshot,
            now,
            maximumRows: Math.Clamp(visibleRows, 1, StandingsOverlayViewModel.MaximumRenderedRows),
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

    private bool EnsureClientHeightForStandingsRows(int rowCount)
    {
        var targetHeight = TargetClientHeightForStandingsRows(rowCount);
        if (ClientSize.Height >= targetHeight)
        {
            return false;
        }

        ClientSize = new Size(ClientSize.Width, targetHeight);
        return true;
    }

    private int TargetClientHeightForStandingsRows(int rowCount)
    {
        var persistedHeight = _settings.Height > 0
            ? _settings.Height
            : _definition.DefaultHeight;
        var visibleRows = Math.Clamp(
            Math.Max(1, rowCount),
            1,
            StandingsOverlayViewModel.MaximumRenderedRows);
        var persistedVisibleRows = StandingsVisibleRowsForHeight(persistedHeight);
        if (visibleRows <= persistedVisibleRows)
        {
            return persistedHeight;
        }

        return Math.Max(
            persistedHeight,
            HeaderHeight + FooterHeight + BodyGap + 1 + RowHeight + (visibleRows * (RowHeight + RowGap)));
    }

    private static int StandingsVisibleRowsForHeight(int clientHeight)
    {
        var bodyHeight = clientHeight - HeaderHeight - FooterHeight - BodyGap - 1;
        return Math.Max(1, (bodyHeight - RowHeight) / (RowHeight + RowGap));
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
        var carsEachSide = RelativeBrowserSettings.CarsEachSide(_settings);
        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            carsEachSide,
            carsEachSide);
        var columns = OverlayContentColumnSettings.VisibleColumnsFor(_settings, OverlayContentColumnSettings.Relative)
            .Select(column => new DesignV2Column(column.Label, column.Width, AlignmentFor(column.Alignment)))
            .ToArray();
        var rows = StableRelativeRows(viewModel, carsEachSide, carsEachSide)
            .Select(row => row is null
                ? BlankTableRow(columns.Length)
                : new DesignV2TableRow(
                    ValuesForRelativeRow(row),
                    row.IsReference,
                    IsClassHeader: false,
                    row.IsPartial ? DesignV2Evidence.Partial : DesignV2Evidence.Measured,
                    row.ClassColorHex,
                    RelativeLapDelta: row.LapDeltaToReference))
            .ToArray();
        return new DesignV2OverlayModel(
            "Relative",
            viewModel.Status,
            viewModel.Source,
            rows.Length == 0 ? DesignV2Evidence.Unavailable : DesignV2Evidence.Live,
            new DesignV2TableBody(columns, rows));
    }

    internal static IReadOnlyList<RelativeOverlayRowViewModel?> StableRelativeRows(
        RelativeOverlayViewModel viewModel,
        int carsAhead,
        int carsBehind)
    {
        return viewModel.StableRows(carsAhead, carsBehind, maximumRows: 17);
    }

    private static DesignV2TableRow BlankTableRow(int columnCount)
    {
        return new DesignV2TableRow(
            Enumerable.Repeat(string.Empty, Math.Max(0, columnCount)).ToArray(),
            IsReference: false,
            IsClassHeader: false,
            DesignV2Evidence.Unavailable,
            ClassColorHex: null);
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

    private DesignV2OverlayModel BuildFuelModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var localContext = LiveLocalStrategyContext.ForFuelCalculator(snapshot, now);
        if (!localContext.IsAvailable)
        {
            return new DesignV2OverlayModel(
                "Fuel Calculator",
                localContext.StatusText,
                "source: waiting",
                DesignV2Evidence.Unavailable,
                new DesignV2MetricRowsBody([]));
        }

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
        var inputs = snapshot.Models.Inputs;
        if (inputs.HasData)
        {
            _inputTrace.Add(new DesignV2InputPoint(
                Math.Clamp(inputs.Throttle ?? 0d, 0d, 1d),
                Math.Clamp(inputs.Brake ?? 0d, 0d, 1d),
                Math.Clamp(inputs.Clutch ?? 0d, 0d, 1d),
                inputs.BrakeAbsActive == true));
            if (_inputTrace.Count > 180)
            {
                _inputTrace.RemoveRange(0, _inputTrace.Count - 180);
            }
        }

        var viewModel = InputStateOverlayViewModel.From(snapshot, now, _unitSystem);
        return new DesignV2OverlayModel(
            "Inputs",
            viewModel.Status,
            viewModel.Source,
            EvidenceFor(viewModel.Tone),
            new DesignV2InputsBody(
                inputs.Throttle,
                inputs.Brake,
                inputs.Clutch,
                inputs.SteeringWheelAngle,
                inputs.SpeedMetersPerSecond,
                inputs.Gear,
                inputs.BrakeAbsActive == true,
                inputs.HasData,
                InputBlockEnabled(OverlayContentColumnSettings.InputThrottleBlockId),
                InputBlockEnabled(OverlayContentColumnSettings.InputBrakeBlockId),
                InputBlockEnabled(OverlayContentColumnSettings.InputClutchBlockId),
                InputBlockEnabled(OverlayContentColumnSettings.InputSteeringBlockId),
                InputBlockEnabled(OverlayContentColumnSettings.InputGearBlockId),
                InputBlockEnabled(OverlayContentColumnSettings.InputSpeedBlockId),
                _inputTrace.ToArray()));
    }

    private DesignV2OverlayModel BuildRadarModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var spatial = snapshot.Models.Spatial;
        var body = RadarBodyFromSpatial(spatial, availability.IsAvailable, _settingsPreviewVisible, _settings);
        var status = !body.IsAvailable
            ? availability.StatusText
            : spatial.HasCarLeft && spatial.HasCarRight
            ? "cars both sides"
            : spatial.HasCarLeft
                ? "car left"
                : spatial.HasCarRight
                    ? "car right"
                    : spatial.StrongestMulticlassApproach is not null
                        ? "class traffic"
                        : "clear";
        return new DesignV2OverlayModel(
            "Car Radar",
            status,
            body.IsAvailable ? "source: spatial telemetry" : "source: waiting",
            body.IsAvailable ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable,
            body);
    }

    internal static DesignV2RadarBody RadarBodyFromSpatial(
        LiveSpatialModel spatial,
        bool overlayAvailable,
        bool previewVisible,
        OverlaySettings settings)
    {
        var isAvailable = overlayAvailable && spatial.HasData;
        var cars = spatial.Cars
            .Where(car => car.RelativeMeters is { } meters && IsFinite(meters))
            .ToArray();
        return new DesignV2RadarBody(
            isAvailable || previewVisible,
            spatial.HasCarLeft,
            spatial.HasCarRight,
            cars,
            spatial.StrongestMulticlassApproach,
            settings.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true),
            previewVisible);
    }

    private DesignV2OverlayModel BuildGapModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var gap = TmrOverlay.App.Overlays.GapToLeader.GapToLeaderLiveModelAdapter.Select(snapshot);
        if (_lastGapSequence != snapshot.Sequence)
        {
            _lastGapSequence = snapshot.Sequence;
            RecordGapSnapshot(snapshot, gap, now);
            if (gap.HasData
                && TmrOverlay.App.Overlays.GapToLeader.GapToLeaderLiveModelAdapter.SelectFocusedTrendPointSeconds(snapshot, gap) is { } seconds
                && IsFinite(seconds))
            {
                _gapPoints.Add(seconds);
                if (_gapPoints.Count > 120)
                {
                    _gapPoints.RemoveRange(0, _gapPoints.Count - 120);
                }
            }
        }

        var selectedSeries = SelectGapSeries();
        var graph = BuildGapGraphBody(selectedSeries, snapshot);
        if (!availability.IsAvailable)
        {
            return new DesignV2OverlayModel(
                "Focused Gap Trend",
                availability.StatusText,
                "source: waiting",
                DesignV2Evidence.Unavailable,
                graph);
        }

        var status = gap.HasData ? "live | race gap" : "waiting";
        var footer = gap.HasData
            ? $"source: live gap telemetry | cars {selectedSeries.Count}/{gap.ClassCars.Count}"
            : "source: waiting";
        return new DesignV2OverlayModel(
            "Focused Gap Trend",
            status,
            footer,
            gap.HasData ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable,
            graph);
    }

    private void RecordGapSnapshot(LiveTelemetrySnapshot snapshot, LiveLeaderGapSnapshot gap, DateTimeOffset now)
    {
        var timestamp = snapshot.LatestSample?.CapturedAtUtc
            ?? snapshot.LastUpdatedAtUtc
            ?? now;
        var axisSeconds = SelectGapAxisSeconds(timestamp, snapshot.Models.Session.SessionTimeSeconds ?? snapshot.LatestSample?.SessionTime);
        _latestGapAxisSeconds = axisSeconds;
        if (_gapTrendStartAxisSeconds is null || axisSeconds < _gapTrendStartAxisSeconds.Value)
        {
            _gapTrendStartAxisSeconds = axisSeconds;
        }

        var context = new DesignV2GapReferenceContext(
            snapshot.Models.Reference.FocusCarIdx ?? snapshot.Models.Timing.FocusCarIdx,
            snapshot.Models.Reference.ReferenceCarClass ?? snapshot.Models.Timing.FocusRow?.CarClass);
        if (_lastGapReferenceContext is not null && _lastGapReferenceContext != context)
        {
            _gapSeries.Clear();
            _gapWeather.Clear();
            _gapDriverChangeMarkers.Clear();
            _gapLeaderChangeMarkers.Clear();
            _gapCarRenderStates.Clear();
            _lastGapClassLeaderCarIdx = null;
            _currentGapFuelStintStartAxisSeconds = null;
            _lastGapFuelLevelLiters = null;
            _gapTrendStartAxisSeconds = axisSeconds;
        }

        _lastGapReferenceContext = context;
        var lapReferenceSeconds = TmrOverlay.App.Overlays.GapToLeader.GapToLeaderLiveModelAdapter.SelectLapReferenceSeconds(snapshot);
        _lastGapLapReferenceSeconds = lapReferenceSeconds;
        RecordGapFuelStint(snapshot, axisSeconds);
        RecordGapWeather(snapshot, axisSeconds);
        RecordGapDriverChangeMarkers(snapshot, gap, timestamp, axisSeconds, lapReferenceSeconds);
        RecordGapLeaderChange(gap, timestamp, axisSeconds);
        foreach (var car in gap.ClassCars)
        {
            if (DesignV2GapSeconds(car, lapReferenceSeconds) is not { } gapSeconds)
            {
                continue;
            }

            if (!_gapSeries.TryGetValue(car.CarIdx, out var points))
            {
                points = [];
                _gapSeries[car.CarIdx] = points;
            }

            var startsSegment = points.Count == 0 || axisSeconds - points[^1].AxisSeconds > GapMissingSegmentSeconds;
            var point = new DesignV2GapTrendPoint(
                timestamp,
                axisSeconds,
                gapSeconds,
                car.CarIdx,
                car.IsReferenceCar,
                car.IsClassLeader,
                car.ClassPosition,
                startsSegment);
            if (points.Count > 0 && Math.Abs(points[^1].AxisSeconds - axisSeconds) < 0.001d)
            {
                points[^1] = point with { StartsSegment = points[^1].StartsSegment };
            }
            else
            {
                points.Add(point);
            }

            if (points.Count > GapMaxTrendPointsPerCar)
            {
                points.RemoveRange(0, points.Count - GapMaxTrendPointsPerCar);
            }
        }

        UpdateGapCarRenderStates(gap, axisSeconds, lapReferenceSeconds);
        PruneGapSeries(axisSeconds);
    }

    private static double SelectGapAxisSeconds(DateTimeOffset timestamp, double? sessionTimeSeconds)
    {
        return sessionTimeSeconds is { } sessionTime && IsFinite(sessionTime) && sessionTime >= 0d
            ? sessionTime
            : timestamp.ToUnixTimeMilliseconds() / 1000d;
    }

    private void RecordGapWeather(LiveTelemetrySnapshot snapshot, double axisSeconds)
    {
        var condition = SelectWeatherCondition(snapshot);
        if (_gapWeather.Count > 0 && Math.Abs(_gapWeather[^1].AxisSeconds - axisSeconds) < 0.001d)
        {
            _gapWeather[^1] = new DesignV2GapWeatherPoint(axisSeconds, condition);
        }
        else
        {
            _gapWeather.Add(new DesignV2GapWeatherPoint(axisSeconds, condition));
        }

        if (_gapWeather.Count > GapMaxWeatherPoints)
        {
            _gapWeather.RemoveRange(0, _gapWeather.Count - GapMaxWeatherPoints);
        }
    }

    private void RecordGapFuelStint(LiveTelemetrySnapshot snapshot, double axisSeconds)
    {
        var fuelLevelLiters = FirstValidFuelLevel(
            snapshot.Models.FuelPit.Fuel.FuelLevelLiters,
            snapshot.Fuel.FuelLevelLiters,
            snapshot.LatestSample?.FuelLevelLiters);
        if (fuelLevelLiters is null)
        {
            return;
        }

        if (_currentGapFuelStintStartAxisSeconds is null)
        {
            _currentGapFuelStintStartAxisSeconds = axisSeconds;
        }
        else if (_lastGapFuelLevelLiters is { } previous
            && fuelLevelLiters.Value - previous >= GapFuelStintResetMinimumLiters)
        {
            _currentGapFuelStintStartAxisSeconds = axisSeconds;
        }

        _lastGapFuelLevelLiters = fuelLevelLiters;
    }

    private void PruneGapSeries(double latestAxisSeconds)
    {
        var cutoff = latestAxisSeconds - GapTrendWindowSeconds;
        foreach (var carIdx in _gapSeries.Keys.ToArray())
        {
            var points = _gapSeries[carIdx];
            points.RemoveAll(point => point.AxisSeconds < cutoff);
            if (points.Count == 0)
            {
                _gapSeries.Remove(carIdx);
            }
        }

        _gapWeather.RemoveAll(point => point.AxisSeconds < cutoff);
        _gapDriverChangeMarkers.RemoveAll(marker => marker.AxisSeconds < cutoff);
        _gapLeaderChangeMarkers.RemoveAll(marker => marker.AxisSeconds < cutoff);
        foreach (var carIdx in _gapCarRenderStates.Keys.ToArray())
        {
            if (!_gapSeries.ContainsKey(carIdx)
                && _gapCarRenderStates[carIdx].LastDesiredAxisSeconds is { } lastDesired
                && latestAxisSeconds - lastDesired > GapStickyVisibilitySeconds())
            {
                _gapCarRenderStates.Remove(carIdx);
            }
        }
    }

    private void RecordGapDriverChangeMarkers(
        LiveTelemetrySnapshot snapshot,
        LiveLeaderGapSnapshot gap,
        DateTimeOffset timestamp,
        double axisSeconds,
        double? lapReferenceSeconds)
    {
        RecordExplicitGapTeamDriverChangeMarker(snapshot, gap, timestamp, axisSeconds, lapReferenceSeconds);
        RecordSessionInfoGapDriverChangeMarkers(snapshot, gap, timestamp, axisSeconds, lapReferenceSeconds);
    }

    private void RecordExplicitGapTeamDriverChangeMarker(
        LiveTelemetrySnapshot snapshot,
        LiveLeaderGapSnapshot gap,
        DateTimeOffset timestamp,
        double axisSeconds,
        double? lapReferenceSeconds)
    {
        if (snapshot.Models.RaceEvents.DriversSoFar is not { } driversSoFar || driversSoFar <= 0)
        {
            return;
        }

        if (_lastGapDriversSoFar is { } previousDrivers)
        {
            if (driversSoFar < previousDrivers)
            {
                _lastGapDriversSoFar = driversSoFar;
                return;
            }

            if (driversSoFar > previousDrivers
                && ReferenceUsesPlayerCar(snapshot)
                && gap.ClassCars.FirstOrDefault(car => car.IsReferenceCar) is { } reference
                && DesignV2GapSeconds(reference, lapReferenceSeconds) is { } gapSeconds)
            {
                AddGapDriverChangeMarker(new DesignV2GapDriverChangeMarker(
                    timestamp,
                    axisSeconds,
                    reference.CarIdx,
                    gapSeconds,
                    true,
                    $"D{driversSoFar}"));
            }
        }

        _lastGapDriversSoFar = driversSoFar;
    }

    private void RecordSessionInfoGapDriverChangeMarkers(
        LiveTelemetrySnapshot snapshot,
        LiveLeaderGapSnapshot gap,
        DateTimeOffset timestamp,
        double axisSeconds,
        double? lapReferenceSeconds)
    {
        if (snapshot.Context.Drivers.Count == 0)
        {
            return;
        }

        foreach (var driver in snapshot.Context.Drivers)
        {
            if (ToGapDriverIdentity(driver) is not { } identity)
            {
                continue;
            }

            if (_gapDriverIdentities.TryGetValue(identity.CarIdx, out var previous)
                && !previous.HasSameDriver(identity)
                && gap.ClassCars.FirstOrDefault(car => car.CarIdx == identity.CarIdx) is { } car
                && DesignV2GapSeconds(car, lapReferenceSeconds) is { } gapSeconds)
            {
                AddGapDriverChangeMarker(new DesignV2GapDriverChangeMarker(
                    timestamp,
                    axisSeconds,
                    identity.CarIdx,
                    gapSeconds,
                    car.IsReferenceCar,
                    identity.ShortLabel));
            }

            _gapDriverIdentities[identity.CarIdx] = identity;
        }
    }

    private void AddGapDriverChangeMarker(DesignV2GapDriverChangeMarker marker)
    {
        if (_gapDriverChangeMarkers.Any(existing =>
                existing.CarIdx == marker.CarIdx
                && Math.Abs(existing.AxisSeconds - marker.AxisSeconds) < 5d))
        {
            return;
        }

        _gapDriverChangeMarkers.Add(marker);
        if (_gapDriverChangeMarkers.Count > GapMaxDriverChangeMarkers)
        {
            _gapDriverChangeMarkers.RemoveRange(0, _gapDriverChangeMarkers.Count - GapMaxDriverChangeMarkers);
        }
    }

    private void RecordGapLeaderChange(LiveLeaderGapSnapshot gap, DateTimeOffset timestamp, double axisSeconds)
    {
        if (gap.ClassLeaderCarIdx is not { } leaderCarIdx)
        {
            return;
        }

        if (_lastGapClassLeaderCarIdx is { } previousLeader && previousLeader != leaderCarIdx)
        {
            _gapLeaderChangeMarkers.Add(new DesignV2GapLeaderChangeMarker(timestamp, axisSeconds, previousLeader, leaderCarIdx));
        }

        _lastGapClassLeaderCarIdx = leaderCarIdx;
    }

    private void UpdateGapCarRenderStates(LiveLeaderGapSnapshot gap, double axisSeconds, double? lapReferenceSeconds)
    {
        var desiredCarIds = SelectDesiredGapCarIds(gap.ClassCars, lapReferenceSeconds);
        foreach (var car in gap.ClassCars)
        {
            if (DesignV2GapSeconds(car, lapReferenceSeconds) is not { } gapSeconds)
            {
                continue;
            }

            if (!_gapCarRenderStates.TryGetValue(car.CarIdx, out var state))
            {
                state = new DesignV2GapCarRenderState(car.CarIdx);
                _gapCarRenderStates[car.CarIdx] = state;
            }

            var wasVisible = ShouldKeepGapSeriesVisible(state, axisSeconds);
            state.LastSeenAxisSeconds = axisSeconds;
            state.LastGapSeconds = gapSeconds;
            state.IsReference = car.IsReferenceCar;
            state.IsClassLeader = car.IsClassLeader;
            state.ClassPosition = car.ClassPosition;
            state.DeltaSecondsToReference = car.DeltaSecondsToReference;
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

        foreach (var state in _gapCarRenderStates.Values)
        {
            if (!desiredCarIds.Contains(state.CarIdx))
            {
                state.IsCurrentlyDesired = false;
            }
        }
    }

    private HashSet<int> SelectDesiredGapCarIds(IReadOnlyList<LiveClassGapCar> cars, double? lapReferenceSeconds)
    {
        var selected = new HashSet<int>();
        var reference = cars.FirstOrDefault(car => car.IsReferenceCar);
        foreach (var car in cars.Where(car => car.IsClassLeader || car.IsReferenceCar))
        {
            selected.Add(car.CarIdx);
        }

        var aheadCount = _settings.GetIntegerOption(OverlayOptionKeys.GapCarsAhead, defaultValue: 5, minimum: 0, maximum: 12);
        var behindCount = _settings.GetIntegerOption(OverlayOptionKeys.GapCarsBehind, defaultValue: 5, minimum: 0, maximum: 12);
        if (reference is null)
        {
            foreach (var car in cars
                .Where(car => !car.IsClassLeader)
                .OrderBy(car => car.ClassPosition ?? int.MaxValue)
                .ThenBy(car => DesignV2GapSeconds(car, lapReferenceSeconds) ?? double.MaxValue)
                .Take(behindCount))
            {
                selected.Add(car.CarIdx);
            }

            return selected;
        }

        var rangeSeconds = GapFilteredRangeSeconds();
        foreach (var car in cars
            .Where(car => !car.IsReferenceCar
                && !car.IsClassLeader
                && IsSameLapGapCandidate(car, reference, lapReferenceSeconds)
                && car.DeltaSecondsToReference is < 0d
                && Math.Abs(car.DeltaSecondsToReference.Value) <= rangeSeconds)
            .OrderByDescending(car => car.DeltaSecondsToReference!.Value)
            .Take(aheadCount))
        {
            selected.Add(car.CarIdx);
        }

        foreach (var car in cars
            .Where(car => !car.IsReferenceCar
                && !car.IsClassLeader
                && IsSameLapGapCandidate(car, reference, lapReferenceSeconds)
                && car.DeltaSecondsToReference is > 0d
                && car.DeltaSecondsToReference.Value <= rangeSeconds)
            .OrderBy(car => car.DeltaSecondsToReference!.Value)
            .Take(behindCount))
        {
            selected.Add(car.CarIdx);
        }

        return selected;
    }

    private IReadOnlyList<DesignV2GapSeriesSelection> SelectGapSeries()
    {
        var now = _latestGapAxisSeconds ?? 0d;
        return _gapCarRenderStates.Values
            .Where(state => ShouldKeepGapSeriesVisible(state, now))
            .Select(state => ToGapSeriesSelection(state, now))
            .OrderBy(selection => selection.State.LastGapSeconds)
            .ToArray();
    }

    private bool ShouldKeepGapSeriesVisible(DesignV2GapCarRenderState state, double axisSeconds)
    {
        return state.LastDesiredAxisSeconds is { } lastDesired
            && axisSeconds - lastDesired <= GapStickyVisibilitySeconds();
    }

    private double GapStickyVisibilitySeconds()
    {
        return GapFilteredRangeSeconds();
    }

    private double GapFilteredRangeSeconds()
    {
        var lapScaledRange = _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
            ? lapSeconds * GapFilteredRangeLaps
            : 0d;
        return Math.Min(
            GapFilteredRangeMaximumSeconds,
            Math.Max(GapFilteredRangeMinimumSeconds, lapScaledRange));
    }

    private double GapTrendLookupToleranceSeconds()
    {
        return Math.Min(
            60d,
            Math.Max(
                8d,
                _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                    ? lapSeconds * 0.08d
                    : 60d * 0.08d));
    }

    private double GapThreatGainThresholdSeconds()
    {
        return Math.Max(
            GapThreatMinimumGainSeconds,
            _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * GapThreatGainLapFraction
                : 0d);
    }

    private double GapMetricDeadbandSeconds()
    {
        return Math.Max(
            GapMetricDeadbandMinimumSeconds,
            _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * GapMetricDeadbandLapFraction
                : 0d);
    }

    private DesignV2GapSeriesSelection ToGapSeriesSelection(DesignV2GapCarRenderState state, double now)
    {
        var lastDesired = state.LastDesiredAxisSeconds ?? now;
        var visibleSince = state.VisibleSinceAxisSeconds ?? lastDesired;
        var isStickyExit = !state.IsCurrentlyDesired;
        var isStale = now - state.LastSeenAxisSeconds > GapMissingTelemetryGraceSeconds;
        var stickySeconds = GapStickyVisibilitySeconds();
        var exitAlpha = isStickyExit
            ? 1d - Math.Clamp((now - lastDesired) / Math.Max(1d, stickySeconds), 0d, 1d)
            : 1d;
        var entryAlpha = Math.Clamp((now - visibleSince) / GapEntryFadeSeconds, 0d, 1d);
        var alpha = Math.Clamp(Math.Min(exitAlpha, 0.35d + entryAlpha * 0.65d), 0.18d, 1d);
        var drawStartSeconds = now - visibleSince <= GapEntryFadeSeconds
            ? Math.Max(0d, visibleSince - GapEntryTailSeconds)
            : double.NegativeInfinity;
        return new DesignV2GapSeriesSelection(state, alpha, isStickyExit, isStale, drawStartSeconds);
    }

    private bool IsSameLapGapCandidate(LiveClassGapCar candidate, LiveClassGapCar reference, double? lapReferenceSeconds)
    {
        return NormalizedDesignV2GapLaps(candidate, lapReferenceSeconds) is { } candidateGapLaps
            && NormalizedDesignV2GapLaps(reference, lapReferenceSeconds) is { } referenceGapLaps
            && Math.Abs(candidateGapLaps - referenceGapLaps) < 0.95d;
    }

    private DesignV2GraphBody BuildGapGraphBody(IReadOnlyList<DesignV2GapSeriesSelection> selectedSeries, LiveTelemetrySnapshot snapshot)
    {
        var endSeconds = _latestGapAxisSeconds ?? 0d;
        var anchorSeconds = _gapTrendStartAxisSeconds ?? FirstVisibleGapAxisSeconds(selectedSeries) ?? endSeconds;
        var elapsedSeconds = Math.Max(0d, endSeconds - anchorSeconds);
        double startSeconds;
        if (elapsedSeconds >= GapTrendWindowSeconds)
        {
            startSeconds = endSeconds - GapTrendWindowSeconds;
        }
        else
        {
            var durationSeconds = Math.Min(
                GapTrendWindowSeconds,
                Math.Max(GapMinimumTrendDomainSecondsForCurrentLap(), elapsedSeconds + GapTrendRightPadding()));
            startSeconds = anchorSeconds;
            endSeconds = anchorSeconds + durationSeconds;
        }

        var trendMetrics = BuildDesignV2GapTrendMetrics();
        var activeThreat = ActiveDesignV2GapThreat(trendMetrics);
        var threatCarIdx = activeThreat?.Chaser?.CarIdx;
        var series = selectedSeries
            .Select(selection => new DesignV2GapSeries(
                selection.State.CarIdx,
                selection.State.IsReference,
                selection.State.IsClassLeader,
                selection.State.ClassPosition,
                selection.Alpha,
                selection.IsStickyExit,
                selection.IsStale,
                PointsForGapCar(selection.State.CarIdx, Math.Max(startSeconds, selection.DrawStartSeconds), endSeconds)))
            .Where(series => series.Points.Count > 0)
            .ToArray();
        var scale = SelectDesignV2GapScale(selectedSeries, startSeconds, endSeconds);
        return new DesignV2GraphBody(
            _gapPoints.ToArray(),
            series,
            _gapWeather.Where(point => point.AxisSeconds >= startSeconds && point.AxisSeconds <= endSeconds).ToArray(),
            _gapLeaderChangeMarkers.Where(marker => marker.AxisSeconds >= startSeconds && marker.AxisSeconds <= endSeconds).ToArray(),
            _gapDriverChangeMarkers.Where(marker => marker.AxisSeconds >= startSeconds && marker.AxisSeconds <= endSeconds).ToArray(),
            StartSeconds: startSeconds,
            EndSeconds: Math.Max(endSeconds, startSeconds + 1d),
            MaxGapSeconds: Math.Max(1d, scale.MaxGapSeconds),
            LapReferenceSeconds: TmrOverlay.App.Overlays.GapToLeader.GapToLeaderLiveModelAdapter.SelectLapReferenceSeconds(snapshot),
            SelectedSeriesCount: selectedSeries.Count,
            TrendMetrics: trendMetrics,
            ActiveThreat: activeThreat,
            ThreatCarIdx: threatCarIdx,
            MetricDeadbandSeconds: GapMetricDeadbandSeconds(),
            Scale: scale);
    }

    private IReadOnlyList<DesignV2GapTrendMetric> BuildDesignV2GapTrendMetrics()
    {
        if (_lastGapLapReferenceSeconds is not { } lapReferenceSeconds
            || !IsValidLapReference(lapReferenceSeconds)
            || _gapCarRenderStates.Values.FirstOrDefault(state => state.IsReference) is not { } referenceState)
        {
            return DefaultDesignV2GapTrendMetrics("unavailable");
        }

        var latest = _latestGapAxisSeconds ?? 0d;
        return new[]
        {
            BuildDesignV2GapTrendMetric("5L", lapReferenceSeconds * 5d, 5d, latest, referenceState),
            BuildDesignV2GapTrendMetric("10L", lapReferenceSeconds * 10d, 10d, latest, referenceState),
            BuildDesignV2GapStintTrendMetric(latest, referenceState)
        };
    }

    private DesignV2GapTrendMetric BuildDesignV2GapStintTrendMetric(double latest, DesignV2GapCarRenderState referenceState)
    {
        if (_currentGapFuelStintStartAxisSeconds is not { } stintStart)
        {
            return new DesignV2GapTrendMetric("stint", null, null, "unavailable", null);
        }

        var lookbackSeconds = latest - stintStart;
        if (!IsFinite(lookbackSeconds) || lookbackSeconds < 5d)
        {
            return new DesignV2GapTrendMetric("stint", null, null, "warming", "out lap");
        }

        return BuildDesignV2GapTrendMetric("stint", lookbackSeconds, null, latest, referenceState);
    }

    private DesignV2GapTrendMetric BuildDesignV2GapTrendMetric(
        string label,
        double lookbackSeconds,
        double? targetLaps,
        double latest,
        DesignV2GapCarRenderState referenceState)
    {
        if (!IsFinite(lookbackSeconds)
            || lookbackSeconds <= 0d
            || LatestDesignV2GapTrendPoint(referenceState.CarIdx) is not { } referenceCurrent)
        {
            return new DesignV2GapTrendMetric(label, null, null, "unavailable", null);
        }

        var targetAxisSeconds = latest - lookbackSeconds;
        if (DesignV2GapLeaderChangedBetween(targetAxisSeconds, latest))
        {
            return new DesignV2GapTrendMetric(label, null, null, "leaderChanged", null);
        }

        if (DesignV2GapTrendPointNear(referenceState.CarIdx, targetAxisSeconds) is not { } referencePast)
        {
            return new DesignV2GapTrendMetric(
                label,
                null,
                null,
                "warming",
                DesignV2GapWarmupLabel(referenceState.CarIdx, latest, targetLaps));
        }

        var focusGapChangeSeconds = referenceCurrent.GapSeconds - referencePast.GapSeconds;
        var chaser = StrongestDesignV2GapBehindGain(referenceState, referenceCurrent, referencePast, targetAxisSeconds);
        return new DesignV2GapTrendMetric(label, focusGapChangeSeconds, chaser, "ready", null);
    }

    private string? DesignV2GapWarmupLabel(int referenceCarIdx, double latest, double? targetLaps)
    {
        if (targetLaps is not { } laps
            || laps <= 0d
            || _lastGapLapReferenceSeconds is not { } lapReferenceSeconds
            || !IsValidLapReference(lapReferenceSeconds)
            || FirstDesignV2GapTrendPoint(referenceCarIdx) is not { } first)
        {
            return null;
        }

        var availableLaps = Math.Max(0d, (latest - first.AxisSeconds) / lapReferenceSeconds);
        return $"{Math.Min(availableLaps, laps).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}L";
    }

    private DesignV2BehindGainMetric? StrongestDesignV2GapBehindGain(
        DesignV2GapCarRenderState referenceState,
        DesignV2GapTrendPoint referenceCurrent,
        DesignV2GapTrendPoint referencePast,
        double targetAxisSeconds)
    {
        DesignV2BehindGainMetric? best = null;
        foreach (var state in _gapCarRenderStates.Values)
        {
            if (state.CarIdx == referenceState.CarIdx
                || state.IsReference
                || LatestDesignV2GapTrendPoint(state.CarIdx) is not { } current
                || current.GapSeconds <= referenceCurrent.GapSeconds
                || DesignV2GapTrendPointNear(state.CarIdx, targetAxisSeconds) is not { } past)
            {
                continue;
            }

            var currentDelta = current.GapSeconds - referenceCurrent.GapSeconds;
            var pastDelta = past.GapSeconds - referencePast.GapSeconds;
            var gainSeconds = pastDelta - currentDelta;
            if (gainSeconds < GapThreatGainThresholdSeconds())
            {
                continue;
            }

            if (best is null || gainSeconds > best.GainSeconds)
            {
                best = new DesignV2BehindGainMetric(state.CarIdx, DesignV2GapCarShortLabel(state), gainSeconds);
            }
        }

        return best;
    }

    private DesignV2GapTrendMetric? ActiveDesignV2GapThreat(IReadOnlyList<DesignV2GapTrendMetric> metrics)
    {
        return metrics
            .Where(metric => string.Equals(metric.State, "ready", StringComparison.Ordinal)
                && metric.Chaser is not null
                && metric.Chaser.GainSeconds >= GapThreatGainThresholdSeconds())
            .OrderByDescending(metric => metric.Chaser!.GainSeconds)
            .FirstOrDefault();
    }

    private DesignV2GapTrendPoint? LatestDesignV2GapTrendPoint(int carIdx)
    {
        return _gapSeries.TryGetValue(carIdx, out var points) && points.Count > 0
            ? points[^1]
            : null;
    }

    private DesignV2GapTrendPoint? FirstDesignV2GapTrendPoint(int carIdx)
    {
        return _gapSeries.TryGetValue(carIdx, out var points) && points.Count > 0
            ? points[0]
            : null;
    }

    private DesignV2GapTrendPoint? DesignV2GapTrendPointNear(int carIdx, double axisSeconds)
    {
        if (!_gapSeries.TryGetValue(carIdx, out var points) || points.Count == 0)
        {
            return null;
        }

        var best = points.MinBy(point => Math.Abs(point.AxisSeconds - axisSeconds));
        return best is not null && Math.Abs(best.AxisSeconds - axisSeconds) <= GapTrendLookupToleranceSeconds()
            ? best
            : null;
    }

    private bool DesignV2GapLeaderChangedBetween(double startSeconds, double endSeconds)
    {
        return _gapLeaderChangeMarkers.Any(marker => marker.AxisSeconds > startSeconds && marker.AxisSeconds <= endSeconds);
    }

    private IReadOnlyList<DesignV2GapTrendMetric> DefaultDesignV2GapTrendMetrics(string state)
    {
        return new[]
        {
            new DesignV2GapTrendMetric("5L", null, null, state, null),
            new DesignV2GapTrendMetric("10L", null, null, state, null),
            new DesignV2GapTrendMetric("stint", null, null, state, null)
        };
    }

    private IReadOnlyList<DesignV2GapTrendPoint> PointsForGapCar(int carIdx, double startSeconds, double endSeconds)
    {
        return _gapSeries.TryGetValue(carIdx, out var points)
            ? points.Where(point => point.AxisSeconds >= startSeconds && point.AxisSeconds <= endSeconds).ToArray()
            : Array.Empty<DesignV2GapTrendPoint>();
    }

    private double GapMinimumTrendDomainSecondsForCurrentLap()
    {
        return Math.Max(
            GapMinimumTrendDomainSeconds,
            _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * GapMinimumTrendDomainLaps
                : 0d);
    }

    private double GapTrendRightPadding()
    {
        return Math.Max(
            GapTrendRightPaddingSeconds,
            _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * GapTrendRightPaddingLaps
                : 0d);
    }

    private double? FirstVisibleGapAxisSeconds(IReadOnlyList<DesignV2GapSeriesSelection> selectedSeries)
    {
        double? firstVisiblePoint = null;
        foreach (var selection in selectedSeries)
        {
            if (!_gapSeries.TryGetValue(selection.State.CarIdx, out var points))
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

    private DesignV2GapScale SelectDesignV2GapScale(
        IReadOnlyList<DesignV2GapSeriesSelection> selectedSeries,
        double startSeconds,
        double endSeconds)
    {
        var leaderScaleMax = SelectDesignV2MaxGapSeconds(selectedSeries, startSeconds, endSeconds);
        var referenceSelection = selectedSeries.FirstOrDefault(selection => selection.State.IsReference);
        if (referenceSelection is null
            || !_gapSeries.TryGetValue(referenceSelection.State.CarIdx, out var rawReferencePoints))
        {
            return DesignV2GapScale.Leader(leaderScaleMax);
        }

        var referencePoints = rawReferencePoints
            .Where(point => point.AxisSeconds >= startSeconds && point.AxisSeconds <= endSeconds)
            .OrderBy(point => point.AxisSeconds)
            .ToArray();
        if (referencePoints.Length == 0)
        {
            return DesignV2GapScale.Leader(leaderScaleMax);
        }

        var latestReferenceGap = GapReferenceAt(referencePoints, endSeconds);
        var triggerGap = GapFocusScaleMinimumReferenceGap();
        if (latestReferenceGap < triggerGap)
        {
            return DesignV2GapScale.Leader(leaderScaleMax);
        }

        var maxAheadSeconds = 0d;
        var maxBehindSeconds = 0d;
        var hasLocalComparison = false;
        foreach (var selection in selectedSeries.Where(selection => !selection.State.IsClassLeader))
        {
            if (!_gapSeries.TryGetValue(selection.State.CarIdx, out var points))
            {
                continue;
            }

            foreach (var point in points.Where(point =>
                point.AxisSeconds >= selection.DrawStartSeconds
                && point.AxisSeconds >= startSeconds
                && point.AxisSeconds <= endSeconds))
            {
                var delta = point.GapSeconds - GapReferenceAt(referencePoints, point.AxisSeconds);
                hasLocalComparison |= !selection.State.IsReference && Math.Abs(delta) > 0.001d;
                if (delta < 0d)
                {
                    maxAheadSeconds = Math.Max(maxAheadSeconds, Math.Abs(delta));
                }
                else
                {
                    maxBehindSeconds = Math.Max(maxBehindSeconds, delta);
                }
            }
        }

        var minimumRange = GapFocusScaleMinimumRange();
        var aheadRange = NiceCeiling(Math.Max(minimumRange, maxAheadSeconds * GapFocusScalePaddingMultiplier));
        var behindRange = NiceCeiling(Math.Max(minimumRange, maxBehindSeconds * GapFocusScalePaddingMultiplier));
        var localRange = Math.Max(aheadRange, behindRange);
        var forceFocusScaleForLappedReference = ShouldForceDesignV2FocusScaleForLappedReference(latestReferenceGap);
        if (!forceFocusScaleForLappedReference
            && (!hasLocalComparison || leaderScaleMax < Math.Max(triggerGap, localRange * GapFocusScaleTriggerRatio)))
        {
            return DesignV2GapScale.Leader(leaderScaleMax);
        }

        return DesignV2GapScale.FocusRelative(
            leaderScaleMax,
            aheadRange,
            behindRange,
            referencePoints,
            latestReferenceGap);
    }

    private double SelectDesignV2MaxGapSeconds(
        IReadOnlyList<DesignV2GapSeriesSelection> selectedSeries,
        double startSeconds,
        double endSeconds)
    {
        var maxGap = selectedSeries
            .Where(selection => _gapSeries.ContainsKey(selection.State.CarIdx))
            .SelectMany(selection => _gapSeries[selection.State.CarIdx].Where(point => point.AxisSeconds >= selection.DrawStartSeconds))
            .Where(point => point.AxisSeconds >= startSeconds && point.AxisSeconds <= endSeconds)
            .Select(point => point.GapSeconds)
            .DefaultIfEmpty(1d)
            .Max();
        return NiceCeiling(Math.Max(1d, maxGap));
    }

    private double GapFocusScaleMinimumReferenceGap()
    {
        return Math.Max(
            GapFocusScaleMinimumReferenceGapSeconds,
            _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * GapFocusScaleMinimumReferenceGapLaps
                : 0d);
    }

    private double GapFocusScaleMinimumRange()
    {
        return Math.Max(
            GapFocusScaleMinimumRangeSeconds,
            _lastGapLapReferenceSeconds is { } lapSeconds && IsValidLapReference(lapSeconds)
                ? lapSeconds * GapFocusScaleMinimumRangeLaps
                : 0d);
    }

    private bool ShouldForceDesignV2FocusScaleForLappedReference(double latestReferenceGap)
    {
        return _lastGapLapReferenceSeconds is { } lapSeconds
            && IsValidLapReference(lapSeconds)
            && latestReferenceGap >= lapSeconds * GapSameLapReferenceBoundaryLaps;
    }

    private static double GapReferenceAt(IReadOnlyList<DesignV2GapTrendPoint> referencePoints, double axisSeconds)
    {
        if (referencePoints.Count == 0)
        {
            return 0d;
        }

        if (axisSeconds <= referencePoints[0].AxisSeconds)
        {
            return referencePoints[0].GapSeconds;
        }

        var last = referencePoints[^1];
        if (axisSeconds >= last.AxisSeconds)
        {
            return last.GapSeconds;
        }

        var low = 0;
        var high = referencePoints.Count - 1;
        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            var midSeconds = referencePoints[mid].AxisSeconds;
            if (Math.Abs(midSeconds - axisSeconds) < 0.001d)
            {
                return referencePoints[mid].GapSeconds;
            }

            if (midSeconds < axisSeconds)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        var after = referencePoints[Math.Clamp(low, 0, referencePoints.Count - 1)];
        var before = referencePoints[Math.Clamp(low - 1, 0, referencePoints.Count - 1)];
        var span = after.AxisSeconds - before.AxisSeconds;
        if (span <= 0.001d)
        {
            return before.GapSeconds;
        }

        var ratio = Math.Clamp((axisSeconds - before.AxisSeconds) / span, 0d, 1d);
        return before.GapSeconds + (after.GapSeconds - before.GapSeconds) * ratio;
    }

    private static double? DesignV2GapSeconds(LiveClassGapCar car, double? lapReferenceSeconds)
    {
        if (car.IsClassLeader)
        {
            return 0d;
        }

        if (car.GapSecondsToClassLeader is { } seconds && IsFinite(seconds) && seconds >= 0d)
        {
            return seconds;
        }

        return car.GapLapsToClassLeader is { } laps && IsFinite(laps) && laps >= 0d
            ? laps * (lapReferenceSeconds is { } lapSeconds && IsFinite(lapSeconds) && lapSeconds > 20d ? lapSeconds : GapDefaultLapReferenceSeconds)
            : null;
    }

    private static double? NormalizedDesignV2GapLaps(LiveClassGapCar car, double? lapReferenceSeconds)
    {
        if (car.GapLapsToClassLeader is { } laps && IsFinite(laps))
        {
            return laps;
        }

        if (DesignV2GapSeconds(car, lapReferenceSeconds) is { } seconds
            && lapReferenceSeconds is { } lapSeconds
            && IsValidLapReference(lapSeconds))
        {
            return seconds / lapSeconds;
        }

        return null;
    }

    private static DesignV2GapDriverIdentity? ToGapDriverIdentity(HistoricalSessionDriver driver)
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

        return new DesignV2GapDriverIdentity(carIdx, key, SelectGapDriverLabel(driver));
    }

    private static string SelectGapDriverLabel(HistoricalSessionDriver driver)
    {
        foreach (var value in new[] { driver.Initials, driver.AbbrevName, driver.UserName })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.Trim();
                return trimmed.Length <= 3
                    ? trimmed
                    : trimmed[..Math.Min(3, trimmed.Length)].ToUpperInvariant();
            }
        }

        return "DR";
    }

    private static DesignV2GapWeatherCondition SelectWeatherCondition(LiveTelemetrySnapshot snapshot)
    {
        var weather = snapshot.Models.Weather;
        if (!weather.HasData)
        {
            return DesignV2GapWeatherCondition.Unknown;
        }

        if (weather.WeatherDeclaredWet == true)
        {
            return DesignV2GapWeatherCondition.DeclaredWet;
        }

        return weather.TrackWetness switch
        {
            >= 4 => DesignV2GapWeatherCondition.Wet,
            >= 2 => DesignV2GapWeatherCondition.Damp,
            >= 0 => DesignV2GapWeatherCondition.Dry,
            _ => DesignV2GapWeatherCondition.Unknown
        };
    }

    private DesignV2OverlayModel BuildTrackMapModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var trackMap = snapshot.Models.TrackMap;
        RefreshTrackMap(snapshot, now);
        return new DesignV2OverlayModel(
            "Track Map",
            availability.IsAvailable ? "live" : availability.StatusText,
            availability.IsAvailable ? "source: live position telemetry" : "source: waiting",
            availability.IsAvailable ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable,
            new DesignV2TrackMapBody(
                SmoothTrackMapMarkers(BuildTrackMapMarkers(snapshot)),
                trackMap.Sectors,
                _settings.GetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, defaultValue: true),
                _settings.Opacity,
                availability.IsAvailable,
                _trackMap));
    }

    private void RefreshTrackMap(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var identity = TrackMapIdentity.From(snapshot.Context.Track);
        var identityChanged = !string.Equals(identity.Key, _trackMapIdentityKey, StringComparison.Ordinal);
        if (!identityChanged && now < _nextTrackMapReloadAtUtc)
        {
            return;
        }

        if (identityChanged)
        {
            _smoothedTrackMarkerProgress.Clear();
            _lastTrackMarkerSmoothingAtUtc = null;
        }

        _trackMapIdentityKey = identity.Key;
        _nextTrackMapReloadAtUtc = now.AddSeconds(TrackMapReloadIntervalSeconds);
        _trackMap = _trackMapStore.TryReadBest(
            snapshot.Context.Track,
            includeUserMaps: _settings.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true));
    }

    private DesignV2OverlayModel BuildStreamChatModel()
    {
        RefreshStreamChatSettings();
        var rows = _chatMessages
            .Skip(Math.Max(0, _chatMessages.Count - VisibleChatMessageBudget))
            .Select(message => new DesignV2ChatRow(message.Name, message.Text, ChatEvidence(message.Kind)))
            .ToArray();
        if (rows.Length == 0)
        {
            rows = [new DesignV2ChatRow("TMR", "Choose Twitch or Streamlabs in settings.", DesignV2Evidence.Unavailable)];
        }

        return new DesignV2OverlayModel(
            "Stream Chat",
            _streamChatStatus,
            "source: stream chat settings",
            rows.Any(row => row.Evidence == DesignV2Evidence.Live)
                ? DesignV2Evidence.Live
                : rows.Any(row => row.Evidence == DesignV2Evidence.Error)
                    ? DesignV2Evidence.Error
                    : DesignV2Evidence.Unavailable,
            new DesignV2ChatBody(rows));
    }

    private void RefreshStreamChatSettings()
    {
        var settings = StreamChatSettingsFromOverlay();
        var settingsKey = $"{settings.Provider}|{settings.StreamlabsWidgetUrl}|{settings.TwitchChannel}|{settings.Status}";
        if (string.Equals(_activeStreamChatSettingsKey, settingsKey, StringComparison.Ordinal))
        {
            return;
        }

        _activeStreamChatSettingsKey = settingsKey;
        _chatConnectedAnnounced = false;
        StopChatConnection();

        if (!settings.IsConfigured)
        {
            ReplaceChatMessages(new StreamChatMessage("TMR", StreamChatStatusText(settings.Status), StreamChatMessageKind.System));
            SetChatStatus("waiting for chat source");
            return;
        }

        if (string.Equals(settings.Provider, StreamChatOverlaySettings.ProviderTwitch, StringComparison.Ordinal)
            && settings.TwitchChannel is { Length: > 0 } channel)
        {
            ReplaceChatMessages(new StreamChatMessage("TMR", $"Connecting to #{channel}...", StreamChatMessageKind.System));
            SetChatStatus("connecting | twitch");
            StartTwitchConnection(channel);
            return;
        }

        if (string.Equals(settings.Provider, StreamChatOverlaySettings.ProviderStreamlabs, StringComparison.Ordinal))
        {
            ReplaceChatMessages(new StreamChatMessage("TMR", "Streamlabs is browser-source only in this build.", StreamChatMessageKind.Error));
            SetChatStatus("streamlabs unavailable");
            return;
        }

        ReplaceChatMessages(new StreamChatMessage("TMR", "Stream chat provider unavailable.", StreamChatMessageKind.Error));
        SetChatStatus("chat provider unavailable");
    }

    private StreamChatBrowserSettings StreamChatSettingsFromOverlay()
    {
        return StreamChatOverlaySettings.FromOverlay(_settings);
    }

    private void StartTwitchConnection(string channel)
    {
        StopChatConnection();
        var cancellation = new CancellationTokenSource();
        _chatConnectionCancellation = cancellation;
        _chatConnectionTask = Task.Run(() => RunTwitchConnectionLoopAsync(channel, cancellation.Token), CancellationToken.None);
    }

    private void StopChatConnection()
    {
        var cancellation = _chatConnectionCancellation;
        var task = _chatConnectionTask;
        _chatConnectionCancellation = null;
        _chatConnectionTask = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        _ = (task ?? Task.CompletedTask).ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RunTwitchConnectionLoopAsync(string channel, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var shouldReconnect = true;
            try
            {
                shouldReconnect = await ConnectAndReadTwitchAsync(channel, cancellationToken).ConfigureAwait(false);
                attempt = shouldReconnect ? attempt + 1 : 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                attempt++;
                LogChatWarningOnce(exception, "connection");
                RunOnUiThread(() =>
                {
                    ConfirmChatMessage(new StreamChatMessage("TMR", "Twitch chat connection error.", StreamChatMessageKind.Error));
                    SetChatStatus("chat reconnecting | twitch");
                });
            }

            if (!shouldReconnect || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var delay = TimeSpan.FromSeconds(Math.Min(15, 3 + attempt * 2));
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<bool> ConnectAndReadTwitchAsync(string channel, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        using var socket = new ClientWebSocket();
        try
        {
            RunOnUiThread(() => SetChatStatus("connecting | twitch"));
            await socket.ConnectAsync(TwitchChatUri, cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, "CAP REQ :twitch.tv/tags twitch.tv/commands", cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, "PASS SCHMOOPIIE", cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, $"NICK justinfan{Random.Shared.Next(10000, 99999)}", cancellationToken).ConfigureAwait(false);
            await SendRawAsync(socket, $"JOIN #{channel}", cancellationToken).ConfigureAwait(false);
            RunOnUiThread(() => SetChatStatus("joining | twitch"));
            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(AppPerformanceMetricIds.OverlayStreamChatConnect, started, succeeded);
        }

        return await ReceiveTwitchMessagesAsync(socket, channel, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ReceiveTwitchMessagesAsync(
        ClientWebSocket socket,
        string channel,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                HandleChatSocketClosed();
                return true;
            }

            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            var shouldContinue = await ProcessTwitchPayloadAsync(
                socket,
                channel,
                builder.ToString(),
                cancellationToken).ConfigureAwait(false);
            builder.Clear();
            if (!shouldContinue)
            {
                return false;
            }
        }

        HandleChatSocketClosed();
        return true;
    }

    private async Task<bool> ProcessTwitchPayloadAsync(
        ClientWebSocket socket,
        string channel,
        string payload,
        CancellationToken cancellationToken)
    {
        foreach (var rawLine in payload.Split("\r\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (StreamChatIrcParser.TryGetPingResponse(rawLine, out var pong))
            {
                await SendRawAsync(socket, pong, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (StreamChatIrcParser.IsAuthFailure(rawLine))
            {
                RunOnUiThread(() =>
                {
                    ConfirmChatMessage(new StreamChatMessage("TMR", "Twitch rejected the chat connection.", StreamChatMessageKind.Error));
                    SetChatStatus("twitch auth rejected");
                });
                return false;
            }

            if (StreamChatIrcParser.IsReconnect(rawLine))
            {
                RunOnUiThread(() => SetChatStatus("chat reconnecting | twitch"));
                return true;
            }

            if (StreamChatIrcParser.IsJoined(rawLine, channel))
            {
                AnnounceChatConnected(channel);
                continue;
            }

            var message = StreamChatIrcParser.TryParsePrivMsg(rawLine);
            if (message is not null)
            {
                RunOnUiThread(() => AddChatMessage(message));
            }
        }

        return true;
    }

    private static async Task SendRawAsync(ClientWebSocket socket, string line, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\r\n");
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    private void AnnounceChatConnected(string channel)
    {
        RunOnUiThread(() =>
        {
            if (_chatConnectedAnnounced)
            {
                return;
            }

            _chatConnectedAnnounced = true;
            ConfirmChatMessage(new StreamChatMessage("TMR", $"Chat connected to #{channel}.", StreamChatMessageKind.System));
            SetChatStatus("chat connected | twitch");
        });
    }

    private void HandleChatSocketClosed()
    {
        RunOnUiThread(() =>
        {
            if (_chatConnectedAnnounced)
            {
                SetChatStatus("chat reconnecting | twitch");
                return;
            }

            ConfirmChatMessage(new StreamChatMessage("TMR", "Twitch chat disconnected before joining.", StreamChatMessageKind.Error));
            SetChatStatus("chat reconnecting | twitch");
        });
    }

    private void ReplaceChatMessages(params StreamChatMessage[] messages)
    {
        _chatMessages.Clear();
        _chatMessages.AddRange(messages);
        Invalidate();
    }

    private void AddChatMessage(StreamChatMessage message)
    {
        _chatMessages.Add(message);
        if (_chatMessages.Count > MaximumChatMessages)
        {
            _chatMessages.RemoveRange(0, _chatMessages.Count - MaximumChatMessages);
        }

        Invalidate();
    }

    private void ConfirmChatMessage(StreamChatMessage message)
    {
        if (_chatMessages.Count == 1 && _chatMessages[0].Kind == StreamChatMessageKind.System)
        {
            _chatMessages[0] = message;
            Invalidate();
            return;
        }

        AddChatMessage(message);
    }

    private void SetChatStatus(string status)
    {
        if (string.Equals(_streamChatStatus, status, StringComparison.Ordinal))
        {
            return;
        }

        _streamChatStatus = status;
        Invalidate();
    }

    private void RunOnUiThread(Action action)
    {
        if (_disposed || IsDisposed)
        {
            return;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
                return;
            }

            action();
        }
        catch (InvalidOperationException)
        {
            // The overlay can close while the chat socket is unwinding.
        }
    }

    private void LogChatWarningOnce(Exception exception, string phase)
    {
        var key = $"{phase}:{exception.GetType().FullName}:{exception.Message}";
        if (string.Equals(_lastChatLoggedError, key, StringComparison.Ordinal))
        {
            return;
        }

        _lastChatLoggedError = key;
        _logger.LogWarning(exception, "Design V2 stream chat overlay {Phase} failed.", phase);
    }

    private static string StreamChatStatusText(string status)
    {
        return status switch
        {
            "missing_or_invalid_streamlabs_url" => "Streamlabs Chat Box URL is missing.",
            "missing_or_invalid_twitch_channel" => "Twitch channel is missing.",
            _ => "Choose a chat source in settings."
        };
    }

    private static string KindName(DesignV2LiveOverlayKind kind)
    {
        return kind switch
        {
            DesignV2LiveOverlayKind.Standings => "standings",
            DesignV2LiveOverlayKind.FuelCalculator => "fuel-calculator",
            DesignV2LiveOverlayKind.Relative => "relative",
            DesignV2LiveOverlayKind.TrackMap => "track-map",
            DesignV2LiveOverlayKind.StreamChat => "stream-chat",
            DesignV2LiveOverlayKind.Flags => "flags",
            DesignV2LiveOverlayKind.SessionWeather => "session-weather",
            DesignV2LiveOverlayKind.PitService => "pit-service",
            DesignV2LiveOverlayKind.InputState => "input-state",
            DesignV2LiveOverlayKind.CarRadar => "car-radar",
            DesignV2LiveOverlayKind.GapToLeader => "gap-to-leader",
            _ => "unknown"
        };
    }

    private static string BodyName(DesignV2Body body)
    {
        return body switch
        {
            DesignV2TableBody => "table",
            DesignV2MetricRowsBody => "metric-rows",
            DesignV2GraphBody => "graph",
            DesignV2InputsBody => "inputs",
            DesignV2RadarBody => "radar",
            DesignV2ChatBody => "chat",
            DesignV2FlagsBody => "flags",
            DesignV2TrackMapBody => "track-map",
            _ => "unknown"
        };
    }

    private static DesignV2Evidence ChatEvidence(StreamChatMessageKind kind)
    {
        return kind switch
        {
            StreamChatMessageKind.Message => DesignV2Evidence.Live,
            StreamChatMessageKind.Error => DesignV2Evidence.Error,
            _ => DesignV2Evidence.Partial
        };
    }

    private DesignV2OverlayModel BuildFlagsModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);
        var flags = viewModel.Flags
            .Where(flag => IsFlagCategoryEnabled(flag.Category))
            .ToArray();
        return new DesignV2OverlayModel(
            "Flags",
            viewModel.Status,
            "source: session flags telemetry",
            EvidenceFor(viewModel.Tone),
            new DesignV2FlagsBody(
                flags,
                viewModel.IsWaiting,
                _flagsManagedEnabled,
                _flagsSettingsOverlayActive));
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

    private bool InputBlockEnabled(string blockId)
    {
        var block = OverlayContentColumnSettings.InputState.Blocks?.FirstOrDefault(
            block => string.Equals(block.Id, blockId, StringComparison.Ordinal));
        return block is null || OverlayContentColumnSettings.BlockEnabled(_settings, block);
    }

    private bool IsFlagCategoryEnabled(FlagDisplayCategory category)
    {
        return category switch
        {
            FlagDisplayCategory.Green => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowGreen, defaultValue: true),
            FlagDisplayCategory.Blue => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowBlue, defaultValue: true),
            FlagDisplayCategory.Yellow => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowYellow, defaultValue: true),
            FlagDisplayCategory.Critical => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowCritical, defaultValue: true),
            FlagDisplayCategory.Finish => _settings.GetBooleanOption(OverlayOptionKeys.FlagsShowFinish, defaultValue: true),
            _ => true
        };
    }

    private IReadOnlyList<DesignV2TrackMapMarker> SmoothTrackMapMarkers(IReadOnlyList<DesignV2TrackMapMarker> markers)
    {
        if (markers.Count == 0)
        {
            _smoothedTrackMarkerProgress.Clear();
            _lastTrackMarkerSmoothingAtUtc = null;
            return markers;
        }

        var now = DateTimeOffset.UtcNow;
        var elapsed = _lastTrackMarkerSmoothingAtUtc is { } last
            ? Math.Clamp((now - last).TotalSeconds, 0d, 0.25d)
            : 0.05d;
        _lastTrackMarkerSmoothingAtUtc = now;
        var alpha = 1d - Math.Exp(-elapsed / 0.14d);
        var activeCarIds = markers.Select(marker => marker.CarIdx).ToHashSet();
        foreach (var carIdx in _smoothedTrackMarkerProgress.Keys.Where(carIdx => !activeCarIds.Contains(carIdx)).ToArray())
        {
            _smoothedTrackMarkerProgress.Remove(carIdx);
        }

        return markers.Select(marker =>
        {
            if (!_smoothedTrackMarkerProgress.TryGetValue(marker.CarIdx, out var current))
            {
                _smoothedTrackMarkerProgress[marker.CarIdx] = marker.LapDistPct;
                return marker;
            }

            var smoothed = NormalizeProgress(current + ProgressDelta(current, marker.LapDistPct) * Math.Clamp(alpha, 0d, 1d));
            _smoothedTrackMarkerProgress[marker.CarIdx] = smoothed;
            return marker with { LapDistPct = smoothed };
        }).ToArray();
    }

    internal static IReadOnlyList<DesignV2TrackMapMarker> BuildTrackMapMarkers(LiveTelemetrySnapshot snapshot)
    {
        var markers = new Dictionary<int, DesignV2TrackMapMarker>();
        var scoringByCarIdx = snapshot.Models.Scoring.Rows
            .GroupBy(row => row.CarIdx)
            .ToDictionary(group => group.Key, group => group.First());
        var referenceCarIdx = snapshot.Models.Reference.FocusCarIdx
            ?? snapshot.Models.Scoring.ReferenceCarIdx
            ?? snapshot.Models.Timing.FocusCarIdx
            ?? snapshot.Models.Spatial.ReferenceCarIdx
            ?? snapshot.LatestSample?.FocusCarIdx;

        foreach (var row in snapshot.Models.Timing.OverallRows.Concat(snapshot.Models.Timing.ClassRows))
        {
            scoringByCarIdx.TryGetValue(row.CarIdx, out var scoringRow);
            var isFocus = row.IsFocus
                || row.CarIdx == referenceCarIdx
                || scoringRow?.IsFocus == true;
            if (!TrackMapMarkerPolicy.ShouldRenderTimingMarker(row, isFocus)
                || row.LapDistPct is not { } lapDistPct)
            {
                continue;
            }

            var marker = new DesignV2TrackMapMarker(
                row.CarIdx,
                NormalizeProgress(lapDistPct),
                isFocus,
                MarkerColor(scoringRow?.CarClassColorHex ?? row.CarClassColorHex, isFocus),
                PositionLabel(row, scoringRow, referenceCarIdx));
            if (!markers.TryGetValue(row.CarIdx, out var existing)
                || marker.IsFocus
                || !existing.IsFocus)
            {
                markers[row.CarIdx] = marker;
            }
        }

        var focusProgress = MarkerProgress(snapshot.LatestSample);
        if (referenceCarIdx is { } focusMarkerCarIdx
            && focusProgress is { } progress
            && IsValidProgress(progress))
        {
            markers[focusMarkerCarIdx] = new DesignV2TrackMapMarker(
                focusMarkerCarIdx,
                NormalizeProgress(progress),
                IsFocus: true,
                Cyan,
                FocusPositionLabel(snapshot, scoringByCarIdx, focusMarkerCarIdx));
        }

        return markers.Values.ToArray();
    }

    private static string? PositionLabel(LiveTimingRow row, LiveScoringRow? scoringRow, int? referenceCarIdx)
    {
        if (!row.IsFocus
            && row.CarIdx != referenceCarIdx
            && scoringRow?.IsFocus != true)
        {
            return null;
        }

        return PositionLabel(scoringRow) ?? PositionLabel(row);
    }

    private static string? PositionLabel(LiveTimingRow? row)
    {
        var position = row?.ClassPosition ?? row?.OverallPosition;
        return position is > 0 ? position.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    private static string? PositionLabel(LiveScoringRow? row)
    {
        var position = row?.ClassPosition ?? row?.OverallPosition;
        return position is > 0 ? position.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    private static string? FocusPositionLabel(
        LiveTelemetrySnapshot snapshot,
        IReadOnlyDictionary<int, LiveScoringRow> scoringByCarIdx,
        int focusCarIdx)
    {
        if (scoringByCarIdx.TryGetValue(focusCarIdx, out var scoringRow))
        {
            return PositionLabel(scoringRow);
        }

        return PositionLabel(snapshot.Models.Timing.FocusRow)
            ?? FocusPositionLabel(snapshot.LatestSample);
    }

    private static string? FocusPositionLabel(HistoricalTelemetrySample? sample)
    {
        var position = sample?.FocusClassPosition
            ?? sample?.FocusPosition;
        return position is > 0 ? position.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    private static double? MarkerProgress(HistoricalTelemetrySample? sample)
    {
        if (!TrackMapMarkerPolicy.ShouldRenderFocusSampleMarker(sample))
        {
            return null;
        }

        var progress = sample?.FocusLapDistPct;
        return progress is { } value
            ? NormalizeProgress(value)
            : null;
    }

    private static Color MarkerColor(string? classColorHex, bool isFocus)
    {
        if (isFocus)
        {
            return Cyan;
        }

        return OverlayClassColor.TryParseWithAlpha(classColorHex, 245) ?? Color.FromArgb(245, 237, 245, 250);
    }

    private static double ProgressDelta(double current, double target)
    {
        var delta = target - current;
        if (delta > 0.5d)
        {
            delta -= 1d;
        }
        else if (delta < -0.5d)
        {
            delta += 1d;
        }

        return delta;
    }

    private bool DrawCustomOverlay(Graphics graphics, Rectangle bounds, DesignV2OverlayModel model)
    {
        var rect = RectangleF.Inflate(bounds, -0.5f, -0.5f);
        switch (model.Body)
        {
            case DesignV2RadarBody radar:
                DrawRadarOverlay(graphics, rect, radar);
                return true;
            case DesignV2InputsBody inputs:
                DrawInputsOverlay(graphics, rect, model, inputs);
                return true;
            case DesignV2TrackMapBody trackMap:
                DrawTrackMapOverlay(graphics, rect, trackMap);
                return true;
            case DesignV2FlagsBody flags:
                DrawFlagsOverlay(graphics, rect, flags);
                return true;
            default:
                return false;
        }
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
        var closeButtonSpace = _closeButton is not null ? 34 : 0;
        DrawText(
            graphics,
            model.HeaderText ?? model.Status,
            statusFont,
            EvidenceColor(model.Evidence),
            new RectangleF(outer.Left + 230, header.Top + 10, Math.Max(1, outer.Width - 244 - closeButtonSpace), 18),
            ContentAlignment.MiddleRight);

        var footerReserve = model.ShowFooter ? FooterHeight : 8;
        var body = new RectangleF(
            outer.Left + PaddingSize,
            header.Bottom + BodyGap,
            outer.Width - PaddingSize * 2,
            Math.Max(1, outer.Height - HeaderHeight - footerReserve - BodyGap - 1));
        DrawBody(graphics, body, model.Body);

        if (model.ShowFooter)
        {
            using var footerFont = FontOf(9.5f);
            DrawText(graphics, model.Footer, footerFont, TextMuted, new RectangleF(outer.Left + 14, outer.Bottom - 24, outer.Width - 28, 14));
        }
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
                DrawGraph(graphics, rect, graph);
                break;
            case DesignV2ChatBody chat:
                DrawChat(graphics, rect, chat.Rows);
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

            var fill = row.Evidence == DesignV2Evidence.Unavailable
                ? SurfaceInset
                : TryParseHexColor(row.ClassColorHex, out var rowClassColor)
                    ? Blend(SurfaceRaised, rowClassColor, row.IsReference ? 10 : 12, 1)
                    : row.IsReference
                        ? Blend(SurfaceRaised, Cyan, 10, 1)
                        : SurfaceRaised;
            FillRounded(graphics, rowRect, 5, fill, Color.FromArgb(90, BorderMuted));

            x = rowRect.Left + 8;
            var rowTextColor = TableTextColor(row);
            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var column = table.Columns[columnIndex];
                var width = Math.Max(MinimumColumnWidth, column.Width) * fit;
                var value = columnIndex < row.Values.Count ? row.Values[columnIndex] : string.Empty;
                DrawText(
                    graphics,
                    value,
                    row.IsReference || columnIndex == 0 ? rowBoldFont : rowFont,
                    row.IsReference ? TextPrimary : rowTextColor,
                    new RectangleF(x, rowRect.Top + 7, width, 16),
                    column.Alignment);
                x += width + ColumnGap;
            }

            y += RowHeight + RowGap;
            drawnRows++;
        }
    }

    private static Color TableTextColor(DesignV2TableRow row)
    {
        if (row.Evidence == DesignV2Evidence.Unavailable)
        {
            return TextMuted;
        }

        return row.RelativeLapDelta switch
        {
            >= 2 => MultipleLapsAheadText,
            1 => OneLapAheadText,
            -1 => OneLapBehindText,
            <= -2 => MultipleLapsBehindText,
            _ => TextSecondary
        };
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

    private void DrawGraph(Graphics graphics, RectangleF rect, DesignV2GraphBody graph)
    {
        FillRounded(graphics, rect, 5, SurfaceInset, BorderMuted);
        var totalSeriesPoints = graph.Series.Sum(series => series.Points.Count);
        if (totalSeriesPoints < 2 && graph.Points.Count < 2)
        {
            using var waitingFont = FontOf(11, FontStyle.Bold);
            DrawText(graphics, "waiting for trend", waitingFont, TextMuted, RectangleF.Inflate(rect, -12, -10));
            return;
        }

        var frame = RectangleF.Inflate(rect, -12, -14);
        const float axisWidth = 58f;
        const float xAxisHeight = 17f;
        var plotHeight = Math.Max(40, frame.Height - xAxisHeight);
        var metricsTableWidth = FocusedGapMetricsTableWidth(frame);
        var metricsTableRect = metricsTableWidth > 0f
            ? new RectangleF(frame.Right - metricsTableWidth, frame.Top, metricsTableWidth, plotHeight)
            : RectangleF.Empty;
        var chartRight = metricsTableWidth > 0f
            ? metricsTableRect.Left - GapMetricsTableGap
            : frame.Right;
        var labelLane = new RectangleF(
            chartRight - GapEndpointLabelLaneWidth,
            frame.Top,
            GapEndpointLabelLaneWidth,
            plotHeight);
        var plot = new RectangleF(
            frame.Left + axisWidth,
            frame.Top,
            Math.Max(40, labelLane.Left - (frame.Left + axisWidth)),
            plotHeight);
        var axisBounds = new RectangleF(frame.Left, frame.Top, axisWidth - 8, plot.Height);
        var scale = graph.Scale ?? DesignV2GapScale.Leader(graph.MaxGapSeconds ?? 1d);
        DrawGapWeatherBands(graphics, graph, plot);
        DrawGapLapIntervalLines(graphics, graph, plot);
        DrawGapGridLines(graphics, graph, scale, plot, axisBounds);
        using var axisFont = FontOf(9.5f);
        var max = Math.Max(1d, scale.MaxGapSeconds);
        DrawText(graphics, FormatTrendWindow(TimeSpan.FromSeconds(graph.EndSeconds - graph.StartSeconds)), axisFont, TextMuted, new RectangleF(plot.Left, plot.Bottom + 3, 56, 14));
        DrawText(graphics, "now", axisFont, TextMuted, new RectangleF(plot.Right - 44, plot.Bottom + 3, 44, 14), ContentAlignment.MiddleRight);
        DrawGapLeaderChangeMarkers(graphics, graph, plot);

        if (graph.Series.Count > 0)
        {
            var endpointLabels = new List<DesignV2GapEndpointLabel>();
            foreach (var (series, index) in graph.Series
                .Select((series, index) => (series, index))
                .OrderBy(item => GapDrawPriority(item.series, graph.ThreatCarIdx)))
            {
                if (scale.IsFocusRelative && series.IsClassLeader)
                {
                    continue;
                }

                if (DrawGapSeries(graphics, plot, graph, series, index, max) is { } label)
                {
                    endpointLabels.Add(label);
                }
            }

            if (graph.ActiveThreat is not null)
            {
                DrawGapThreatAnnotation(graphics, graph.ActiveThreat, plot);
            }

            DrawGapEndpointLabels(graphics, endpointLabels, plot, labelLane);
            DrawGapDriverChangeMarkers(graphics, graph, plot, max);
            DrawGapScaleLabels(graphics, scale, plot, axisBounds);
            if (metricsTableWidth > 0f)
            {
                DrawGapFocusedMetricsTable(graphics, metricsTableRect, graph);
            }

            return;
        }

        using var linePen = new Pen(Cyan, 2f);
        using var path = new GraphicsPath();
        for (var index = 0; index < graph.Points.Count; index++)
        {
            var progress = index / (float)Math.Max(1, graph.Points.Count - 1);
            var normalized = (float)Math.Clamp(graph.Points[index] / max, 0d, 1d);
            var point = new PointF(plot.Left + progress * plot.Width, plot.Top + normalized * plot.Height);
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

        DrawGapScaleLabels(graphics, scale, plot, axisBounds);
        graphics.DrawPath(linePen, path);
    }

    private static float FocusedGapMetricsTableWidth(RectangleF frame)
    {
        var availableAfterTable = frame.Width
            - 58f
            - GapEndpointLabelLaneWidth
            - GapMetricsTableGap
            - GapMetricsTableWidth;
        return availableAfterTable >= GapMetricsMinimumPlotWidth ? GapMetricsTableWidth : 0f;
    }

    private void DrawGapThreatAnnotation(Graphics graphics, DesignV2GapTrendMetric metric, RectangleF plot)
    {
        if (metric.Chaser is not { } chaser)
        {
            return;
        }

        var text = $"THREAT {chaser.Label} {FormatGapChangeSeconds(chaser.GainSeconds)} {metric.Label}";
        using var font = FontOf(8.5f, FontStyle.Bold);
        var size = graphics.MeasureString(text, font);
        var x = Math.Min(Math.Max(plot.Left + 2f, plot.Left + plot.Width / 2f - size.Width / 2f), plot.Right - size.Width - 8f);
        var y = plot.Bottom - GapThreatBadgeHeight - 6f;
        var badge = new RectangleF(x - 4f, y - 1f, size.Width + 8f, GapThreatBadgeHeight);
        FillRounded(graphics, badge, 3f, Color.FromArgb(214, 18, 24, 28), Color.FromArgb(97, Error));
        DrawText(graphics, text, font, Error, new RectangleF(x, y, size.Width + 2f, GapThreatBadgeHeight - 1f));
    }

    private void DrawGapFocusedMetricsTable(Graphics graphics, RectangleF rect, DesignV2GraphBody graph)
    {
        FillRounded(graphics, rect, 3f, Color.FromArgb(188, 18, 24, 28), Color.FromArgb(38, TextPrimary));
        using var titleFont = FontOf(10f, FontStyle.Bold);
        using var headerFont = FontOf(8f);
        using var rowFont = FontOf(9f);
        DrawText(graphics, "TREND", titleFont, TextPrimary, new RectangleF(rect.Left + 8f, rect.Top + 4f, rect.Width - 16f, 14f));
        DrawText(graphics, "win", headerFont, TextMuted, new RectangleF(rect.Left + 8f, rect.Top + 20f, 32f, 12f));
        DrawText(graphics, "leader d", headerFont, TextMuted, new RectangleF(rect.Left + 43f, rect.Top + 20f, 58f, 12f));
        DrawText(graphics, "threat", headerFont, TextMuted, new RectangleF(rect.Left + 104f, rect.Top + 20f, rect.Width - 110f, 12f));

        for (var index = 0; index < graph.TrendMetrics.Count; index++)
        {
            var metric = graph.TrendMetrics[index];
            var y = rect.Top + 38f + index * 22f;
            DrawText(graphics, metric.Label, rowFont, TextSecondary, new RectangleF(rect.Left + 8f, y, 32f, 14f));
            DrawText(
                graphics,
                GapMetricValueText(metric),
                rowFont,
                GapMetricValueColor(metric, graph.MetricDeadbandSeconds),
                new RectangleF(rect.Left + 43f, y, 58f, 14f));
            DrawText(
                graphics,
                GapMetricChaserText(metric),
                rowFont,
                GapMetricChaserColor(metric),
                new RectangleF(rect.Left + 104f, y, rect.Width - 110f, 14f));
        }
    }

    private static string GapMetricValueText(DesignV2GapTrendMetric metric)
    {
        return metric.State switch
        {
            "ready" when metric.FocusGapChangeSeconds is { } value => FormatGapChangeSeconds(value),
            "warming" => string.IsNullOrWhiteSpace(metric.StateLabel) ? "--" : metric.StateLabel,
            "leaderChanged" => "leader",
            _ => "--"
        };
    }

    private static Color GapMetricValueColor(DesignV2GapTrendMetric metric, double deadbandSeconds)
    {
        if (!string.Equals(metric.State, "ready", StringComparison.Ordinal) || metric.FocusGapChangeSeconds is not { } value)
        {
            return string.Equals(metric.State, "warming", StringComparison.Ordinal)
                || string.Equals(metric.State, "leaderChanged", StringComparison.Ordinal)
                ? TextMuted
                : Color.FromArgb(184, TextMuted);
        }

        if (Math.Abs(value) < deadbandSeconds)
        {
            return TextSecondary;
        }

        return value > 0d ? Error : Green;
    }

    private static string GapMetricChaserText(DesignV2GapTrendMetric metric)
    {
        return metric.State switch
        {
            "ready" when metric.Chaser is { } chaser => $"{chaser.Label} {FormatGapChangeSeconds(chaser.GainSeconds)}",
            "leaderChanged" => "reset",
            _ => "--"
        };
    }

    private static Color GapMetricChaserColor(DesignV2GapTrendMetric metric)
    {
        return string.Equals(metric.State, "ready", StringComparison.Ordinal) && metric.Chaser is not null
            ? Error
            : Color.FromArgb(184, TextMuted);
    }

    private void DrawGapEndpointLabels(
        Graphics graphics,
        IReadOnlyList<DesignV2GapEndpointLabel> labels,
        RectangleF plot,
        RectangleF labelLane)
    {
        if (labels.Count == 0)
        {
            return;
        }

        var pinnedLabels = labels.Where(label => ShouldPinGapEndpointLabel(label, plot)).ToArray();
        var floatingLabels = labels.Where(label => !ShouldPinGapEndpointLabel(label, plot)).ToArray();
        foreach (var label in floatingLabels.OrderBy(GapEndpointLabelDrawPriority).ThenBy(label => label.Point.Y))
        {
            var y = ClampGapEndpointLabelY(label.Point.Y - GapEndpointLabelHeight / 2f, plot);
            DrawGapEndpointLabel(graphics, label, y, plot, plot, false);
        }

        DrawPinnedGapEndpointLabels(graphics, pinnedLabels, plot, labelLane);
    }

    private void DrawPinnedGapEndpointLabels(
        Graphics graphics,
        IReadOnlyList<DesignV2GapEndpointLabel> labels,
        RectangleF plot,
        RectangleF labelLane)
    {
        if (labels.Count == 0)
        {
            return;
        }

        var positioned = labels
            .OrderBy(label => label.Point.Y)
            .ThenBy(GapEndpointLabelDrawPriority)
            .Select(label => new DesignV2PositionedGapEndpointLabel(
                label,
                ClampGapEndpointLabelY(label.Point.Y - GapEndpointLabelHeight / 2f, labelLane)))
            .ToArray();

        for (var index = 1; index < positioned.Length; index++)
        {
            var minimumY = positioned[index - 1].Y + GapEndpointLabelHeight + GapEndpointLabelGap;
            if (positioned[index].Y < minimumY)
            {
                positioned[index] = positioned[index] with { Y = minimumY };
            }
        }

        var maxY = labelLane.Bottom - GapEndpointLabelHeight - 1f;
        if (positioned.Length > 0 && positioned[^1].Y > maxY)
        {
            var shift = positioned[^1].Y - maxY;
            for (var index = 0; index < positioned.Length; index++)
            {
                positioned[index] = positioned[index] with
                {
                    Y = Math.Max(labelLane.Top + 1f, positioned[index].Y - shift)
                };
            }
        }

        foreach (var item in positioned.OrderBy(item => GapEndpointLabelDrawPriority(item.Label)))
        {
            DrawGapEndpointLabel(graphics, item.Label, item.Y, plot, labelLane, true);
        }
    }

    private void DrawGapEndpointLabel(
        Graphics graphics,
        DesignV2GapEndpointLabel label,
        float y,
        RectangleF plot,
        RectangleF labelBounds,
        bool pinnedToLane)
    {
        using var font = FontOf(label.IsReference ? 10f : 9f);
        var size = graphics.MeasureString(label.Text, font);
        var x = pinnedToLane
            ? Math.Min(labelBounds.Right - size.Width - 1f, Math.Max(labelBounds.Left + 4f, label.Point.X + 8f))
            : Math.Min(labelBounds.Right - size.Width - 2f, label.Point.X + 6f);
        var background = new RectangleF(x - 2f, y, size.Width + 4f, GapEndpointLabelHeight);
        if (pinnedToLane || Math.Abs(y + GapEndpointLabelHeight / 2f - label.Point.Y) > 3f)
        {
            using var connector = new Pen(Color.FromArgb(82, label.Color), 1f);
            graphics.DrawLine(connector, label.Point.X + 3f, label.Point.Y, background.Left, y + GapEndpointLabelHeight / 2f);
        }

        using var backgroundBrush = new SolidBrush(Color.FromArgb(label.IsReference ? 188 : 150, 18, 30, 42));
        graphics.FillRectangle(backgroundBrush, background);
        DrawText(
            graphics,
            label.Text,
            font,
            WithAlpha(label.Color, label.IsReference ? 1d : 0.78d),
            new RectangleF(x, y - 1f, size.Width + 2f, GapEndpointLabelHeight + 2f));
    }

    private static bool ShouldPinGapEndpointLabel(DesignV2GapEndpointLabel label, RectangleF plot)
    {
        return label.Point.X >= plot.Right - GapEndpointLabelPinThreshold;
    }

    private static float ClampGapEndpointLabelY(float y, RectangleF bounds)
    {
        return Math.Min(Math.Max(y, bounds.Top + 1f), bounds.Bottom - GapEndpointLabelHeight - 1f);
    }

    private static int GapEndpointLabelDrawPriority(DesignV2GapEndpointLabel label)
    {
        if (label.IsReference)
        {
            return 2;
        }

        return label.IsClassLeader ? 1 : 0;
    }

    private DesignV2GapEndpointLabel? DrawGapSeries(
        Graphics graphics,
        RectangleF plot,
        DesignV2GraphBody graph,
        DesignV2GapSeries series,
        int seriesIndex,
        double maxGapSeconds)
    {
        var color = WithAlpha(
            GapSeriesColor(series, seriesIndex, graph.ThreatCarIdx),
            series.Alpha * GapSeriesAlphaMultiplier(series, graph.ThreatCarIdx));
        using var pen = new Pen(color, series.IsReference ? 2.6f : series.IsClassLeader ? 1.8f : 1.25f)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        if (series.IsStale || series.IsStickyExit)
        {
            pen.DashStyle = DashStyle.Dash;
        }

        using var path = new GraphicsPath();
        var hasPoint = false;
        foreach (var point in series.Points.OrderBy(point => point.AxisSeconds))
        {
            var graphPoint = GapGraphPoint(point, graph, plot, maxGapSeconds);
            if (!hasPoint || point.StartsSegment)
            {
                path.StartFigure();
                path.AddLine(graphPoint, graphPoint);
                hasPoint = true;
                continue;
            }

            path.AddLine(path.GetLastPoint(), graphPoint);
        }

        graphics.DrawPath(pen, path);
        var latest = series.Points.OrderBy(point => point.AxisSeconds).LastOrDefault();
        if (latest is null)
        {
            return null;
        }

        var latestPoint = GapGraphPoint(latest, graph, plot, maxGapSeconds);
        var label = series.IsClassLeader
            ? "P1"
            : series.ClassPosition is { } position
                ? $"P{position}"
                : $"#{series.CarIdx}";
        if (series.IsStale)
        {
            using var markerPen = new Pen(color, 1.2f);
            graphics.DrawLine(markerPen, latestPoint.X - 4f, latestPoint.Y - 4f, latestPoint.X + 4f, latestPoint.Y + 4f);
            graphics.DrawLine(markerPen, latestPoint.X - 4f, latestPoint.Y + 4f, latestPoint.X + 4f, latestPoint.Y - 4f);
        }

        return new DesignV2GapEndpointLabel(label, latestPoint, color, series.IsReference, series.IsClassLeader);
    }

    private void DrawGapGridLines(
        Graphics graphics,
        DesignV2GraphBody graph,
        DesignV2GapScale scale,
        RectangleF plot,
        RectangleF axisBounds)
    {
        using var axisPen = new Pen(Color.FromArgb(70, TextMuted), 1);
        graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Right, plot.Top);
        graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

        if (scale.IsFocusRelative)
        {
            DrawGapFocusGridLines(graphics, scale, plot, axisBounds);
            return;
        }

        using var gridPen = new Pen(Color.FromArgb(46, TextMuted), 1);
        using var gridFont = FontOf(7.5f);
        var step = NiceGridStep(scale.MaxGapSeconds / 4d);
        for (var value = step; value < scale.MaxGapSeconds; value += step)
        {
            var y = GapToY(value, scale.MaxGapSeconds, plot);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            DrawText(
                graphics,
                FormatAxisSeconds(value),
                gridFont,
                TextMuted,
                new RectangleF(axisBounds.Left, y - 8f, axisBounds.Width, 16f),
                ContentAlignment.MiddleRight);
        }

        if (graph.LapReferenceSeconds is not { } lapSeconds || lapSeconds < 20d || scale.MaxGapSeconds < lapSeconds * 0.85d)
        {
            return;
        }

        using var lapPen = new Pen(Color.FromArgb(130, TextPrimary), 1.2f);
        for (var lap = 1; lap * lapSeconds < scale.MaxGapSeconds; lap++)
        {
            var y = GapToY(lap * lapSeconds, scale.MaxGapSeconds, plot);
            graphics.DrawLine(lapPen, plot.Left, y, plot.Right, y);
            DrawText(
                graphics,
                $"+{lap} lap",
                gridFont,
                TextPrimary,
                new RectangleF(axisBounds.Left, y - 8f, axisBounds.Width, 16f),
                ContentAlignment.MiddleRight);
        }
    }

    private void DrawGapFocusGridLines(Graphics graphics, DesignV2GapScale scale, RectangleF plot, RectangleF axisBounds)
    {
        using var gridPen = new Pen(Color.FromArgb(46, TextMuted), 1);
        using var referencePen = new Pen(Color.FromArgb(110, Green), 1.2f);
        using var gridFont = FontOf(7.5f);
        var referenceY = GapFocusReferenceY(plot);
        graphics.DrawLine(referencePen, plot.Left, referenceY, plot.Right, referenceY);
        DrawText(
            graphics,
            "focus",
            gridFont,
            Green,
            new RectangleF(axisBounds.Left, referenceY - 8f, axisBounds.Width, 16f),
            ContentAlignment.MiddleRight);

        var aheadStep = NiceGridStep(scale.AheadSeconds / 2d);
        for (var value = aheadStep; value < scale.AheadSeconds; value += aheadStep)
        {
            var y = GapDeltaToY(-value, scale, plot);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            DrawText(
                graphics,
                FormatDeltaSeconds(-value),
                gridFont,
                TextMuted,
                new RectangleF(axisBounds.Left, y - 8f, axisBounds.Width, 16f),
                ContentAlignment.MiddleRight);
        }

        var behindStep = NiceGridStep(scale.BehindSeconds / 2d);
        for (var value = behindStep; value < scale.BehindSeconds; value += behindStep)
        {
            var y = GapDeltaToY(value, scale, plot);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            DrawText(
                graphics,
                FormatDeltaSeconds(value),
                gridFont,
                TextMuted,
                new RectangleF(axisBounds.Left, y - 8f, axisBounds.Width, 16f),
                ContentAlignment.MiddleRight);
        }
    }

    private void DrawGapLapIntervalLines(Graphics graphics, DesignV2GraphBody graph, RectangleF plot)
    {
        if (graph.LapReferenceSeconds is not { } lapSeconds || lapSeconds < 20d)
        {
            return;
        }

        var intervalSeconds = lapSeconds * 5d;
        var durationSeconds = graph.EndSeconds - graph.StartSeconds;
        if (durationSeconds < intervalSeconds * 0.75d)
        {
            return;
        }

        using var linePen = new Pen(Color.FromArgb(34, TextPrimary), 1f);
        using var labelFont = FontOf(7f);
        for (var elapsed = intervalSeconds; elapsed < durationSeconds; elapsed += intervalSeconds)
        {
            var x = plot.Left + (float)(elapsed / durationSeconds * plot.Width);
            graphics.DrawLine(linePen, x, plot.Top, x, plot.Bottom);
            DrawText(
                graphics,
                $"{elapsed / lapSeconds:0}L",
                labelFont,
                TextMuted,
                new RectangleF(x - 18f, plot.Bottom + 1f, 36f, 12f),
                ContentAlignment.MiddleCenter);
        }
    }

    private void DrawGapScaleLabels(Graphics graphics, DesignV2GapScale scale, RectangleF plot, RectangleF axisBounds)
    {
        using var font = FontOf(9.5f);
        DrawText(graphics, scale.IsFocusRelative ? "local" : "leader", font, TextMuted, new RectangleF(axisBounds.Left, plot.Top - 7, axisBounds.Width, 14), ContentAlignment.MiddleRight);
        if (scale.IsFocusRelative)
        {
            DrawText(graphics, FormatDeltaSeconds(-scale.AheadSeconds), font, TextMuted, new RectangleF(axisBounds.Left, plot.Top + GapFocusScaleTopPadding - 8f, axisBounds.Width, 16f), ContentAlignment.MiddleRight);
            DrawText(graphics, FormatDeltaSeconds(scale.BehindSeconds), font, TextMuted, new RectangleF(axisBounds.Left, plot.Bottom - GapFocusScaleBottomPadding - 8f, axisBounds.Width, 16f), ContentAlignment.MiddleRight);
            return;
        }

        DrawText(graphics, FormatAxisSeconds(scale.MaxGapSeconds), font, TextMuted, new RectangleF(axisBounds.Left, plot.Bottom - 7, axisBounds.Width, 14), ContentAlignment.MiddleRight);
    }

    private void DrawGapLeaderChangeMarkers(Graphics graphics, DesignV2GraphBody graph, RectangleF plot)
    {
        if (graph.LeaderChanges.Count == 0)
        {
            return;
        }

        var domain = Math.Max(1d, graph.EndSeconds - graph.StartSeconds);
        using var pen = new Pen(Color.FromArgb(115, TextPrimary), 1f)
        {
            DashStyle = DashStyle.Dot
        };
        using var font = FontOf(7.5f, FontStyle.Bold);
        foreach (var marker in graph.LeaderChanges)
        {
            var x = plot.Left + (float)Math.Clamp((marker.AxisSeconds - graph.StartSeconds) / domain, 0d, 1d) * plot.Width;
            graphics.DrawLine(pen, x, plot.Top, x, plot.Bottom);
            DrawText(graphics, "leader", font, TextMuted, new RectangleF(x + 4f, plot.Top + 4f, 42f, 12f));
        }
    }

    private void DrawGapDriverChangeMarkers(Graphics graphics, DesignV2GraphBody graph, RectangleF plot, double maxGapSeconds)
    {
        if (graph.DriverChanges.Count == 0)
        {
            return;
        }

        using var font = FontOf(7.5f, FontStyle.Bold);
        foreach (var marker in graph.DriverChanges)
        {
            var point = GapGraphPoint(marker.AxisSeconds, marker.GapSeconds, graph, plot, maxGapSeconds);
            var color = marker.IsReference ? Green : TextSecondary;
            using var linePen = new Pen(Color.FromArgb(170, color), 1.2f);
            using var fill = new SolidBrush(Surface);
            graphics.DrawLine(linePen, point.X, point.Y - 8f, point.X, point.Y + 8f);
            graphics.FillEllipse(fill, point.X - 4f, point.Y - 4f, 8f, 8f);
            graphics.DrawEllipse(linePen, point.X - 4f, point.Y - 4f, 8f, 8f);
            DrawText(graphics, marker.Label, font, color, new RectangleF(point.X + 6f, point.Y - 16f, 28f, 12f));
        }
    }

    internal static PointF GapGraphPoint(
        DesignV2GapTrendPoint point,
        DesignV2GraphBody graph,
        RectangleF plot,
        double maxGapSeconds)
    {
        return GapGraphPoint(point.AxisSeconds, point.GapSeconds, graph, plot, maxGapSeconds);
    }

    private static PointF GapGraphPoint(
        double axisSeconds,
        double gapSeconds,
        DesignV2GraphBody graph,
        RectangleF plot,
        double maxGapSeconds)
    {
        var domain = Math.Max(1d, graph.EndSeconds - graph.StartSeconds);
        var x = plot.Left + (float)Math.Clamp((axisSeconds - graph.StartSeconds) / domain, 0d, 1d) * plot.Width;
        var scale = graph.Scale ?? DesignV2GapScale.Leader(maxGapSeconds);
        var y = scale.IsFocusRelative
            ? GapDeltaToY(gapSeconds - GapReferenceAt(scale.ReferencePoints, axisSeconds), scale, plot)
            : GapToY(gapSeconds, Math.Max(1d, maxGapSeconds), plot);
        return new PointF(x, y);
    }

    private static float GapToY(double gapSeconds, double maxGapSeconds, RectangleF plot)
    {
        return plot.Top + (float)(Math.Clamp(gapSeconds / Math.Max(1d, maxGapSeconds), 0d, 1d) * plot.Height);
    }

    private static float GapDeltaToY(double deltaSeconds, DesignV2GapScale scale, RectangleF plot)
    {
        var referenceY = GapFocusReferenceY(plot);
        if (deltaSeconds < 0d)
        {
            var ratio = Math.Clamp(Math.Abs(deltaSeconds) / Math.Max(1d, scale.AheadSeconds), 0d, 1d);
            return referenceY - (float)(ratio * Math.Max(1f, referenceY - (plot.Top + GapFocusScaleTopPadding)));
        }

        var behindRatio = Math.Clamp(deltaSeconds / Math.Max(1d, scale.BehindSeconds), 0d, 1d);
        return referenceY + (float)(behindRatio * Math.Max(1f, plot.Bottom - GapFocusScaleBottomPadding - referenceY));
    }

    private static float GapFocusReferenceY(RectangleF plot)
    {
        return plot.Top + plot.Height * GapFocusScaleReferenceRatio;
    }

    private static int GapDrawPriority(DesignV2GapSeries series, int? threatCarIdx)
    {
        if (series.IsReference)
        {
            return 3;
        }

        if (series.IsClassLeader)
        {
            return 2;
        }

        return series.CarIdx == threatCarIdx ? 1 : 0;
    }

    private static Color GapSeriesColor(DesignV2GapSeries series, int index, int? threatCarIdx)
    {
        if (series.CarIdx == threatCarIdx)
        {
            return Error;
        }

        if (series.IsReference)
        {
            return Cyan;
        }

        if (series.IsClassLeader)
        {
            return TextPrimary;
        }

        return (index % 3) switch
        {
            0 => Amber,
            1 => Green,
            _ => Magenta
        };
    }

    private static double GapSeriesAlphaMultiplier(DesignV2GapSeries series, int? threatCarIdx)
    {
        return series.IsClassLeader || series.IsReference || series.CarIdx == threatCarIdx
            ? 1d
            : 0.48d;
    }

    private static void DrawGapWeatherBands(Graphics graphics, DesignV2GraphBody graph, RectangleF plot)
    {
        if (graph.Weather.Count == 0)
        {
            return;
        }

        var domain = Math.Max(1d, graph.EndSeconds - graph.StartSeconds);
        foreach (var (point, index) in graph.Weather.Select((point, index) => (point, index)))
        {
            if (GapWeatherColor(point.Condition) is not { } color)
            {
                continue;
            }

            var nextAxis = index + 1 < graph.Weather.Count
                ? graph.Weather[index + 1].AxisSeconds
                : graph.EndSeconds;
            var x = plot.Left + (float)Math.Clamp((point.AxisSeconds - graph.StartSeconds) / domain, 0d, 1d) * plot.Width;
            var nextX = plot.Left + (float)Math.Clamp((nextAxis - graph.StartSeconds) / domain, 0d, 1d) * plot.Width;
            if (nextX <= x)
            {
                continue;
            }

            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, x, plot.Top, nextX - x, plot.Height);
        }
    }

    private static Color? GapWeatherColor(DesignV2GapWeatherCondition condition)
    {
        return condition switch
        {
            DesignV2GapWeatherCondition.Damp => Color.FromArgb(16, Cyan),
            DesignV2GapWeatherCondition.Wet => Color.FromArgb(24, 70, 135, 230),
            DesignV2GapWeatherCondition.DeclaredWet => Color.FromArgb(32, 78, 142, 238),
            _ => null
        };
    }

    private void DrawChat(Graphics graphics, RectangleF rect, IReadOnlyList<DesignV2ChatRow> rows)
    {
        FillRounded(graphics, rect, 5, SurfaceInset, BorderMuted);
        using var authorFont = FontOf(9.8f, FontStyle.Bold);
        using var messageFont = FontOf(10.5f);
        var maximumRows = Math.Max(1, (int)(rect.Height / 48f));
        foreach (var (row, index) in rows
            .Skip(Math.Max(0, rows.Count - maximumRows))
            .Select((row, index) => (row, index)))
        {
            var rowRect = new RectangleF(rect.Left + 8, rect.Top + 8 + index * 48f, rect.Width - 16, 40);
            FillRounded(graphics, rowRect, 5, SurfaceRaised, Color.FromArgb(76, BorderMuted));
            DrawText(graphics, row.Author, authorFont, EvidenceColor(row.Evidence), new RectangleF(rowRect.Left + 10, rowRect.Top + 5, rowRect.Width - 20, 13));
            DrawText(graphics, row.Message, messageFont, TextSecondary, new RectangleF(rowRect.Left + 10, rowRect.Top + 20, rowRect.Width - 20, 15));
        }
    }

    private void DrawInputs(Graphics graphics, RectangleF rect, DesignV2InputsBody body)
    {
        DrawInputsOverlay(
            graphics,
            rect,
            new DesignV2OverlayModel("Inputs", body.IsAvailable ? "live" : "waiting", "source: local input telemetry", body.IsAvailable ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable, body),
            body);
    }

    private void DrawInputsOverlay(Graphics graphics, RectangleF rect, DesignV2OverlayModel model, DesignV2InputsBody body)
    {
        FillRounded(graphics, rect, 8, Surface, Color.FromArgb(235, Cyan));
        var header = new RectangleF(rect.Left, rect.Top, rect.Width, HeaderHeight);
        using (var titleBrush = new SolidBrush(TitleBar))
        {
            graphics.FillRectangle(titleBrush, header);
        }

        using (var accent = new SolidBrush(Magenta))
        {
            graphics.FillRectangle(accent, rect.Left, header.Bottom - 2, rect.Width, 2);
        }

        using var titleFont = FontOf(13f, FontStyle.Bold);
        DrawText(graphics, "Inputs", titleFont, TextPrimary, new RectangleF(rect.Left + 14, rect.Top + 10, 100, 16));

        var content = new RectangleF(rect.Left + 18, header.Bottom + 18, Math.Max(1, rect.Width - 36), Math.Max(1, rect.Height - HeaderHeight - 34));
        var railVisible = body.ShowThrottle || body.ShowBrake || body.ShowClutch || body.ShowSteering || body.ShowGear || body.ShowSpeed;
        var railWidth = railVisible ? Math.Min(204f, Math.Max(136f, content.Width * 0.40f)) : 0f;
        var graph = new RectangleF(
            content.Left,
            content.Top + 6,
            Math.Max(160, content.Width - railWidth - (railVisible ? 18 : 0)),
            Math.Max(40, content.Height - 12));
        FillRounded(graphics, graph, 5, SurfaceInset, BorderMuted);
        DrawInputGrid(graphics, graph);
        if (body.ShowThrottle)
        {
            DrawInputTrace(graphics, body.Trace, graph, Green, point => point.Throttle);
        }
        if (body.ShowBrake)
        {
            DrawInputTrace(graphics, body.Trace, graph, Error, point => point.Brake);
            DrawInputAbsTrace(graphics, body.Trace, graph);
        }
        if (body.ShowClutch)
        {
            DrawInputTrace(graphics, body.Trace, graph, Cyan, point => point.Clutch);
        }
        if (body.Trace.Count < 2 && !body.IsAvailable)
        {
            using var waitingFont = FontOf(11, FontStyle.Bold);
            DrawText(graphics, "waiting for inputs", waitingFont, TextMuted, graph, ContentAlignment.MiddleCenter);
        }

        if (railVisible)
        {
            var rail = new RectangleF(graph.Right + 18, content.Top, railWidth, content.Height);
            DrawInputRail(graphics, body, rail);
        }
    }

    private static void DrawInputGrid(Graphics graphics, RectangleF rect)
    {
        using var pen = new Pen(Color.FromArgb(46, TextMuted), 1);
        for (var step = 1; step < 4; step++)
        {
            var y = rect.Top + rect.Height * step / 4f;
            graphics.DrawLine(pen, rect.Left, y, rect.Right, y);
        }
    }

    private static void DrawInputTrace(
        Graphics graphics,
        IReadOnlyList<DesignV2InputPoint> trace,
        RectangleF rect,
        Color color,
        Func<DesignV2InputPoint, double> select)
    {
        if (trace.Count < 2)
        {
            using var waitingPen = new Pen(Color.FromArgb(60, color), 2);
            graphics.DrawLine(waitingPen, rect.Left + 8, rect.Top + rect.Height / 2f, rect.Right - 8, rect.Top + rect.Height / 2f);
            return;
        }

        using var pen = new Pen(color, 2)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        var points = trace.Select((point, index) => new PointF(
            rect.Left + index / (float)Math.Max(1, trace.Count - 1) * rect.Width,
            rect.Bottom - (float)Math.Clamp(select(point), 0d, 1d) * rect.Height)).ToArray();
        if (points.Length > 1)
        {
            using var path = SmoothInputTracePath(points);
            graphics.DrawPath(pen, path);
        }
    }

    private static GraphicsPath SmoothInputTracePath(IReadOnlyList<PointF> points)
    {
        var path = new GraphicsPath();
        path.StartFigure();
        for (var index = 0; index < points.Count - 1; index++)
        {
            var p0 = index == 0 ? points[index] : points[index - 1];
            var p1 = points[index];
            var p2 = points[index + 1];
            var p3 = index + 2 < points.Count ? points[index + 2] : p2;
            var control1 = new PointF(
                p1.X + (p2.X - p0.X) / 6f,
                ClampSmoothInputControlY(p1.Y + (p2.Y - p0.Y) / 6f, p1.Y, p2.Y));
            var control2 = new PointF(
                p2.X - (p3.X - p1.X) / 6f,
                ClampSmoothInputControlY(p2.Y - (p3.Y - p1.Y) / 6f, p1.Y, p2.Y));
            path.AddBezier(p1, control1, control2, p2);
        }

        return path;
    }

    private static float ClampSmoothInputControlY(float value, float segmentStartY, float segmentEndY)
    {
        var min = Math.Min(segmentStartY, segmentEndY);
        var max = Math.Max(segmentStartY, segmentEndY);
        return Math.Clamp(value, min, max);
    }

    private static void DrawInputAbsTrace(Graphics graphics, IReadOnlyList<DesignV2InputPoint> trace, RectangleF rect)
    {
        if (trace.Count < 2)
        {
            return;
        }

        using var pen = new Pen(Amber, 3)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        for (var index = 1; index < trace.Count; index++)
        {
            if (!trace[index].BrakeAbsActive)
            {
                continue;
            }

            var previous = trace[index - 1];
            var current = trace[index];
            var x1 = rect.Left + (index - 1) / (float)Math.Max(1, trace.Count - 1) * rect.Width;
            var x2 = rect.Left + index / (float)Math.Max(1, trace.Count - 1) * rect.Width;
            var y1 = rect.Bottom - (float)Math.Clamp(previous.Brake, 0d, 1d) * rect.Height;
            var y2 = rect.Bottom - (float)Math.Clamp(current.Brake, 0d, 1d) * rect.Height;
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }
    }

    private void DrawInputRail(Graphics graphics, DesignV2InputsBody body, RectangleF rect)
    {
        var layout = BuildInputRailLayout(
            rect,
            body.ShowThrottle,
            body.ShowBrake,
            body.ShowClutch,
            body.ShowSteering,
            body.ShowGear,
            body.ShowSpeed);
        foreach (var item in layout.Items)
        {
            switch (item.Kind)
            {
                case DesignV2InputRailItemKind.Throttle:
                    DrawInputBar(graphics, "THR", body.Throttle, Green, item.Bounds);
                    break;
                case DesignV2InputRailItemKind.Brake:
                    DrawInputBar(graphics, body.BrakeAbsActive ? "ABS" : "BRK", body.Brake, body.BrakeAbsActive ? Amber : Error, item.Bounds);
                    break;
                case DesignV2InputRailItemKind.Clutch:
                    DrawInputBar(graphics, "CLT", body.Clutch, Cyan, item.Bounds);
                    break;
                case DesignV2InputRailItemKind.SteeringWheel:
                    DrawInputWheel(graphics, body.SteeringWheelAngle, item.Bounds);
                    break;
                case DesignV2InputRailItemKind.Gear:
                    DrawInputReadout(graphics, "GEAR", FormatGear(body.Gear), item.Bounds);
                    break;
                case DesignV2InputRailItemKind.Speed:
                    DrawInputReadout(graphics, "SPD", SimpleTelemetryOverlayViewModel.FormatSpeed(body.SpeedMetersPerSecond, _unitSystem), item.Bounds);
                    break;
            }
        }
    }

    internal static DesignV2InputRailLayout BuildInputRailLayout(
        RectangleF rect,
        bool showThrottle,
        bool showBrake,
        bool showClutch,
        bool showSteering,
        bool showGear,
        bool showSpeed)
    {
        var items = new List<DesignV2InputRailItem>();
        var compact = rect.Height < 190f;
        var barHeight = compact ? 24f : 27f;
        var barGap = compact ? 7f : 11f;
        var readoutHeight = 20f;
        var readoutGap = compact ? 4f : 8f;
        var readoutKinds = new List<DesignV2InputRailItemKind>();
        if (showGear)
        {
            readoutKinds.Add(DesignV2InputRailItemKind.Gear);
        }

        if (showSpeed)
        {
            readoutKinds.Add(DesignV2InputRailItemKind.Speed);
        }

        var readoutReserve = readoutKinds.Count == 0
            ? 0f
            : readoutKinds.Count * readoutHeight + Math.Max(0, readoutKinds.Count - 1) * readoutGap;
        var y = rect.Top;
        AddBar(showThrottle, DesignV2InputRailItemKind.Throttle);
        AddBar(showBrake, DesignV2InputRailItemKind.Brake);
        AddBar(showClutch, DesignV2InputRailItemKind.Clutch);

        if (showSteering)
        {
            var readoutGapReserve = readoutReserve > 0f ? 8f : 0f;
            var wheelBottom = rect.Bottom - readoutReserve - readoutGapReserve;
            var wheelAvailable = wheelBottom - y;
            if (wheelAvailable >= 24f)
            {
                var wheelHeight = Math.Min(compact ? 58f : 78f, Math.Max(28f, wheelAvailable));
                items.Add(new DesignV2InputRailItem(
                    DesignV2InputRailItemKind.SteeringWheel,
                    new RectangleF(rect.Left, y, rect.Width, wheelHeight)));
            }
        }

        if (readoutKinds.Count > 0)
        {
            var readoutTop = Math.Max(rect.Top, rect.Bottom - readoutReserve);
            for (var index = 0; index < readoutKinds.Count; index++)
            {
                items.Add(new DesignV2InputRailItem(
                    readoutKinds[index],
                    new RectangleF(
                        rect.Left,
                        readoutTop + index * (readoutHeight + readoutGap),
                        rect.Width,
                        readoutHeight)));
            }
        }

        return new DesignV2InputRailLayout(items);

        void AddBar(bool enabled, DesignV2InputRailItemKind kind)
        {
            if (!enabled)
            {
                return;
            }

            items.Add(new DesignV2InputRailItem(kind, new RectangleF(rect.Left, y, rect.Width, barHeight)));
            y += barHeight + barGap;
        }
    }

    private void DrawInputBar(Graphics graphics, string label, double? value, Color color, RectangleF rect)
    {
        using var font = FontOf(9.5f, FontStyle.Bold);
        using var valueFont = FontOf(8.5f, FontStyle.Bold);
        DrawText(graphics, label, font, TextMuted, new RectangleF(rect.Left, rect.Top + 3, 42, 14));
        var bar = new RectangleF(rect.Left + 48, rect.Top + 5, Math.Max(8, rect.Width - 48), 12);
        FillRounded(graphics, bar, 6, SurfaceRaised, null);
        var fill = new RectangleF(bar.Left, bar.Top, bar.Width * (float)Math.Clamp(value ?? 0d, 0d, 1d), bar.Height);
        FillRounded(graphics, fill, 6, color, null);
        DrawText(graphics, FormatPercent(value), valueFont, TextMuted, new RectangleF(bar.Left, bar.Bottom + 1, bar.Width, 11), ContentAlignment.MiddleRight);
    }

    private void DrawInputReadout(Graphics graphics, string label, string value, RectangleF rect)
    {
        using var labelFont = FontOf(8.5f, FontStyle.Bold);
        using var valueFont = FontOf(12.5f, FontStyle.Bold);
        DrawText(graphics, label, labelFont, TextMuted, new RectangleF(rect.Left, rect.Top + 3, 42, 14));
        DrawText(graphics, value, valueFont, TextPrimary, new RectangleF(rect.Left + 50, rect.Top, rect.Width - 50, 18), ContentAlignment.MiddleRight);
    }

    private void DrawInputWheel(Graphics graphics, double? angleRadians, RectangleF rect)
    {
        using var labelFont = FontOf(8.5f, FontStyle.Bold);
        using var valueFont = FontOf(10.5f, FontStyle.Bold);
        DrawText(graphics, "WHEEL", labelFont, TextMuted, new RectangleF(rect.Left, rect.Top, 54, 14));
        DrawText(graphics, FormatSteering(angleRadians), valueFont, TextPrimary, new RectangleF(rect.Left + 58, rect.Top - 1, rect.Width - 58, 16), ContentAlignment.MiddleRight);

        var wheelSlot = new RectangleF(
            rect.Left + 2,
            rect.Top + 20,
            Math.Max(1, rect.Width - 4),
            Math.Max(1, rect.Height - 22));
        var diameter = Math.Max(8, Math.Min(wheelSlot.Width, wheelSlot.Height) - 4);
        var wheel = new RectangleF(
            wheelSlot.Left + (wheelSlot.Width - diameter) / 2f,
            wheelSlot.Top + (wheelSlot.Height - diameter) / 2f,
            diameter,
            diameter);
        using var ringPen = new Pen(TextSecondary, 3.4f);
        graphics.DrawEllipse(ringPen, wheel);
        var center = new PointF(wheel.Left + wheel.Width / 2f, wheel.Top + wheel.Height / 2f);
        var angle = (float)(angleRadians ?? 0d);
        using var spokePen = new Pen(Cyan, 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        foreach (var spokeAngle in new[] { -Math.PI / 2d, Math.PI / 6d, Math.PI * 5d / 6d })
        {
            var combined = spokeAngle + angle;
            var end = new PointF(
                center.X + (float)Math.Cos(combined) * wheel.Width * 0.34f,
                center.Y + (float)Math.Sin(combined) * wheel.Height * 0.34f);
            graphics.DrawLine(spokePen, center, end);
        }
    }

    private void DrawRadar(Graphics graphics, RectangleF rect, DesignV2RadarBody radar)
    {
        DrawRadarOverlay(graphics, rect, radar);
    }

    private void DrawRadarOverlay(Graphics graphics, RectangleF rect, DesignV2RadarBody radar)
    {
        if (!radar.IsAvailable && !radar.PreviewVisible)
        {
            return;
        }

        var diameter = Math.Max(20, Math.Min(rect.Width, rect.Height) - 8);
        var radarRect = new RectangleF(
            rect.Left + (rect.Width - diameter) / 2f,
            rect.Top + (rect.Height - diameter) / 2f,
            diameter,
            diameter);
        using (var fill = new SolidBrush(Color.FromArgb(235, Surface)))
        {
            graphics.FillEllipse(fill, radarRect);
        }
        using (var borderPen = new Pen(Cyan, 2f))
        {
            graphics.DrawEllipse(borderPen, radarRect);
        }

        using var titleFont = FontOf(12f, FontStyle.Bold);
        using var statusFont = FontOf(10f, FontStyle.Bold);
        DrawText(graphics, "CAR RADAR", titleFont, TextPrimary, new RectangleF(radarRect.Left + 20, radarRect.Top + 18, 116, 16));
        DrawText(graphics, RadarStatusText(radar), statusFont, radar.IsAvailable ? Error : TextMuted, new RectangleF(radarRect.Right - 104, radarRect.Top + 18, 84, 16), ContentAlignment.MiddleRight);

        var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
        using var ringPen = new Pen(Color.FromArgb(62, TextMuted), 1f);
        foreach (var fraction in new[] { 1f / 3f, 2f / 3f })
        {
            var inset = radarRect.Width * fraction / 2f;
            graphics.DrawEllipse(ringPen, RectangleF.Inflate(radarRect, -inset, -inset));
        }
        graphics.DrawLine(ringPen, radarRect.Left, center.Y, radarRect.Right, center.Y);
        graphics.DrawLine(ringPen, center.X, radarRect.Top, center.X, radarRect.Bottom);

        DrawMulticlassApproachWarning(graphics, radarRect, radar);
        DrawRadarCars(graphics, radar, radarRect);
        if (radar.HasLeft)
        {
            DrawRadarCar(graphics, new RectangleF(center.X - 94, center.Y - 28, 28, 58), Error);
        }
        if (radar.HasRight)
        {
            DrawRadarCar(graphics, new RectangleF(center.X + 66, center.Y - 64, 28, 58), Cyan);
        }
        DrawRadarCar(graphics, new RectangleF(center.X - 12, center.Y - 24, 24, 48), TextPrimary);
    }

    private void DrawMulticlassApproachWarning(Graphics graphics, RectangleF rect, DesignV2RadarBody radar)
    {
        if (!radar.ShowMulticlassWarning || radar.StrongestMulticlassApproach?.RelativeSeconds is not { } seconds)
        {
            return;
        }

        using var pen = new Pen(Amber, 4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var arc = RectangleF.Inflate(rect, -18, -18);
        graphics.DrawArc(pen, arc, 242.5f, 55f);
        using var font = FontOf(9.5f, FontStyle.Bold);
        DrawText(graphics, $"{Math.Abs(seconds):0.0}s", font, Amber, new RectangleF(rect.Left + 58, rect.Bottom - 42, rect.Width - 116, 16), ContentAlignment.MiddleCenter);
    }

    private void DrawRadarCars(Graphics graphics, DesignV2RadarBody radar, RectangleF rect)
    {
        IReadOnlyList<LiveSpatialCar> cars = radar.IsAvailable && radar.Cars.Count > 0
            ? radar.Cars
            : radar.PreviewVisible
                ?
                [
                    new LiveSpatialCar(12, LiveModelQuality.Reliable, LiveSignalEvidence.Reliable("preview"), 0.014d, 1.2d, 8d, 6, 5, 4098, null, false, "#FFDA59"),
                    new LiveSpatialCar(51, LiveModelQuality.Reliable, LiveSignalEvidence.Reliable("preview"), -0.065d, -3.4d, -12d, 3, 1, 4099, null, false, "#33CEFF")
                ]
                : [];
        var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
        var usableRadius = rect.Width / 2f - 48f;
        foreach (var (car, index) in cars.Take(8).Select((car, index) => (car, index)))
        {
            var normalized = Math.Clamp((car.RelativeMeters ?? 0d) / RadarRangeMeters, -1d, 1d);
            var lane = ((index % 3) - 1) * 36f;
            var x = center.X + lane;
            var y = center.Y - (float)normalized * usableRadius;
            var color = OverlayClassColor.TryParseWithAlpha(car.CarClassColorHex, 245)
                ?? (Math.Abs(normalized) < 0.35d ? Error : Amber);
            DrawRadarCar(graphics, new RectangleF(x - 12, y - 25, 24, 50), color);
        }
    }

    private static void DrawRadarCar(Graphics graphics, RectangleF rect, Color color)
    {
        FillRounded(graphics, rect, 6, Color.FromArgb(245, color), Color.FromArgb(72, TextPrimary));
    }

    private void DrawTrackMapOverlay(Graphics graphics, RectangleF rect, DesignV2TrackMapBody body)
    {
        var size = Math.Max(20, Math.Min(rect.Width, rect.Height) - 40);
        var trackRect = new RectangleF(
            rect.Left + (rect.Width - size) / 2f,
            rect.Top + (rect.Height - size) / 2f,
            size,
            size);
        var opacity = Math.Clamp(body.InternalOpacity, 0.2d, 1d);
        using var interior = new SolidBrush(Color.FromArgb((int)Math.Round(150 * opacity), TrackInterior));
        if (body.TrackMap?.RacingLine.Points is { Count: >= 3 }
            && DesignV2TrackMapTransform.From(body.TrackMap, trackRect) is { } transform)
        {
            DrawGeneratedTrackMap(graphics, trackRect, body.TrackMap, transform, body.Markers, body.Sectors, interior, body.ShowSectorBoundaries);
            return;
        }

        DrawCircleTrackMap(graphics, trackRect, body.Markers, body.Sectors, interior, body.ShowSectorBoundaries);
    }

    private void DrawGeneratedTrackMap(
        Graphics graphics,
        RectangleF trackRect,
        TrackMapDocument trackMap,
        DesignV2TrackMapTransform transform,
        IReadOnlyList<DesignV2TrackMapMarker> markers,
        IReadOnlyList<LiveTrackSectorSegment> sectors,
        Brush interior,
        bool showSectorBoundaries)
    {
        FillTrackMapGeometry(graphics, trackMap.RacingLine, transform, interior);
        using (var halo = new Pen(TrackHalo, 11f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        using (var line = new Pen(TrackLine, 4.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            DrawTrackMapGeometry(graphics, trackMap.RacingLine, transform, halo);
            DrawTrackMapGeometry(graphics, trackMap.RacingLine, transform, line);
        }

        DrawGeneratedTrackMapSectorHighlights(graphics, trackMap.RacingLine, transform, sectors);
        if (showSectorBoundaries)
        {
            DrawGeneratedTrackMapSectorBoundaries(graphics, trackMap.RacingLine, transform, sectors);
        }

        if (trackMap.PitLane is { Points.Count: >= 2 } pitLane)
        {
            using var pitPen = new Pen(PitLineColor, TrackPitLineWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            DrawTrackMapGeometry(graphics, pitLane, transform, pitPen);
        }

        DrawTrackMapMarkers(
            graphics,
            markers,
            marker => PointOnTrackMapGeometry(trackMap.RacingLine, transform, marker.LapDistPct));
    }

    private void DrawCircleTrackMap(
        Graphics graphics,
        RectangleF trackRect,
        IReadOnlyList<DesignV2TrackMapMarker> markers,
        IReadOnlyList<LiveTrackSectorSegment> sectors,
        Brush interior,
        bool showSectorBoundaries)
    {
        graphics.FillEllipse(interior, trackRect);
        using (var halo = new Pen(TrackHalo, 11f))
        {
            graphics.DrawEllipse(halo, trackRect);
        }
        using (var line = new Pen(TrackLine, 4.4f))
        {
            graphics.DrawEllipse(line, trackRect);
        }

        DrawTrackMapSectorHighlights(graphics, trackRect, sectors);
        if (showSectorBoundaries)
        {
            DrawTrackMapSectorBoundaries(graphics, trackRect, sectors);
        }
        DrawTrackMapMarkers(graphics, trackRect, markers);
    }

    private static void DrawTrackMapGeometry(
        Graphics graphics,
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        Pen pen)
    {
        if (geometry.Points.Count < 2)
        {
            return;
        }

        using var path = new GraphicsPath();
        var previous = transform.Map(geometry.Points[0]);
        for (var index = 1; index < geometry.Points.Count; index++)
        {
            var current = transform.Map(geometry.Points[index]);
            path.AddLine(previous, current);
            previous = current;
        }

        if (geometry.Closed)
        {
            path.CloseFigure();
        }

        graphics.DrawPath(pen, path);
    }

    private static void FillTrackMapGeometry(
        Graphics graphics,
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        Brush brush)
    {
        if (!geometry.Closed || geometry.Points.Count < 3)
        {
            return;
        }

        using var path = TrackMapGeometryPath(geometry, transform);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath TrackMapGeometryPath(
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform)
    {
        var path = new GraphicsPath();
        var previous = transform.Map(geometry.Points[0]);
        for (var index = 1; index < geometry.Points.Count; index++)
        {
            var current = transform.Map(geometry.Points[index]);
            path.AddLine(previous, current);
            previous = current;
        }

        if (geometry.Closed)
        {
            path.CloseFigure();
        }

        return path;
    }

    private static void DrawGeneratedTrackMapSectorHighlights(
        Graphics graphics,
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        IReadOnlyList<LiveTrackSectorSegment> sectors)
    {
        foreach (var sector in sectors.Where(HasTrackMapHighlight))
        {
            using var pen = new Pen(TrackMapSectorHighlightColor(sector.Highlight), 5.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            DrawTrackMapGeometrySegment(graphics, geometry, transform, sector.StartPct, sector.EndPct, pen);
        }
    }

    private static void DrawGeneratedTrackMapSectorBoundaries(
        Graphics graphics,
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        IReadOnlyList<LiveTrackSectorSegment> sectors)
    {
        foreach (var progress in TrackMapBoundaryProgresses(sectors))
        {
            if (TrackMapGeometryBoundaryTick(geometry, transform, progress) is { } tick)
            {
                DrawTrackMapBoundaryTick(graphics, tick.Start, tick.End, IsStartFinishProgress(progress));
            }
        }
    }

    private static void DrawTrackMapGeometrySegment(
        Graphics graphics,
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        double startPct,
        double endPct,
        Pen pen)
    {
        if (geometry.Points.Count < 2)
        {
            return;
        }

        using var path = new GraphicsPath();
        foreach (var range in TrackMapSegmentRanges(startPct, endPct))
        {
            AddTrackMapGeometrySegment(path, geometry, transform, range.StartPct, range.EndPct);
        }

        if (path.PointCount > 1)
        {
            graphics.DrawPath(pen, path);
        }
    }

    private static void AddTrackMapGeometrySegment(
        GraphicsPath path,
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        double startPct,
        double endPct)
    {
        if (endPct <= startPct)
        {
            return;
        }

        var startPoint = PointOnTrackMapGeometry(geometry, transform, startPct);
        var endPoint = PointOnTrackMapGeometry(geometry, transform, endPct);
        if (startPoint is null || endPoint is null)
        {
            return;
        }

        path.StartFigure();
        var previous = startPoint.Value;
        foreach (var point in geometry.Points.Where(point => point.LapDistPct > startPct && point.LapDistPct < endPct))
        {
            var current = transform.Map(point);
            path.AddLine(previous, current);
            previous = current;
        }

        path.AddLine(previous, endPoint.Value);
    }

    private static DesignV2TrackMapBoundaryTick? TrackMapGeometryBoundaryTick(
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        double progress)
    {
        var center = PointOnTrackMapGeometry(geometry, transform, progress);
        var before = PointOnTrackMapGeometry(geometry, transform, progress - 0.002d);
        var after = PointOnTrackMapGeometry(geometry, transform, progress + 0.002d);
        if (center is null || before is null || after is null)
        {
            return null;
        }

        var dx = after.Value.X - before.Value.X;
        var dy = after.Value.Y - before.Value.Y;
        var length = Math.Max(0.001f, (float)Math.Sqrt(dx * dx + dy * dy));
        var normalX = -dy / length;
        var normalY = dx / length;
        var half = TrackMapBoundaryTickLength(progress) / 2f;
        return new DesignV2TrackMapBoundaryTick(
            new PointF(center.Value.X - normalX * half, center.Value.Y - normalY * half),
            new PointF(center.Value.X + normalX * half, center.Value.Y + normalY * half));
    }

    private static PointF? PointOnTrackMapGeometry(
        TrackMapGeometry geometry,
        DesignV2TrackMapTransform transform,
        double progress)
    {
        var points = geometry.Points;
        if (points.Count == 0)
        {
            return null;
        }

        if (points.Count == 1)
        {
            return transform.Map(points[0]);
        }

        var target = NormalizeProgress(progress);
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            if (target < previous.LapDistPct || target > current.LapDistPct)
            {
                continue;
            }

            return InterpolateTrackMapPoint(previous, current, target, transform);
        }

        if (geometry.Closed)
        {
            var previous = points[^1];
            var current = points[0] with { LapDistPct = points[0].LapDistPct + 1d };
            var adjustedTarget = target < previous.LapDistPct ? target + 1d : target;
            return InterpolateTrackMapPoint(previous, current, adjustedTarget, transform);
        }

        return transform.Map(points.MinBy(point => Math.Abs(point.LapDistPct - target)) ?? points[0]);
    }

    private static PointF InterpolateTrackMapPoint(
        TrackMapPoint previous,
        TrackMapPoint current,
        double target,
        DesignV2TrackMapTransform transform)
    {
        var span = current.LapDistPct - previous.LapDistPct;
        var ratio = span <= 0d ? 0d : Math.Clamp((target - previous.LapDistPct) / span, 0d, 1d);
        return transform.Map(new TrackMapPoint(
            target,
            previous.X + (current.X - previous.X) * ratio,
            previous.Y + (current.Y - previous.Y) * ratio));
    }

    private static void DrawTrackMapSectorHighlights(Graphics graphics, RectangleF rect, IReadOnlyList<LiveTrackSectorSegment> sectors)
    {
        foreach (var sector in sectors.Where(HasTrackMapHighlight))
        {
            using var pen = new Pen(TrackMapSectorHighlightColor(sector.Highlight), 5.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            foreach (var range in TrackMapSegmentRanges(sector.StartPct, sector.EndPct))
            {
                var start = (float)(range.StartPct * 360d - 90d);
                var sweep = (float)((range.EndPct - range.StartPct) * 360d);
                if (sweep > 0)
                {
                    graphics.DrawArc(pen, rect, start, sweep);
                }
            }
        }
    }

    private static void DrawTrackMapSectorBoundaries(Graphics graphics, RectangleF rect, IReadOnlyList<LiveTrackSectorSegment> sectors)
    {
        if (sectors.Count < 2)
        {
            return;
        }

        var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
        foreach (var progress in TrackMapBoundaryProgresses(sectors))
        {
            var point = TrackMapPoint(rect, progress);
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            var length = Math.Max(0.001f, (float)Math.Sqrt(dx * dx + dy * dy));
            var unitX = dx / length;
            var unitY = dy / length;
            var half = TrackMapBoundaryTickLength(progress) / 2f;
            DrawTrackMapBoundaryTick(
                graphics,
                point.X - unitX * half,
                point.Y - unitY * half,
                point.X + unitX * half,
                point.Y + unitY * half,
                IsStartFinishProgress(progress));
        }
    }

    private static void DrawTrackMapBoundaryTick(
        Graphics graphics,
        float x1,
        float y1,
        float x2,
        float y2,
        bool isStartFinish)
    {
        DrawTrackMapBoundaryTick(graphics, new PointF(x1, y1), new PointF(x2, y2), isStartFinish);
    }

    private static void DrawTrackMapBoundaryTick(Graphics graphics, PointF start, PointF end, bool isStartFinish)
    {
        if (isStartFinish)
        {
            using var shadowPen = new Pen(StartFinishBoundaryShadowColor, 5.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var mainPen = new Pen(StartFinishBoundaryColor, 3.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var highlightPen = new Pen(Color.FromArgb(235, 255, 247, 255), 1.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(shadowPen, start, end);
            graphics.DrawLine(mainPen, start, end);
            graphics.DrawLine(highlightPen, start, end);
            return;
        }

        using var pen = new Pen(Cyan, 2.2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(pen, start, end);
    }

    private void DrawTrackMapMarkers(Graphics graphics, RectangleF rect, IReadOnlyList<DesignV2TrackMapMarker> markers)
    {
        DrawTrackMapMarkers(graphics, markers, marker => TrackMapPoint(rect, marker.LapDistPct));
    }

    private void DrawTrackMapMarkers(
        Graphics graphics,
        IReadOnlyList<DesignV2TrackMapMarker> markers,
        Func<DesignV2TrackMapMarker, PointF?> pointForMarker)
    {
        foreach (var marker in markers.OrderBy(marker => marker.IsFocus).ThenBy(marker => marker.CarIdx))
        {
            if (pointForMarker(marker) is not { } point)
            {
                continue;
            }

            using var markerFont = FontOf(7.6f, FontStyle.Bold);
            var labelSize = marker.IsFocus && marker.PositionLabel is not null
                ? graphics.MeasureString(marker.PositionLabel, markerFont)
                : SizeF.Empty;
            var radius = marker.IsFocus
                ? Math.Max(5.7f, Math.Max(labelSize.Width, labelSize.Height) / 2f + 3.5f)
                : 3.6f;
            var markerRect = new RectangleF(point.X - radius, point.Y - radius, radius * 2f, radius * 2f);
            using (var brush = new SolidBrush(marker.Color))
            {
                graphics.FillEllipse(brush, markerRect);
            }
            using (var pen = new Pen(TrackMarkerBorder, marker.IsFocus ? 2f : 1.4f))
            {
                graphics.DrawEllipse(pen, markerRect);
            }
            if (marker.IsFocus && marker.PositionLabel is not null)
            {
                DrawText(graphics, marker.PositionLabel, markerFont, Color.FromArgb(5, 13, 17), markerRect, ContentAlignment.MiddleCenter);
            }
        }
    }

    private void DrawFlagsOverlay(Graphics graphics, RectangleF rect, DesignV2FlagsBody body)
    {
        if (!body.ManagedEnabled || body.SettingsOverlayActive || body.Flags.Count == 0)
        {
            return;
        }

        if (body.Flags.Count == 0)
        {
            using var waitingFont = FontOf(12f, FontStyle.Bold);
            DrawText(graphics, body.IsWaiting ? "waiting for flags" : "no active flags", waitingFont, TextMuted, rect, ContentAlignment.MiddleCenter);
            return;
        }

        var bounds = new RectangleF(
            rect.Left + FlagOuterPadding,
            rect.Top + FlagOuterPadding,
            Math.Max(1f, rect.Width - FlagOuterPadding * 2f),
            Math.Max(1f, rect.Height - FlagOuterPadding * 2f));
        var (columns, rows) = FlagGridFor(body.Flags.Count);
        var cellWidth = (bounds.Width - (columns - 1) * FlagCellGap) / columns;
        var cellHeight = (bounds.Height - (rows - 1) * FlagCellGap) / rows;
        for (var index = 0; index < body.Flags.Count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var cell = new RectangleF(
                bounds.Left + column * (cellWidth + FlagCellGap),
                bounds.Top + row * (cellHeight + FlagCellGap),
                cellWidth,
                cellHeight);
            DrawFlagCell(graphics, cell, body.Flags[index], index);
        }
    }

    private static void DrawFlagCell(Graphics graphics, RectangleF cell, FlagOverlayDisplayItem flag, int index)
    {
        var compact = cell.Height < 92f || cell.Width < 132f;
        var poleX = cell.Left + Math.Max(12f, cell.Width * 0.16f);
        var poleTop = cell.Top + 4f;
        var poleBottom = cell.Bottom - 2f;
        using (var shadowPen = new Pen(FlagPoleShadowColor, compact ? 2f : 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        })
        {
            graphics.DrawLine(shadowPen, poleX + 1f, poleTop + 1f, poleX + 1f, poleBottom + 1f);
        }
        using (var polePen = new Pen(FlagPoleColor, compact ? 2f : 3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        })
        {
            graphics.DrawLine(polePen, poleX, poleTop, poleX, poleBottom);
        }

        var clothLeft = poleX + 1f;
        var clothWidth = Math.Max(48f, cell.Right - clothLeft - 8f);
        var clothHeight = Math.Max(24f, Math.Min(cell.Height * 0.7f, clothWidth * 0.58f));
        var clothTop = cell.Top + Math.Max(4f, (cell.Height - clothHeight) * 0.32f);
        var clothBounds = new RectangleF(clothLeft, clothTop, clothWidth, clothHeight);
        using var path = CreateFlagPath(clothBounds, compact ? 3.5f : 5.5f, index);
        DrawFlagCloth(graphics, path, flag, clothBounds);
    }

    private static void DrawFlagCloth(
        Graphics graphics,
        GraphicsPath path,
        FlagOverlayDisplayItem flag,
        RectangleF clothBounds)
    {
        if (flag.Kind == FlagDisplayKind.Checkered)
        {
            DrawCheckeredFlag(graphics, path, clothBounds);
            return;
        }

        using (var brush = new SolidBrush(FlagFillColor(flag.Kind)))
        {
            graphics.FillPath(brush, path);
        }

        if (flag.Kind == FlagDisplayKind.Meatball)
        {
            var diameter = Math.Min(clothBounds.Width, clothBounds.Height) * 0.44f;
            using var discBrush = new SolidBrush(Orange);
            graphics.FillEllipse(
                discBrush,
                clothBounds.Left + (clothBounds.Width - diameter) / 2f,
                clothBounds.Top + (clothBounds.Height - diameter) / 2f,
                diameter,
                diameter);
        }
        else if (flag.Kind == FlagDisplayKind.Caution)
        {
            using var stripeBrush = new SolidBrush(Color.FromArgb(72, 0, 0, 0));
            var stripeWidth = Math.Max(8f, clothBounds.Width * 0.12f);
            var oldClip = graphics.Clip;
            try
            {
                graphics.SetClip(path, CombineMode.Intersect);
                for (var x = clothBounds.Left - clothBounds.Height; x < clothBounds.Right; x += stripeWidth * 2.5f)
                {
                    graphics.FillPolygon(
                        stripeBrush,
                        [
                            new PointF(x, clothBounds.Bottom),
                            new PointF(x + stripeWidth, clothBounds.Bottom),
                            new PointF(x + stripeWidth + clothBounds.Height, clothBounds.Top),
                            new PointF(x + clothBounds.Height, clothBounds.Top)
                        ]);
                }
            }
            finally
            {
                graphics.SetClip(oldClip, CombineMode.Replace);
                oldClip.Dispose();
            }
        }

        DrawFlagOutline(graphics, path, flag.Kind);
    }

    private static void DrawCheckeredFlag(Graphics graphics, GraphicsPath path, RectangleF clothBounds)
    {
        var oldClip = graphics.Clip;
        try
        {
            graphics.SetClip(path, CombineMode.Intersect);
            using var whiteBrush = new SolidBrush(Color.FromArgb(245, 247, 250));
            using var blackBrush = new SolidBrush(Color.FromArgb(8, 10, 12));
            graphics.FillRectangle(whiteBrush, clothBounds);
            const int columns = 6;
            const int rows = 4;
            var squareWidth = clothBounds.Width / columns;
            var squareHeight = clothBounds.Height / rows;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if ((row + column) % 2 == 0)
                    {
                        continue;
                    }

                    graphics.FillRectangle(
                        blackBrush,
                        clothBounds.Left + column * squareWidth,
                        clothBounds.Top + row * squareHeight,
                        squareWidth + 1f,
                        squareHeight + 1f);
                }
            }
        }
        finally
        {
            graphics.SetClip(oldClip, CombineMode.Replace);
            oldClip.Dispose();
        }

        DrawFlagOutline(graphics, path, FlagDisplayKind.Checkered);
    }

    private static void DrawFlagOutline(Graphics graphics, GraphicsPath path, FlagDisplayKind kind)
    {
        var outline = kind == FlagDisplayKind.White || kind == FlagDisplayKind.Checkered
            ? Color.FromArgb(220, 26, 30, 34)
            : Color.FromArgb(172, 255, 255, 255);
        using var pen = new Pen(outline, 1.4f)
        {
            LineJoin = LineJoin.Round
        };
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateFlagPath(RectangleF bounds, float wave, int index)
    {
        var phase = index % 2 == 0 ? 1f : -1f;
        var path = new GraphicsPath();
        var leftTop = new PointF(bounds.Left, bounds.Top);
        var rightTop = new PointF(bounds.Right, bounds.Top + wave * phase);
        var rightBottom = new PointF(bounds.Right, bounds.Bottom + wave * 0.4f * phase);
        var leftBottom = new PointF(bounds.Left, bounds.Bottom);
        path.StartFigure();
        path.AddBezier(
            leftTop,
            new PointF(bounds.Left + bounds.Width * 0.28f, bounds.Top - wave * phase),
            new PointF(bounds.Left + bounds.Width * 0.62f, bounds.Top + wave * phase),
            rightTop);
        path.AddLine(rightTop, rightBottom);
        path.AddBezier(
            rightBottom,
            new PointF(bounds.Left + bounds.Width * 0.62f, bounds.Bottom - wave * phase),
            new PointF(bounds.Left + bounds.Width * 0.28f, bounds.Bottom + wave * phase),
            leftBottom);
        path.CloseFigure();
        return path;
    }

    private static Color FlagFillColor(FlagDisplayKind kind)
    {
        return kind switch
        {
            FlagDisplayKind.Green => Color.FromArgb(48, 214, 109),
            FlagDisplayKind.Blue => Color.FromArgb(55, 162, 255),
            FlagDisplayKind.Yellow or FlagDisplayKind.Caution => Color.FromArgb(255, 207, 74),
            FlagDisplayKind.Red => Color.FromArgb(236, 76, 86),
            FlagDisplayKind.Black or FlagDisplayKind.Meatball => Color.FromArgb(8, 10, 12),
            FlagDisplayKind.White => Color.FromArgb(246, 248, 250),
            _ => Color.White
        };
    }

    private static (int Columns, int Rows) FlagGridFor(int count)
    {
        return count switch
        {
            <= 1 => (1, 1),
            2 => (2, 1),
            <= 4 => (2, 2),
            <= 6 => (3, 2),
            _ => (4, 2)
        };
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

    private static bool UsesTransparentBackground(DesignV2LiveOverlayKind kind)
    {
        return kind is DesignV2LiveOverlayKind.TrackMap
            or DesignV2LiveOverlayKind.CarRadar
            or DesignV2LiveOverlayKind.Flags;
    }

    private static string RadarStatusText(DesignV2RadarBody radar)
    {
        if (!radar.IsAvailable)
        {
            return "WAITING";
        }

        if (radar.ShowMulticlassWarning
            && radar.StrongestMulticlassApproach?.RelativeSeconds is { } seconds
            && IsFinite(seconds))
        {
            return $"{Math.Abs(seconds):0.0}s";
        }

        if (radar.HasLeft || radar.HasRight)
        {
            return "SIDE";
        }

        return "CLEAR";
    }

    internal static bool IsInputTransparentKind(DesignV2LiveOverlayKind kind)
    {
        return kind is DesignV2LiveOverlayKind.StreamChat;
    }

    private Button CreateStreamChatCloseButton()
    {
        var button = new Button
        {
            Text = "X",
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Width = 22,
            Height = 22,
            BackColor = TitleBar,
            ForeColor = TextPrimary,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(74, 40, 48, 62);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(96, 60, 34, 44);
        button.Click += (_, _) => DisableOverlayAndClose();
        return button;
    }

    private void LayoutStreamChatCloseButton()
    {
        if (_closeButton is null)
        {
            return;
        }

        _closeButton.Location = new Point(Math.Max(4, ClientSize.Width - _closeButton.Width - 10), 9);
    }

    private bool IsStreamChatCloseButtonHit(Point clientPoint)
    {
        return _closeButton is not null && _closeButton.Bounds.Contains(clientPoint);
    }

    internal static bool IsStreamChatDragHit(Point clientPoint, Size clientSize)
    {
        return clientPoint.X >= 0
            && clientPoint.X < clientSize.Width
            && clientPoint.Y >= 0
            && clientPoint.Y < Math.Min(HeaderHeight, clientSize.Height);
    }

    private static bool HasTrackMapHighlight(LiveTrackSectorSegment sector)
    {
        return string.Equals(sector.Highlight, LiveTrackSectorHighlights.PersonalBest, StringComparison.Ordinal)
            || string.Equals(sector.Highlight, LiveTrackSectorHighlights.BestLap, StringComparison.Ordinal);
    }

    private static Color TrackMapSectorHighlightColor(string highlight)
    {
        return string.Equals(highlight, LiveTrackSectorHighlights.BestLap, StringComparison.Ordinal)
            ? BestLapSectorColor
            : PersonalBestSectorColor;
    }

    private static IEnumerable<DesignV2SectorProgressRange> TrackMapSegmentRanges(double startPct, double endPct)
    {
        var start = NormalizeProgress(startPct);
        var end = endPct >= 1d ? 1d : NormalizeProgress(endPct);
        if (end <= start && endPct < 1d)
        {
            yield return new DesignV2SectorProgressRange(start, 1d);
            yield return new DesignV2SectorProgressRange(0d, end);
            yield break;
        }

        yield return new DesignV2SectorProgressRange(start, Math.Clamp(end, 0d, 1d));
    }

    private static IEnumerable<double> TrackMapBoundaryProgresses(IReadOnlyList<LiveTrackSectorSegment> sectors)
    {
        var seen = new HashSet<int>();
        foreach (var sector in sectors)
        {
            var progress = NormalizeProgress(sector.StartPct);
            var key = (int)Math.Round(progress * 100_000d);
            if (seen.Add(key))
            {
                yield return progress;
            }
        }
    }

    private static PointF TrackMapPoint(RectangleF rect, double progress)
    {
        var angle = NormalizeProgress(progress) * Math.PI * 2d - Math.PI / 2d;
        return new PointF(
            rect.Left + rect.Width / 2f + (float)Math.Cos(angle) * rect.Width / 2f,
            rect.Top + rect.Height / 2f + (float)Math.Sin(angle) * rect.Height / 2f);
    }

    private static float TrackMapBoundaryTickLength(double progress)
    {
        return IsStartFinishProgress(progress)
            ? TrackSectorBoundaryTickLength * 1.45f
            : TrackSectorBoundaryTickLength;
    }

    private static bool IsStartFinishProgress(double progress)
    {
        var normalized = NormalizeProgress(progress);
        return normalized <= 0.0005d || normalized >= 0.9995d;
    }

    private static double NormalizeProgress(double value)
    {
        if (!IsFinite(value))
        {
            return 0d;
        }

        var normalized = value % 1d;
        return normalized < 0d ? normalized + 1d : normalized;
    }

    private static bool IsValidProgress(double value)
    {
        return IsFinite(value) && value >= 0d;
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

    private static string FormatAxisSeconds(double seconds)
    {
        if (!IsFinite(seconds))
        {
            return "--";
        }

        return seconds < 60d
            ? $"+{seconds:0.0}s"
            : $"+{Math.Floor(seconds / 60d):0}:{seconds % 60d:00.0}";
    }

    private static string FormatDeltaSeconds(double seconds)
    {
        if (!IsFinite(seconds))
        {
            return "--";
        }

        var sign = seconds > 0d ? "+" : seconds < 0d ? "-" : string.Empty;
        var absolute = Math.Abs(seconds);
        return absolute >= 60d
            ? $"{sign}{Math.Floor(absolute / 60d):0}:{absolute % 60d:00.0}"
            : $"{sign}{absolute:0.0}s";
    }

    private static string FormatTrendWindow(TimeSpan trendWindow)
    {
        return trendWindow.TotalHours >= 1d
            ? $"{trendWindow.TotalHours:0.#}h"
            : $"{trendWindow.TotalMinutes:0}m";
    }

    private static string FormatGapChangeSeconds(double seconds)
    {
        if (!IsFinite(seconds))
        {
            return "--";
        }

        return Math.Abs(seconds) < 0.05d
            ? "0.0"
            : $"{(seconds > 0d ? "+" : string.Empty)}{seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private static double NiceCeiling(double value)
    {
        if (!IsFinite(value) || value <= 0d)
        {
            return 1d;
        }

        var exponent = Math.Floor(Math.Log10(value));
        var baseValue = Math.Pow(10d, exponent);
        var normalized = value / baseValue;
        var nice = normalized <= 1d ? 1d
            : normalized <= 2d ? 2d
            : normalized <= 5d ? 5d
            : 10d;
        return nice * baseValue;
    }

    private static double NiceGridStep(double value)
    {
        if (!IsFinite(value) || value <= 0.25d)
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

    private static string FormatPercent(double? value)
    {
        return value is { } number && IsFinite(number)
            ? $"{Math.Round(Math.Clamp(number, 0d, 1d) * 100d):0}%"
            : "--";
    }

    private static string FormatGear(int? gear)
    {
        return gear switch
        {
            -1 => "R",
            0 => "N",
            > 0 => gear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => "--"
        };
    }

    private static string FormatSteering(double? angleRadians)
    {
        return angleRadians is { } value && IsFinite(value)
            ? $"{(value * 180d / Math.PI).ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture)} deg"
            : "--";
    }

    private static double? FirstValidFuelLevel(params double?[] values)
    {
        foreach (var value in values)
        {
            if (value is { } fuelLevel && IsFinite(fuelLevel) && fuelLevel > 0d)
            {
                return fuelLevel;
            }
        }

        return null;
    }

    private static string DesignV2GapCarShortLabel(DesignV2GapCarRenderState state)
    {
        return $"#{state.CarIdx}";
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

    private static Color WithAlpha(Color color, double alpha)
    {
        return Color.FromArgb(
            (int)Math.Clamp(color.A * alpha, 0d, 255d),
            color.R,
            color.G,
            color.B);
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
    DesignV2Body Body,
    string? HeaderText = null,
    bool ShowFooter = true);

internal abstract record DesignV2Body;

internal sealed record DesignV2TableBody(
    IReadOnlyList<DesignV2Column> Columns,
    IReadOnlyList<DesignV2TableRow> Rows) : DesignV2Body;

internal sealed record DesignV2MetricRowsBody(
    IReadOnlyList<DesignV2MetricRow> Rows) : DesignV2Body;

internal sealed record DesignV2GraphBody(
    IReadOnlyList<double> Points,
    IReadOnlyList<DesignV2GapSeries> Series,
    IReadOnlyList<DesignV2GapWeatherPoint> Weather,
    IReadOnlyList<DesignV2GapLeaderChangeMarker> LeaderChanges,
    IReadOnlyList<DesignV2GapDriverChangeMarker> DriverChanges,
    double StartSeconds,
    double EndSeconds,
    double? MaxGapSeconds,
    double? LapReferenceSeconds,
    int SelectedSeriesCount,
    IReadOnlyList<DesignV2GapTrendMetric> TrendMetrics,
    DesignV2GapTrendMetric? ActiveThreat,
    int? ThreatCarIdx,
    double MetricDeadbandSeconds,
    DesignV2GapScale? Scale = null) : DesignV2Body
{
    public DesignV2GraphBody(IReadOnlyList<double> points)
        : this(points, [], [], [], [], 0d, 0d, null, null, 0, [], null, null, 0d)
    {
    }
}

internal sealed record DesignV2GapTrendMetric(
    string Label,
    double? FocusGapChangeSeconds,
    DesignV2BehindGainMetric? Chaser,
    string State,
    string? StateLabel);

internal sealed record DesignV2BehindGainMetric(
    int CarIdx,
    string Label,
    double GainSeconds);

internal sealed record DesignV2GapScale(
    double MaxGapSeconds,
    bool IsFocusRelative,
    double AheadSeconds,
    double BehindSeconds,
    IReadOnlyList<DesignV2GapTrendPoint> ReferencePoints,
    double LatestReferenceGapSeconds)
{
    public static DesignV2GapScale Leader(double maxGapSeconds)
    {
        return new DesignV2GapScale(
            MaxGapSeconds: maxGapSeconds,
            IsFocusRelative: false,
            AheadSeconds: 0d,
            BehindSeconds: 0d,
            ReferencePoints: [],
            LatestReferenceGapSeconds: 0d);
    }

    public static DesignV2GapScale FocusRelative(
        double maxGapSeconds,
        double aheadSeconds,
        double behindSeconds,
        IReadOnlyList<DesignV2GapTrendPoint> referencePoints,
        double latestReferenceGapSeconds)
    {
        return new DesignV2GapScale(
            MaxGapSeconds: maxGapSeconds,
            IsFocusRelative: true,
            AheadSeconds: aheadSeconds,
            BehindSeconds: behindSeconds,
            ReferencePoints: referencePoints,
            LatestReferenceGapSeconds: latestReferenceGapSeconds);
    }
}

internal sealed record DesignV2GapSeries(
    int CarIdx,
    bool IsReference,
    bool IsClassLeader,
    int? ClassPosition,
    double Alpha,
    bool IsStickyExit,
    bool IsStale,
    IReadOnlyList<DesignV2GapTrendPoint> Points);

internal sealed record DesignV2GapTrendPoint(
    DateTimeOffset TimestampUtc,
    double AxisSeconds,
    double GapSeconds,
    int CarIdx,
    bool IsReference,
    bool IsClassLeader,
    int? ClassPosition,
    bool StartsSegment);

internal sealed record DesignV2GapWeatherPoint(
    double AxisSeconds,
    DesignV2GapWeatherCondition Condition);

internal sealed record DesignV2GapLeaderChangeMarker(
    DateTimeOffset TimestampUtc,
    double AxisSeconds,
    int PreviousLeaderCarIdx,
    int NewLeaderCarIdx);

internal sealed record DesignV2GapDriverChangeMarker(
    DateTimeOffset TimestampUtc,
    double AxisSeconds,
    int CarIdx,
    double GapSeconds,
    bool IsReference,
    string Label);

internal sealed record DesignV2GapEndpointLabel(
    string Text,
    PointF Point,
    Color Color,
    bool IsReference,
    bool IsClassLeader);

internal sealed record DesignV2PositionedGapEndpointLabel(
    DesignV2GapEndpointLabel Label,
    float Y);

internal sealed record DesignV2GapSeriesSelection(
    DesignV2GapCarRenderState State,
    double Alpha,
    bool IsStickyExit,
    bool IsStale,
    double DrawStartSeconds);

internal sealed record DesignV2GapDriverIdentity(
    int CarIdx,
    string DriverKey,
    string ShortLabel)
{
    public bool HasSameDriver(DesignV2GapDriverIdentity other)
    {
        return string.Equals(DriverKey, other.DriverKey, StringComparison.Ordinal);
    }
}

internal sealed class DesignV2GapCarRenderState(int carIdx)
{
    public int CarIdx { get; } = carIdx;

    public double LastSeenAxisSeconds { get; set; }

    public double LastGapSeconds { get; set; }

    public double? LastDesiredAxisSeconds { get; set; }

    public double? VisibleSinceAxisSeconds { get; set; }

    public bool IsCurrentlyDesired { get; set; }

    public bool IsReference { get; set; }

    public bool IsClassLeader { get; set; }

    public int? ClassPosition { get; set; }

    public double? DeltaSecondsToReference { get; set; }
}

internal sealed record DesignV2GapReferenceContext(int? CarIdx, int? CarClass);

internal enum DesignV2GapWeatherCondition
{
    Unknown,
    Dry,
    Damp,
    Wet,
    DeclaredWet
}

internal sealed record DesignV2InputsBody(
    double? Throttle,
    double? Brake,
    double? Clutch,
    double? SteeringWheelAngle,
    double? SpeedMetersPerSecond,
    int? Gear,
    bool BrakeAbsActive,
    bool IsAvailable,
    bool ShowThrottle,
    bool ShowBrake,
    bool ShowClutch,
    bool ShowSteering,
    bool ShowGear,
    bool ShowSpeed,
    IReadOnlyList<DesignV2InputPoint> Trace) : DesignV2Body;

internal sealed record DesignV2RadarBody(
    bool IsAvailable,
    bool HasLeft,
    bool HasRight,
    IReadOnlyList<LiveSpatialCar> Cars,
    LiveMulticlassApproach? StrongestMulticlassApproach,
    bool ShowMulticlassWarning,
    bool PreviewVisible) : DesignV2Body;

internal sealed record DesignV2ChatBody(
    IReadOnlyList<DesignV2ChatRow> Rows) : DesignV2Body;

internal sealed record DesignV2FlagsBody(
    IReadOnlyList<FlagOverlayDisplayItem> Flags,
    bool IsWaiting,
    bool ManagedEnabled,
    bool SettingsOverlayActive) : DesignV2Body;

internal sealed record DesignV2TrackMapBody(
    IReadOnlyList<DesignV2TrackMapMarker> Markers,
    IReadOnlyList<LiveTrackSectorSegment> Sectors,
    bool ShowSectorBoundaries,
    double InternalOpacity,
    bool IsAvailable,
    TrackMapDocument? TrackMap) : DesignV2Body;

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
    string ClassHeaderDetail = "",
    int? RelativeLapDelta = null);

internal sealed record DesignV2MetricRow(
    string Label,
    string Value,
    DesignV2Evidence Evidence);

internal sealed record DesignV2ChatRow(
    string Author,
    string Message,
    DesignV2Evidence Evidence);

internal sealed record DesignV2InputPoint(
    double Throttle,
    double Brake,
    double Clutch,
    bool BrakeAbsActive);

internal sealed record DesignV2InputRailLayout(
    IReadOnlyList<DesignV2InputRailItem> Items);

internal sealed record DesignV2InputRailItem(
    DesignV2InputRailItemKind Kind,
    RectangleF Bounds);

internal enum DesignV2InputRailItemKind
{
    Throttle,
    Brake,
    Clutch,
    SteeringWheel,
    Gear,
    Speed
}

internal sealed record DesignV2TrackMapMarker(
    int CarIdx,
    double LapDistPct,
    bool IsFocus,
    Color Color,
    string? PositionLabel);

internal readonly record struct DesignV2SectorProgressRange(double StartPct, double EndPct);

internal readonly record struct DesignV2TrackMapBoundaryTick(PointF Start, PointF End);

internal sealed record DesignV2TrackMapTransform(
    double MinX,
    double MaxY,
    double Scale,
    float Left,
    float Top,
    float Width,
    float Height)
{
    public static DesignV2TrackMapTransform? From(TrackMapDocument document, RectangleF bounds)
    {
        var points = document.RacingLine.Points
            .Concat(document.PitLane?.Points ?? [])
            .Where(point => IsFinite(point.X) && IsFinite(point.Y))
            .ToArray();
        if (points.Length == 0)
        {
            return null;
        }

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        var geometryWidth = Math.Max(1d, maxX - minX);
        var geometryHeight = Math.Max(1d, maxY - minY);
        var scale = Math.Min(bounds.Width / geometryWidth, bounds.Height / geometryHeight);
        if (!IsFinite(scale) || scale <= 0d)
        {
            return null;
        }

        var renderedWidth = (float)(geometryWidth * scale);
        var renderedHeight = (float)(geometryHeight * scale);
        return new DesignV2TrackMapTransform(
            minX,
            maxY,
            scale,
            bounds.Left + (bounds.Width - renderedWidth) / 2f,
            bounds.Top + (bounds.Height - renderedHeight) / 2f,
            renderedWidth,
            renderedHeight);
    }

    public PointF Map(TrackMapPoint point)
    {
        return new PointF(
            Left + (float)((point.X - MinX) * Scale),
            Top + (float)((MaxY - point.Y) * Scale));
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
