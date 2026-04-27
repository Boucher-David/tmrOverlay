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
    LiveFuelSnapshot Fuel)
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
        Fuel: LiveFuelSnapshot.Unavailable);
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
        var fuelLevelPercent = IsFinite(sample.FuelLevelPercent) && sample.FuelLevelPercent >= 0d
            ? sample.FuelLevelPercent
            : null;
        var fuelUsePerHourKg = IsPositiveFinite(sample.FuelUsePerHourKg)
            ? sample.FuelUsePerHourKg
            : null;
        var fuelUsePerHourLiters = CalculateFuelUsePerHourLiters(context, fuelUsePerHourKg);
        var hasFuelUse = fuelUsePerHourLiters is not null && fuelUsePerHourLiters.Value > 0d;
        var estimatedMinutesRemaining = hasFuelUse
            ? fuelLevelLiters / fuelUsePerHourLiters!.Value * 60d
            : null;
        var lapTime = SelectLapTime(context, sample);
        var fuelPerLapLiters = CalculateFuelPerLapLiters(fuelUsePerHourLiters, lapTime.Seconds);
        var estimatedLapsRemaining = fuelPerLapLiters is not null && fuelPerLapLiters.Value > 0d
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
