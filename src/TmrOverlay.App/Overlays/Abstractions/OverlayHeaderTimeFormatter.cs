using System.Globalization;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Abstractions;

internal static class OverlayHeaderTimeFormatter
{
    public static string FormatTimeRemaining(LiveTelemetrySnapshot snapshot)
    {
        var session = snapshot.Models.Session;
        return FormatTimeRemaining(
            session.SessionTimeRemainSeconds,
            session.SessionState,
            OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot));
    }

    internal static string FormatTimeRemaining(
        double? seconds,
        int? sessionState,
        OverlaySessionKind? sessionKind)
    {
        if (seconds is not { } value || !IsFinite(value) || value < 0d)
        {
            return string.Empty;
        }

        return IsRacePreGreenCountdown(sessionKind, sessionState)
            ? FormatMinutesSeconds(value)
            : FormatHoursMinutes(value);
    }

    private static bool IsRacePreGreenCountdown(OverlaySessionKind? sessionKind, int? sessionState)
    {
        return sessionKind == OverlaySessionKind.Race && sessionState is >= 1 and <= 3;
    }

    private static string FormatMinutesSeconds(double seconds)
    {
        var totalSeconds = (int)Math.Ceiling(Math.Max(0d, seconds));
        var minutes = totalSeconds / 60;
        var remainingSeconds = totalSeconds % 60;
        return $"{minutes.ToString("00", CultureInfo.InvariantCulture)}:{remainingSeconds.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static string FormatHoursMinutes(double seconds)
    {
        var totalMinutes = (int)Math.Ceiling(Math.Max(0d, seconds) / 60d);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return $"{hours.ToString("00", CultureInfo.InvariantCulture)}:{minutes.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
