using System.Globalization;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.SimpleTelemetry;

internal sealed record SimpleTelemetryOverlayViewModel(
    string Title,
    string Status,
    string Source,
    SimpleTelemetryTone Tone,
    IReadOnlyList<SimpleTelemetryRowViewModel> Rows,
    IReadOnlyList<SimpleTelemetryGridSectionViewModel> Sections)
{
    public IReadOnlyList<SimpleTelemetryMetricSectionViewModel> MetricSections { get; init; } = [];

    public SimpleTelemetryOverlayViewModel(
        string Title,
        string Status,
        string Source,
        SimpleTelemetryTone Tone,
        IReadOnlyList<SimpleTelemetryRowViewModel> Rows)
        : this(Title, Status, Source, Tone, Rows, [])
    {
    }

    public SimpleTelemetryOverlayViewModel(
        string Title,
        string Status,
        string Source,
        SimpleTelemetryTone Tone,
        IReadOnlyList<SimpleTelemetryRowViewModel> Rows,
        IReadOnlyList<SimpleTelemetryMetricSectionViewModel> MetricSections,
        IReadOnlyList<SimpleTelemetryGridSectionViewModel> Sections)
        : this(Title, Status, Source, Tone, Rows, Sections)
    {
        this.MetricSections = MetricSections;
    }

    public static SimpleTelemetryOverlayViewModel Waiting(string title, string status)
    {
        return new SimpleTelemetryOverlayViewModel(
            Title: title,
            Status: status,
            Source: "source: waiting",
            Tone: SimpleTelemetryTone.Waiting,
            Rows: []);
    }

    public static SimpleTelemetryOverlayViewModel ApplyContentSettings(
        SimpleTelemetryOverlayViewModel model,
        OverlaySettings? settings,
        OverlayContentDefinition contentDefinition)
    {
        if (settings is null || contentDefinition.Blocks is not { Count: > 0 } blocks)
        {
            return model;
        }

        if (blocks.All(block => OverlayContentColumnSettings.BlockEnabled(settings, block)))
        {
            return model;
        }

        var blockById = blocks.ToDictionary(block => block.Id, StringComparer.OrdinalIgnoreCase);
        var rows = FilterRows(model.Rows).ToArray();
        var metricSections = model.MetricSections
            .Select(section => new SimpleTelemetryMetricSectionViewModel(section.Title, FilterRows(section.Rows).ToArray()))
            .Where(section => section.Rows.Count > 0)
            .ToArray();

        return model with
        {
            Rows = rows,
            MetricSections = metricSections
        };

        IEnumerable<SimpleTelemetryRowViewModel> FilterRows(IReadOnlyList<SimpleTelemetryRowViewModel> sourceRows)
        {
            foreach (var row in sourceRows)
            {
                if (FilterRow(row) is { } filtered)
                {
                    yield return filtered;
                }
            }
        }

        SimpleTelemetryRowViewModel? FilterRow(SimpleTelemetryRowViewModel row)
        {
            if (row.Segments.Count == 0)
            {
                return row;
            }

            var filteredSegments = row.Segments
                .Where(SegmentEnabled)
                .ToArray();
            if (filteredSegments.Length == 0)
            {
                return null;
            }

            if (filteredSegments.Length == row.Segments.Count)
            {
                return row;
            }

            return row with
            {
                Value = JoinAvailable(filteredSegments.Select(segment => segment.Value).ToArray()),
                Segments = filteredSegments
            };
        }

        bool SegmentEnabled(SimpleTelemetryMetricSegmentViewModel segment)
        {
            if (string.IsNullOrWhiteSpace(segment.Key)
                || !blockById.TryGetValue(segment.Key, out var block))
            {
                return true;
            }

            return OverlayContentColumnSettings.BlockEnabled(settings, block);
        }
    }

    public static bool IsFresh(LiveTelemetrySnapshot snapshot, DateTimeOffset now, out string waitingStatus)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        waitingStatus = availability.IsAvailable ? string.Empty : availability.StatusText;
        return availability.IsAvailable;
    }

    public static string FormatDuration(double? seconds, bool compact = false)
    {
        if (seconds is not { } value || !IsFinite(value) || value < 0d)
        {
            return "--";
        }

        var totalSeconds = (int)Math.Round(value);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var remainingSeconds = totalSeconds % 60;
        return compact && hours == 0
            ? $"{minutes.ToString(CultureInfo.InvariantCulture)}:{remainingSeconds.ToString("00", CultureInfo.InvariantCulture)}"
            : $"{hours.ToString(CultureInfo.InvariantCulture)}:{minutes.ToString("00", CultureInfo.InvariantCulture)}:{remainingSeconds.ToString("00", CultureInfo.InvariantCulture)}";
    }

    public static string FormatTemperature(double? celsius, string unitSystem)
    {
        if (celsius is not { } value || !IsFinite(value))
        {
            return "--";
        }

        if (IsImperial(unitSystem))
        {
            return $"{(value * 9d / 5d + 32d).ToString("0", CultureInfo.InvariantCulture)} F";
        }

        return $"{value.ToString("0", CultureInfo.InvariantCulture)} C";
    }

    public static string FormatSpeed(double? metersPerSecond, string unitSystem)
    {
        if (metersPerSecond is not { } value || !IsFinite(value))
        {
            return "--";
        }

        return IsImperial(unitSystem)
            ? $"{(value * 2.2369362921d).ToString("0", CultureInfo.InvariantCulture)} mph"
            : $"{(value * 3.6d).ToString("0", CultureInfo.InvariantCulture)} km/h";
    }

    public static string FormatFuelVolume(double? liters, string unitSystem)
    {
        if (liters is not { } value || !IsFinite(value))
        {
            return "--";
        }

        return IsImperial(unitSystem)
            ? $"{(value * 0.2641720524d).ToString("0.0", CultureInfo.InvariantCulture)} gal"
            : $"{value.ToString("0.0", CultureInfo.InvariantCulture)} L";
    }

    public static string FormatPressure(double? bar, string unitSystem)
    {
        if (bar is not { } value || !IsFinite(value))
        {
            return "--";
        }

        return IsImperial(unitSystem)
            ? $"{(value * 14.5037738d).ToString("0", CultureInfo.InvariantCulture)} psi"
            : $"{value.ToString("0.0", CultureInfo.InvariantCulture)} bar";
    }

    public static string FormatPercent(double? value)
    {
        return value is { } number && IsFinite(number)
            ? $"{(number * 100d).ToString("0", CultureInfo.InvariantCulture)}%"
            : "--";
    }

    public static string FormatInteger(int? value)
    {
        return value is { } number ? number.ToString(CultureInfo.InvariantCulture) : "--";
    }

    public static string JoinAvailable(params string?[] values)
    {
        var parts = values
            .Where(value => !string.IsNullOrWhiteSpace(value) && value != "--")
            .Select(value => value!.Trim())
            .ToArray();
        return parts.Length == 0 ? "--" : string.Join(" | ", parts);
    }

    public static bool IsImperial(string unitSystem)
    {
        return string.Equals(unitSystem, "Imperial", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record SimpleTelemetryRowViewModel(
    string Label,
    string Value,
    SimpleTelemetryTone Tone = SimpleTelemetryTone.Normal)
{
    public IReadOnlyList<SimpleTelemetryMetricSegmentViewModel> Segments { get; init; } = [];

    public string? RowColorHex { get; init; }
}

internal sealed record SimpleTelemetryMetricSegmentViewModel(
    string Label,
    string Value,
    SimpleTelemetryTone Tone = SimpleTelemetryTone.Normal,
    string? AccentHex = null,
    double? RotationDegrees = null,
    string? Key = null);

internal sealed record SimpleTelemetryMetricSectionViewModel(
    string Title,
    IReadOnlyList<SimpleTelemetryRowViewModel> Rows);

internal sealed record SimpleTelemetryGridSectionViewModel(
    string Title,
    IReadOnlyList<string> Headers,
    IReadOnlyList<SimpleTelemetryGridRowViewModel> Rows);

internal sealed record SimpleTelemetryGridRowViewModel(
    string Label,
    IReadOnlyList<SimpleTelemetryGridCellViewModel> Cells,
    SimpleTelemetryTone Tone = SimpleTelemetryTone.Normal);

internal sealed record SimpleTelemetryGridCellViewModel(
    string Value,
    SimpleTelemetryTone Tone = SimpleTelemetryTone.Normal);

internal enum SimpleTelemetryTone
{
    Normal,
    Waiting,
    Info,
    Success,
    Modeled,
    Warning,
    Error
}
