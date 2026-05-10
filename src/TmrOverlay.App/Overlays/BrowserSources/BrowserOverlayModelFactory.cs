using TmrOverlay.App.History;
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
            model = BuildFuel(snapshot, settings, unitSystem);
        }
        else if (string.Equals(overlayId, SessionWeatherOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = FromSimple(
                SessionWeatherOverlayDefinition.Definition.Id,
                _sessionWeatherBuilder(snapshot, now, unitSystem));
        }
        else if (string.Equals(overlayId, PitServiceOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = FromSimple(
                PitServiceOverlayDefinition.Definition.Id,
                _pitServiceBuilder(snapshot, now, unitSystem));
        }
        else if (string.Equals(overlayId, InputStateOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = FromSimple(
                InputStateOverlayDefinition.Definition.Id,
                InputStateOverlayViewModel.From(snapshot, now, unitSystem));
        }
        else if (string.Equals(overlayId, GapToLeaderOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            model = BuildGapToLeader(snapshot);
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
                CarClassColorHex: row.CarClassColorHex,
                HeaderTitle: row.IsClassHeader ? row.Driver : null,
                HeaderDetail: row.IsClassHeader ? ClassHeaderDetail(row.Gap, row.Interval) : null))
            .ToArray();

        return BrowserOverlayDisplayModel.Table(
            StandingsOverlayDefinition.Definition.Id,
            StandingsOverlayDefinition.Definition.DisplayName,
            viewModel.Status,
            viewModel.Source,
            browserSettings.Columns,
            rows);
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
                CarClassColorHex: row.ClassColorHex,
                HeaderTitle: null,
                HeaderDetail: null))
            .ToArray();

        return BrowserOverlayDisplayModel.Table(
            RelativeOverlayDefinition.Definition.Id,
            RelativeOverlayDefinition.Definition.DisplayName,
            viewModel.Status,
            viewModel.Source,
            browserSettings.Columns,
            rows);
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
        string unitSystem)
    {
        var overlay = FindOverlay(settings, FuelCalculatorOverlayDefinition.Definition.Id);
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

        return BrowserOverlayDisplayModel.MetricRows(
            FuelCalculatorOverlayDefinition.Definition.Id,
            FuelCalculatorOverlayDefinition.Definition.DisplayName,
            viewModel.Status,
            viewModel.Source,
            metrics);
    }

    private static BrowserOverlayDisplayModel FromSimple(
        string overlayId,
        SimpleTelemetryOverlayViewModel viewModel)
    {
        return BrowserOverlayDisplayModel.MetricRows(
            overlayId,
            viewModel.Title,
            viewModel.Status,
            viewModel.Source,
            viewModel.Rows
                .Select(row => new BrowserOverlayMetricRow(
                    row.Label,
                    row.Value,
                    ToneName(row.Tone)))
                .ToArray());
    }

    private BrowserOverlayDisplayModel BuildGapToLeader(LiveTelemetrySnapshot snapshot)
    {
        var gap = GapToLeaderLiveModelAdapter.Select(snapshot);
        if (gap.HasData && gap.ClassLeaderGap.Seconds is { } seconds && IsFinite(seconds))
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

        return new BrowserOverlayDisplayModel(
            GapToLeaderOverlayDefinition.Definition.Id,
            GapToLeaderOverlayDefinition.Definition.DisplayName,
            gap.HasData ? "live | race gap" : "waiting for timing",
            gap.HasData ? $"source: live gap telemetry | cars {gap.ClassCars.Count}" : "source: waiting",
            "graph",
            Columns: [],
            Rows: [],
            Metrics: metrics,
            Points: _gapPoints.ToArray());
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
    IReadOnlyList<double> Points)
{
    public static BrowserOverlayDisplayModel Table(
        string overlayId,
        string title,
        string status,
        string source,
        IReadOnlyList<OverlayContentBrowserColumn> columns,
        IReadOnlyList<BrowserOverlayDisplayRow> rows)
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
            []);
    }

    public static BrowserOverlayDisplayModel MetricRows(
        string overlayId,
        string title,
        string status,
        string source,
        IReadOnlyList<BrowserOverlayMetricRow> metrics)
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
            []);
    }
}

internal sealed record BrowserOverlayDisplayRow(
    IReadOnlyList<string> Cells,
    bool IsReference,
    bool IsClassHeader,
    bool IsPit,
    bool IsPartial,
    string? CarClassColorHex,
    string? HeaderTitle,
    string? HeaderDetail);

internal sealed record BrowserOverlayMetricRow(
    string Label,
    string Value,
    string Tone);

internal static class BrowserOverlayTone
{
    public const string Live = "live";
    public const string Modeled = "modeled";
}
