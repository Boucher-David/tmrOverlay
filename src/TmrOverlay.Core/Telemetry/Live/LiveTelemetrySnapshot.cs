using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal sealed record LiveTelemetrySnapshot(
    bool IsConnected,
    bool IsCollecting,
    string? SourceId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastUpdatedAtUtc,
    long Sequence,
    HistoricalSessionContext Context,
    HistoricalComboIdentity Combo,
    HistoricalTelemetrySample? LatestSample,
    LiveFuelSnapshot Fuel,
    LiveProximitySnapshot Proximity,
    LiveLeaderGapSnapshot LeaderGap,
    LiveWeatherSnapshot Weather)
{
    public int CompletedStintCount { get; init; }

    public LiveCarContextSnapshot TeamCar { get; init; } = LiveCarContextSnapshot.Unavailable;

    public LiveCarContextSnapshot FocusCar { get; init; } = LiveCarContextSnapshot.Unavailable;

    public IReadOnlyList<LiveObservedCar> ObservedCars { get; init; } = [];

    public int ObservedCarCount => ObservedCars.Count;

    public TelemetryAvailabilitySnapshot TelemetryAvailability { get; init; } = TelemetryAvailabilitySnapshot.Empty;

    public bool IsLocalDriverInCar => LatestSample is { IsOnTrack: true, IsInGarage: false };

    public bool IsSpectating => IsCollecting && !IsLocalDriverInCar && FocusCar.HasData;

    public bool IsSpectatingFocusedCar => IsSpectating && !FocusCar.IsTeamCar;

    public string LiveMode =>
        !IsCollecting
            ? "waiting"
            : IsLocalDriverInCar
                ? "driving"
                : IsSpectating
                    ? FocusCar.IsTeamCar ? "spectating-team" : "spectating-focus"
                    : "local-idle";

    public static LiveTelemetrySnapshot Empty { get; } = new(
        IsConnected: false,
        IsCollecting: false,
        SourceId: null,
        StartedAtUtc: null,
        LastUpdatedAtUtc: null,
        Sequence: 0,
        Context: HistoricalSessionContext.Empty,
        Combo: HistoricalComboIdentity.From(HistoricalSessionContext.Empty),
        LatestSample: null,
        Fuel: LiveFuelSnapshot.Unavailable,
        Proximity: LiveProximitySnapshot.Unavailable,
        LeaderGap: LiveLeaderGapSnapshot.Unavailable,
        Weather: LiveWeatherSnapshot.Unavailable);
}

internal sealed record LiveCarContextSnapshot(
    bool HasData,
    int? CarIdx,
    string Role,
    bool IsTeamCar,
    int? OverallPosition,
    int? ClassPosition,
    int? CarClass,
    bool? OnPitRoad,
    double? ProgressLaps,
    double? CurrentStintLaps,
    double? CurrentStintSeconds,
    int ObservedPitStopCount,
    string StintSource)
{
    public IReadOnlyList<LiveObservedStint> CompletedStints { get; init; } = [];

    public int CompletedStintCount => CompletedStints.Count;

    public double? AverageCompletedStintLaps => CompletedStints.Count > 0
        ? CompletedStints.Average(stint => stint.DistanceLaps)
        : null;

    public static LiveCarContextSnapshot Unavailable { get; } = new(
        HasData: false,
        CarIdx: null,
        Role: "unavailable",
        IsTeamCar: false,
        OverallPosition: null,
        ClassPosition: null,
        CarClass: null,
        OnPitRoad: null,
        ProgressLaps: null,
        CurrentStintLaps: null,
        CurrentStintSeconds: null,
        ObservedPitStopCount: 0,
        StintSource: "unavailable");
}

internal sealed record LiveObservedStint(
    int Number,
    double StartSessionTime,
    double EndSessionTime,
    double StartProgressLaps,
    double EndProgressLaps,
    double DistanceLaps,
    double DurationSeconds,
    string Source);

internal sealed record LiveObservedCar(
    int CarIdx,
    int? OverallPosition,
    int? ClassPosition,
    int? CarClass,
    bool? OnPitRoad,
    double ProgressLaps,
    double CurrentStintLaps,
    double CurrentStintSeconds,
    int ObservedPitStopCount,
    string StintSource,
    IReadOnlyList<LiveObservedStint> CompletedStints)
{
    public int CompletedStintCount => CompletedStints.Count;

    public double? AverageCompletedStintLaps => CompletedStints.Count > 0
        ? CompletedStints.Average(stint => stint.DistanceLaps)
        : null;
}

internal sealed record LiveWeatherSnapshot(
    bool HasData,
    DateTimeOffset? CapturedAtUtc,
    double? SessionTime,
    double? TrackTempC,
    double? TrackTempCrewC,
    double? AirTempC,
    int? TrackWetness,
    string SurfaceMoistureClass,
    bool? WeatherDeclaredWet,
    int? Skies,
    string? SkiesLabel,
    double? WindVelMetersPerSecond,
    double? WindDirRadians,
    double? RelativeHumidityPercent,
    double? FogLevelPercent,
    double? PrecipitationPercent,
    double? AirDensityKgPerCubicMeter,
    double? AirPressurePa,
    double? SolarAltitudeRadians,
    double? SolarAzimuthRadians,
    string? SessionTrackWeatherType,
    string? SessionTrackSkies,
    double? SessionTrackSurfaceTempC,
    double? SessionTrackSurfaceTempCrewC,
    double? SessionTrackAirTempC,
    double? SessionTrackWindVelMetersPerSecond,
    double? SessionTrackWindDirRadians,
    double? SessionTrackRelativeHumidityPercent,
    double? SessionTrackFogLevelPercent,
    double? SessionTrackPrecipitationPercent,
    string? SessionTrackRubberState,
    bool? DeclaredWetSurfaceMismatch)
{
    public static LiveWeatherSnapshot Unavailable { get; } = new(
        HasData: false,
        CapturedAtUtc: null,
        SessionTime: null,
        TrackTempC: null,
        TrackTempCrewC: null,
        AirTempC: null,
        TrackWetness: null,
        SurfaceMoistureClass: "unknown",
        WeatherDeclaredWet: null,
        Skies: null,
        SkiesLabel: null,
        WindVelMetersPerSecond: null,
        WindDirRadians: null,
        RelativeHumidityPercent: null,
        FogLevelPercent: null,
        PrecipitationPercent: null,
        AirDensityKgPerCubicMeter: null,
        AirPressurePa: null,
        SolarAltitudeRadians: null,
        SolarAzimuthRadians: null,
        SessionTrackWeatherType: null,
        SessionTrackSkies: null,
        SessionTrackSurfaceTempC: null,
        SessionTrackSurfaceTempCrewC: null,
        SessionTrackAirTempC: null,
        SessionTrackWindVelMetersPerSecond: null,
        SessionTrackWindDirRadians: null,
        SessionTrackRelativeHumidityPercent: null,
        SessionTrackFogLevelPercent: null,
        SessionTrackPrecipitationPercent: null,
        SessionTrackRubberState: null,
        DeclaredWetSurfaceMismatch: null);

    public static LiveWeatherSnapshot From(HistoricalSessionContext context, HistoricalTelemetrySample sample)
    {
        var trackTemp = Clean(sample.TrackTempC);
        var trackTempCrew = Clean(sample.TrackTempCrewC);
        var airTemp = Clean(sample.AirTempC);
        var windVel = Clean(sample.WindVelMetersPerSecond);
        var windDir = Clean(sample.WindDirRadians);
        var humidity = Clean(sample.RelativeHumidityPercent);
        var fog = Clean(sample.FogLevelPercent);
        var precipitation = Clean(sample.PrecipitationPercent);
        var airDensity = Clean(sample.AirDensityKgPerCubicMeter);
        var airPressure = Clean(sample.AirPressurePa);
        var solarAltitude = Clean(sample.SolarAltitudeRadians);
        var solarAzimuth = Clean(sample.SolarAzimuthRadians);
        var trackWetness = sample.TrackWetness >= 0 ? sample.TrackWetness : (int?)null;
        var moistureClass = SurfaceClass(trackWetness);
        var hasData = trackTemp is not null
            || trackTempCrew is not null
            || airTemp is not null
            || trackWetness is not null
            || sample.WeatherDeclaredWet
            || sample.Skies is not null
            || windVel is not null
            || windDir is not null
            || humidity is not null
            || fog is not null
            || precipitation is not null
            || airDensity is not null
            || airPressure is not null
            || solarAltitude is not null
            || solarAzimuth is not null;

        return new LiveWeatherSnapshot(
            HasData: hasData,
            CapturedAtUtc: sample.CapturedAtUtc,
            SessionTime: Clean(sample.SessionTime),
            TrackTempC: trackTemp,
            TrackTempCrewC: trackTempCrew,
            AirTempC: airTemp,
            TrackWetness: trackWetness,
            SurfaceMoistureClass: moistureClass,
            WeatherDeclaredWet: hasData ? sample.WeatherDeclaredWet : null,
            Skies: sample.Skies,
            SkiesLabel: FormatSkiesLabel(sample.Skies),
            WindVelMetersPerSecond: windVel,
            WindDirRadians: windDir,
            RelativeHumidityPercent: humidity,
            FogLevelPercent: fog,
            PrecipitationPercent: precipitation,
            AirDensityKgPerCubicMeter: airDensity,
            AirPressurePa: airPressure,
            SolarAltitudeRadians: solarAltitude,
            SolarAzimuthRadians: solarAzimuth,
            SessionTrackWeatherType: context.Conditions.TrackWeatherType,
            SessionTrackSkies: context.Conditions.TrackSkies,
            SessionTrackSurfaceTempC: context.Conditions.TrackSurfaceTempC,
            SessionTrackSurfaceTempCrewC: context.Conditions.TrackSurfaceTempCrewC,
            SessionTrackAirTempC: context.Conditions.TrackAirTempC,
            SessionTrackWindVelMetersPerSecond: context.Conditions.TrackWindVelMetersPerSecond,
            SessionTrackWindDirRadians: context.Conditions.TrackWindDirRadians,
            SessionTrackRelativeHumidityPercent: context.Conditions.TrackRelativeHumidityPercent,
            SessionTrackFogLevelPercent: context.Conditions.TrackFogLevelPercent,
            SessionTrackPrecipitationPercent: context.Conditions.TrackPrecipitationPercent,
            SessionTrackRubberState: context.Conditions.SessionTrackRubberState,
            DeclaredWetSurfaceMismatch: DetermineDeclaredWetSurfaceMismatch(sample.WeatherDeclaredWet, moistureClass));
    }

    private static bool? DetermineDeclaredWetSurfaceMismatch(bool declaredWet, string moistureClass)
    {
        return moistureClass switch
        {
            "dry" => declaredWet,
            "wet" => !declaredWet,
            _ => null
        };
    }

    private static string SurfaceClass(int? trackWetness)
    {
        return trackWetness switch
        {
            null => "unknown",
            < 0 => "unknown",
            <= 1 => "dry",
            <= 3 => "damp",
            _ => "wet"
        };
    }

    private static string? FormatSkiesLabel(int? skies)
    {
        return skies switch
        {
            0 => "clear",
            1 => "partly-cloudy",
            2 => "mostly-cloudy",
            3 => "overcast",
            null => null,
            _ => $"code-{skies.Value}"
        };
    }

    private static double? Clean(double? value)
    {
        return value is { } number && !double.IsNaN(number) && !double.IsInfinity(number)
            ? number
            : null;
    }
}

internal sealed record LiveProximitySnapshot(
    bool HasData,
    int? CarLeftRight,
    string SideStatus,
    bool HasCarLeft,
    bool HasCarRight,
    IReadOnlyList<LiveProximityCar> NearbyCars,
    LiveProximityCar? NearestAhead,
    LiveProximityCar? NearestBehind,
    IReadOnlyList<LiveMulticlassApproach> MulticlassApproaches,
    LiveMulticlassApproach? StrongestMulticlassApproach)
{
    public static LiveProximitySnapshot Unavailable { get; } = new(
        HasData: false,
        CarLeftRight: null,
        SideStatus: "waiting",
        HasCarLeft: false,
        HasCarRight: false,
        NearbyCars: [],
        NearestAhead: null,
        NearestBehind: null,
        MulticlassApproaches: [],
        StrongestMulticlassApproach: null);

    public static LiveProximitySnapshot From(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        double? lapTimeSeconds)
    {
        var referenceLapDistPct = ReferenceLapDistPct(sample);
        if (referenceLapDistPct is null)
        {
            return Unavailable with
            {
                CarLeftRight = sample.CarLeftRight,
                SideStatus = FormatSideStatus(sample.CarLeftRight),
                HasCarLeft = DetectCarLeft(sample.CarLeftRight),
                HasCarRight = DetectCarRight(sample.CarLeftRight)
            };
        }

        var trackLengthMeters = context.Track.TrackLengthKm is { } km && IsPositiveFinite(km)
            ? km * 1000d
            : (double?)null;
        var cars = (sample.NearbyCars ?? [])
            .Select(car => ToLiveCar(
                car,
                referenceLapDistPct.Value,
                ReferenceEstimatedTimeSeconds(sample),
                lapTimeSeconds,
                trackLengthMeters))
            .Where(car => Math.Abs(car.RelativeLaps) <= 0.5d && Math.Abs(car.RelativeLaps) > 0.00001d)
            .OrderBy(car => Math.Abs(car.RelativeLaps))
            .ToArray();

        return new LiveProximitySnapshot(
            HasData: sample.CarLeftRight is not null || cars.Length > 0,
            CarLeftRight: sample.CarLeftRight,
            SideStatus: FormatSideStatus(sample.CarLeftRight),
            HasCarLeft: DetectCarLeft(sample.CarLeftRight),
            HasCarRight: DetectCarRight(sample.CarLeftRight),
            NearbyCars: cars,
            NearestAhead: cars
                .Where(car => car.RelativeLaps > 0d)
                .MinBy(car => car.RelativeLaps),
            NearestBehind: cars
                .Where(car => car.RelativeLaps < 0d)
                .MaxBy(car => car.RelativeLaps),
            MulticlassApproaches: [],
            StrongestMulticlassApproach: null);
    }

    private static LiveProximityCar ToLiveCar(
        HistoricalCarProximity car,
        double playerLapDistPct,
        double? playerEstimatedTimeSeconds,
        double? lapTimeSeconds,
        double? trackLengthMeters)
    {
        var relativeLaps = car.LapDistPct - playerLapDistPct;
        if (relativeLaps > 0.5d)
        {
            relativeLaps -= 1d;
        }
        else if (relativeLaps < -0.5d)
        {
            relativeLaps += 1d;
        }

        return new LiveProximityCar(
            CarIdx: car.CarIdx,
            RelativeLaps: relativeLaps,
            RelativeSeconds: CalculateRelativeSeconds(
                car.EstimatedTimeSeconds,
                playerEstimatedTimeSeconds,
                lapTimeSeconds,
                relativeLaps),
            RelativeMeters: trackLengthMeters is { } meters && IsPositiveFinite(meters)
                ? relativeLaps * meters
                : null,
            OverallPosition: car.Position,
            ClassPosition: car.ClassPosition,
            CarClass: car.CarClass,
            TrackSurface: car.TrackSurface,
            OnPitRoad: car.OnPitRoad,
            F2TimeSeconds: car.F2TimeSeconds,
            EstimatedTimeSeconds: car.EstimatedTimeSeconds);
    }

    private static double? CalculateRelativeSeconds(
        double? carEstimatedTimeSeconds,
        double? playerEstimatedTimeSeconds,
        double? lapTimeSeconds,
        double relativeLaps)
    {
        if (carEstimatedTimeSeconds is { } carEst
            && playerEstimatedTimeSeconds is { } playerEst
            && lapTimeSeconds is { } lapSeconds
            && IsPositiveFinite(carEst)
            && IsPositiveFinite(playerEst)
            && IsPositiveFinite(lapSeconds))
        {
            var delta = carEst - playerEst;
            if (delta > lapSeconds / 2d)
            {
                delta -= lapSeconds;
            }
            else if (delta < -lapSeconds / 2d)
            {
                delta += lapSeconds;
            }

            return delta;
        }

        return lapTimeSeconds is { } fallbackLapSeconds && IsPositiveFinite(fallbackLapSeconds)
            ? relativeLaps * fallbackLapSeconds
            : null;
    }

    private static double? ReferenceLapDistPct(HistoricalTelemetrySample sample)
    {
        if (sample.FocusLapDistPct is { } focusLapDistPct && IsFinite(focusLapDistPct) && focusLapDistPct >= 0d)
        {
            return Math.Clamp(focusLapDistPct, 0d, 1d);
        }

        if (HasNonTeamFocus(sample))
        {
            return null;
        }

        if (sample.TeamLapDistPct is { } teamLapDistPct && IsFinite(teamLapDistPct) && teamLapDistPct >= 0d)
        {
            return Math.Clamp(teamLapDistPct, 0d, 1d);
        }

        return IsFinite(sample.LapDistPct) && sample.LapDistPct >= 0d
            ? Math.Clamp(sample.LapDistPct, 0d, 1d)
            : null;
    }

    private static double? ReferenceEstimatedTimeSeconds(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample)
            ? sample.FocusEstimatedTimeSeconds
            : sample.FocusEstimatedTimeSeconds ?? sample.TeamEstimatedTimeSeconds;
    }

    private static bool HasNonTeamFocus(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is { } focusCarIdx
            && sample.PlayerCarIdx is { } playerCarIdx
            && focusCarIdx != playerCarIdx;
    }

    private static string FormatSideStatus(int? carLeftRight)
    {
        return carLeftRight switch
        {
            2 => "left",
            3 => "right",
            4 => "both sides",
            5 => "two left",
            6 => "two right",
            1 => "clear",
            0 => "off",
            _ => "waiting"
        };
    }

    private static bool DetectCarLeft(int? carLeftRight)
    {
        return carLeftRight is 2 or 4 or 5;
    }

    private static bool DetectCarRight(int? carLeftRight)
    {
        return carLeftRight is 3 or 4 or 6;
    }

    private static bool IsPositiveFinite(double value)
    {
        return IsFinite(value) && value > 0d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record LiveProximityCar(
    int CarIdx,
    double RelativeLaps,
    double? RelativeSeconds,
    double? RelativeMeters,
    int? OverallPosition,
    int? ClassPosition,
    int? CarClass,
    int? TrackSurface,
    bool? OnPitRoad,
    double? F2TimeSeconds,
    double? EstimatedTimeSeconds);

internal sealed record LiveMulticlassApproach(
    int CarIdx,
    int? CarClass,
    double RelativeLaps,
    double? RelativeSeconds,
    double? ClosingRateSecondsPerSecond,
    double Urgency);

internal sealed record LiveLeaderGapSnapshot(
    bool HasData,
    int? ReferenceOverallPosition,
    int? ReferenceClassPosition,
    int? OverallLeaderCarIdx,
    int? ClassLeaderCarIdx,
    LiveGapValue OverallLeaderGap,
    LiveGapValue ClassLeaderGap,
    IReadOnlyList<LiveClassGapCar> ClassCars)
{
    public int? TeamOverallPosition => ReferenceOverallPosition;

    public int? TeamClassPosition => ReferenceClassPosition;

    public static LiveLeaderGapSnapshot Unavailable { get; } = new(
        HasData: false,
        ReferenceOverallPosition: null,
        ReferenceClassPosition: null,
        OverallLeaderCarIdx: null,
        ClassLeaderCarIdx: null,
        OverallLeaderGap: LiveGapValue.Unavailable,
        ClassLeaderGap: LiveGapValue.Unavailable,
        ClassCars: []);

    public static LiveLeaderGapSnapshot From(HistoricalTelemetrySample sample)
    {
        return From(HistoricalSessionContext.Empty, sample);
    }

    public static LiveLeaderGapSnapshot From(HistoricalSessionContext context, HistoricalTelemetrySample sample)
    {
        var referenceCarIdx = ReferenceCarIdx(sample);
        var referenceProgress = ReferenceProgress(sample);
        var referenceClassLeaderCarIdx = ReferenceClassLeaderCarIdx(sample);
        var referenceClassLeaderProgress = ReferenceClassLeaderProgress(sample);
        var overallGap = BuildGap(
            position: ReferencePosition(sample),
            leaderCarIdx: sample.LeaderCarIdx,
            playerCarIdx: referenceCarIdx,
            teamF2TimeSeconds: ReferenceF2TimeSeconds(sample),
            leaderF2TimeSeconds: sample.LeaderF2TimeSeconds,
            teamProgress: referenceProgress,
            leaderProgress: Progress(sample.LeaderLapCompleted, sample.LeaderLapDistPct));
        var classGap = BuildGap(
            position: GetReferenceClassPosition(sample),
            leaderCarIdx: referenceClassLeaderCarIdx,
            playerCarIdx: referenceCarIdx,
            teamF2TimeSeconds: ReferenceF2TimeSeconds(sample),
            leaderF2TimeSeconds: ReferenceClassLeaderF2TimeSeconds(sample),
            teamProgress: referenceProgress,
            leaderProgress: referenceClassLeaderProgress);

        return new LiveLeaderGapSnapshot(
            HasData: overallGap.HasData || classGap.HasData,
            ReferenceOverallPosition: ReferencePosition(sample),
            ReferenceClassPosition: GetReferenceClassPosition(sample),
            OverallLeaderCarIdx: sample.LeaderCarIdx,
            ClassLeaderCarIdx: referenceClassLeaderCarIdx,
            OverallLeaderGap: overallGap,
            ClassLeaderGap: classGap,
            ClassCars: BuildClassCars(context, sample, referenceCarIdx, classGap));
    }

    private static IReadOnlyList<LiveClassGapCar> BuildClassCars(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        int? referenceCarIdx,
        LiveGapValue referenceClassGap)
    {
        var referenceClass = ReferenceCarClass(sample);
        var classLeaderCarIdx = ReferenceClassLeaderCarIdx(sample);
        var classLeaderF2 = ValidGapSeconds(ReferenceClassLeaderF2TimeSeconds(sample));
        var classLeaderProgress = ReferenceClassLeaderProgress(sample);
        var classCandidates = sample.ClassCars is { Count: > 0 }
            ? sample.ClassCars
            : sample.NearbyCars ?? [];
        var requireExplicitClassMatch = sample.ClassCars is not { Count: > 0 };
        var classColorByCarIdx = context.Drivers
            .Where(driver => driver.CarIdx is not null && !string.IsNullOrWhiteSpace(driver.CarClassColorHex))
            .GroupBy(driver => driver.CarIdx!.Value)
            .ToDictionary(group => group.Key, group => group.First().CarClassColorHex);
        var referenceClassColorHex = referenceCarIdx is { } referenceIdx && classColorByCarIdx.TryGetValue(referenceIdx, out var referenceColor)
            ? referenceColor
            : context.Car.CarClassColorHex;
        var cars = new List<LiveClassGapCar>();

        if (classLeaderCarIdx is { } leaderIdx)
        {
            var leaderOnPitRoad = classCandidates.FirstOrDefault(car => car.CarIdx == leaderIdx)?.OnPitRoad;
            cars.Add(new LiveClassGapCar(
                CarIdx: leaderIdx,
                IsReferenceCar: referenceCarIdx == leaderIdx,
                IsClassLeader: true,
                ClassPosition: 1,
                GapSecondsToClassLeader: 0d,
                GapLapsToClassLeader: 0d,
                DeltaSecondsToReference: referenceClassGap.Seconds is { } referenceSeconds ? -referenceSeconds : null,
                CarClassColorHex: classColorByCarIdx.TryGetValue(leaderIdx, out var leaderColor) ? leaderColor : referenceClassColorHex,
                OnPitRoad: referenceCarIdx == leaderIdx ? ReferenceOnPitRoad(sample) : leaderOnPitRoad));
        }

        if (referenceCarIdx is { } referenceIdxForRow)
        {
            cars.Add(new LiveClassGapCar(
                CarIdx: referenceIdxForRow,
                IsReferenceCar: true,
                IsClassLeader: referenceClassGap.IsLeader,
                ClassPosition: GetReferenceClassPosition(sample),
                GapSecondsToClassLeader: referenceClassGap.Seconds,
                GapLapsToClassLeader: referenceClassGap.Laps,
                DeltaSecondsToReference: 0d,
                CarClassColorHex: referenceClassColorHex,
                OnPitRoad: ReferenceOnPitRoad(sample)));
        }

        foreach (var car in classCandidates)
        {
            if (car.CarIdx == referenceCarIdx || car.CarIdx == classLeaderCarIdx)
            {
                continue;
            }

            if (!IsUserClassCar(car, referenceClass, requireExplicitClassMatch))
            {
                continue;
            }

            var gapSeconds = CalculateClassGapSeconds(car.F2TimeSeconds, classLeaderF2);
            var gapLaps = gapSeconds is null && classLeaderProgress is not null
                ? CalculateClassGapLaps(car.LapCompleted, car.LapDistPct, classLeaderProgress.Value)
                : null;
            if (gapSeconds is null && gapLaps is null)
            {
                continue;
            }

            cars.Add(new LiveClassGapCar(
                CarIdx: car.CarIdx,
                IsReferenceCar: false,
                IsClassLeader: car.CarIdx == classLeaderCarIdx,
                ClassPosition: car.ClassPosition,
                GapSecondsToClassLeader: gapSeconds,
                GapLapsToClassLeader: gapLaps,
                DeltaSecondsToReference: CalculateDeltaSecondsToTeam(gapSeconds, referenceClassGap.Seconds),
                CarClassColorHex: classColorByCarIdx.TryGetValue(car.CarIdx, out var carColor) ? carColor : referenceClassColorHex,
                OnPitRoad: car.OnPitRoad));
        }

        return cars
            .GroupBy(car => car.CarIdx)
            .Select(group => group
                .OrderByDescending(car => car.IsTeamCar)
                .ThenByDescending(car => car.IsClassLeader)
                .First())
            .OrderBy(car => car.GapSecondsToClassLeader ?? double.MaxValue)
            .ThenBy(car => car.GapLapsToClassLeader ?? double.MaxValue)
            .ThenBy(car => car.ClassPosition ?? int.MaxValue)
            .ToArray();
    }

    private static bool IsUserClassCar(
        HistoricalCarProximity car,
        int? playerClass,
        bool requireExplicitClassMatch)
    {
        if (playerClass is null)
        {
            return !requireExplicitClassMatch;
        }

        return car.CarClass == playerClass;
    }

    private static double? CalculateClassGapSeconds(double? carF2TimeSeconds, double? classLeaderF2TimeSeconds)
    {
        return ValidGapSeconds(carF2TimeSeconds) is { } carF2
            && classLeaderF2TimeSeconds is { } leaderF2
            && carF2 >= leaderF2
            ? carF2 - leaderF2
            : null;
    }

    private static double? CalculateClassGapLaps(int lapCompleted, double lapDistPct, double classLeaderProgress)
    {
        return IsFinite(lapDistPct) && lapCompleted >= 0 && lapDistPct >= 0d
            ? Math.Max(0d, classLeaderProgress - (lapCompleted + Math.Clamp(lapDistPct, 0d, 1d)))
            : null;
    }

    private static double? CalculateDeltaSecondsToTeam(double? gapSeconds, double? teamGapSeconds)
    {
        return gapSeconds is not null && teamGapSeconds is not null
            ? gapSeconds.Value - teamGapSeconds.Value
            : null;
    }

    private static LiveGapValue BuildGap(
        int? position,
        int? leaderCarIdx,
        int? playerCarIdx,
        double? teamF2TimeSeconds,
        double? leaderF2TimeSeconds,
        double? teamProgress,
        double? leaderProgress)
    {
        if (position == 1 || (leaderCarIdx is not null && leaderCarIdx == playerCarIdx))
        {
            return new LiveGapValue(
                HasData: true,
                IsLeader: true,
                Seconds: 0d,
                Laps: 0d,
                Source: "position");
        }

        if (ValidGapSeconds(teamF2TimeSeconds) is { } teamF2)
        {
            var leaderF2 = ValidGapSeconds(leaderF2TimeSeconds) ?? 0d;
            if (teamF2 >= leaderF2)
            {
                return new LiveGapValue(
                    HasData: true,
                    IsLeader: false,
                    Seconds: teamF2 - leaderF2,
                    Laps: null,
                    Source: "CarIdxF2Time");
            }
        }

        if (teamProgress is not null && leaderProgress is not null)
        {
            return new LiveGapValue(
                HasData: true,
                IsLeader: false,
                Seconds: null,
                Laps: Math.Max(0d, leaderProgress.Value - teamProgress.Value),
                Source: "CarIdxLapDistPct");
        }

        return LiveGapValue.Unavailable;
    }

    private static int? ReferenceCarIdx(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx ?? sample.PlayerCarIdx;
    }

    private static bool HasNonTeamFocus(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is { } focusCarIdx
            && sample.PlayerCarIdx is { } playerCarIdx
            && focusCarIdx != playerCarIdx;
    }

    private static int? ReferencePosition(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample) ? sample.FocusPosition : sample.FocusPosition ?? sample.TeamPosition;
    }

    private static int? GetReferenceClassPosition(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample) ? sample.FocusClassPosition : sample.FocusClassPosition ?? sample.TeamClassPosition;
    }

    private static int? ReferenceCarClass(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample) ? sample.FocusCarClass : sample.FocusCarClass ?? sample.TeamCarClass;
    }

    private static double? ReferenceF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample) ? sample.FocusF2TimeSeconds : sample.FocusF2TimeSeconds ?? sample.TeamF2TimeSeconds;
    }

    private static int? ReferenceClassLeaderCarIdx(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample) ? sample.FocusClassLeaderCarIdx : sample.FocusClassLeaderCarIdx ?? sample.ClassLeaderCarIdx;
    }

    private static double? ReferenceClassLeaderF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample)
            ? sample.FocusClassLeaderF2TimeSeconds
            : sample.FocusClassLeaderF2TimeSeconds ?? sample.ClassLeaderF2TimeSeconds;
    }

    private static double? ReferenceClassLeaderProgress(HistoricalTelemetrySample sample)
    {
        if (HasNonTeamFocus(sample))
        {
            return Progress(sample.FocusClassLeaderLapCompleted, sample.FocusClassLeaderLapDistPct);
        }

        return Progress(
            sample.FocusClassLeaderLapCompleted ?? sample.ClassLeaderLapCompleted,
            sample.FocusClassLeaderLapDistPct ?? sample.ClassLeaderLapDistPct);
    }

    private static bool? ReferenceOnPitRoad(HistoricalTelemetrySample sample)
    {
        return HasNonTeamFocus(sample) ? sample.FocusOnPitRoad : sample.FocusOnPitRoad ?? sample.TeamOnPitRoad ?? sample.OnPitRoad;
    }

    private static double? ReferenceProgress(HistoricalTelemetrySample sample)
    {
        if (sample.FocusLapCompleted is { } focusLapCompleted
            && sample.FocusLapDistPct is { } focusLapDistPct
            && IsFinite(focusLapDistPct)
            && focusLapDistPct >= 0d)
        {
            return focusLapCompleted + Math.Clamp(focusLapDistPct, 0d, 1d);
        }

        if (HasNonTeamFocus(sample))
        {
            return null;
        }

        if (sample.TeamLapCompleted is { } teamLapCompleted
            && sample.TeamLapDistPct is { } teamLapDistPct
            && IsFinite(teamLapDistPct)
            && teamLapDistPct >= 0d)
        {
            return teamLapCompleted + Math.Clamp(teamLapDistPct, 0d, 1d);
        }

        return sample.LapCompleted >= 0 && IsFinite(sample.LapDistPct) && sample.LapDistPct >= 0d
            ? sample.LapCompleted + Math.Clamp(sample.LapDistPct, 0d, 1d)
            : null;
    }

    private static double? Progress(int? lapCompleted, double? lapDistPct)
    {
        return lapCompleted is not null
            && lapCompleted.Value >= 0
            && lapDistPct is { } pct
            && IsFinite(pct)
            && pct >= 0d
            ? lapCompleted.Value + Math.Clamp(pct, 0d, 1d)
            : null;
    }

    private static double? ValidGapSeconds(double? seconds)
    {
        return seconds is { } value && IsFinite(value) && value >= 0d && value < 86400d
            ? value
            : null;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record LiveGapValue(
    bool HasData,
    bool IsLeader,
    double? Seconds,
    double? Laps,
    string Source)
{
    public static LiveGapValue Unavailable { get; } = new(
        HasData: false,
        IsLeader: false,
        Seconds: null,
        Laps: null,
        Source: "unavailable");
}

internal sealed record LiveClassGapCar(
    int CarIdx,
    bool IsReferenceCar,
    bool IsClassLeader,
    int? ClassPosition,
    double? GapSecondsToClassLeader,
    double? GapLapsToClassLeader,
    double? DeltaSecondsToReference,
    string? CarClassColorHex,
    bool? OnPitRoad)
{
    public bool IsTeamCar => IsReferenceCar;

    public double? DeltaSecondsToTeam => DeltaSecondsToReference;
}

internal sealed record LiveFuelSnapshot(
    bool HasValidFuel,
    string Source,
    double? FuelLevelLiters,
    double? FuelLevelPercent,
    double? FuelUsePerHourKg,
    double? FuelUsePerHourLiters,
    double? FuelPerLapLiters,
    double? LapTimeSeconds,
    string LapTimeSource,
    double? EstimatedMinutesRemaining,
    double? EstimatedLapsRemaining,
    string Confidence)
{
    public static LiveFuelSnapshot Unavailable { get; } = new(
        HasValidFuel: false,
        Source: "unavailable",
        FuelLevelLiters: null,
        FuelLevelPercent: null,
        FuelUsePerHourKg: null,
        FuelUsePerHourLiters: null,
        FuelPerLapLiters: null,
        LapTimeSeconds: null,
        LapTimeSource: "unavailable",
        EstimatedMinutesRemaining: null,
        EstimatedLapsRemaining: null,
        Confidence: "none");

    public static LiveFuelSnapshot From(HistoricalSessionContext context, HistoricalTelemetrySample sample)
    {
        if (!IsPositiveFinite(sample.FuelLevelLiters))
        {
            return Unavailable;
        }

        var fuelLevelLiters = sample.FuelLevelLiters;
        double? fuelLevelPercent = IsFinite(sample.FuelLevelPercent) && sample.FuelLevelPercent >= 0d
            ? sample.FuelLevelPercent
            : null;
        double? fuelUsePerHourKg = IsPositiveFinite(sample.FuelUsePerHourKg)
            ? sample.FuelUsePerHourKg
            : null;
        var fuelUsePerHourLiters = CalculateFuelUsePerHourLiters(context, fuelUsePerHourKg);
        var hasFuelUse = fuelUsePerHourLiters is not null && fuelUsePerHourLiters.Value > 0d;
        double? estimatedMinutesRemaining = hasFuelUse
            ? fuelLevelLiters / fuelUsePerHourLiters!.Value * 60d
            : null;
        var lapTime = SelectLapTime(context, sample);
        var fuelPerLapLiters = CalculateFuelPerLapLiters(fuelUsePerHourLiters, lapTime.Seconds);
        double? estimatedLapsRemaining = fuelPerLapLiters is not null && fuelPerLapLiters.Value > 0d
            ? fuelLevelLiters / fuelPerLapLiters.Value
            : null;

        return new LiveFuelSnapshot(
            HasValidFuel: true,
            Source: "local-driver-scalar",
            FuelLevelLiters: fuelLevelLiters,
            FuelLevelPercent: fuelLevelPercent,
            FuelUsePerHourKg: fuelUsePerHourKg,
            FuelUsePerHourLiters: fuelUsePerHourLiters,
            FuelPerLapLiters: fuelPerLapLiters,
            LapTimeSeconds: lapTime.Seconds,
            LapTimeSource: lapTime.Source,
            EstimatedMinutesRemaining: estimatedMinutesRemaining,
            EstimatedLapsRemaining: estimatedLapsRemaining,
            Confidence: hasFuelUse ? "live" : "level-only");
    }

    private static double? CalculateFuelUsePerHourLiters(HistoricalSessionContext context, double? fuelUsePerHourKg)
    {
        var fuelKgPerLiter = context.Car.DriverCarFuelKgPerLiter;
        if (fuelUsePerHourKg is null || fuelKgPerLiter is null || fuelKgPerLiter <= 0d)
        {
            return null;
        }

        return fuelUsePerHourKg.Value / fuelKgPerLiter.Value;
    }

    private static double? CalculateFuelPerLapLiters(double? fuelUsePerHourLiters, double? estimatedLapSeconds)
    {
        if (fuelUsePerHourLiters is null
            || fuelUsePerHourLiters.Value <= 0d
            || estimatedLapSeconds is null
            || estimatedLapSeconds.Value <= 0d)
        {
            return null;
        }

        return fuelUsePerHourLiters.Value * estimatedLapSeconds.Value / 3600d;
    }

    private static LapTimeSelection SelectLapTime(HistoricalSessionContext context, HistoricalTelemetrySample sample)
    {
        if (IsValidLapTime(sample.TeamLastLapTimeSeconds))
        {
            return new LapTimeSelection(sample.TeamLastLapTimeSeconds, "team-last-lap");
        }

        if (IsValidLapTime(sample.LapLastLapTimeSeconds))
        {
            return new LapTimeSelection(sample.LapLastLapTimeSeconds, "player-last-lap");
        }

        if (IsValidLapTime(context.Car.DriverCarEstLapTimeSeconds))
        {
            return new LapTimeSelection(context.Car.DriverCarEstLapTimeSeconds, "driver-estimate");
        }

        if (IsValidLapTime(context.Car.CarClassEstLapTimeSeconds))
        {
            return new LapTimeSelection(context.Car.CarClassEstLapTimeSeconds, "class-estimate");
        }

        return new LapTimeSelection(null, "unavailable");
    }

    private static bool IsValidLapTime(double? seconds)
    {
        return seconds is > 20d and < 1800d && IsFinite(seconds.Value);
    }

    private static bool IsPositiveFinite(double value)
    {
        return IsFinite(value) && value > 0d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private sealed record LapTimeSelection(double? Seconds, string Source);
}
