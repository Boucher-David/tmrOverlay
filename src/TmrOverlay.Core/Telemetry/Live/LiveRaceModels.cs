using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal enum LiveModelQuality
{
    Unavailable = 0,
    Partial = 1,
    Inferred = 2,
    Reliable = 3
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
    double? TrackLengthMeters,
    double? ReferenceLapDistPct,
    IReadOnlyList<LiveSpatialCar> Cars)
{
    public static LiveSpatialModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        ReferenceCarIdx: null,
        TrackLengthMeters: null,
        ReferenceLapDistPct: null,
        Cars: []);
}

internal sealed record LiveSpatialCar(
    int CarIdx,
    LiveModelQuality Quality,
    double RelativeLaps,
    double? RelativeMeters,
    int? CarClass,
    int? TrackSurface,
    bool? OnPitRoad);

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
    bool HasSteeringInput)
{
    public static LiveInputTelemetryModel Empty { get; } = new(
        HasData: false,
        Quality: LiveModelQuality.Unavailable,
        SpeedMetersPerSecond: null,
        PlayerTireCompound: null,
        HasPedalInputs: false,
        HasSteeringInput: false);
}
