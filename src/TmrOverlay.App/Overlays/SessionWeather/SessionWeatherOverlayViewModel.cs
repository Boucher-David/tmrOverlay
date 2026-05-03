using System.Globalization;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.SessionWeather;

internal static class SessionWeatherOverlayViewModel
{
    public static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem)
    {
        if (!SimpleTelemetryOverlayViewModel.IsFresh(snapshot, now, out var waitingStatus))
        {
            return SimpleTelemetryOverlayViewModel.Waiting("Session / Weather", waitingStatus);
        }

        var session = snapshot.Models.Session;
        var weather = snapshot.Models.Weather;
        if (!session.HasData && !weather.HasData)
        {
            return SimpleTelemetryOverlayViewModel.Waiting("Session / Weather", "waiting for session telemetry");
        }

        var status = BuildStatus(session, weather);
        var tone = weather.DeclaredWetSurfaceMismatch == true
            ? SimpleTelemetryTone.Warning
            : weather.WeatherDeclaredWet || weather.TrackWetness is > 0
                ? SimpleTelemetryTone.Info
                : SimpleTelemetryTone.Normal;
        var rows = new[]
        {
            new SimpleTelemetryRowViewModel("Session", FormatSession(session)),
            new SimpleTelemetryRowViewModel("Clock", FormatClock(session)),
            new SimpleTelemetryRowViewModel("Laps", FormatLaps(session)),
            new SimpleTelemetryRowViewModel("Track", FormatTrack(session)),
            new SimpleTelemetryRowViewModel("Temps", FormatTemps(weather, unitSystem)),
            new SimpleTelemetryRowViewModel("Surface", FormatSurface(weather), tone),
            new SimpleTelemetryRowViewModel("Sky", FormatSky(weather)),
            new SimpleTelemetryRowViewModel("Rubber", FormatRubber(weather))
        };

        return new SimpleTelemetryOverlayViewModel(
            Title: "Session / Weather",
            Status: status,
            Source: weather.HasData ? "source: session + weather telemetry" : "source: session telemetry",
            Tone: tone,
            Rows: rows);
    }

    private static string BuildStatus(LiveSessionModel session, LiveWeatherModel weather)
    {
        if (weather.DeclaredWetSurfaceMismatch == true)
        {
            return "wet mismatch";
        }

        if (weather.WeatherDeclaredWet || weather.TrackWetness is > 0)
        {
            return weather.TrackWetnessLabel ?? "wet declared";
        }

        return string.IsNullOrWhiteSpace(session.SessionType)
            ? "live session"
            : session.SessionType.Trim();
    }

    private static string FormatSession(LiveSessionModel session)
    {
        return SimpleTelemetryOverlayViewModel.JoinAvailable(
            Trim(session.SessionType),
            Trim(session.SessionName),
            session.TeamRacing == true ? "team" : null);
    }

    private static string FormatClock(LiveSessionModel session)
    {
        var elapsed = SimpleTelemetryOverlayViewModel.FormatDuration(session.SessionTimeSeconds, compact: true);
        var remain = SimpleTelemetryOverlayViewModel.FormatDuration(session.SessionTimeRemainSeconds, compact: true);
        if (elapsed == "--" && remain == "--")
        {
            return "--";
        }

        return $"{elapsed} elapsed | {remain} left";
    }

    private static string FormatLaps(LiveSessionModel session)
    {
        var remain = session.SessionLapsRemain is { } left && left >= 0
            ? left.ToString(CultureInfo.InvariantCulture)
            : "--";
        var total = session.SessionLapsTotal is { } totalLaps && totalLaps > 0
            ? totalLaps.ToString(CultureInfo.InvariantCulture)
            : session.RaceLaps is { } raceLaps && raceLaps > 0
                ? raceLaps.ToString(CultureInfo.InvariantCulture)
                : "--";
        return remain == "--" && total == "--" ? "--" : $"{remain} left | {total} total";
    }

    private static string FormatTrack(LiveSessionModel session)
    {
        string? length = session.TrackLengthKm is { } km && SimpleTelemetryOverlayViewModel.IsFinite(km)
            ? $"{km.ToString("0.00", CultureInfo.InvariantCulture)} km"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(Trim(session.TrackDisplayName), length);
    }

    private static string FormatTemps(LiveWeatherModel weather, string unitSystem)
    {
        var air = SimpleTelemetryOverlayViewModel.FormatTemperature(weather.AirTempC, unitSystem);
        var track = SimpleTelemetryOverlayViewModel.FormatTemperature(weather.TrackTempCrewC, unitSystem);
        return air == "--" && track == "--" ? "--" : $"air {air} | track {track}";
    }

    private static string FormatSurface(LiveWeatherModel weather)
    {
        var wetness = weather.TrackWetnessLabel ?? (weather.TrackWetness is { } raw
            ? raw.ToString(CultureInfo.InvariantCulture)
            : "--");
        if (weather.WeatherDeclaredWet)
        {
            return wetness == "--" ? "declared wet" : $"{wetness} | declared wet";
        }

        return wetness;
    }

    private static string FormatSky(LiveWeatherModel weather)
    {
        string? precipitation = weather.PrecipitationPercent is { } value
            ? $"{value.ToString("0", CultureInfo.InvariantCulture)}% precip"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(Trim(weather.SkiesLabel), Trim(weather.WeatherType), precipitation);
    }

    private static string FormatRubber(LiveWeatherModel weather)
    {
        return Trim(weather.RubberState) ?? "--";
    }

    private static string? Trim(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
