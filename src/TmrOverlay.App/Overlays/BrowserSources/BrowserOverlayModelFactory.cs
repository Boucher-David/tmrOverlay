using TmrOverlay.App.History;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.BrowserSources;

internal sealed class BrowserOverlayModelFactory
{
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> _sessionWeatherBuilder;
    private readonly Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> _pitServiceBuilder;
    private readonly List<double> _gapPoints = [];
    private HistoricalComboIdentity? _cachedHistoryCombo;
    private SessionHistoryLookupResult? _cachedHistory;
    private DateTimeOffset _cachedHistoryAtUtc;

    public BrowserOverlayModelFactory(SessionHistoryQueryService historyQueryService)
    {
        _historyQueryService = historyQueryService;
        _sessionWeatherBuilder = SessionWeatherOverlayViewModel.CreateBuilder();
        _pitServiceBuilder = PitServiceOverlayViewModel.CreateBuilder();
    }

    public bool TryBuild(
        string overlayId,
        LiveTelemetrySnapshot snapshot,
        ApplicationSettings settings,
        DateTimeOffset now,
        out BrowserOverlayModelResponse response)
    {
        var unitSystem = UnitSystem(settings);
        BrowserOverlayDisplayModel? model = null;
        if (string.Equals(overlayId, StandingsOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = BuildStandings(snapshot, settings, now);
        }
        else if (string.Equals(overlayId, RelativeOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = BuildRelative(snapshot, settings, now);
        }
        else if (string.Equals(overlayId, FuelCalculatorOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = BuildFuel(snapshot, settings, unitSystem, now);
        }
        else if (string.Equals(overlayId, SessionWeatherOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            var viewModel = _sessionWeatherBuilder(snapshot, now, unitSystem);
            var overlay = FindOverlay(settings, SessionWeatherOverlayDefinition.Definition.Id);
            var headerItems = HeaderItems(overlay, snapshot, viewModel.Status);
            model = FromSimple(
                SessionWeatherOverlayDefinition.Definition.Id,
                viewModel,
                headerItems,
                SourceText(overlay, snapshot, viewModel.Source));
        }
        else if (string.Equals(overlayId, PitServiceOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            var viewModel = _pitServiceBuilder(snapshot, now, unitSystem);
            var overlay = FindOverlay(settings, PitServiceOverlayDefinition.Definition.Id);
            var headerItems = HeaderItems(overlay, snapshot, viewModel.Status);
            model = FromSimple(
                PitServiceOverlayDefinition.Definition.Id,
                viewModel,
                headerItems,
                SourceText(overlay, snapshot, viewModel.Source));
        }
        else if (string.Equals(overlayId, InputStateOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            var viewModel = InputStateOverlayViewModel.From(snapshot, now, unitSystem);
            var overlay = FindOverlay(settings, InputStateOverlayDefinition.Definition.Id);
            var headerItems = HeaderItems(overlay, snapshot, viewModel.Status);
            model = FromSimple(
                InputStateOverlayDefinition.Definition.Id,
                viewModel,
                headerItems,
                SourceText(overlay, snapshot, viewModel.Source));
        }
        else if (string.Equals(overlayId, GapToLeaderOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = BuildGapToLeader(snapshot, settings);
        }

        if (model is null)
        {
            response = null!;
            return false;
        }

        response = new BrowserOverlayModelResponse(now, model);
        return true;
    }

    private BrowserOverlayDisplayModel BuildStandings(
        LiveTelemetrySnapshot snapshot,
        ApplicationSettings settings,
        DateTimeOffset now)
    {
        var browserSettings = StandingsBrowserSettings.From(settings);
        var overlay = FindOverlay(settings, StandingsOverlayDefinition.Definition.Id);
        var viewModel = StandingsOverlayViewModel.From(
            snapshot,
            now,
            browserSettings.MaximumRows,
            browserSettings.OtherClassRowsPerClass,
            browserSettings.ClassSeparatorsEnabled);
        var rows = viewModel.Rows
            .Select(row => new BrowserOverlayDisplayRow(
                Cells: browserSettings.Columns
                    .Select(column => StandingsCell(row, column.DataKey))
                    .ToArray(),
                IsReference: row.IsReference,
                IsClassHeader: row.IsClassHeader,
                IsPit: !string.IsNullOrWhiteSpace(row.Pit),
                IsPartial: row.IsPartial,
                IsPendingGrid: row.IsPendingGrid,
                CarClassColorHex: row.CarClassColorHex,
                HeaderTitle: row.IsClassHeader ? row.Driver : null,
                HeaderDetail: row.IsClassHeader ? ClassHeaderDetail(row.Gap, row.Interval) : null))
            .ToArray();
        var headerItems = HeaderItems(overlay, snapshot, viewModel.Status);

        return BrowserOverlayDisplayModel.Table(
            StandingsOverlayDefinition.Definition.Id,
            StandingsOverlayDefinition.Definition.DisplayName,
            BrowserStatus(headerItems, viewModel.Status),
            SourceText(overlay, snapshot, viewModel.Source),
            browserSettings.Columns,
            rows,
            headerItems);
    }

    private static string StandingsCell(StandingsOverlayRowViewModel row, string dataKey)
    {
        return dataKey switch
        {
            OverlayContentColumnSettings.DataClassPosition => row.IsClassHeader ? string.Empty : row.ClassPosition,
            OverlayContentColumnSettings.DataCarNumber => row.IsClassHeader ? string.Empty : row.CarNumber,
            OverlayContentColumnSettings.DataDriver => row.Driver,
            OverlayContentColumnSettings.DataGap => row.IsClassHeader ? row.Gap : row.Gap,
            OverlayContentColumnSettings.DataInterval => row.Interval,
            OverlayContentColumnSettings.DataPit => row.Pit,
            _ => string.Empty
        };
    }

    private BrowserOverlayDisplayModel BuildRelative(
        LiveTelemetrySnapshot snapshot,
        ApplicationSettings settings,
        DateTimeOffset now)
    {
        var browserSettings = RelativeBrowserSettings.From(settings);
        var overlay = FindOverlay(settings, RelativeOverlayDefinition.Definition.Id);
        var viewModel = RelativeOverlayViewModel.From(
            snapshot,
            now,
            browserSettings.CarsAhead,
            browserSettings.CarsBehind);
        var rows = viewModel.Rows
            .Select(row => new BrowserOverlayDisplayRow(
                Cells: browserSettings.Columns
                    .Select(column => RelativeCell(row, column.DataKey))
                    .ToArray(),
                IsReference: row.IsReference,
                IsClassHeader: false,
                IsPit: row.IsPit,
                IsPartial: row.IsPartial,
                IsPendingGrid: false,
                CarClassColorHex: row.ClassColorHex,
                HeaderTitle: null,
                HeaderDetail: null))
            .ToArray();
        var headerItems = HeaderItems(overlay, snapshot, viewModel.Status);

        return BrowserOverlayDisplayModel.Table(
            RelativeOverlayDefinition.Definition.Id,
            RelativeOverlayDefinition.Definition.DisplayName,
            BrowserStatus(headerItems, viewModel.Status),
            SourceText(overlay, snapshot, viewModel.Source),
            browserSettings.Columns,
            rows,
            headerItems);
    }

    private static string RelativeCell(RelativeOverlayRowViewModel row, string dataKey)
    {
        return dataKey switch
        {
            OverlayContentColumnSettings.DataRelativePosition => row.Position,
            OverlayContentColumnSettings.DataDriver => row.Driver,
            OverlayContentColumnSettings.DataGap => row.Gap,
            OverlayContentColumnSettings.DataPit => row.IsPit ? "PIT" : string.Empty,
            _ => string.Empty
        };
    }

    private BrowserOverlayDisplayModel BuildFuel(
        LiveTelemetrySnapshot snapshot,
        ApplicationSettings settings,
        string unitSystem,
        DateTimeOffset now)
    {
        var overlay = FindOverlay(settings, FuelCalculatorOverlayDefinition.Definition.Id);
        var localContext = LiveLocalStrategyContext.ForFuelCalculator(snapshot, now);
        if (!localContext.IsAvailable)
        {
            var waitingHeaderItems = HeaderItems(overlay, snapshot, localContext.StatusText);
            return BrowserOverlayDisplayModel.MetricRows(
                FuelCalculatorOverlayDefinition.Definition.Id,
                FuelCalculatorOverlayDefinition.Definition.DisplayName,
                BrowserStatus(waitingHeaderItems, localContext.StatusText),
                SourceText(overlay, snapshot, "source: waiting"),
                [],
                waitingHeaderItems);
        }

        var history = LookupHistory(snapshot.Combo);
        var strategy = FuelStrategyCalculator.From(snapshot, history);
        var viewModel = FuelCalculatorViewModel.From(
            strategy,
            history,
            overlay?.GetBooleanOption(OverlayOptionKeys.FuelAdvice, defaultValue: true) ?? true,
            unitSystem,
            maximumRows: 8);
        var metrics = new List<BrowserOverlayMetricRow>
        {
            new("Plan", viewModel.Overview, BrowserOverlayTone.Modeled)
        };
        metrics.AddRange(viewModel.Rows.Select(row => new BrowserOverlayMetricRow(
            row.Label,
            string.IsNullOrWhiteSpace(row.Advice) ? row.Value : $"{row.Value} | {row.Advice}",
            BrowserOverlayTone.Modeled)));
        var headerItems = HeaderItems(overlay, snapshot, viewModel.Status);

        return BrowserOverlayDisplayModel.MetricRows(
            FuelCalculatorOverlayDefinition.Definition.Id,
            FuelCalculatorOverlayDefinition.Definition.DisplayName,
            BrowserStatus(headerItems, viewModel.Status),
            SourceText(overlay, snapshot, viewModel.Source),
            metrics,
            headerItems);
    }

    private static BrowserOverlayDisplayModel FromSimple(
        string overlayId,
        SimpleTelemetryOverlayViewModel viewModel,
        IReadOnlyList<BrowserOverlayHeaderItem>? headerItems = null,
        string? source = null)
    {
        headerItems ??= [];
        return BrowserOverlayDisplayModel.MetricRows(
            overlayId,
            viewModel.Title,
            BrowserStatus(headerItems, viewModel.Status),
            source ?? viewModel.Source,
            viewModel.Rows
                .Select(row => new BrowserOverlayMetricRow(
                    row.Label,
                    row.Value,
                    ToneName(row.Tone)))
                .ToArray(),
            headerItems);
    }

    private BrowserOverlayDisplayModel BuildGapToLeader(LiveTelemetrySnapshot snapshot, ApplicationSettings settings)
    {
        var overlay = FindOverlay(settings, GapToLeaderOverlayDefinition.Definition.Id);
        var gap = GapToLeaderLiveModelAdapter.Select(snapshot);
        if (gap.HasData
            && GapToLeaderLiveModelAdapter.SelectFocusedTrendPointSeconds(snapshot, gap) is { } seconds
            && ShouldAcceptGapPoint(snapshot, seconds))
        {
            _gapPoints.Add(seconds);
            if (_gapPoints.Count > 120)
            {
                _gapPoints.RemoveRange(0, _gapPoints.Count - 120);
            }
        }

        var metrics = new[]
        {
            new BrowserOverlayMetricRow("Class pos", FormatPosition(gap.ReferenceClassPosition), BrowserOverlayTone.Live),
            new BrowserOverlayMetricRow("Class leader", FormatGap(gap.ClassLeaderGap), BrowserOverlayTone.Live)
        };

        var status = gap.HasData ? "live | race gap" : "waiting for timing";
        var headerItems = HeaderItems(overlay, snapshot, status);
        return new BrowserOverlayDisplayModel(
            GapToLeaderOverlayDefinition.Definition.Id,
            GapToLeaderOverlayDefinition.Definition.DisplayName,
            BrowserStatus(headerItems, status),
            SourceText(overlay, snapshot, gap.HasData ? $"source: live gap telemetry | cars {gap.ClassCars.Count}" : "source: waiting"),
            "graph",
            Columns: [],
            Rows: [],
            Metrics: metrics,
            Points: _gapPoints.ToArray(),
            HeaderItems: headerItems);
    }

    private bool ShouldAcceptGapPoint(LiveTelemetrySnapshot snapshot, double seconds)
    {
        if (!IsFinite(seconds) || seconds < 0d)
        {
            return false;
        }

        if (_gapPoints.Count == 0)
        {
            return true;
        }

        var previous = _gapPoints[^1];
        var lapReferenceSeconds = GapToLeaderLiveModelAdapter.SelectLapReferenceSeconds(snapshot);
        var maximumJump = Math.Max(30d, Math.Min(180d, (lapReferenceSeconds ?? 90d) * 0.5d));
        return Math.Abs(seconds - previous) <= maximumJump;
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

    private static OverlaySettings? FindOverlay(ApplicationSettings settings, string overlayId)
    {
        return settings.Overlays.FirstOrDefault(
            overlay => string.Equals(overlay.Id, overlayId, StringComparison.OrdinalIgnoreCase));
    }

    private static string UnitSystem(ApplicationSettings settings)
    {
        return string.Equals(settings.General.UnitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
            ? "Imperial"
            : "Metric";
    }

    private static IReadOnlyList<BrowserOverlayHeaderItem> HeaderItems(
        OverlaySettings? overlay,
        LiveTelemetrySnapshot snapshot,
        string status)
    {
        var items = new List<BrowserOverlayHeaderItem>();
        if (overlay is null || OverlayChromeSettings.ShowHeaderStatus(overlay, snapshot))
        {
            items.Add(new BrowserOverlayHeaderItem("status", status));
        }

        if (overlay is null || OverlayChromeSettings.ShowHeaderTimeRemaining(overlay, snapshot))
        {
            var timeRemaining = OverlayHeaderTimeFormatter.FormatTimeRemaining(snapshot);
            if (!string.IsNullOrWhiteSpace(timeRemaining))
            {
                items.Add(new BrowserOverlayHeaderItem("timeRemaining", timeRemaining));
            }
        }

        return items;
    }

    private static string SourceText(OverlaySettings? overlay, LiveTelemetrySnapshot snapshot, string source)
    {
        return overlay is null || OverlayChromeSettings.ShowFooterSource(overlay, snapshot)
            ? source
            : string.Empty;
    }

    private static string BrowserStatus(IReadOnlyList<BrowserOverlayHeaderItem> headerItems, string fallback)
    {
        return headerItems.FirstOrDefault(item => string.Equals(item.Key, "status", StringComparison.OrdinalIgnoreCase))?.Value
            ?? headerItems.FirstOrDefault()?.Value
            ?? fallback;
    }

    private static string ClassHeaderDetail(params string[] parts)
    {
        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FormatPosition(int? position)
    {
        return position is > 0
            ? position.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "--";
    }

    private static string FormatGap(LiveGapValue gap)
    {
        if (gap.IsLeader)
        {
            return "LEADER";
        }

        return FormatGap(gap.Seconds, gap.Laps);
    }

    private static string FormatGap(double? seconds, double? laps)
    {
        if (seconds is { } secondsValue && IsFinite(secondsValue))
        {
            return FormatSignedSeconds(secondsValue);
        }

        if (laps is { } lapsValue && IsFinite(lapsValue))
        {
            return $"+{lapsValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} lap";
        }

        return "--";
    }

    private static string FormatSignedSeconds(double? seconds)
    {
        return seconds is { } value && IsFinite(value)
            ? $"{(value > 0d ? "+" : string.Empty)}{value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}"
            : "--";
    }

    private static string ToneName(SimpleTelemetryTone tone)
    {
        return tone.ToString().ToLowerInvariant();
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record BrowserOverlayModelResponse(
    DateTimeOffset GeneratedAtUtc,
    BrowserOverlayDisplayModel Model);

internal sealed record BrowserOverlayDisplayModel(
    string OverlayId,
    string Title,
    string Status,
    string Source,
    string BodyKind,
    IReadOnlyList<OverlayContentBrowserColumn> Columns,
    IReadOnlyList<BrowserOverlayDisplayRow> Rows,
    IReadOnlyList<BrowserOverlayMetricRow> Metrics,
    IReadOnlyList<double> Points,
    IReadOnlyList<BrowserOverlayHeaderItem> HeaderItems)
{
    public static BrowserOverlayDisplayModel Table(
        string overlayId,
        string title,
        string status,
        string source,
        IReadOnlyList<OverlayContentBrowserColumn> columns,
        IReadOnlyList<BrowserOverlayDisplayRow> rows,
        IReadOnlyList<BrowserOverlayHeaderItem>? headerItems = null)
    {
        return new BrowserOverlayDisplayModel(
            overlayId,
            title,
            status,
            source,
            "table",
            columns,
            rows,
            [],
            [],
            headerItems ?? []);
    }

    public static BrowserOverlayDisplayModel MetricRows(
        string overlayId,
        string title,
        string status,
        string source,
        IReadOnlyList<BrowserOverlayMetricRow> metrics,
        IReadOnlyList<BrowserOverlayHeaderItem>? headerItems = null)
    {
        return new BrowserOverlayDisplayModel(
            overlayId,
            title,
            status,
            source,
            "metrics",
            [],
            [],
            metrics,
            [],
            headerItems ?? []);
    }
}

internal sealed record BrowserOverlayDisplayRow(
    IReadOnlyList<string> Cells,
    bool IsReference,
    bool IsClassHeader,
    bool IsPit,
    bool IsPartial,
    bool IsPendingGrid,
    string? CarClassColorHex,
    string? HeaderTitle,
    string? HeaderDetail);

internal sealed record BrowserOverlayHeaderItem(
    string Key,
    string Value);

internal sealed record BrowserOverlayMetricRow(
    string Label,
    string Value,
    string Tone);

internal static class BrowserOverlayTone
{
    public const string Live = "live";
    public const string Modeled = "modeled";
}
