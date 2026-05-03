using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal enum LiveModelQuality
{
    Unavailable = 0,
    Partial = 1,
    Inferred = 2,
    Reliable = 3
}

internal sealed record LiveSignalEvidence(
    string Source,
    LiveModelQuality Quality,
    bool IsUsable,
    string? MissingReason)
{
    public static LiveSignalEvidence Reliable(string source)
    {
        return new LiveSignalEvidence(source, LiveModelQuality.Reliable, IsUsable: true, MissingReason: null);
    }

    public static LiveSignalEvidence Inferred(string source)
    {
        return new LiveSignalEvidence(source, LiveModelQuality.Inferred, IsUsable: true, MissingReason: null);
    }

    public static LiveSignalEvidence Partial(string source, string reason)
    {
        return new LiveSignalEvidence(source, LiveModelQuality.Partial, IsUsable: false, MissingReason: reason);
    }

    public static LiveSignalEvidence DiagnosticOnly(string source, string reason)
    {
        return new LiveSignalEvidence(source, LiveModelQuality.Partial, IsUsable: false, MissingReason: reason);
    }

    public static LiveSignalEvidence Unavailable(string source, string reason)
    {
        return new LiveSignalEvidence(source, LiveModelQuality.Unavailable, IsUsable: false, MissingReason: reason);
    }
}

internal sealed record LiveRaceModels(
    LiveSessionModel Session,
    LiveDriverDirectoryModel DriverDirectory,
    LiveTimingModel Timing,
    LiveRelativeModel Relative,
    LiveSpatialModel Spatial,
    LiveWeatherModel Weather,
    LiveFuelPitModel FuelPit,
    LiveRaceEventModel RaceEvents,
    LiveInputTelemetryModel Inputs)
{
    public static LiveRaceModels Empty { get; } = new(
        Session: LiveSessionModel.Empty,
        DriverDirectory: LiveDriverDirectoryModel.Empty,
        Timing: LiveTimingModel.Empty,
        Relative: LiveRelativeModel.Empty,
        Spatial: LiveSpatialModel.Empty,
        Weather: LiveWeatherModel.Empty,
        FuelPit: LiveFuelPitModel.Empty,
        RaceEvents: LiveRaceEventModel.Empty,
        Inputs: LiveInputTelemetryModel.Empty);
}

internal sealed record LiveSessionModel(
    bool HasData,
    LiveModelQuality Quality,
    HistoricalComboIdentity Combo,
    string? SessionType,
    string? SessionName,
    string? EventType,
    bool? TeamRacing,
    double? SessionTimeSeconds,
    double? SessionTimeRemainSeconds,
    double? SessionTimeTotalSeconds,
    int? SessionLapsRemain,
    int? SessionLapsTotal,
    int? RaceLaps,
    int? SessionState,
    int? SessionFlags,
    string? TrackDisplayName,
    double? TrackLengthKm,
    string? CarDisplayName,
    IReadOnlyList<string> MissingSignals)
{
    public static LiveSessionModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        Combo: HistoricalComboIdentity.From(HistoricalSessionContext.Empty),
        SessionType: null,
        SessionName: null,
        EventType: null,
        TeamRacing: null,
        SessionTimeSeconds: null,
        SessionTimeRemainSeconds: null,
        SessionTimeTotalSeconds: null,
        SessionLapsRemain: null,
        SessionLapsTotal: null,
        RaceLaps: null,
        SessionState: null,
        SessionFlags: null,
        TrackDisplayName: null,
        TrackLengthKm: null,
        CarDisplayName: null,
        MissingSignals: []);
}

internal sealed record LiveDriverDirectoryModel(
    bool HasData,
    LiveModelQuality Quality,
    int? PlayerCarIdx,
    int? FocusCarIdx,
    int? ReferenceCarClass,
    LiveDriverIdentity? PlayerDriver,
    LiveDriverIdentity? FocusDriver,
    IReadOnlyList<LiveDriverIdentity> Drivers)
{
    public static LiveDriverDirectoryModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        PlayerCarIdx: null,
        FocusCarIdx: null,
        ReferenceCarClass: null,
        PlayerDriver: null,
        FocusDriver: null,
        Drivers: []);
}

internal sealed record LiveDriverIdentity(
    int CarIdx,
    string? DriverName,
    string? AbbrevName,
    string? Initials,
    int? UserId,
    int? TeamId,
    string? TeamName,
    string? CarNumber,
    int? CarClassId,
    string? CarClassName,
    string? CarClassColorHex,
    bool? IsSpectator);

internal sealed record LiveTimingModel(
    bool HasData,
    LiveModelQuality Quality,
    int? PlayerCarIdx,
    int? FocusCarIdx,
    int? OverallLeaderCarIdx,
    int? ClassLeaderCarIdx,
    LiveSignalEvidence OverallLeaderGapEvidence,
    LiveSignalEvidence ClassLeaderGapEvidence,
    LiveTimingRow? PlayerRow,
    LiveTimingRow? FocusRow,
    IReadOnlyList<LiveTimingRow> OverallRows,
    IReadOnlyList<LiveTimingRow> ClassRows)
{
    public static LiveTimingModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        PlayerCarIdx: null,
        FocusCarIdx: null,
        OverallLeaderCarIdx: null,
        ClassLeaderCarIdx: null,
        OverallLeaderGapEvidence: LiveSignalEvidence.Unavailable("overall-gap", "no_timing_sample"),
        ClassLeaderGapEvidence: LiveSignalEvidence.Unavailable("class-gap", "no_timing_sample"),
        PlayerRow: null,
        FocusRow: null,
        OverallRows: [],
        ClassRows: []);
}

internal sealed record LiveTimingRow(
    int CarIdx,
    LiveModelQuality Quality,
    string Source,
    bool IsPlayer,
    bool IsFocus,
    bool IsOverallLeader,
    bool IsClassLeader,
    bool HasTiming,
    bool HasSpatialProgress,
    bool CanUseForRadarPlacement,
    LiveSignalEvidence TimingEvidence,
    LiveSignalEvidence SpatialEvidence,
    LiveSignalEvidence RadarPlacementEvidence,
    LiveSignalEvidence GapEvidence,
    string? DriverName,
    string? TeamName,
    string? CarNumber,
    string? CarClassName,
    string? CarClassColorHex,
    int? OverallPosition,
    int? ClassPosition,
    int? CarClass,
    int? LapCompleted,
    double? LapDistPct,
    double? ProgressLaps,
    double? F2TimeSeconds,
    double? EstimatedTimeSeconds,
    double? LastLapTimeSeconds,
    double? BestLapTimeSeconds,
    double? GapSecondsToClassLeader,
    double? GapLapsToClassLeader,
    double? DeltaSecondsToFocus,
    int? TrackSurface,
    bool? OnPitRoad);

internal sealed record LiveRelativeModel(
    bool HasData,
    LiveModelQuality Quality,
    int? ReferenceCarIdx,
    IReadOnlyList<LiveRelativeRow> Rows)
{
    public static LiveRelativeModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        ReferenceCarIdx: null,
        Rows: []);
}

internal sealed record LiveRelativeRow(
    int CarIdx,
    LiveModelQuality Quality,
    string Source,
    bool IsAhead,
    bool IsBehind,
    bool IsSameClass,
    LiveSignalEvidence TimingEvidence,
    LiveSignalEvidence PlacementEvidence,
    string? DriverName,
    int? OverallPosition,
    int? ClassPosition,
    int? CarClass,
    double? RelativeSeconds,
    double? RelativeLaps,
    double? RelativeMeters,
    bool? OnPitRoad);

internal sealed record LiveSpatialModel(
    bool HasData,
    LiveModelQuality Quality,
    int? ReferenceCarIdx,
    int? ReferenceCarClass,
    int? CarLeftRight,
    string SideStatus,
    bool HasCarLeft,
    bool HasCarRight,
    double SideOverlapWindowSeconds,
    double? TrackLengthMeters,
    double? ReferenceLapDistPct,
    IReadOnlyList<LiveSpatialCar> Cars,
    LiveSpatialCar? NearestAhead,
    LiveSpatialCar? NearestBehind,
    IReadOnlyList<LiveMulticlassApproach> MulticlassApproaches,
    LiveMulticlassApproach? StrongestMulticlassApproach)
{
    public static LiveSpatialModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        ReferenceCarIdx: null,
        ReferenceCarClass: null,
        CarLeftRight: null,
        SideStatus: "waiting",
        HasCarLeft: false,
        HasCarRight: false,
        SideOverlapWindowSeconds: 0.22d,
        TrackLengthMeters: null,
        ReferenceLapDistPct: null,
        Cars: [],
        NearestAhead: null,
        NearestBehind: null,
        MulticlassApproaches: [],
        StrongestMulticlassApproach: null);
}

internal sealed record LiveSpatialCar(
    int CarIdx,
    LiveModelQuality Quality,
    LiveSignalEvidence PlacementEvidence,
    double RelativeLaps,
    double? RelativeSeconds,
    double? RelativeMeters,
    int? OverallPosition,
    int? ClassPosition,
    int? CarClass,
    int? TrackSurface,
    bool? OnPitRoad,
    string? CarClassColorHex = null)
{
    public bool HasReliableRelativeSeconds =>
        RelativeSeconds is { } seconds && !double.IsNaN(seconds) && !double.IsInfinity(seconds);
}

internal sealed record LiveWeatherModel(
    bool HasData,
    LiveModelQuality Quality,
    double? AirTempC,
    double? TrackTempCrewC,
    int? TrackWetness,
    string? TrackWetnessLabel,
    bool? WeatherDeclaredWet,
    bool DeclaredWetSurfaceMismatch,
    string? WeatherType,
    string? SkiesLabel,
    double? PrecipitationPercent,
    string? RubberState)
{
    public static LiveWeatherModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        AirTempC: null,
        TrackTempCrewC: null,
        TrackWetness: null,
        TrackWetnessLabel: null,
        WeatherDeclaredWet: null,
        DeclaredWetSurfaceMismatch: false,
        WeatherType: null,
        SkiesLabel: null,
        PrecipitationPercent: null,
        RubberState: null);
}

internal sealed record LiveFuelPitModel(
    bool HasData,
    LiveModelQuality Quality,
    LiveFuelSnapshot Fuel,
    bool OnPitRoad,
    bool PitstopActive,
    bool PlayerCarInPitStall,
    bool? TeamOnPitRoad,
    LiveSignalEvidence FuelLevelEvidence,
    LiveSignalEvidence InstantaneousBurnEvidence,
    LiveSignalEvidence MeasuredBurnEvidence,
    LiveSignalEvidence BaselineEligibilityEvidence,
    int? PitServiceFlags,
    double? PitServiceFuelLiters,
    double? PitRepairLeftSeconds,
    double? PitOptRepairLeftSeconds,
    int? TireSetsUsed,
    int? FastRepairUsed,
    int? TeamFastRepairsUsed)
{
    public static LiveFuelPitModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        Fuel: LiveFuelSnapshot.Unavailable,
        OnPitRoad: false,
        PitstopActive: false,
        PlayerCarInPitStall: false,
        TeamOnPitRoad: null,
        FuelLevelEvidence: LiveSignalEvidence.Unavailable("FuelLevel", "missing_or_zero_fuel_level"),
        InstantaneousBurnEvidence: LiveSignalEvidence.Unavailable("FuelUsePerHour", "missing_or_zero_fuel_use"),
        MeasuredBurnEvidence: LiveSignalEvidence.Unavailable("rolling-local-fuel-delta", "requires_two_green_distance_samples"),
        BaselineEligibilityEvidence: LiveSignalEvidence.Unavailable("measured-local-fuel-baseline", "requires_rolling_local_driver_window"),
        PitServiceFlags: null,
        PitServiceFuelLiters: null,
        PitRepairLeftSeconds: null,
        PitOptRepairLeftSeconds: null,
        TireSetsUsed: null,
        FastRepairUsed: null,
        TeamFastRepairsUsed: null);
}

internal sealed record LiveRaceEventModel(
    bool HasData,
    LiveModelQuality Quality,
    bool IsOnTrack,
    bool IsInGarage,
    bool OnPitRoad,
    int Lap,
    int LapCompleted,
    double LapDistPct,
    int? DriversSoFar,
    int? DriverChangeLapStatus)
{
    public static LiveRaceEventModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        IsOnTrack: false,
        IsInGarage: false,
        OnPitRoad: false,
        Lap: 0,
        LapCompleted: 0,
        LapDistPct: 0d,
        DriversSoFar: null,
        DriverChangeLapStatus: null);
}

internal sealed record LiveInputTelemetryModel(
    bool HasData,
    LiveModelQuality Quality,
    double? SpeedMetersPerSecond,
    int? PlayerTireCompound,
    bool HasPedalInputs,
    bool HasSteeringInput,
    int? Gear,
    double? Rpm,
    double? Throttle,
    double? Brake,
    double? Clutch,
    double? SteeringWheelAngle,
    int? EngineWarnings,
    double? Voltage,
    double? WaterTempC,
    double? FuelPressureBar,
    double? OilTempC,
    double? OilPressureBar)
{
    public static LiveInputTelemetryModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        SpeedMetersPerSecond: null,
        PlayerTireCompound: null,
        HasPedalInputs: false,
        HasSteeringInput: false,
        Gear: null,
        Rpm: null,
        Throttle: null,
        Brake: null,
        Clutch: null,
        SteeringWheelAngle: null,
        EngineWarnings: null,
        Voltage: null,
        WaterTempC: null,
        FuelPressureBar: null,
        OilTempC: null,
        OilPressureBar: null);
}
