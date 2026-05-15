using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.GapToLeader;

internal static class GapToLeaderLiveModelAdapter
{
    private const int OnTrackSurface = 3;

    public static LiveLeaderGapSnapshot Select(LiveTelemetrySnapshot snapshot)
    {
        var timing = snapshot.Models.Timing;
        var progress = snapshot.Models.RaceProgress;
        var reference = snapshot.Models.Reference;
        if (!timing.HasData && !progress.HasData)
        {
            return snapshot.LeaderGap;
        }

        var focusRow = timing.FocusRow;
        var overallGap = BuildGap(
            focusRow?.OverallPosition ?? reference.OverallPosition ?? progress.ReferenceOverallPosition,
            timing.OverallLeaderCarIdx,
            reference.FocusCarIdx ?? timing.FocusCarIdx,
            referenceRow: null,
            progress.ReferenceOverallLeaderGapLaps);
        var classGap = BuildGap(
            focusRow?.ClassPosition ?? reference.ClassPosition ?? progress.ReferenceClassPosition,
            timing.ClassLeaderCarIdx,
            reference.FocusCarIdx ?? timing.FocusCarIdx,
            focusRow,
            progress.ReferenceClassLeaderGapLaps);
        var classCars = BuildClassCars(timing, progress).ToArray();

        return new LiveLeaderGapSnapshot(
            HasData: classGap.HasData
                || overallGap.HasData
                || classCars.Any(car => !car.IsClassLeader && HasChartGap(car)),
            ReferenceOverallPosition: focusRow?.OverallPosition ?? reference.OverallPosition ?? progress.ReferenceOverallPosition,
            ReferenceClassPosition: focusRow?.ClassPosition ?? reference.ClassPosition ?? progress.ReferenceClassPosition,
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
        if (referenceRow is not null && IsPlaceholderPitGapRow(referenceRow))
        {
            return LiveGapValue.Unavailable;
        }

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
        var canUseRowGap = !IsPlaceholderPitGapRow(row);
        double? gapSeconds = row.IsClassLeader && canUseRowGap
            ? 0d
            : canUseRowGap && row.GapEvidence.IsUsable
                ? ValidGapSeconds(row.GapSecondsToClassLeader)
                : null;
        double? gapLaps = row.IsClassLeader && canUseRowGap
            ? 0d
            : canUseRowGap && row.GapEvidence.IsUsable
                ? ValidGapLaps(row.GapLapsToClassLeader)
                : null;
        if (canUseRowGap && row.IsFocus && gapSeconds is null && gapLaps is null)
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
            DeltaSecondsToReference: canUseRowGap && row.GapEvidence.IsUsable ? row.DeltaSecondsToFocus : null,
            IsOnPitRoad: IsPitRoadLike(row.TrackSurface, row.OnPitRoad),
            CurrentLap: row.LapCompleted is >= 0 ? row.LapCompleted.Value + 1 : null);
    }

    private static bool IsPlaceholderPitGapRow(LiveTimingRow row)
    {
        if (row.HasTakenGrid)
        {
            return false;
        }

        if (row.OnPitRoad != true && !IsKnownNonTrackSurface(row.TrackSurface))
        {
            return false;
        }

        return !HasUsableRaceTiming(row)
            && row.LapCompleted is null or <= 0;
    }

    private static bool HasUsableRaceTiming(LiveTimingRow row)
    {
        return ValidGapSeconds(row.F2TimeSeconds) is { } f2
            && f2 >= 0.1d
            && !IsRaceF2Placeholder(row.F2TimeSeconds, row.OverallPosition);
    }

    private static bool IsKnownNonTrackSurface(int? trackSurface)
    {
        return trackSurface is not null && trackSurface != OnTrackSurface;
    }

    private static bool IsPitRoadLike(int? trackSurface, bool? onPitRoad)
    {
        return onPitRoad == true || trackSurface is 1 or 2;
    }

    private static bool IsRaceF2Placeholder(double? f2TimeSeconds, int? overallPosition)
    {
        if (overallPosition is not > 1
            || ValidGapSeconds(f2TimeSeconds) is not { } f2)
        {
            return false;
        }

        return Math.Abs(f2 - ((overallPosition.Value - 1) / 1000d)) <= 0.00002d;
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
        return lapReferenceSeconds is { } seconds && IsValidLapReference(seconds) ? seconds : 60d;
    }

    private static bool ReferenceUsesPlayerCar(LiveTelemetrySnapshot snapshot)
    {
        var reference = snapshot.Models.Reference;
        if (reference.HasData)
        {
            return reference.FocusIsPlayer;
        }

        var directory = snapshot.Models.DriverDirectory;
        return directory.FocusCarIdx is not null
            && directory.PlayerCarIdx is not null
            && directory.FocusCarIdx == directory.PlayerCarIdx;
    }

    private static bool ReferenceUsesTeamClass(LiveTelemetrySnapshot snapshot)
    {
        var reference = snapshot.Models.Reference;
        if (reference.HasData && reference.FocusCarIdx is null)
        {
            return false;
        }

        if (reference.HasData
            && reference.FocusIsPlayer
            && reference.ReferenceCarClass is not null
            && reference.PlayerCarClass is not null)
        {
            return reference.ReferenceCarClass == reference.PlayerCarClass;
        }

        var directory = snapshot.Models.DriverDirectory;
        if (directory.FocusCarIdx is null)
        {
            return false;
        }

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
