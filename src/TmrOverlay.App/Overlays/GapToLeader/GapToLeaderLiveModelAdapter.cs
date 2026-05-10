using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.GapToLeader;

internal static class GapToLeaderLiveModelAdapter
{
    public static LiveLeaderGapSnapshot Select(LiveTelemetrySnapshot snapshot)
    {
        var timing = snapshot.Models.Timing;
        var progress = snapshot.Models.RaceProgress;
        if (!timing.HasData && !progress.HasData)
        {
            return snapshot.LeaderGap;
        }

        var focusRow = timing.FocusRow;
        var overallGap = BuildGap(
            focusRow?.OverallPosition ?? progress.ReferenceOverallPosition,
            timing.OverallLeaderCarIdx,
            timing.FocusCarIdx,
            referenceRow: null,
            progress.ReferenceOverallLeaderGapLaps);
        var classGap = BuildGap(
            focusRow?.ClassPosition ?? progress.ReferenceClassPosition,
            timing.ClassLeaderCarIdx,
            timing.FocusCarIdx,
            focusRow,
            progress.ReferenceClassLeaderGapLaps);
        var classCars = BuildClassCars(timing, progress).ToArray();

        return new LiveLeaderGapSnapshot(
            HasData: classGap.HasData
                || overallGap.HasData
                || classCars.Any(car => car.IsReferenceCar || (!car.IsClassLeader && HasChartGap(car))),
            ReferenceOverallPosition: focusRow?.OverallPosition ?? progress.ReferenceOverallPosition,
            ReferenceClassPosition: focusRow?.ClassPosition ?? progress.ReferenceClassPosition,
            OverallLeaderCarIdx: timing.OverallLeaderCarIdx,
            ClassLeaderCarIdx: timing.ClassLeaderCarIdx,
            OverallLeaderGap: overallGap,
            ClassLeaderGap: classGap,
            ClassCars: classCars);
    }

    public static double? SelectFocusedTrendPointSeconds(
        LiveTelemetrySnapshot snapshot,
        LiveLeaderGapSnapshot? gap = null)
    {
        var selectedGap = gap ?? Select(snapshot);
        var lapReferenceSeconds = SelectLapReferenceSeconds(snapshot);
        return ChartGapSeconds(selectedGap.ClassLeaderGap, lapReferenceSeconds)
            ?? selectedGap.ClassCars
                .Where(car => car.IsReferenceCar)
                .Select(car => ChartGapSeconds(car, lapReferenceSeconds))
                .FirstOrDefault(seconds => seconds is not null);
    }

    public static double? SelectLapReferenceSeconds(LiveTelemetrySnapshot snapshot)
    {
        var focusRow = snapshot.Models.Timing.FocusRow;
        if (IsValidLapReference(focusRow?.LastLapTimeSeconds))
        {
            return focusRow?.LastLapTimeSeconds;
        }

        if (IsValidLapReference(focusRow?.BestLapTimeSeconds))
        {
            return focusRow?.BestLapTimeSeconds;
        }

        if (ReferenceUsesPlayerCar(snapshot) && IsValidLapReference(snapshot.Models.FuelPit.Fuel.LapTimeSeconds))
        {
            return snapshot.Models.FuelPit.Fuel.LapTimeSeconds;
        }

        if (IsValidLapReference(snapshot.Models.RaceProgress.StrategyLapTimeSeconds))
        {
            return snapshot.Models.RaceProgress.StrategyLapTimeSeconds;
        }

        if (ReferenceUsesPlayerCar(snapshot) && IsValidLapReference(snapshot.Context.Car.DriverCarEstLapTimeSeconds))
        {
            return snapshot.Context.Car.DriverCarEstLapTimeSeconds;
        }

        return ReferenceUsesTeamClass(snapshot) && IsValidLapReference(snapshot.Context.Car.CarClassEstLapTimeSeconds)
            ? snapshot.Context.Car.CarClassEstLapTimeSeconds
            : null;
    }

    private static LiveGapValue BuildGap(
        int? position,
        int? leaderCarIdx,
        int? referenceCarIdx,
        LiveTimingRow? referenceRow,
        double? progressGapLaps)
    {
        if (position == 1 || (leaderCarIdx is not null && leaderCarIdx == referenceCarIdx))
        {
            return new LiveGapValue(
                HasData: true,
                IsLeader: true,
                Seconds: 0d,
                Laps: 0d,
                Source: "position");
        }

        if (referenceRow is not null && referenceRow.GapEvidence.IsUsable)
        {
            if (ValidGapSeconds(referenceRow.GapSecondsToClassLeader) is { } seconds)
            {
                return new LiveGapValue(
                    HasData: true,
                    IsLeader: false,
                    Seconds: seconds,
                    Laps: null,
                    Source: referenceRow.GapEvidence.Source);
            }

            if (ValidGapLaps(referenceRow.GapLapsToClassLeader) is { } laps)
            {
                return new LiveGapValue(
                    HasData: true,
                    IsLeader: false,
                    Seconds: null,
                    Laps: laps,
                    Source: referenceRow.GapEvidence.Source);
            }
        }

        if (ValidGapLaps(progressGapLaps) is { } progressLaps)
        {
            return new LiveGapValue(
                HasData: true,
                IsLeader: false,
                Seconds: null,
                Laps: progressLaps,
                Source: "LiveRaceProgress");
        }

        return LiveGapValue.Unavailable;
    }

    private static IEnumerable<LiveClassGapCar> BuildClassCars(
        LiveTimingModel timing,
        LiveRaceProgressModel progress)
    {
        var rows = timing.ClassRows.Count > 0
            ? timing.ClassRows
            : timing.OverallRows;
        var cars = rows
            .Where(row => row.IsClassLeader
                || row.IsFocus
                || row.GapEvidence.IsUsable
                || row.DeltaSecondsToFocus is not null)
            .Select(row => ToClassGapCar(row, progress))
            .Where(car => car.IsClassLeader || car.IsReferenceCar || HasChartGap(car) || car.DeltaSecondsToReference is not null)
            .GroupBy(car => car.CarIdx)
            .Select(group => group
                .OrderByDescending(car => car.IsReferenceCar)
                .ThenByDescending(car => car.IsClassLeader)
                .First())
            .OrderBy(car => car.GapSecondsToClassLeader ?? double.MaxValue)
            .ThenBy(car => car.GapLapsToClassLeader ?? double.MaxValue)
            .ThenBy(car => car.ClassPosition ?? int.MaxValue)
            .ThenBy(car => car.CarIdx);

        return cars;
    }

    private static LiveClassGapCar ToClassGapCar(LiveTimingRow row, LiveRaceProgressModel progress)
    {
        double? gapSeconds = row.IsClassLeader
            ? 0d
            : row.GapEvidence.IsUsable
                ? ValidGapSeconds(row.GapSecondsToClassLeader)
                : null;
        double? gapLaps = row.IsClassLeader
            ? 0d
            : row.GapEvidence.IsUsable
                ? ValidGapLaps(row.GapLapsToClassLeader)
                : null;
        if (row.IsFocus && gapSeconds is null && gapLaps is null)
        {
            gapLaps = ValidGapLaps(progress.ReferenceClassLeaderGapLaps);
        }

        return new LiveClassGapCar(
            CarIdx: row.CarIdx,
            IsReferenceCar: row.IsFocus,
            IsClassLeader: row.IsClassLeader,
            ClassPosition: row.ClassPosition,
            GapSecondsToClassLeader: gapSeconds,
            GapLapsToClassLeader: gapLaps,
            DeltaSecondsToReference: row.GapEvidence.IsUsable ? row.DeltaSecondsToFocus : null);
    }

    private static bool HasChartGap(LiveClassGapCar car)
    {
        return car.GapSecondsToClassLeader is not null || car.GapLapsToClassLeader is not null;
    }

    private static double? ChartGapSeconds(LiveGapValue gap, double? lapReferenceSeconds)
    {
        if (gap.IsLeader)
        {
            return 0d;
        }

        return ValidGapSeconds(gap.Seconds)
            ?? (ValidGapLaps(gap.Laps) is { } laps ? laps * ChartLapReferenceSeconds(lapReferenceSeconds) : null);
    }

    private static double? ChartGapSeconds(LiveClassGapCar car, double? lapReferenceSeconds)
    {
        return ValidGapSeconds(car.GapSecondsToClassLeader)
            ?? (ValidGapLaps(car.GapLapsToClassLeader) is { } laps ? laps * ChartLapReferenceSeconds(lapReferenceSeconds) : null);
    }

    private static double ChartLapReferenceSeconds(double? lapReferenceSeconds)
    {
        return IsValidLapReference(lapReferenceSeconds) ? lapReferenceSeconds.Value : 60d;
    }

    private static bool ReferenceUsesPlayerCar(LiveTelemetrySnapshot snapshot)
    {
        var directory = snapshot.Models.DriverDirectory;
        return directory.FocusCarIdx is null
            || directory.PlayerCarIdx is null
            || directory.FocusCarIdx == directory.PlayerCarIdx;
    }

    private static bool ReferenceUsesTeamClass(LiveTelemetrySnapshot snapshot)
    {
        var directory = snapshot.Models.DriverDirectory;
        var focusClass = directory.FocusDriver?.CarClassId ?? directory.ReferenceCarClass;
        var playerClass = directory.PlayerDriver?.CarClassId ?? directory.ReferenceCarClass;
        return focusClass is null
            || playerClass is null
            || focusClass == playerClass;
    }

    private static bool IsValidLapReference(double? seconds)
    {
        return seconds is { } value && value is > 20d and < 1800d && IsFinite(value);
    }

    private static double? ValidGapSeconds(double? seconds)
    {
        return seconds is { } value && IsFinite(value) && value >= 0d && value < 86400d
            ? value
            : null;
    }

    private static double? ValidGapLaps(double? laps)
    {
        return laps is { } value && IsFinite(value) && value >= 0d
            ? value
            : null;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
