using TmrOverlay.App.AppInfo;

namespace TmrOverlay.App.History;

internal sealed class HistoricalSessionContext
{
    public static HistoricalSessionContext Empty { get; } = new()
    {
        Car = new HistoricalCarIdentity(),
        Track = new HistoricalTrackIdentity(),
        Session = new HistoricalSessionIdentity(),
        Conditions = new HistoricalSessionInfoConditions()
    };

    public required HistoricalCarIdentity Car { get; init; }

    public required HistoricalTrackIdentity Track { get; init; }

    public required HistoricalSessionIdentity Session { get; init; }

    public required HistoricalSessionInfoConditions Conditions { get; init; }
}

internal sealed class HistoricalSessionSummary
{
    public int SummaryVersion { get; init; } = 1;

    public required string SourceCaptureId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset FinishedAtUtc { get; init; }

    public required HistoricalComboIdentity Combo { get; init; }

    public required HistoricalCarIdentity Car { get; init; }

    public required HistoricalTrackIdentity Track { get; init; }

    public required HistoricalSessionIdentity Session { get; init; }

    public required HistoricalConditions Conditions { get; init; }

    public required HistoricalSessionMetrics Metrics { get; init; }

    public required HistoricalDataQuality Quality { get; init; }

    public AppVersionInfo? AppVersion { get; init; }
}

internal sealed class HistoricalComboIdentity
{
    public required string CarKey { get; init; }

    public required string TrackKey { get; init; }

    public required string SessionKey { get; init; }

    public static HistoricalComboIdentity From(HistoricalSessionContext context)
    {
        return new HistoricalComboIdentity
        {
            CarKey = SessionHistoryPath.Slug(
                context.Car.CarId is not null
                    ? $"car-{context.Car.CarId}-{context.Car.CarPath ?? context.Car.CarScreenName}"
                    : $"car-{context.Car.CarPath ?? context.Car.CarScreenName ?? "unknown"}"),
            TrackKey = SessionHistoryPath.Slug(
                context.Track.TrackId is not null
                    ? $"track-{context.Track.TrackId}-{context.Track.TrackName ?? context.Track.TrackDisplayName}"
                    : $"track-{context.Track.TrackName ?? context.Track.TrackDisplayName ?? "unknown"}"),
            SessionKey = SessionHistoryPath.Slug(context.Session.SessionType ?? context.Session.EventType ?? "unknown-session")
        };
    }
}

internal sealed class HistoricalCarIdentity
{
    public int? CarId { get; init; }

    public string? CarPath { get; init; }

    public string? CarScreenName { get; init; }

    public string? CarScreenNameShort { get; init; }

    public int? CarClassId { get; init; }

    public string? CarClassShortName { get; init; }

    public double? CarClassEstLapTimeSeconds { get; init; }

    public double? DriverCarFuelMaxLiters { get; init; }

    public double? DriverCarFuelKgPerLiter { get; init; }

    public double? DriverCarEstLapTimeSeconds { get; init; }

    public string? DriverCarVersion { get; init; }

    public string? DriverGearboxType { get; init; }

    public string? DriverSetupName { get; init; }

    public bool? DriverSetupIsModified { get; init; }
}

internal sealed class HistoricalTrackIdentity
{
    public int? TrackId { get; init; }

    public string? TrackName { get; init; }

    public string? TrackDisplayName { get; init; }

    public string? TrackConfigName { get; init; }

    public double? TrackLengthKm { get; init; }

    public string? TrackCity { get; init; }

    public string? TrackCountry { get; init; }

    public int? TrackNumTurns { get; init; }

    public string? TrackType { get; init; }

    public string? TrackVersion { get; init; }
}

internal sealed class HistoricalSessionIdentity
{
    public int? CurrentSessionNum { get; init; }

    public int? SessionNum { get; init; }

    public string? SessionType { get; init; }

    public string? SessionName { get; init; }

    public string? SessionTime { get; init; }

    public string? SessionLaps { get; init; }

    public string? EventType { get; init; }

    public string? Category { get; init; }

    public bool? Official { get; init; }

    public bool? TeamRacing { get; init; }

    public int? SeriesId { get; init; }

    public int? SeasonId { get; init; }

    public int? SessionId { get; init; }

    public int? SubSessionId { get; init; }

    public string? BuildVersion { get; init; }
}

internal sealed class HistoricalSessionInfoConditions
{
    public string? TrackWeatherType { get; init; }

    public string? TrackSkies { get; init; }

    public double? TrackPrecipitationPercent { get; init; }

    public string? SessionTrackRubberState { get; init; }
}

internal sealed class HistoricalConditions
{
    public double? AirTempC { get; init; }

    public double? TrackTempCrewC { get; init; }

    public int? TrackWetness { get; init; }

    public bool? WeatherDeclaredWet { get; init; }

    public int? PlayerTireCompound { get; init; }

    public string? TrackWeatherType { get; init; }

    public string? TrackSkies { get; init; }

    public double? TrackPrecipitationPercent { get; init; }

    public string? SessionTrackRubberState { get; init; }
}

internal sealed class HistoricalSessionMetrics
{
    public int SampleFrameCount { get; init; }

    public int DroppedFrameCount { get; init; }

    public int SessionInfoSnapshotCount { get; init; }

    public double CaptureDurationSeconds { get; init; }

    public double OnTrackTimeSeconds { get; init; }

    public double PitRoadTimeSeconds { get; init; }

    public double MovingTimeSeconds { get; init; }

    public double ValidGreenTimeSeconds { get; init; }

    public double ValidDistanceLaps { get; init; }

    public int CompletedValidLaps { get; init; }

    public double FuelUsedLiters { get; init; }

    public double FuelAddedLiters { get; init; }

    public double? FuelPerHourLiters { get; init; }

    public double? FuelPerLapLiters { get; init; }

    public double? AverageLapSeconds { get; init; }

    public double? MedianLapSeconds { get; init; }

    public double? BestLapSeconds { get; init; }

    public double? StartingFuelLiters { get; init; }

    public double? EndingFuelLiters { get; init; }

    public double? MinimumFuelLiters { get; init; }

    public double? MaximumFuelLiters { get; init; }

    public int PitRoadEntryCount { get; init; }

    public int PitServiceCount { get; init; }

    public int StintCount { get; init; }
}

internal sealed class HistoricalDataQuality
{
    public required string Confidence { get; init; }

    public required bool ContributesToBaseline { get; init; }

    public required string[] Reasons { get; init; }

    public static HistoricalDataQuality From(HistoricalSessionContext context, HistoricalSessionMetrics metrics)
    {
        var reasons = new List<string>();

        if (metrics.SampleFrameCount == 0)
        {
            reasons.Add("no_frames");
        }

        if (metrics.ValidGreenTimeSeconds < 120d)
        {
            reasons.Add("short_green_sample");
        }

        if (metrics.ValidDistanceLaps < 0.5d)
        {
            reasons.Add("insufficient_distance");
        }

        if (metrics.CompletedValidLaps == 0)
        {
            reasons.Add("no_completed_laps");
        }

        if (metrics.FuelPerLapLiters is null)
        {
            reasons.Add("no_reliable_fuel_per_lap");
        }

        if (IsNonRaceTestSession(context))
        {
            reasons.Add("non_race_test_session");
        }

        if (metrics.DroppedFrameCount > 0)
        {
            reasons.Add("dropped_frames");
        }

        var confidence = DetermineConfidence(metrics);

        return new HistoricalDataQuality
        {
            Confidence = confidence,
            ContributesToBaseline = confidence is "medium" or "high",
            Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static string DetermineConfidence(HistoricalSessionMetrics metrics)
    {
        if (metrics.FuelPerLapLiters is not null && metrics.ValidDistanceLaps >= 3d && metrics.CompletedValidLaps >= 3)
        {
            return "high";
        }

        if (metrics.FuelPerLapLiters is not null && (metrics.ValidDistanceLaps >= 1d || metrics.CompletedValidLaps >= 1))
        {
            return "medium";
        }

        if (metrics.FuelPerHourLiters is not null && metrics.FuelUsedLiters >= 0.25d)
        {
            return "low";
        }

        return "none";
    }

    private static bool IsNonRaceTestSession(HistoricalSessionContext context)
    {
        return string.Equals(context.Session.EventType, "Test", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.Session.SessionType, "Offline Testing", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record HistoricalTelemetrySample(
    DateTimeOffset CapturedAtUtc,
    double SessionTime,
    int SessionTick,
    int SessionInfoUpdate,
    bool IsOnTrack,
    bool IsInGarage,
    bool OnPitRoad,
    bool PitstopActive,
    bool PlayerCarInPitStall,
    double FuelLevelLiters,
    double FuelLevelPercent,
    double FuelUsePerHourKg,
    double SpeedMetersPerSecond,
    int Lap,
    int LapCompleted,
    double LapDistPct,
    double? LapLastLapTimeSeconds,
    double? LapBestLapTimeSeconds,
    double AirTempC,
    double TrackTempCrewC,
    int TrackWetness,
    bool WeatherDeclaredWet,
    int PlayerTireCompound);
