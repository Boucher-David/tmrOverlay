namespace TmrOverlay.Core.Telemetry.Live;

internal static class LiveModelParityAnalyzer
{
    private const double SecondsTolerance = 0.05d;
    private const double DistanceMetersTolerance = 0.5d;
    private const double LapDistanceTolerance = 0.00001d;
    private const double TemperatureTolerance = 0.05d;
    private const double FuelTolerance = 0.01d;

    public static LiveModelParityFrame Analyze(LiveTelemetrySnapshot snapshot)
    {
        var observations = new List<LiveModelParityObservation>();
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return new LiveModelParityFrame(
                CapturedAtUtc: snapshot.LastUpdatedAtUtc ?? DateTimeOffset.UtcNow,
                Sequence: snapshot.Sequence,
                Coverage: LiveModelParityCoverage.Empty,
                Observations: []);
        }

        CompareFuel(snapshot, observations);
        ComparePitState(snapshot, observations);
        CompareProximity(snapshot, observations);
        CompareLeaderGap(snapshot, observations);
        CompareWeather(snapshot, observations);
        CompareRaceEvents(snapshot, observations);
        CompareSession(snapshot, observations);

        return new LiveModelParityFrame(
            CapturedAtUtc: sample.CapturedAtUtc,
            Sequence: snapshot.Sequence,
            Coverage: BuildCoverage(snapshot),
            Observations: observations);
    }

    private static LiveModelParityCoverage BuildCoverage(LiveTelemetrySnapshot snapshot)
    {
        var models = snapshot.Models;
        return new LiveModelParityCoverage(
            HasLegacyFuel: snapshot.Fuel.HasValidFuel,
            HasModelFuel: models.FuelPit.Fuel.HasValidFuel,
            HasLegacyProximity: snapshot.Proximity.HasData,
            HasModelRelative: models.Relative.HasData,
            HasModelSpatial: models.Spatial.HasData,
            HasLegacyLeaderGap: snapshot.LeaderGap.HasData,
            HasModelTiming: models.Timing.HasData,
            HasLegacyWeather: snapshot.LatestSample is { } sample
                && (IsFinite(sample.AirTempC)
                    || IsFinite(sample.TrackTempCrewC)
                    || sample.TrackWetness >= 0
                    || sample.WeatherDeclaredWet),
            HasModelWeather: models.Weather.HasData,
            LegacyProximityCarCount: snapshot.Proximity.NearbyCars.Count,
            ModelRelativeRowCount: models.Relative.Rows.Count,
            ModelSpatialCarCount: models.Spatial.Cars.Count,
            LegacyClassGapCarCount: snapshot.LeaderGap.ClassCars.Count,
            ModelClassTimingRowCount: models.Timing.ClassRows.Count);
    }

    private static void CompareFuel(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var legacy = snapshot.Fuel;
        var model = snapshot.Models.FuelPit.Fuel;
        CompareBoolean(observations, "fuel", "has-valid-fuel", legacy.HasValidFuel, model.HasValidFuel);
        CompareString(observations, "fuel", "source", legacy.Source, model.Source);
        CompareNullableDouble(observations, "fuel", "fuel-level-liters", legacy.FuelLevelLiters, model.FuelLevelLiters, FuelTolerance);
        CompareNullableDouble(observations, "fuel", "fuel-use-per-hour-liters", legacy.FuelUsePerHourLiters, model.FuelUsePerHourLiters, FuelTolerance);
        CompareNullableDouble(observations, "fuel", "fuel-per-lap-liters", legacy.FuelPerLapLiters, model.FuelPerLapLiters, FuelTolerance);
        CompareNullableDouble(observations, "fuel", "estimated-laps-remaining", legacy.EstimatedLapsRemaining, model.EstimatedLapsRemaining, FuelTolerance);
    }

    private static void ComparePitState(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var model = snapshot.Models.FuelPit;
        CompareBoolean(observations, "pit", "on-pit-road", sample.OnPitRoad, model.OnPitRoad);
        CompareBoolean(observations, "pit", "pitstop-active", sample.PitstopActive, model.PitstopActive);
        CompareBoolean(observations, "pit", "player-car-in-pit-stall", sample.PlayerCarInPitStall, model.PlayerCarInPitStall);
        CompareNullableBoolean(observations, "pit", "team-on-pit-road", sample.TeamOnPitRoad, model.TeamOnPitRoad);
        CompareNullableInt(observations, "pit", "pit-service-flags", sample.PitServiceFlags, model.PitServiceFlags);
        CompareNullableDouble(observations, "pit", "pit-service-fuel-liters", ValidNonNegative(sample.PitServiceFuelLiters), model.PitServiceFuelLiters, FuelTolerance);
        CompareNullableDouble(observations, "pit", "pit-repair-left-seconds", ValidNonNegative(sample.PitRepairLeftSeconds), model.PitRepairLeftSeconds, SecondsTolerance);
        CompareNullableDouble(observations, "pit", "pit-opt-repair-left-seconds", ValidNonNegative(sample.PitOptRepairLeftSeconds), model.PitOptRepairLeftSeconds, SecondsTolerance);
    }

    private static void CompareProximity(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var expectedReferenceCarIdx = sample.FocusCarIdx ?? sample.PlayerCarIdx;
        CompareNullableInt(observations, "proximity", "relative-reference-car-idx", expectedReferenceCarIdx, snapshot.Models.Relative.ReferenceCarIdx);
        CompareNullableInt(observations, "proximity", "spatial-reference-car-idx", expectedReferenceCarIdx, snapshot.Models.Spatial.ReferenceCarIdx);

        var relativeByCarIdx = snapshot.Models.Relative.Rows.ToDictionary(row => row.CarIdx);
        var spatialByCarIdx = snapshot.Models.Spatial.Cars.ToDictionary(car => car.CarIdx);
        foreach (var legacyCar in snapshot.Proximity.NearbyCars)
        {
            if (!relativeByCarIdx.TryGetValue(legacyCar.CarIdx, out var relative))
            {
                observations.Add(Missing("proximity", $"relative-row-{legacyCar.CarIdx}", "legacy nearby car missing from model relative rows"));
            }
            else
            {
                CompareNullableDouble(observations, "proximity", $"relative-seconds-{legacyCar.CarIdx}", legacyCar.RelativeSeconds, relative.RelativeSeconds, SecondsTolerance);
                CompareNullableDouble(observations, "proximity", $"relative-laps-{legacyCar.CarIdx}", legacyCar.RelativeLaps, relative.RelativeLaps, LapDistanceTolerance);
                CompareNullableDouble(observations, "proximity", $"relative-meters-{legacyCar.CarIdx}", legacyCar.RelativeMeters, relative.RelativeMeters, DistanceMetersTolerance);
                CompareNullableInt(observations, "proximity", $"relative-class-{legacyCar.CarIdx}", legacyCar.CarClass, relative.CarClass);
                CompareNullableBoolean(observations, "proximity", $"relative-pit-road-{legacyCar.CarIdx}", legacyCar.OnPitRoad, relative.OnPitRoad);
            }

            if (!spatialByCarIdx.TryGetValue(legacyCar.CarIdx, out var spatial))
            {
                observations.Add(Missing("proximity", $"spatial-car-{legacyCar.CarIdx}", "legacy nearby car missing from model spatial cars"));
            }
            else
            {
                CompareNullableDouble(observations, "proximity", $"spatial-laps-{legacyCar.CarIdx}", legacyCar.RelativeLaps, spatial.RelativeLaps, LapDistanceTolerance);
                CompareNullableDouble(observations, "proximity", $"spatial-meters-{legacyCar.CarIdx}", legacyCar.RelativeMeters, spatial.RelativeMeters, DistanceMetersTolerance);
                CompareNullableInt(observations, "proximity", $"spatial-class-{legacyCar.CarIdx}", legacyCar.CarClass, spatial.CarClass);
            }
        }
    }

    private static void CompareLeaderGap(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var legacy = snapshot.LeaderGap;
        var timing = snapshot.Models.Timing;
        CompareNullableInt(observations, "timing", "overall-leader-car-idx", legacy.OverallLeaderCarIdx, timing.OverallLeaderCarIdx);
        CompareNullableInt(observations, "timing", "class-leader-car-idx", legacy.ClassLeaderCarIdx, timing.ClassLeaderCarIdx);

        var timingByCarIdx = timing.ClassRows
            .Concat(timing.OverallRows)
            .GroupBy(row => row.CarIdx)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var legacyCar in legacy.ClassCars)
        {
            if (!timingByCarIdx.TryGetValue(legacyCar.CarIdx, out var row))
            {
                observations.Add(Missing("timing", $"class-gap-row-{legacyCar.CarIdx}", "legacy class-gap car missing from model timing rows"));
                continue;
            }

            CompareNullableInt(observations, "timing", $"class-position-{legacyCar.CarIdx}", legacyCar.ClassPosition, row.ClassPosition);
            CompareNullableDouble(observations, "timing", $"gap-seconds-{legacyCar.CarIdx}", legacyCar.GapSecondsToClassLeader, row.GapSecondsToClassLeader, SecondsTolerance);
            CompareNullableDouble(observations, "timing", $"gap-laps-{legacyCar.CarIdx}", legacyCar.GapLapsToClassLeader, row.GapLapsToClassLeader, LapDistanceTolerance);
            CompareNullableDouble(observations, "timing", $"delta-to-focus-{legacyCar.CarIdx}", legacyCar.DeltaSecondsToReference, row.DeltaSecondsToFocus, SecondsTolerance);
        }
    }

    private static void CompareWeather(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var model = snapshot.Models.Weather;
        CompareNullableDouble(observations, "weather", "air-temp-c", ValidFinite(sample.AirTempC), model.AirTempC, TemperatureTolerance);
        CompareNullableDouble(observations, "weather", "track-temp-crew-c", ValidFinite(sample.TrackTempCrewC), model.TrackTempCrewC, TemperatureTolerance);
        CompareNullableInt(observations, "weather", "track-wetness", sample.TrackWetness >= 0 ? sample.TrackWetness : null, model.TrackWetness);
        CompareNullableBoolean(observations, "weather", "declared-wet", sample.WeatherDeclaredWet, model.WeatherDeclaredWet);
        CompareString(observations, "weather", "skies-label", TrimToNull(snapshot.Context.Conditions.TrackSkies), model.SkiesLabel);
        CompareString(observations, "weather", "weather-type", TrimToNull(snapshot.Context.Conditions.TrackWeatherType), model.WeatherType);
    }

    private static void CompareRaceEvents(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var model = snapshot.Models.RaceEvents;
        CompareBoolean(observations, "race-events", "is-on-track", sample.IsOnTrack, model.IsOnTrack);
        CompareBoolean(observations, "race-events", "is-in-garage", sample.IsInGarage, model.IsInGarage);
        CompareBoolean(observations, "race-events", "on-pit-road", sample.OnPitRoad, model.OnPitRoad);
        CompareNullableInt(observations, "race-events", "lap", sample.Lap, model.Lap);
        CompareNullableInt(observations, "race-events", "lap-completed", sample.LapCompleted, model.LapCompleted);
        CompareNullableDouble(observations, "race-events", "lap-dist-pct", ValidLapDistPct(sample.LapDistPct), model.LapDistPct, LapDistanceTolerance);
        CompareNullableInt(observations, "race-events", "drivers-so-far", sample.DriversSoFar, model.DriversSoFar);
        CompareNullableInt(observations, "race-events", "driver-change-lap-status", sample.DriverChangeLapStatus, model.DriverChangeLapStatus);
    }

    private static void CompareSession(
        LiveTelemetrySnapshot snapshot,
        List<LiveModelParityObservation> observations)
    {
        var sample = snapshot.LatestSample;
        if (sample is null)
        {
            return;
        }

        var model = snapshot.Models.Session;
        CompareNullableDouble(observations, "session", "session-time-seconds", ValidNonNegative(sample.SessionTime), model.SessionTimeSeconds, SecondsTolerance);
        CompareNullableDouble(observations, "session", "session-time-remain-seconds", ValidNonNegative(sample.SessionTimeRemain), model.SessionTimeRemainSeconds, SecondsTolerance);
        CompareNullableDouble(observations, "session", "session-time-total-seconds", ValidPositive(sample.SessionTimeTotal), model.SessionTimeTotalSeconds, SecondsTolerance);
        CompareNullableInt(observations, "session", "session-laps-remain", sample.SessionLapsRemainEx is >= 0 ? sample.SessionLapsRemainEx : null, model.SessionLapsRemain);
        CompareNullableInt(observations, "session", "session-laps-total", sample.SessionLapsTotal is >= 0 ? sample.SessionLapsTotal : null, model.SessionLapsTotal);
        CompareNullableInt(observations, "session", "session-state", sample.SessionState, model.SessionState);
        CompareString(observations, "session", "session-type", TrimToNull(snapshot.Context.Session.SessionType), model.SessionType);
    }

    private static LiveModelParityObservation Missing(string family, string key, string detail)
    {
        return new LiveModelParityObservation(
            Family: family,
            Key: key,
            Severity: "mismatch",
            LegacyValue: "present",
            ModelValue: "missing",
            Detail: detail);
    }

    private static void CompareBoolean(
        List<LiveModelParityObservation> observations,
        string family,
        string key,
        bool legacy,
        bool model)
    {
        if (legacy == model)
        {
            return;
        }

        observations.Add(new LiveModelParityObservation(family, key, "mismatch", legacy.ToString(), model.ToString(), null));
    }

    private static void CompareNullableBoolean(
        List<LiveModelParityObservation> observations,
        string family,
        string key,
        bool? legacy,
        bool? model)
    {
        if (legacy == model)
        {
            return;
        }

        observations.Add(new LiveModelParityObservation(family, key, "mismatch", FormatValue(legacy), FormatValue(model), null));
    }

    private static void CompareNullableInt(
        List<LiveModelParityObservation> observations,
        string family,
        string key,
        int? legacy,
        int? model)
    {
        if (legacy == model)
        {
            return;
        }

        observations.Add(new LiveModelParityObservation(family, key, "mismatch", FormatValue(legacy), FormatValue(model), null));
    }

    private static void CompareString(
        List<LiveModelParityObservation> observations,
        string family,
        string key,
        string? legacy,
        string? model)
    {
        if (string.Equals(TrimToNull(legacy), TrimToNull(model), StringComparison.Ordinal))
        {
            return;
        }

        observations.Add(new LiveModelParityObservation(family, key, "mismatch", FormatValue(legacy), FormatValue(model), null));
    }

    private static void CompareNullableDouble(
        List<LiveModelParityObservation> observations,
        string family,
        string key,
        double? legacy,
        double? model,
        double tolerance)
    {
        if (NumbersEqual(legacy, model, tolerance))
        {
            return;
        }

        observations.Add(new LiveModelParityObservation(family, key, "mismatch", FormatValue(legacy), FormatValue(model), $"tolerance={tolerance}"));
    }

    private static bool NumbersEqual(double? left, double? right, double tolerance)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return Math.Abs(left.Value - right.Value) <= tolerance;
    }

    private static double? ValidLapDistPct(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d
            ? Math.Clamp(number, 0d, 1d)
            : null;
    }

    private static double? ValidFinite(double value)
    {
        return IsFinite(value) ? value : null;
    }

    private static double? ValidNonNegative(double value)
    {
        return IsFinite(value) && value >= 0d ? value : null;
    }

    private static double? ValidNonNegative(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d ? number : null;
    }

    private static double? ValidPositive(double? value)
    {
        return value is { } number && IsFinite(number) && number > 0d ? number : null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string? FormatValue(bool? value)
    {
        return value?.ToString();
    }

    private static string? FormatValue(int? value)
    {
        return value?.ToString();
    }

    private static string? FormatValue(double? value)
    {
        return value is null ? null : Math.Round(value.Value, 6).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? FormatValue(string? value)
    {
        return TrimToNull(value);
    }
}

internal sealed record LiveModelParityFrame(
    DateTimeOffset CapturedAtUtc,
    long Sequence,
    LiveModelParityCoverage Coverage,
    IReadOnlyList<LiveModelParityObservation> Observations)
{
    public bool HasMismatch => Observations.Count > 0;
}

internal sealed record LiveModelParityCoverage(
    bool HasLegacyFuel,
    bool HasModelFuel,
    bool HasLegacyProximity,
    bool HasModelRelative,
    bool HasModelSpatial,
    bool HasLegacyLeaderGap,
    bool HasModelTiming,
    bool HasLegacyWeather,
    bool HasModelWeather,
    int LegacyProximityCarCount,
    int ModelRelativeRowCount,
    int ModelSpatialCarCount,
    int LegacyClassGapCarCount,
    int ModelClassTimingRowCount)
{
    public static LiveModelParityCoverage Empty { get; } = new(
        HasLegacyFuel: false,
        HasModelFuel: false,
        HasLegacyProximity: false,
        HasModelRelative: false,
        HasModelSpatial: false,
        HasLegacyLeaderGap: false,
        HasModelTiming: false,
        HasLegacyWeather: false,
        HasModelWeather: false,
        LegacyProximityCarCount: 0,
        ModelRelativeRowCount: 0,
        ModelSpatialCarCount: 0,
        LegacyClassGapCarCount: 0,
        ModelClassTimingRowCount: 0);
}

internal sealed record LiveModelParityObservation(
    string Family,
    string Key,
    string Severity,
    string? LegacyValue,
    string? ModelValue,
    string? Detail);
