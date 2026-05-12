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
        return IsStreamChatCloseButtonHit(clientPoint);
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
        return _kind switch
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
        if (ClientSize.Height == targetHeight)
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
        var proximity = snapshot.Proximity;
        var isAvailable = availability.IsAvailable && proximity.HasData;
        var status = !isAvailable
            ? availability.StatusText
            : proximity.HasCarLeft && proximity.HasCarRight
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
            isAvailable ? "source: spatial telemetry" : "source: waiting",
            isAvailable ? DesignV2Evidence.Live : DesignV2Evidence.Unavailable,
            new DesignV2RadarBody(
                isAvailable || _settingsPreviewVisible,
                proximity.HasCarLeft,
                proximity.HasCarRight,
                proximity.NearbyCars,
                proximity.StrongestMulticlassApproach,
                _settings.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, defaultValue: true),
                _settingsPreviewVisible));
    }

    private DesignV2OverlayModel BuildGapModel(LiveTelemetrySnapshot snapshot, DateTimeOffset now)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var gap = TmrOverlay.App.Overlays.GapToLeader.GapToLeaderLiveModelAdapter.Select(snapshot);
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

    private static IReadOnlyList<DesignV2TrackMapMarker> BuildTrackMapMarkers(LiveTelemetrySnapshot snapshot)
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
            if (!row.HasSpatialProgress
                || row.LapDistPct is not { } lapDistPct
                || !IsValidProgress(lapDistPct))
            {
                continue;
            }

            scoringByCarIdx.TryGetValue(row.CarIdx, out var scoringRow);
            var isFocus = row.IsFocus
                || row.CarIdx == referenceCarIdx
                || scoringRow?.IsFocus == true;
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

        if (markers.Count == 0 && snapshot.Models.Scoring.Rows.Count > 0)
        {
            AddStartingGridMarkers(markers, snapshot.Models.Scoring.Rows, referenceCarIdx);
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

    private static void AddStartingGridMarkers(
        Dictionary<int, DesignV2TrackMapMarker> markers,
        IReadOnlyList<LiveScoringRow> scoringRows,
        int? referenceCarIdx)
    {
        var rows = scoringRows
            .OrderBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenBy(row => row.CarIdx)
            .ToArray();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var isFocus = row.IsFocus || row.CarIdx == referenceCarIdx;
            markers[row.CarIdx] = new DesignV2TrackMapMarker(
                row.CarIdx,
                rows.Length <= 1 ? 0d : NormalizeProgress(index / (double)rows.Length),
                isFocus,
                MarkerColor(row.CarClassColorHex, isFocus),
                isFocus ? PositionLabel(row) : null);
        }
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
        var progress = sample?.FocusLapDistPct;
        return progress is { } value && IsValidProgress(value)
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
            model.Status,
            statusFont,
            EvidenceColor(model.Evidence),
            new RectangleF(outer.Left + 230, header.Top + 10, Math.Max(1, outer.Width - 244 - closeButtonSpace), 18),
            ContentAlignment.MiddleRight);

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
        var frame = RectangleF.Inflate(rect, -12, -14);
        const float axisWidth = 58f;
        const float xAxisHeight = 17f;
        var plot = new RectangleF(
            frame.Left + axisWidth,
            frame.Top,
            Math.Max(40, frame.Width - axisWidth - 4),
            Math.Max(40, frame.Height - xAxisHeight));
        using var gridPen = new Pen(Color.FromArgb(80, TextMuted), 1);
        for (var index = 1; index < 4; index++)
        {
            var y = plot.Top + index * plot.Height / 4f;
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
        }

        using var axisPen = new Pen(Color.FromArgb(70, TextMuted), 1);
        graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        using var axisFont = FontOf(9.5f);
        DrawText(graphics, "leader", axisFont, TextMuted, new RectangleF(frame.Left, plot.Bottom - 7, axisWidth - 8, 14), ContentAlignment.MiddleRight);
        DrawText(graphics, FormatAxisSeconds(max), axisFont, TextMuted, new RectangleF(frame.Left, plot.Top - 7, axisWidth - 8, 14), ContentAlignment.MiddleRight);
        DrawText(graphics, "10m", axisFont, TextMuted, new RectangleF(plot.Left, plot.Bottom + 3, 44, 14));
        DrawText(graphics, "now", axisFont, TextMuted, new RectangleF(plot.Right - 44, plot.Bottom + 3, 44, 14), ContentAlignment.MiddleRight);

        using var linePen = new Pen(Cyan, 2f);
        using var path = new GraphicsPath();
        for (var index = 0; index < points.Count; index++)
        {
            var progress = index / (float)Math.Max(1, points.Count - 1);
            var normalized = (float)((points[index] - min) / span);
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

        graphics.DrawPath(linePen, path);
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
        using var pillFont = FontOf(9f, FontStyle.Bold);
        DrawText(graphics, "Inputs", titleFont, TextPrimary, new RectangleF(rect.Left + 14, rect.Top + 10, 100, 16));
        var pill = new RectangleF(rect.Right - 92, rect.Top + 8, 76, 20);
        FillRounded(graphics, pill, 10, Color.FromArgb(62, Cyan), Color.FromArgb(115, Cyan));
        DrawText(graphics, body.IsAvailable ? "LIVE" : "WAIT", pillFont, Cyan, RectangleF.Inflate(pill, -10, -4), ContentAlignment.MiddleCenter);

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
        var y = rect.Top;
        if (body.ShowThrottle)
        {
            DrawInputBar(graphics, "THR", body.Throttle, Green, new RectangleF(rect.Left, y, rect.Width, 27));
            y += 38;
        }
        if (body.ShowBrake)
        {
            DrawInputBar(graphics, body.BrakeAbsActive ? "ABS" : "BRK", body.Brake, body.BrakeAbsActive ? Amber : Error, new RectangleF(rect.Left, y, rect.Width, 27));
            y += 38;
        }
        if (body.ShowClutch)
        {
            DrawInputBar(graphics, "CLT", body.Clutch, Cyan, new RectangleF(rect.Left, y, rect.Width, 27));
            y += 40;
        }
        if (body.ShowSteering)
        {
            var wheelHeight = Math.Min(78f, Math.Max(52f, rect.Bottom - y - 58f));
            DrawInputWheel(graphics, body.SteeringWheelAngle, new RectangleF(rect.Left, y, rect.Width, wheelHeight));
            y += wheelHeight + 8;
        }

        var readouts = new List<(string Label, string Value)>();
        if (body.ShowGear)
        {
            readouts.Add(("GEAR", FormatGear(body.Gear)));
        }
        if (body.ShowSpeed)
        {
            readouts.Add(("SPD", SimpleTelemetryOverlayViewModel.FormatSpeed(body.SpeedMetersPerSecond, _unitSystem)));
        }
        if (body.ShowSteering)
        {
            readouts.Add(("STR", FormatSteering(body.SteeringWheelAngle)));
        }

        var readoutTop = Math.Max(y + 4, rect.Bottom - Math.Max(1, readouts.Count) * 28);
        for (var index = 0; index < readouts.Count; index++)
        {
            DrawInputReadout(graphics, readouts[index].Label, readouts[index].Value, new RectangleF(rect.Left, readoutTop + index * 28, rect.Width, 20));
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
        IReadOnlyList<LiveProximityCar> cars = radar.IsAvailable && radar.NearbyCars.Count > 0
            ? radar.NearbyCars
            : radar.PreviewVisible
                ?
                [
                    new LiveProximityCar(12, 0.014d, 1.2d, null, 6, 5, 4098, null, false, null, null, "#FFDA59"),
                    new LiveProximityCar(51, -0.065d, -3.4d, null, 3, 1, 4099, null, false, null, null, "#33CEFF")
                ]
                : [];
        var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
        var usableRadius = rect.Width / 2f - 48f;
        foreach (var (car, index) in cars.Take(8).Select((car, index) => (car, index)))
        {
            var seconds = car.RelativeSeconds ?? car.RelativeLaps * 120d;
            var normalized = Math.Clamp(seconds / 2d, -1d, 1d);
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
    IReadOnlyList<LiveProximityCar> NearbyCars,
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
    string ClassHeaderDetail = "");

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
