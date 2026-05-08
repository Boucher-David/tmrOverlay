using System.Globalization;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.SessionWeather;

internal static class SessionWeatherOverlayViewModel
{
    private static readonly TimeSpan ChangeHighlightDuration = TimeSpan.FromSeconds(45);
    private const int MaxDisplayLapCount = 1000;

    public static Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> CreateBuilder()
    {
        var changeTracker = new ChangeTracker(ChangeHighlightDuration);
        return (snapshot, now, unitSystem) => From(snapshot, now, unitSystem, changeTracker);
    }

    public static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem)
    {
        return From(snapshot, now, unitSystem, changeTracker: null);
    }

    private static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem,
        ChangeTracker? changeTracker)
    {
        if (!SimpleTelemetryOverlayViewModel.IsFresh(snapshot, now, out var waitingStatus))
        {
            changeTracker?.Reset();
            return SimpleTelemetryOverlayViewModel.Waiting("Session / Weather", waitingStatus);
        }

        var session = snapshot.Models.Session;
        var weather = snapshot.Models.Weather;
        var raceProgress = snapshot.Models.RaceProgress;
        var raceProjection = snapshot.Models.RaceProjection;
        if (!session.HasData && !weather.HasData)
        {
            changeTracker?.Reset();
            return SimpleTelemetryOverlayViewModel.Waiting("Session / Weather", "waiting for session telemetry");
        }

        var status = BuildStatus(session, weather);
        var baseTone = weather.DeclaredWetSurfaceMismatch == true
            ? SimpleTelemetryTone.Warning
            : HasWetSurfaceSignal(weather)
                ? SimpleTelemetryTone.Info
                : SimpleTelemetryTone.Normal;

        var temps = FormatTemps(weather, unitSystem);
        var surface = FormatSurface(weather);
        var sky = FormatSky(weather);
        var wind = FormatWindAtmosphere(weather, unitSystem);
        var tempsChanged = IsChanged(changeTracker, "temps", temps, now);
        var surfaceChanged = IsChanged(changeTracker, "surface", surface, now);
        var skyChanged = IsChanged(changeTracker, "sky", sky, now);
        var windChanged = IsChanged(changeTracker, "wind", wind, now);
        var changeActive = tempsChanged || surfaceChanged || skyChanged || windChanged;
        var tone = StrongestTone(baseTone, changeActive ? SimpleTelemetryTone.Info : SimpleTelemetryTone.Normal);
        var rows = new[]
        {
            new SimpleTelemetryRowViewModel("Session", FormatSession(session)),
            new SimpleTelemetryRowViewModel("Clock", FormatClock(session)),
            new SimpleTelemetryRowViewModel("Laps", FormatLaps(session, raceProgress, raceProjection)),
            new SimpleTelemetryRowViewModel("Track", FormatTrack(session)),
            new SimpleTelemetryRowViewModel("Temps", temps, tempsChanged ? SimpleTelemetryTone.Info : SimpleTelemetryTone.Normal),
            new SimpleTelemetryRowViewModel("Surface", surface, StrongestTone(baseTone, surfaceChanged ? SimpleTelemetryTone.Info : SimpleTelemetryTone.Normal)),
            new SimpleTelemetryRowViewModel("Sky", sky, skyChanged ? SimpleTelemetryTone.Info : SimpleTelemetryTone.Normal),
            new SimpleTelemetryRowViewModel("Wind", wind, windChanged ? SimpleTelemetryTone.Info : SimpleTelemetryTone.Normal)
        };

        return new SimpleTelemetryOverlayViewModel(
            Title: "Session / Weather",
            Status: status,
            Source: weather.HasData ? "source: session + live weather telemetry" : "source: session telemetry",
            Tone: tone,
            Rows: rows);
    }

    private static string BuildStatus(LiveSessionModel session, LiveWeatherModel weather)
    {
        if (weather.DeclaredWetSurfaceMismatch == true)
        {
            return "wet mismatch";
        }

        if (HasWetSurfaceSignal(weather))
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

    private static string FormatLaps(
        LiveSessionModel session,
        LiveRaceProgressModel raceProgress,
        LiveRaceProjectionModel raceProjection)
    {
        var remain = FormatLapCount(session.SessionLapsRemain)
            ?? FormatEstimatedLapCount(raceProjection.EstimatedTeamLapsRemaining)
            ?? FormatEstimatedLapCount(raceProgress.RaceLapsRemaining);
        var total = FormatLapCount(session.SessionLapsTotal)
            ?? FormatEstimatedTotalLaps(raceProjection)
            ?? FormatEstimatedTotalLaps(raceProgress)
            ?? FormatLapCount(session.RaceLaps);
        return remain is null && total is null ? "--" : $"{remain ?? "--"} left | {total ?? "--"} total";
    }

    private static string? FormatEstimatedLapCount(double? laps)
    {
        return laps is { } value && SimpleTelemetryOverlayViewModel.IsFinite(value) && value >= 0d && value <= MaxDisplayLapCount
            ? $"{value.ToString("0.#", CultureInfo.InvariantCulture)} est"
            : null;
    }

    private static string? FormatEstimatedTotalLaps(LiveRaceProgressModel raceProgress)
    {
        if (raceProgress.RaceLapsRemaining is not { } remaining
            || !SimpleTelemetryOverlayViewModel.IsFinite(remaining)
            || remaining < 0d
            || remaining > MaxDisplayLapCount)
        {
            return null;
        }

        var progress = raceProgress.OverallLeaderProgressLaps
            ?? raceProgress.ClassLeaderProgressLaps
            ?? raceProgress.StrategyCarProgressLaps;
        return progress is { } value && SimpleTelemetryOverlayViewModel.IsFinite(value) && value >= 0d
            ? $"{Math.Ceiling(value + remaining).ToString(CultureInfo.InvariantCulture)} est"
            : null;
    }

    private static string? FormatEstimatedTotalLaps(LiveRaceProjectionModel raceProjection)
    {
        return raceProjection.EstimatedFinishLap is { } value
            && SimpleTelemetryOverlayViewModel.IsFinite(value)
            && value >= 0d
            && value <= MaxDisplayLapCount
                ? $"{Math.Ceiling(value).ToString(CultureInfo.InvariantCulture)} est"
                : null;
    }

    private static string? FormatLapCount(int? laps)
    {
        return laps is { } value && value is > 0 and <= MaxDisplayLapCount
            ? value.ToString(CultureInfo.InvariantCulture)
            : null;
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
        var rubber = Trim(weather.RubberState) is { } value ? $"rubber {value}" : null;
        if (weather.WeatherDeclaredWet == true)
        {
            return SimpleTelemetryOverlayViewModel.JoinAvailable(wetness, "declared wet", rubber);
        }

        return SimpleTelemetryOverlayViewModel.JoinAvailable(wetness, rubber);
    }

    private static string FormatSky(LiveWeatherModel weather)
    {
        string? precipitation = weather.PrecipitationPercent is { } value
            ? $"rain {value.ToString("0", CultureInfo.InvariantCulture)}%"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(Trim(weather.SkiesLabel), Trim(weather.WeatherType), precipitation);
    }

    private static string FormatWindAtmosphere(LiveWeatherModel weather, string unitSystem)
    {
        var windSpeed = SimpleTelemetryOverlayViewModel.FormatSpeed(weather.WindVelocityMetersPerSecond, unitSystem);
        var windDirection = FormatCardinalDirection(weather.WindDirectionRadians);
        var wind = windSpeed == "--" && windDirection is null
            ? null
            : SimpleTelemetryOverlayViewModel.JoinAvailable(windDirection, windSpeed == "--" ? null : windSpeed);
        var humidity = FormatPercentValue(weather.RelativeHumidityPercent, "hum");
        var fog = FormatPercentValue(weather.FogLevelPercent, "fog");
        return SimpleTelemetryOverlayViewModel.JoinAvailable(wind, humidity, fog);
    }

    private static bool HasWetSurfaceSignal(LiveWeatherModel weather)
    {
        return weather.WeatherDeclaredWet == true || weather.TrackWetness is > 1;
    }

    private static string? Trim(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FormatPercentValue(double? value, string label)
    {
        return value is { } number && SimpleTelemetryOverlayViewModel.IsFinite(number)
            ? $"{label} {number.ToString("0", CultureInfo.InvariantCulture)}%"
            : null;
    }

    private static string? FormatCardinalDirection(double? radians)
    {
        if (radians is not { } value || !SimpleTelemetryOverlayViewModel.IsFinite(value))
        {
            return null;
        }

        var degrees = value * 180d / Math.PI;
        degrees %= 360d;
        if (degrees < 0d)
        {
            degrees += 360d;
        }

        var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var index = (int)Math.Round(degrees / 45d, MidpointRounding.AwayFromZero) % directions.Length;
        return directions[index];
    }

    private static bool IsChanged(ChangeTracker? tracker, string key, string value, DateTimeOffset now)
    {
        return tracker?.IsHighlighted(key, value, now) == true;
    }

    private static SimpleTelemetryTone StrongestTone(SimpleTelemetryTone left, SimpleTelemetryTone right)
    {
        return Weight(left) >= Weight(right) ? left : right;
    }

    private static int Weight(SimpleTelemetryTone tone)
    {
        return tone switch
        {
            SimpleTelemetryTone.Error => 50,
            SimpleTelemetryTone.Warning => 40,
            SimpleTelemetryTone.Info => 30,
            SimpleTelemetryTone.Success => 20,
            SimpleTelemetryTone.Waiting => 10,
            _ => 0
        };
    }

    private sealed class ChangeTracker
    {
        private readonly TimeSpan _duration;
        private readonly Dictionary<string, string> _lastValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _highlightUntil = new(StringComparer.OrdinalIgnoreCase);

        public ChangeTracker(TimeSpan duration)
        {
            _duration = duration;
        }

        public bool IsHighlighted(string key, string value, DateTimeOffset now)
        {
            var normalized = value.Trim();
            if (_lastValues.TryGetValue(key, out var previous)
                && !string.Equals(previous, normalized, StringComparison.Ordinal))
            {
                _highlightUntil[key] = now.Add(_duration);
            }

            _lastValues[key] = normalized;
            return _highlightUntil.TryGetValue(key, out var until) && until >= now;
        }

        public void Reset()
        {
            _lastValues.Clear();
            _highlightUntil.Clear();
        }
    }
}
