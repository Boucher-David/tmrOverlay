using System.Globalization;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.SimpleTelemetry;

internal sealed record SimpleTelemetryOverlayViewModel(
    string Title,
    string Status,
    string Source,
    SimpleTelemetryTone Tone,
    IReadOnlyList<SimpleTelemetryRowViewModel> Rows)
{
    public static SimpleTelemetryOverlayViewModel Waiting(string title, string status)
    {
        return new SimpleTelemetryOverlayViewModel(
            Title: title,
            Status: status,
            Source: "source: waiting",
            Tone: SimpleTelemetryTone.Waiting,
            Rows: []);
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
    SimpleTelemetryTone Tone = SimpleTelemetryTone.Normal);

internal enum SimpleTelemetryTone
{
    Normal,
    Waiting,
    Info,
    Success,
    Warning,
    Error
}
