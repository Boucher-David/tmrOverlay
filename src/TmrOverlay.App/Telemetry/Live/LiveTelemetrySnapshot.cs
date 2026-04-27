using TmrOverlay.App.History;

namespace TmrOverlay.App.Telemetry.Live;

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
    LiveLeaderGapSnapshot LeaderGap)
{
    public int CompletedStintCount { get; init; }

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
        LeaderGap: LiveLeaderGapSnapshot.Unavailable);
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
        var playerLapDistPct = PlayerLapDistPct(sample);
        if (playerLapDistPct is null)
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
                playerLapDistPct.Value,
                sample.TeamEstimatedTimeSeconds,
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

    private static double? PlayerLapDistPct(HistoricalTelemetrySample sample)
    {
        if (sample.TeamLapDistPct is { } teamLapDistPct && IsFinite(teamLapDistPct) && teamLapDistPct >= 0d)
        {
            return Math.Clamp(teamLapDistPct, 0d, 1d);
        }

        return IsFinite(sample.LapDistPct) && sample.LapDistPct >= 0d
            ? Math.Clamp(sample.LapDistPct, 0d, 1d)
            : null;
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
    int? TeamOverallPosition,
    int? TeamClassPosition,
    int? OverallLeaderCarIdx,
    int? ClassLeaderCarIdx,
    LiveGapValue OverallLeaderGap,
    LiveGapValue ClassLeaderGap,
    IReadOnlyList<LiveClassGapCar> ClassCars)
{
    public static LiveLeaderGapSnapshot Unavailable { get; } = new(
        HasData: false,
        TeamOverallPosition: null,
        TeamClassPosition: null,
        OverallLeaderCarIdx: null,
        ClassLeaderCarIdx: null,
        OverallLeaderGap: LiveGapValue.Unavailable,
        ClassLeaderGap: LiveGapValue.Unavailable,
        ClassCars: []);

    public static LiveLeaderGapSnapshot From(HistoricalTelemetrySample sample)
    {
        var teamProgress = TeamProgress(sample);
        var overallGap = BuildGap(
            position: sample.TeamPosition,
            leaderCarIdx: sample.LeaderCarIdx,
            playerCarIdx: sample.PlayerCarIdx,
            teamF2TimeSeconds: sample.TeamF2TimeSeconds,
            leaderF2TimeSeconds: sample.LeaderF2TimeSeconds,
            teamProgress: teamProgress,
            leaderProgress: Progress(sample.LeaderLapCompleted, sample.LeaderLapDistPct));
        var classGap = BuildGap(
            position: sample.TeamClassPosition,
            leaderCarIdx: sample.ClassLeaderCarIdx,
            playerCarIdx: sample.PlayerCarIdx,
            teamF2TimeSeconds: sample.TeamF2TimeSeconds,
            leaderF2TimeSeconds: sample.ClassLeaderF2TimeSeconds,
            teamProgress: teamProgress,
            leaderProgress: Progress(sample.ClassLeaderLapCompleted, sample.ClassLeaderLapDistPct));

        return new LiveLeaderGapSnapshot(
            HasData: overallGap.HasData || classGap.HasData,
            TeamOverallPosition: sample.TeamPosition,
            TeamClassPosition: sample.TeamClassPosition,
            OverallLeaderCarIdx: sample.LeaderCarIdx,
            ClassLeaderCarIdx: sample.ClassLeaderCarIdx,
            OverallLeaderGap: overallGap,
            ClassLeaderGap: classGap,
            ClassCars: BuildClassCars(sample, teamProgress, classGap));
    }

    private static IReadOnlyList<LiveClassGapCar> BuildClassCars(
        HistoricalTelemetrySample sample,
        double? teamProgress,
        LiveGapValue teamClassGap)
    {
        var playerCarIdx = sample.PlayerCarIdx;
        var playerClass = sample.TeamCarClass;
        var classLeaderF2 = ValidGapSeconds(sample.ClassLeaderF2TimeSeconds);
        var classLeaderProgress = Progress(sample.ClassLeaderLapCompleted, sample.ClassLeaderLapDistPct);
        var cars = new List<LiveClassGapCar>();

        if (sample.ClassLeaderCarIdx is { } leaderIdx)
        {
            cars.Add(new LiveClassGapCar(
                CarIdx: leaderIdx,
                IsTeamCar: playerCarIdx == leaderIdx,
                IsClassLeader: true,
                ClassPosition: 1,
                GapSecondsToClassLeader: 0d,
                GapLapsToClassLeader: 0d,
                DeltaSecondsToTeam: teamClassGap.Seconds is { } teamSeconds ? -teamSeconds : null));
        }

        if (playerCarIdx is { } teamIdx)
        {
            cars.Add(new LiveClassGapCar(
                CarIdx: teamIdx,
                IsTeamCar: true,
                IsClassLeader: teamClassGap.IsLeader,
                ClassPosition: sample.TeamClassPosition,
                GapSecondsToClassLeader: teamClassGap.Seconds,
                GapLapsToClassLeader: teamClassGap.Laps,
                DeltaSecondsToTeam: 0d));
        }

        var classCandidates = sample.ClassCars is { Count: > 0 }
            ? sample.ClassCars
            : sample.NearbyCars ?? [];
        var requireExplicitClassMatch = sample.ClassCars is not { Count: > 0 };

        foreach (var car in classCandidates)
        {
            if (car.CarIdx == playerCarIdx || car.CarIdx == sample.ClassLeaderCarIdx)
            {
                continue;
            }

            if (!IsUserClassCar(car, playerClass, requireExplicitClassMatch))
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
                IsTeamCar: false,
                IsClassLeader: car.CarIdx == sample.ClassLeaderCarIdx,
                ClassPosition: car.ClassPosition,
                GapSecondsToClassLeader: gapSeconds,
                GapLapsToClassLeader: gapLaps,
                DeltaSecondsToTeam: CalculateDeltaSecondsToTeam(gapSeconds, teamClassGap.Seconds)));
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

    private static double? TeamProgress(HistoricalTelemetrySample sample)
    {
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
    bool IsTeamCar,
    bool IsClassLeader,
    int? ClassPosition,
    double? GapSecondsToClassLeader,
    double? GapLapsToClassLeader,
    double? DeltaSecondsToTeam);

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
