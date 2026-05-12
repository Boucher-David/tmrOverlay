using System.Globalization;
using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal static class LiveRaceModelBuilder
{
    private const double SuspiciousEstimatedTimingSeconds = 0.05d;
    private const double SuspiciousEstimatedTimingLapsWithoutLapTime = 0.001d;
    private const double SuspiciousEstimatedTimingLapEstimateSeconds = 0.5d;
    private const int OnTrackSurface = 3;

    public static LiveRaceModels From(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveFuelSnapshot fuel,
        LiveProximitySnapshot proximity,
        LiveLeaderGapSnapshot leaderGap,
        LiveTrackMapModel? trackMap = null,
        IReadOnlySet<int>? griddedCarIdxs = null)
    {
        var session = BuildSession(context, sample);
        var drivers = BuildDriverDirectory(context, sample);
        var timing = BuildTiming(context, sample, leaderGap, drivers, griddedCarIdxs);
        var scoring = BuildScoring(context, sample, drivers, timing);
        var spatial = BuildSpatial(context, sample, proximity);
        var coverage = BuildCoverage(context, scoring, timing, spatial, proximity);
        var raceProgress = BuildRaceProgress(context, sample, session);

        return new LiveRaceModels(
            Session: session,
            DriverDirectory: drivers,
            Coverage: coverage,
            Scoring: scoring,
            Timing: timing,
            RaceProgress: raceProgress,
            RaceProjection: LiveRaceProjectionModel.Empty,
            Relative: BuildRelative(context, sample, proximity, timing),
            Spatial: spatial,
            TrackMap: trackMap ?? BuildTrackMap(context),
            Weather: BuildWeather(context, sample),
            FuelPit: BuildFuelPit(sample, fuel),
            RaceEvents: BuildRaceEvents(sample),
            Inputs: BuildInputs(sample));
    }

    private static LiveTrackMapModel BuildTrackMap(HistoricalSessionContext context)
    {
        var sectors = NormalizeSectors(context.Sectors)
            .Select(sector => new LiveTrackSectorSegment(
                sector.SectorNum,
                sector.StartPct,
                sector.EndPct,
                LiveTrackSectorHighlights.None))
            .ToArray();

        return new LiveTrackMapModel(
            HasSectors: sectors.Length >= 2,
            HasLiveTiming: false,
            Quality: sectors.Length >= 2 ? LiveModelQuality.Partial : LiveModelQuality.Unavailable,
            Sectors: sectors);
    }

    private static LiveSessionModel BuildSession(HistoricalSessionContext context, HistoricalTelemetrySample sample)
    {
        var missing = new List<string>();
        if (sample.SessionTimeRemain is null)
        {
            missing.Add("SessionTimeRemain");
        }

        if (sample.SessionTimeTotal is null)
        {
            missing.Add("SessionTimeTotal");
        }

        if (sample.SessionLapsRemainEx is null)
        {
            missing.Add("SessionLapsRemainEx");
        }

        if (sample.SessionLapsTotal is null)
        {
            missing.Add("SessionLapsTotal");
        }

        var hasContext = !string.IsNullOrWhiteSpace(context.Session.SessionType)
            || !string.IsNullOrWhiteSpace(context.Session.SessionName)
            || !string.IsNullOrWhiteSpace(context.Track.TrackDisplayName)
            || !string.IsNullOrWhiteSpace(context.Car.CarScreenName);
        var hasLiveClock = IsNonNegativeFinite(sample.SessionTime);

        return new LiveSessionModel(
            HasData: hasContext || hasLiveClock,
            Quality: hasContext && hasLiveClock
                ? LiveModelQuality.Reliable
                : hasLiveClock
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            Combo: HistoricalComboIdentity.From(context),
            SessionType: context.Session.SessionType,
            SessionName: context.Session.SessionName,
            EventType: context.Session.EventType,
            TeamRacing: context.Session.TeamRacing,
            SessionTimeSeconds: hasLiveClock ? sample.SessionTime : null,
            SessionTimeRemainSeconds: ValidNonNegative(sample.SessionTimeRemain),
            SessionTimeTotalSeconds: ValidPositive(sample.SessionTimeTotal),
            SessionLapsRemain: sample.SessionLapsRemainEx is >= 0 ? sample.SessionLapsRemainEx : null,
            SessionLapsTotal: sample.SessionLapsTotal is >= 0 ? sample.SessionLapsTotal : null,
            RaceLaps: sample.RaceLaps is >= 0 ? sample.RaceLaps : null,
            SessionState: sample.SessionState,
            SessionFlags: sample.SessionFlags,
            TrackDisplayName: FirstNonEmpty(context.Track.TrackDisplayName, context.Track.TrackName),
            TrackLengthKm: ValidPositive(context.Track.TrackLengthKm),
            CarDisplayName: FirstNonEmpty(context.Car.CarScreenName, context.Car.CarScreenNameShort, context.Car.CarPath),
            MissingSignals: missing);
    }

    private static LiveRaceProgressModel BuildRaceProgress(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveSessionModel session)
    {
        var missing = new List<string>();
        var playerProgress = Progress(sample.LapCompleted, sample.LapDistPct);
        double? raceLapsProgress = sample.RaceLaps is { } raceLaps && raceLaps >= 0
            ? raceLaps
            : null;
        var strategyProgress = Progress(sample.TeamLapCompleted, sample.TeamLapDistPct)
            ?? playerProgress
            ?? raceLapsProgress;
        var referenceProgress = Progress(FocusLapCompleted(sample), FocusLapDistPct(sample))
            ?? strategyProgress;
        var overallLeaderProgress = Progress(sample.LeaderLapCompleted, sample.LeaderLapDistPct);
        var classLeaderProgress = Progress(FocusClassLeaderLapCompleted(sample), FocusClassLeaderLapDistPct(sample));
        var strategyLapTime = SelectStrategyLapTime(context, sample);
        var racePace = SelectRacePace(sample, strategyLapTime);
        var raceLapEstimate = LiveRaceProgressProjector.EstimateLapsRemaining(
            context,
            session,
            strategyProgress,
            overallLeaderProgress,
            classLeaderProgress,
            racePace.Value,
            racePace.Source);

        if (strategyProgress is null)
        {
            missing.Add("strategy-progress");
        }

        if (overallLeaderProgress is null)
        {
            missing.Add("overall-leader-progress");
        }

        if (classLeaderProgress is null)
        {
            missing.Add("class-leader-progress");
        }

        if (racePace.Value is null)
        {
            missing.Add("race-pace");
        }

        if (raceLapEstimate.LapsRemaining is null)
        {
            missing.Add("race-laps-remaining");
        }

        var allowLiveRaceGaps = AllowsLiveRaceGaps(sample);
        var strategyOverallGap = allowLiveRaceGaps ? LiveRaceProgressProjector.CalculateGapLaps(overallLeaderProgress, strategyProgress) : null;
        var strategyClassGap = allowLiveRaceGaps ? LiveRaceProgressProjector.CalculateGapLaps(classLeaderProgress, strategyProgress) : null;
        var referenceOverallGap = allowLiveRaceGaps ? LiveRaceProgressProjector.CalculateGapLaps(overallLeaderProgress, referenceProgress) : null;
        var referenceClassGap = allowLiveRaceGaps ? LiveRaceProgressProjector.CalculateGapLaps(classLeaderProgress, referenceProgress) : null;
        var hasData = strategyProgress is not null
            || referenceProgress is not null
            || overallLeaderProgress is not null
            || classLeaderProgress is not null
            || strategyLapTime.Value is not null
            || racePace.Value is not null
            || raceLapEstimate.LapsRemaining is not null
            || sample.TeamPosition is not null
            || sample.TeamClassPosition is not null
            || FocusPosition(sample) is not null
            || FocusClassPosition(sample) is not null;

        return new LiveRaceProgressModel(
            HasData: hasData,
            Quality: raceLapEstimate.LapsRemaining is not null && strategyProgress is not null
                ? LiveModelQuality.Reliable
                : hasData
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            StrategyCarProgressLaps: strategyProgress,
            ReferenceCarProgressLaps: referenceProgress,
            OverallLeaderProgressLaps: overallLeaderProgress,
            ClassLeaderProgressLaps: classLeaderProgress,
            StrategyOverallLeaderGapLaps: strategyOverallGap,
            StrategyClassLeaderGapLaps: strategyClassGap,
            ReferenceOverallLeaderGapLaps: referenceOverallGap,
            ReferenceClassLeaderGapLaps: referenceClassGap,
            StrategyOverallPosition: sample.TeamPosition,
            StrategyClassPosition: sample.TeamClassPosition,
            ReferenceOverallPosition: FocusPosition(sample),
            ReferenceClassPosition: FocusClassPosition(sample),
            StrategyLapTimeSeconds: strategyLapTime.Value,
            StrategyLapTimeSource: strategyLapTime.Source,
            RacePaceSeconds: racePace.Value,
            RacePaceSource: racePace.Source,
            RaceLapsRemaining: raceLapEstimate.LapsRemaining,
            RaceLapsRemainingSource: raceLapEstimate.Source,
            MissingSignals: missing);
    }

    private static RaceProgressMetric SelectStrategyLapTime(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample)
    {
        if (LiveRaceProgressProjector.ValidLapTime(sample.TeamLastLapTimeSeconds) is { } teamLastLap)
        {
            return new RaceProgressMetric(teamLastLap, "team last lap");
        }

        if (LiveRaceProgressProjector.ValidLapTime(sample.LapLastLapTimeSeconds) is { } playerLastLap)
        {
            return new RaceProgressMetric(playerLastLap, "player last lap");
        }

        if (LiveRaceProgressProjector.ValidLapTime(context.Car.DriverCarEstLapTimeSeconds) is { } driverEstimate)
        {
            return new RaceProgressMetric(driverEstimate, "driver estimate");
        }

        if (LiveRaceProgressProjector.ValidLapTime(context.Car.CarClassEstLapTimeSeconds) is { } classEstimate)
        {
            return new RaceProgressMetric(classEstimate, "class estimate");
        }

        return new RaceProgressMetric(null, "unavailable");
    }

    private static RaceProgressMetric SelectRacePace(
        HistoricalTelemetrySample sample,
        RaceProgressMetric strategyLapTime)
    {
        if (LiveRaceProgressProjector.ValidLapTime(sample.LeaderLastLapTimeSeconds) is { } leaderLastLap)
        {
            return new RaceProgressMetric(leaderLastLap, "overall leader last lap");
        }

        if (LiveRaceProgressProjector.ValidLapTime(FocusClassLeaderLastLapTimeSeconds(sample)) is { } classLeaderLastLap)
        {
            return new RaceProgressMetric(classLeaderLastLap, "class leader last lap");
        }

        if (LiveRaceProgressProjector.ValidLapTime(sample.LeaderBestLapTimeSeconds) is { } leaderBestLap)
        {
            return new RaceProgressMetric(leaderBestLap, "overall leader best lap");
        }

        if (LiveRaceProgressProjector.ValidLapTime(FocusClassLeaderBestLapTimeSeconds(sample)) is { } classLeaderBestLap)
        {
            return new RaceProgressMetric(classLeaderBestLap, "class leader best lap");
        }

        if (strategyLapTime.Value is not null)
        {
            return strategyLapTime;
        }

        return new RaceProgressMetric(null, "unavailable");
    }

    private static LiveDriverDirectoryModel BuildDriverDirectory(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample)
    {
        var classNamesByClassId = BuildClassNamesByClassId(context.Drivers);
        var drivers = context.Drivers
            .Where(driver => driver.CarIdx is not null)
            .Select(driver => ToLiveDriver(driver, classNamesByClassId))
            .OrderBy(driver => driver.CarIdx)
            .ToArray();
        var playerCarIdx = sample.PlayerCarIdx;
        var focusCarIdx = FocusCarIdx(sample);

        return new LiveDriverDirectoryModel(
            HasData: drivers.Length > 0 || playerCarIdx is not null || focusCarIdx is not null,
            Quality: drivers.Length > 0
                ? LiveModelQuality.Reliable
                : playerCarIdx is not null || focusCarIdx is not null
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            PlayerCarIdx: playerCarIdx,
            FocusCarIdx: focusCarIdx,
            ReferenceCarClass: ReferenceCarClass(sample),
            PlayerDriver: playerCarIdx is { } playerIdx ? drivers.FirstOrDefault(driver => driver.CarIdx == playerIdx) : null,
            FocusDriver: focusCarIdx is { } focusIdx ? drivers.FirstOrDefault(driver => driver.CarIdx == focusIdx) : null,
            Drivers: drivers);
    }

    private static LiveCoverageModel BuildCoverage(
        HistoricalSessionContext context,
        LiveScoringModel scoring,
        LiveTimingModel timing,
        LiveSpatialModel spatial,
        LiveProximitySnapshot proximity)
    {
        var timingRows = timing.OverallRows
            .GroupBy(row => row.CarIdx)
            .Select(group => group.First())
            .ToArray();
        var resultCarIdxs = scoring.Rows
            .Select(row => row.CarIdx)
            .ToHashSet();
        var scoredTimingRows = resultCarIdxs.Count > 0
            ? timingRows.Where(row => resultCarIdxs.Contains(row.CarIdx)).ToArray()
            : timingRows;

        return new LiveCoverageModel(
            RosterCount: context.Drivers.Count(IsRaceRosterDriver),
            ResultRowCount: scoring.Rows.Count,
            LiveScoringRowCount: scoredTimingRows.Count(row => row.OverallPosition is not null || row.ClassPosition is not null),
            LiveTimingRowCount: scoredTimingRows.Count(row => row.HasTiming),
            LiveSpatialRowCount: scoredTimingRows.Count(row => row.HasSpatialProgress),
            LiveProximityRowCount: proximity.NearbyCars.Count);
    }

    private static bool IsRaceRosterDriver(HistoricalSessionDriver driver)
    {
        return driver.CarIdx is >= 0
            && driver.IsSpectator != true
            && driver.UserId != -1;
    }

    private static LiveScoringModel BuildScoring(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveDriverDirectoryModel driverDirectory,
        LiveTimingModel timing)
    {
        var selection = SelectScoringResults(context, sample, timing);
        if (selection.Results.Length == 0)
        {
            return LiveScoringModel.Empty with
            {
                ReferenceCarIdx = FocusCarIdx(sample),
                ReferenceCarClass = ReferenceCarClass(sample)
            };
        }

        var zeroBasedOverall = selection.Results.Any(result => result.Position == 0);
        var zeroBasedClass = selection.Results.Any(result => result.ClassPosition == 0);
        var referenceCarIdx = FocusCarIdx(sample);
        var referenceClass = ReferenceCarClass(sample);
        var driversByCarIdx = driverDirectory.Drivers.ToDictionary(driver => driver.CarIdx);
        var timingByCarIdx = timing.OverallRows
            .Concat(timing.ClassRows)
            .GroupBy(row => row.CarIdx)
            .ToDictionary(group => group.Key, group => group.First());
        var rows = selection.Results
            .Select(result => ToScoringRow(
                result,
                driversByCarIdx,
                timingByCarIdx,
                referenceCarIdx,
                referenceClass,
                sample,
                zeroBasedOverall,
                zeroBasedClass))
            .OrderBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenBy(row => row.CarIdx)
            .ToArray();
        var classGroups = rows
            .GroupBy(row => row.CarClass)
            .Select(group => ToScoringClassGroup(group, referenceClass))
            .OrderByDescending(group => group.IsReferenceClass)
            .ThenBy(group => group.Rows.Min(row => row.OverallPosition ?? int.MaxValue))
            .ThenBy(group => group.CarClass ?? int.MaxValue)
            .ToArray();

        return new LiveScoringModel(
            HasData: rows.Length > 0,
            Quality: LiveModelQuality.Reliable,
            Source: selection.Source,
            ReferenceCarIdx: referenceCarIdx,
            ReferenceCarClass: referenceClass,
            ClassGroups: classGroups,
            Rows: rows);
    }

    private static ScoringResultSelection SelectScoringResults(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveTimingModel timing)
    {
        var sessionResults = NormalizeResultSet(context.ResultPositions);
        var startingGrid = NormalizeResultSet(context.StartingGridPositions);
        if (ShouldUseStartingGrid(context, sample, timing, startingGrid))
        {
            return new ScoringResultSelection(LiveScoringSource.StartingGrid, startingGrid);
        }

        return sessionResults.Length > 0
            ? new ScoringResultSelection(LiveScoringSource.SessionResults, sessionResults)
            : new ScoringResultSelection(LiveScoringSource.None, []);
    }

    private static HistoricalSessionResultPosition[] NormalizeResultSet(
        IReadOnlyList<HistoricalSessionResultPosition> results)
    {
        return results
            .Where(result => result.CarIdx is >= 0)
            .GroupBy(result => result.CarIdx!.Value)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool ShouldUseStartingGrid(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveTimingModel timing,
        IReadOnlyList<HistoricalSessionResultPosition> startingGrid)
    {
        if (startingGrid.Count == 0 || !IsRaceSession(context))
        {
            return false;
        }

        if (sample.SessionState is { } sessionState)
        {
            return sessionState < 4 || !HasMeaningfulRaceScoringCoverage(context, timing, startingGrid.Count);
        }

        return sample.LapCompleted <= 0
            && sample.LapDistPct >= 0d
            && sample.LapDistPct < 0.08d;
    }

    private static bool HasMeaningfulRaceScoringCoverage(
        HistoricalSessionContext context,
        LiveTimingModel timing,
        int startingGridRowCount)
    {
        var rows = timing.OverallRows
            .GroupBy(row => row.CarIdx)
            .Select(group => group.First())
            .ToArray();
        if (rows.Length == 0)
        {
            return false;
        }

        var officialPositionCount = rows.Count(row => row.OverallPosition is > 0);
        var officialClassPositionCount = rows.Count(row => row.ClassPosition is > 0);
        var progressCount = rows.Count(row => row.ProgressLaps is not null);
        if (officialPositionCount == 0 || officialClassPositionCount == 0 || progressCount == 0)
        {
            return false;
        }

        var expectedRows = startingGridRowCount > 0
            ? startingGridRowCount
            : context.Drivers.Count(IsRaceRosterDriver);
        var targetRows = expectedRows > 0
            ? Math.Min(expectedRows, rows.Length)
            : rows.Length;
        var requiredRows = targetRows <= 3
            ? targetRows
            : Math.Max(3, (int)Math.Ceiling(targetRows * 0.5d));

        return officialPositionCount >= requiredRows
            && officialClassPositionCount >= requiredRows
            && progressCount >= requiredRows;
    }

    private static bool IsRaceSession(HistoricalSessionContext context)
    {
        return ContainsRace(context.Session.SessionType)
            || ContainsRace(context.Session.SessionName)
            || ContainsRace(context.Session.EventType);
    }

    private static bool ContainsRace(string? value)
    {
        return value?.IndexOf("race", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool AllowsLiveRaceGaps(HistoricalTelemetrySample sample)
    {
        return sample.SessionState is null or >= 4;
    }

    private static bool AllowsEstimatedRelativeTiming(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample)
    {
        if (!IsRaceSession(context))
        {
            return false;
        }

        return sample.SessionState is 3 or >= 4 or null;
    }

    private static LiveScoringRow ToScoringRow(
        HistoricalSessionResultPosition result,
        IReadOnlyDictionary<int, LiveDriverIdentity> driversByCarIdx,
        IReadOnlyDictionary<int, LiveTimingRow> timingByCarIdx,
        int? referenceCarIdx,
        int? referenceClass,
        HistoricalTelemetrySample sample,
        bool zeroBasedOverall,
        bool zeroBasedClass)
    {
        var carIdx = result.CarIdx!.Value;
        driversByCarIdx.TryGetValue(carIdx, out var driver);
        timingByCarIdx.TryGetValue(carIdx, out var timingRow);
        var carClass = driver?.CarClassId;
        return new LiveScoringRow(
            CarIdx: carIdx,
            OverallPositionRaw: result.Position,
            ClassPositionRaw: result.ClassPosition,
            OverallPosition: NormalizeResultPosition(result.Position, zeroBasedOverall),
            ClassPosition: NormalizeResultPosition(result.ClassPosition, zeroBasedClass),
            CarClass: carClass,
            DriverName: driver?.DriverName,
            TeamName: driver?.TeamName,
            CarNumber: driver?.CarNumber,
            CarClassName: driver?.CarClassName,
            CarClassColorHex: driver?.CarClassColorHex,
            IsPlayer: sample.PlayerCarIdx == carIdx,
            IsFocus: referenceCarIdx == carIdx,
            IsReferenceClass: referenceClass is not null && carClass == referenceClass,
            Lap: result.Lap,
            LapsComplete: result.LapsComplete,
            LastLapTimeSeconds: ValidPositive(result.LastTimeSeconds),
            BestLapTimeSeconds: ValidPositive(result.FastestTimeSeconds),
            ReasonOut: result.ReasonOut,
            HasTakenGrid: timingRow?.HasTakenGrid == true);
    }

    private static LiveScoringClassGroup ToScoringClassGroup(
        IGrouping<int?, LiveScoringRow> group,
        int? referenceClass)
    {
        var rows = group
            .OrderBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.CarIdx)
            .ToArray();
        var className = FirstNonEmpty(rows.Select(row => row.CarClassName))
            ?? (group.Key is { } carClass ? $"Class {carClass}" : "Class");

        return new LiveScoringClassGroup(
            CarClass: group.Key,
            ClassName: className,
            CarClassColorHex: FirstNonEmpty(rows.Select(row => row.CarClassColorHex)),
            IsReferenceClass: referenceClass is not null && group.Key == referenceClass,
            RowCount: rows.Length,
            Rows: rows);
    }

    private static int? NormalizeResultPosition(int? rawPosition, bool zeroBased)
    {
        if (rawPosition is null || rawPosition < 0)
        {
            return null;
        }

        return zeroBased ? rawPosition.Value + 1 : rawPosition.Value;
    }

    private static LiveTimingModel BuildTiming(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveLeaderGapSnapshot leaderGap,
        LiveDriverDirectoryModel driverDirectory,
        IReadOnlySet<int>? griddedCarIdxs)
    {
        var rows = new List<LiveTimingRow>();
        var focusCarIdx = FocusCarIdx(sample);
        var playerCarIdx = sample.PlayerCarIdx;
        var classLeaderCarIdx = leaderGap.ClassLeaderCarIdx ?? FocusClassLeaderCarIdx(sample);
        var allowLiveRaceGaps = AllowsLiveRaceGaps(sample);
        var isRaceSession = IsRaceSession(context);
        var overallGapEvidence = BuildLeaderGapEvidence(
            source: "overall-gap",
            position: FocusPosition(sample),
            leaderCarIdx: sample.LeaderCarIdx,
            referenceCarIdx: focusCarIdx,
            referenceF2TimeSeconds: allowLiveRaceGaps
                ? UsableF2ForTiming(FocusF2TimeSeconds(sample), FocusPosition(sample), isRaceSession)
                : null,
            leaderF2TimeSeconds: allowLiveRaceGaps ? sample.LeaderF2TimeSeconds : null,
            referenceProgress: allowLiveRaceGaps ? Progress(FocusLapCompleted(sample), FocusLapDistPct(sample)) : null,
            leaderProgress: allowLiveRaceGaps ? Progress(sample.LeaderLapCompleted, sample.LeaderLapDistPct) : null);
        var classGapEvidence = BuildLeaderGapEvidence(
            source: "class-gap",
            position: FocusClassPosition(sample),
            leaderCarIdx: classLeaderCarIdx,
            referenceCarIdx: focusCarIdx,
            referenceF2TimeSeconds: allowLiveRaceGaps
                ? UsableF2ForTiming(FocusF2TimeSeconds(sample), FocusPosition(sample), isRaceSession)
                : null,
            leaderF2TimeSeconds: allowLiveRaceGaps ? FocusClassLeaderF2TimeSeconds(sample) : null,
            referenceProgress: allowLiveRaceGaps ? Progress(FocusLapCompleted(sample), FocusLapDistPct(sample)) : null,
            leaderProgress: allowLiveRaceGaps ? Progress(FocusClassLeaderLapCompleted(sample), FocusClassLeaderLapDistPct(sample)) : null);

        AddKnownRow(
            rows,
            context,
            driverDirectory,
            carIdx: focusCarIdx,
            quality: LiveModelQuality.Reliable,
            source: "focus",
            isPlayer: focusCarIdx is not null && focusCarIdx == playerCarIdx,
            isFocus: true,
            isOverallLeader: focusCarIdx is not null && focusCarIdx == leaderGap.OverallLeaderCarIdx,
            isClassLeader: focusCarIdx is not null && focusCarIdx == classLeaderCarIdx,
            overallPosition: FocusPosition(sample),
            classPosition: FocusClassPosition(sample),
            carClass: ReferenceCarClass(sample),
            lapCompleted: FocusLapCompleted(sample),
            lapDistPct: FocusLapDistPct(sample),
            f2TimeSeconds: FocusF2TimeSeconds(sample),
            estimatedTimeSeconds: FocusEstimatedTimeSeconds(sample),
            lastLapTimeSeconds: FocusLastLapTimeSeconds(sample),
            bestLapTimeSeconds: FocusBestLapTimeSeconds(sample),
            trackSurface: sample.FocusTrackSurface,
            onPitRoad: FocusOnPitRoad(sample),
            griddedCarIdxs: griddedCarIdxs);

        AddKnownRow(
            rows,
            context,
            driverDirectory,
            carIdx: playerCarIdx,
            quality: LiveModelQuality.Reliable,
            source: "player-team",
            isPlayer: true,
            isFocus: playerCarIdx is not null && playerCarIdx == focusCarIdx,
            isOverallLeader: playerCarIdx is not null && playerCarIdx == leaderGap.OverallLeaderCarIdx,
            isClassLeader: playerCarIdx is not null && playerCarIdx == classLeaderCarIdx,
            overallPosition: sample.TeamPosition,
            classPosition: sample.TeamClassPosition,
            carClass: sample.TeamCarClass,
            lapCompleted: sample.TeamLapCompleted,
            lapDistPct: sample.TeamLapDistPct,
            f2TimeSeconds: sample.TeamF2TimeSeconds,
            estimatedTimeSeconds: sample.TeamEstimatedTimeSeconds,
            lastLapTimeSeconds: sample.TeamLastLapTimeSeconds,
            bestLapTimeSeconds: sample.TeamBestLapTimeSeconds,
            trackSurface: sample.PlayerTrackSurface,
            onPitRoad: sample.TeamOnPitRoad ?? sample.OnPitRoad,
            griddedCarIdxs: griddedCarIdxs);

        AddKnownRow(
            rows,
            context,
            driverDirectory,
            carIdx: sample.LeaderCarIdx,
            quality: LiveModelQuality.Inferred,
            source: "overall-leader",
            isPlayer: sample.LeaderCarIdx is not null && sample.LeaderCarIdx == playerCarIdx,
            isFocus: sample.LeaderCarIdx is not null && sample.LeaderCarIdx == focusCarIdx,
            isOverallLeader: true,
            isClassLeader: sample.LeaderCarIdx is not null && sample.LeaderCarIdx == classLeaderCarIdx,
            overallPosition: 1,
            classPosition: null,
            carClass: null,
            lapCompleted: sample.LeaderLapCompleted,
            lapDistPct: sample.LeaderLapDistPct,
            f2TimeSeconds: sample.LeaderF2TimeSeconds,
            estimatedTimeSeconds: sample.LeaderEstimatedTimeSeconds,
            lastLapTimeSeconds: sample.LeaderLastLapTimeSeconds,
            bestLapTimeSeconds: sample.LeaderBestLapTimeSeconds,
            trackSurface: null,
            onPitRoad: null,
            griddedCarIdxs: griddedCarIdxs);

        AddKnownRow(
            rows,
            context,
            driverDirectory,
            carIdx: classLeaderCarIdx,
            quality: LiveModelQuality.Inferred,
            source: "class-leader",
            isPlayer: classLeaderCarIdx is not null && classLeaderCarIdx == playerCarIdx,
            isFocus: classLeaderCarIdx is not null && classLeaderCarIdx == focusCarIdx,
            isOverallLeader: classLeaderCarIdx is not null && classLeaderCarIdx == leaderGap.OverallLeaderCarIdx,
            isClassLeader: true,
            overallPosition: null,
            classPosition: 1,
            carClass: ReferenceCarClass(sample),
            lapCompleted: FocusClassLeaderLapCompleted(sample),
            lapDistPct: FocusClassLeaderLapDistPct(sample),
            f2TimeSeconds: FocusClassLeaderF2TimeSeconds(sample),
            estimatedTimeSeconds: FocusClassLeaderEstimatedTimeSeconds(sample),
            lastLapTimeSeconds: FocusClassLeaderLastLapTimeSeconds(sample),
            bestLapTimeSeconds: FocusClassLeaderBestLapTimeSeconds(sample),
            trackSurface: null,
            onPitRoad: null,
            griddedCarIdxs: griddedCarIdxs);

        AddProximityRows(rows, context, driverDirectory, sample.FocusClassCars, "focus-class-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx, griddedCarIdxs);
        AddProximityRows(rows, context, driverDirectory, sample.ClassCars, "player-class-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx, griddedCarIdxs);
        AddProximityRows(rows, context, driverDirectory, sample.NearbyCars, "nearby-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx, griddedCarIdxs);
        AddProximityRows(rows, context, driverDirectory, sample.AllCars, "all-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx, griddedCarIdxs);

        var classGapByCarIdx = leaderGap.ClassCars.ToDictionary(car => car.CarIdx);
        var mergedRows = rows
            .GroupBy(row => row.CarIdx)
            .Select(group => ApplyClassGap(MergeRows(group), classGapByCarIdx, classGapEvidence, isRaceSession))
            .OrderBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenByDescending(row => row.ProgressLaps ?? double.MinValue)
            .ThenBy(row => row.CarIdx)
            .ToArray();
        mergedRows = ApplyDerivedClassGaps(context, sample, mergedRows);

        var referenceClass = ReferenceCarClass(sample);
        var classRows = mergedRows
            .Where(row => row.IsFocus
                || row.IsClassLeader
                || referenceClass is null
                || row.CarClass == referenceClass)
            .OrderBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenBy(row => row.GapSecondsToClassLeader ?? double.MaxValue)
            .ThenBy(row => row.GapLapsToClassLeader ?? double.MaxValue)
            .ThenBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.CarIdx)
            .ToArray();

        var playerRow = playerCarIdx is { } playerIdx
            ? mergedRows.FirstOrDefault(row => row.CarIdx == playerIdx)
            : null;
        var focusRow = focusCarIdx is { } focusIdx
            ? mergedRows.FirstOrDefault(row => row.CarIdx == focusIdx)
            : null;
        var quality = mergedRows.Length > 0
            ? mergedRows.Max(row => row.Quality)
            : LiveModelQuality.Unavailable;

        return new LiveTimingModel(
            HasData: mergedRows.Length > 0,
            Quality: quality,
            PlayerCarIdx: playerCarIdx,
            FocusCarIdx: focusCarIdx,
            OverallLeaderCarIdx: leaderGap.OverallLeaderCarIdx,
            ClassLeaderCarIdx: classLeaderCarIdx,
            OverallLeaderGapEvidence: overallGapEvidence,
            ClassLeaderGapEvidence: classGapEvidence,
            PlayerRow: playerRow,
            FocusRow: focusRow,
            OverallRows: mergedRows,
            ClassRows: classRows);
    }

    private static LiveRelativeModel BuildRelative(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveProximitySnapshot proximity,
        LiveTimingModel timing)
    {
        var referenceClass = ReferenceCarClass(sample);
        var timingByCarIdx = timing.OverallRows.ToDictionary(row => row.CarIdx);
        var rows = new List<LiveRelativeRow>();

        foreach (var car in proximity.NearbyCars)
        {
            timingByCarIdx.TryGetValue(car.CarIdx, out var timingRow);
            var inferredRelativeSeconds = car.RelativeSeconds ?? InferRelativeSecondsFromLapDistance(car.RelativeLaps, sample);
            rows.Add(new LiveRelativeRow(
                CarIdx: car.CarIdx,
                Quality: car.RelativeSeconds is not null || car.RelativeMeters is not null
                    ? LiveModelQuality.Reliable
                    : inferredRelativeSeconds is not null
                        ? LiveModelQuality.Inferred
                        : LiveModelQuality.Partial,
                Source: "proximity",
                IsAhead: car.RelativeLaps > 0d,
                IsBehind: car.RelativeLaps < 0d,
                IsSameClass: referenceClass is not null && car.CarClass == referenceClass,
                TimingEvidence: car.RelativeSeconds is not null
                    ? LiveSignalEvidence.Reliable("proximity-relative-seconds")
                    : inferredRelativeSeconds is not null
                        ? LiveSignalEvidence.Inferred("CarIdxLapDistPct+lap-time")
                        : LiveSignalEvidence.Partial("proximity-relative-seconds", "relative_seconds_missing"),
                PlacementEvidence: car.RelativeMeters is not null
                    ? LiveSignalEvidence.Reliable("CarIdxLapDistPct+track-length")
                    : LiveSignalEvidence.Inferred("CarIdxLapDistPct"),
                DriverName: timingRow?.DriverName,
                OverallPosition: car.OverallPosition ?? timingRow?.OverallPosition,
                ClassPosition: car.ClassPosition ?? timingRow?.ClassPosition,
                CarClass: car.CarClass ?? timingRow?.CarClass,
                RelativeSeconds: inferredRelativeSeconds,
                RelativeLaps: car.RelativeLaps,
                RelativeMeters: car.RelativeMeters,
                OnPitRoad: car.OnPitRoad ?? timingRow?.OnPitRoad));
        }

        foreach (var car in sample.NearbyCars ?? [])
        {
            if (!IsPitRoadLike(car.TrackSurface, car.OnPitRoad)
                || !timingByCarIdx.TryGetValue(car.CarIdx, out var timingRow)
                || RelativeLapsFromLapDistance(car.LapDistPct, FocusLapDistPct(sample)) is not { } relativeLaps)
            {
                continue;
            }

            var inferredRelativeSeconds = InferRelativeSecondsFromLapDistance(relativeLaps, sample);
            rows.Add(new LiveRelativeRow(
                CarIdx: car.CarIdx,
                Quality: inferredRelativeSeconds is not null ? LiveModelQuality.Inferred : LiveModelQuality.Partial,
                Source: "proximity",
                IsAhead: relativeLaps > 0d,
                IsBehind: relativeLaps < 0d,
                IsSameClass: referenceClass is not null && (car.CarClass ?? timingRow.CarClass) == referenceClass,
                TimingEvidence: inferredRelativeSeconds is not null
                    ? LiveSignalEvidence.Inferred("CarIdxLapDistPct+lap-time")
                    : LiveSignalEvidence.Partial("proximity-relative-seconds", "relative_seconds_missing"),
                PlacementEvidence: LiveSignalEvidence.Inferred("CarIdxLapDistPct"),
                DriverName: timingRow.DriverName,
                OverallPosition: car.Position ?? timingRow.OverallPosition,
                ClassPosition: car.ClassPosition ?? timingRow.ClassPosition,
                CarClass: car.CarClass ?? timingRow.CarClass,
                RelativeSeconds: inferredRelativeSeconds,
                RelativeLaps: relativeLaps,
                RelativeMeters: null,
                OnPitRoad: true));
        }

        if (rows.Count == 0)
        {
            rows.AddRange(timing.OverallRows
                .Where(row => !row.IsFocus
                    && row.DeltaSecondsToFocus is not null
                    && row.GapEvidence.IsUsable)
                .Select(row => new LiveRelativeRow(
                    CarIdx: row.CarIdx,
                    Quality: LiveModelQuality.Inferred,
                    Source: "class-gap",
                    IsAhead: row.DeltaSecondsToFocus < 0d,
                    IsBehind: row.DeltaSecondsToFocus > 0d,
                    IsSameClass: referenceClass is not null && row.CarClass == referenceClass,
                    TimingEvidence: row.GapEvidence,
                    PlacementEvidence: LiveSignalEvidence.Unavailable("class-gap", "no_lap_distance_placement"),
                    DriverName: row.DriverName,
                    OverallPosition: row.OverallPosition,
                    ClassPosition: row.ClassPosition,
                    CarClass: row.CarClass,
                    RelativeSeconds: row.DeltaSecondsToFocus,
                    RelativeLaps: null,
                    RelativeMeters: null,
                    OnPitRoad: row.OnPitRoad)));
        }

        if (rows.Count == 0 && AllowsEstimatedRelativeTiming(context, sample))
        {
            rows.AddRange(BuildEstimatedRelativeRows(context, sample, timing, referenceClass));
        }

        var orderedRows = rows
            .GroupBy(row => row.CarIdx)
            .Select(group => group
                .OrderByDescending(row => row.Source == "proximity")
                .ThenBy(row => Math.Abs(row.RelativeSeconds ?? double.MaxValue))
                .First())
            .OrderBy(row => Math.Abs(row.RelativeSeconds ?? double.MaxValue))
            .ThenBy(row => Math.Abs(row.RelativeLaps ?? double.MaxValue))
            .ThenBy(row => row.CarIdx)
            .ToArray();

        return new LiveRelativeModel(
            HasData: orderedRows.Length > 0,
            Quality: orderedRows.Length > 0 ? orderedRows.Max(row => row.Quality) : LiveModelQuality.Unavailable,
            ReferenceCarIdx: FocusCarIdx(sample),
            Rows: orderedRows);
    }

    private static IReadOnlyList<LiveRelativeRow> BuildEstimatedRelativeRows(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveTimingModel timing,
        int? referenceClass)
    {
        var focusCarIdx = FocusCarIdx(sample);
        var focusRow = timing.FocusRow
            ?? (focusCarIdx is { } carIdx
                ? timing.OverallRows.FirstOrDefault(row => row.CarIdx == carIdx)
                    ?? timing.ClassRows.FirstOrDefault(row => row.CarIdx == carIdx)
                : null);
        if (focusRow is null || !CanUseEstimatedTiming(focusRow))
        {
            return [];
        }

        var lapTimeSeconds = PreGreenEstimatedLapTimeSeconds(context, sample);
        var evidence = LiveSignalEvidence.Inferred("CarIdxEstTime+CarIdxLapDistPct");
        return timing.OverallRows
            .Where(row => row.CarIdx != focusRow.CarIdx)
            .Select(row =>
            {
                if (row.LapDistPct is not { } rowLapDistPct
                    || RelativeLapsFromLapDistance(rowLapDistPct, focusRow.LapDistPct) is not { } relativeLaps
                    || Math.Abs(relativeLaps) <= 0.00001d
                    || EstimatedRelativeSeconds(row, focusRow, lapTimeSeconds) is not { } relativeSeconds)
                {
                    return null;
                }

                return new LiveRelativeRow(
                    CarIdx: row.CarIdx,
                    Quality: LiveModelQuality.Inferred,
                    Source: "estimated-relative",
                    IsAhead: relativeLaps > 0d,
                    IsBehind: relativeLaps < 0d,
                    IsSameClass: referenceClass is not null && row.CarClass == referenceClass,
                    TimingEvidence: evidence,
                    PlacementEvidence: LiveSignalEvidence.Inferred("CarIdxLapDistPct"),
                    DriverName: row.DriverName,
                    OverallPosition: row.OverallPosition,
                    ClassPosition: row.ClassPosition,
                    CarClass: row.CarClass,
                    RelativeSeconds: relativeSeconds,
                    RelativeLaps: relativeLaps,
                    RelativeMeters: null,
                    OnPitRoad: row.OnPitRoad);
            })
            .Where(row => row is not null)
            .Select(row => row!)
            .ToArray();
    }

    private static double? RelativeLapsFromLapDistance(double carLapDistPct, double? referenceLapDistPct)
    {
        if (ValidLapDistPct(carLapDistPct) is not { } carPct
            || ValidLapDistPct(referenceLapDistPct) is not { } referencePct)
        {
            return null;
        }

        var relativeLaps = carPct - referencePct;
        if (relativeLaps > 0.5d)
        {
            relativeLaps -= 1d;
        }
        else if (relativeLaps < -0.5d)
        {
            relativeLaps += 1d;
        }

        return relativeLaps;
    }

    private static double? InferRelativeSecondsFromLapDistance(double relativeLaps, HistoricalTelemetrySample sample)
    {
        if (!IsFinite(relativeLaps) || Math.Abs(relativeLaps) <= 0.00001d)
        {
            return null;
        }

        return RelativeLapTimeSeconds(sample) is { } lapSeconds
            ? relativeLaps * lapSeconds
            : null;
    }

    private static LiveSpatialModel BuildSpatial(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveProximitySnapshot proximity)
    {
        var trackLengthMeters = ValidPositive(context.Track.TrackLengthKm) is { } km ? km * 1000d : (double?)null;
        var localRadarAvailable = LiveLocalRadarContext.IsAvailable(sample);
        var referenceCarIdx = localRadarAvailable ? LiveLocalRadarContext.ReferenceCarIdx(sample) : null;
        var referenceLapDistPct = localRadarAvailable ? LiveLocalRadarContext.LapDistPct(sample) : null;
        var cars = proximity.NearbyCars
            .Where(car => car.RelativeMeters is not null)
            .Select(car => new LiveSpatialCar(
                CarIdx: car.CarIdx,
                Quality: SpatialCarQuality(car),
                PlacementEvidence: SpatialPlacementEvidence(car),
                RelativeLaps: car.RelativeLaps,
                RelativeSeconds: car.RelativeSeconds,
                RelativeMeters: car.RelativeMeters,
                OverallPosition: car.OverallPosition,
                ClassPosition: car.ClassPosition,
                CarClass: car.CarClass,
                TrackSurface: car.TrackSurface,
                OnPitRoad: car.OnPitRoad,
                CarClassColorHex: car.CarClassColorHex))
            .OrderBy(car => Math.Abs(car.RelativeMeters ?? car.RelativeSeconds ?? car.RelativeLaps))
            .ThenBy(car => car.CarIdx)
            .ToArray();

        return new LiveSpatialModel(
            HasData: proximity.HasData || proximity.HasCarLeft || proximity.HasCarRight || cars.Length > 0 || referenceLapDistPct is not null,
            Quality: cars.Length > 0
                ? cars.Max(car => car.Quality)
                : proximity.HasCarLeft || proximity.HasCarRight || referenceLapDistPct is not null
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            ReferenceCarIdx: referenceCarIdx,
            ReferenceCarClass: proximity.ReferenceCarClass,
            CarLeftRight: proximity.CarLeftRight,
            SideStatus: proximity.SideStatus,
            HasCarLeft: proximity.HasCarLeft,
            HasCarRight: proximity.HasCarRight,
            SideOverlapWindowSeconds: proximity.SideOverlapWindowSeconds,
            TrackLengthMeters: trackLengthMeters,
            ReferenceLapDistPct: referenceLapDistPct,
            Cars: cars,
            NearestAhead: cars
                .Where(car => car.RelativeLaps > 0d)
                .MinBy(car => car.RelativeLaps),
            NearestBehind: cars
                .Where(car => car.RelativeLaps < 0d)
                .MaxBy(car => car.RelativeLaps),
            MulticlassApproaches: proximity.MulticlassApproaches,
            StrongestMulticlassApproach: proximity.StrongestMulticlassApproach);
    }

    private static LiveWeatherModel BuildWeather(HistoricalSessionContext context, HistoricalTelemetrySample sample)
    {
        var trackWetness = sample.TrackWetness >= 0 ? sample.TrackWetness : (int?)null;
        var livePrecipitationPercent = ValidPercent(sample.PrecipitationPercent);
        var windVelocityMetersPerSecond = ValidNonNegative(sample.WindVelocityMetersPerSecond);
        var windDirectionRadians = ValidFinite(sample.WindDirectionRadians);
        var relativeHumidityPercent = ValidPercent(sample.RelativeHumidityPercent);
        var fogLevelPercent = ValidPercent(sample.FogLevelPercent);
        var airPressurePa = ValidNonNegative(sample.AirPressurePa);
        var solarAltitudeRadians = ValidFinite(sample.SolarAltitudeRadians);
        var solarAzimuthRadians = ValidFinite(sample.SolarAzimuthRadians);
        var hasLiveWeather = IsFinite(sample.AirTempC)
            || IsFinite(sample.TrackTempCrewC)
            || trackWetness is not null
            || sample.WeatherDeclaredWet
            || sample.Skies is not null
            || livePrecipitationPercent is not null
            || windVelocityMetersPerSecond is not null
            || windDirectionRadians is not null
            || relativeHumidityPercent is not null
            || fogLevelPercent is not null
            || airPressurePa is not null
            || solarAltitudeRadians is not null
            || solarAzimuthRadians is not null;
        var hasSessionWeather = !string.IsNullOrWhiteSpace(context.Conditions.TrackWeatherType)
            || !string.IsNullOrWhiteSpace(context.Conditions.TrackSkies)
            || context.Conditions.TrackPrecipitationPercent is not null
            || !string.IsNullOrWhiteSpace(context.Conditions.SessionTrackRubberState);

        return new LiveWeatherModel(
            HasData: hasLiveWeather || hasSessionWeather,
            Quality: hasLiveWeather && hasSessionWeather
                ? LiveModelQuality.Reliable
                : hasLiveWeather
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            AirTempC: IsFinite(sample.AirTempC) ? sample.AirTempC : null,
            TrackTempCrewC: IsFinite(sample.TrackTempCrewC) ? sample.TrackTempCrewC : null,
            TrackWetness: trackWetness,
            TrackWetnessLabel: trackWetness is null ? null : FormatTrackWetness(trackWetness.Value),
            WeatherDeclaredWet: sample.WeatherDeclaredWet,
            DeclaredWetSurfaceMismatch: DetermineDeclaredWetSurfaceMismatch(sample.WeatherDeclaredWet, trackWetness),
            WeatherType: context.Conditions.TrackWeatherType,
            SkiesLabel: sample.Skies is { } skies ? FormatSkiesLabel(skies) : FormatSkiesLabel(context.Conditions.TrackSkies),
            Skies: sample.Skies,
            PrecipitationPercent: livePrecipitationPercent ?? context.Conditions.TrackPrecipitationPercent,
            WindVelocityMetersPerSecond: windVelocityMetersPerSecond,
            WindDirectionRadians: windDirectionRadians,
            RelativeHumidityPercent: relativeHumidityPercent,
            FogLevelPercent: fogLevelPercent,
            AirPressurePa: airPressurePa,
            SolarAltitudeRadians: solarAltitudeRadians,
            SolarAzimuthRadians: solarAzimuthRadians,
            RubberState: context.Conditions.SessionTrackRubberState);
    }

    private static LiveFuelPitModel BuildFuelPit(HistoricalTelemetrySample sample, LiveFuelSnapshot fuel)
    {
        var fuelLevelEvidence = BuildFuelLevelEvidence(sample);
        var instantaneousBurnEvidence = BuildInstantaneousBurnEvidence(sample, fuel);
        var measuredBurnEvidence = LiveSignalEvidence.Unavailable(
            "rolling-local-fuel-delta",
            "requires_two_green_distance_samples");
        var baselineEligibilityEvidence = BuildBaselineEligibilityEvidence(sample, fuel);
        var hasPitData = sample.OnPitRoad
            || sample.PitstopActive
            || sample.PlayerCarInPitStall
            || sample.TeamOnPitRoad is not null
            || sample.PitServiceStatus is not null
            || sample.PitServiceFlags is not null
            || sample.PitServiceFuelLiters is not null
            || sample.PitRepairLeftSeconds is not null
            || sample.PitOptRepairLeftSeconds is not null
            || sample.TireSetsUsed is not null
            || sample.FastRepairUsed is not null
            || sample.TeamFastRepairsUsed is not null;

        return new LiveFuelPitModel(
            HasData: fuel.HasValidFuel || hasPitData,
            Quality: fuel.HasValidFuel
                ? LiveModelQuality.Reliable
                : hasPitData
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            Fuel: fuel,
            OnPitRoad: sample.OnPitRoad,
            PitstopActive: sample.PitstopActive,
            PlayerCarInPitStall: sample.PlayerCarInPitStall,
            TeamOnPitRoad: sample.TeamOnPitRoad,
            FuelLevelEvidence: fuelLevelEvidence,
            InstantaneousBurnEvidence: instantaneousBurnEvidence,
            MeasuredBurnEvidence: measuredBurnEvidence,
            BaselineEligibilityEvidence: baselineEligibilityEvidence,
            PitServiceStatus: sample.PitServiceStatus,
            PitServiceFlags: sample.PitServiceFlags,
            PitServiceFuelLiters: ValidNonNegative(sample.PitServiceFuelLiters),
            PitRepairLeftSeconds: ValidNonNegative(sample.PitRepairLeftSeconds),
            PitOptRepairLeftSeconds: ValidNonNegative(sample.PitOptRepairLeftSeconds),
            TireSetsUsed: sample.TireSetsUsed,
            FastRepairUsed: sample.FastRepairUsed,
            TeamFastRepairsUsed: sample.TeamFastRepairsUsed);
    }

    private static LiveRaceEventModel BuildRaceEvents(HistoricalTelemetrySample sample)
    {
        return new LiveRaceEventModel(
            HasData: true,
            Quality: LiveModelQuality.Partial,
            IsOnTrack: sample.IsOnTrack,
            IsInGarage: sample.IsInGarage,
            IsGarageVisible: sample.IsGarageVisible == true,
            OnPitRoad: sample.OnPitRoad,
            Lap: sample.Lap,
            LapCompleted: sample.LapCompleted,
            LapDistPct: sample.LapDistPct,
            DriversSoFar: sample.DriversSoFar,
            DriverChangeLapStatus: sample.DriverChangeLapStatus);
    }

    private static LiveModelQuality SpatialCarQuality(LiveProximityCar car)
    {
        if (car.RelativeMeters is not null)
        {
            return LiveModelQuality.Reliable;
        }

        return car.HasReliableRelativeSeconds
            ? LiveModelQuality.Inferred
            : LiveModelQuality.Partial;
    }

    private static LiveSignalEvidence SpatialPlacementEvidence(LiveProximityCar car)
    {
        if (car.RelativeMeters is not null)
        {
            return LiveSignalEvidence.Reliable("CarIdxLapDistPct+track-length");
        }

        return car.HasReliableRelativeSeconds
            ? LiveSignalEvidence.Inferred("CarIdxEstTime/CarIdxF2Time")
            : LiveSignalEvidence.Partial("CarIdxLapDistPct", "track_length_or_timing_missing");
    }

    private static LiveInputTelemetryModel BuildInputs(HistoricalTelemetrySample sample)
    {
        var speed = IsFinite(sample.SpeedMetersPerSecond) ? sample.SpeedMetersPerSecond : (double?)null;
        var throttle = ValidUnitInterval(sample.Throttle);
        var brake = ValidUnitInterval(sample.Brake);
        var clutch = ValidUnitInterval(sample.Clutch);
        var steering = sample.SteeringWheelAngle is { } steeringValue && IsFinite(steeringValue) ? steeringValue : (double?)null;
        var gear = sample.Gear is >= -1 and <= 20 ? sample.Gear : null;
        var rpm = ValidNonNegative(sample.Rpm);
        var voltage = ValidPositive(sample.Voltage);
        var waterTemp = sample.WaterTempC is { } waterTempValue && IsFinite(waterTempValue) ? waterTempValue : (double?)null;
        var fuelPressure = ValidNonNegative(sample.FuelPressureBar);
        var oilTemp = sample.OilTempC is { } oilTempValue && IsFinite(oilTempValue) ? oilTempValue : (double?)null;
        var oilPressure = ValidNonNegative(sample.OilPressureBar);
        var hasPedals = throttle is not null || brake is not null || clutch is not null;
        var hasSteering = steering is not null;
        var hasCarState = gear is not null
            || rpm is not null
            || sample.EngineWarnings is not null
            || voltage is not null
            || waterTemp is not null
            || fuelPressure is not null
            || oilTemp is not null
            || oilPressure is not null;

        return new LiveInputTelemetryModel(
            HasData: speed is not null || sample.PlayerTireCompound >= 0 || hasPedals || hasSteering || hasCarState,
            Quality: hasPedals || hasSteering || hasCarState
                ? LiveModelQuality.Reliable
                : speed is not null
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            SpeedMetersPerSecond: speed,
            PlayerTireCompound: sample.PlayerTireCompound >= 0 ? sample.PlayerTireCompound : null,
            HasPedalInputs: hasPedals,
            HasSteeringInput: hasSteering,
            Gear: gear,
            Rpm: rpm,
            Throttle: throttle,
            Brake: brake,
            Clutch: clutch,
            SteeringWheelAngle: steering,
            BrakeAbsActive: sample.BrakeAbsActive,
            EngineWarnings: sample.EngineWarnings,
            Voltage: voltage,
            WaterTempC: waterTemp,
            FuelPressureBar: fuelPressure,
            OilTempC: oilTemp,
            OilPressureBar: oilPressure);
    }

    private static void AddKnownRow(
        List<LiveTimingRow> rows,
        HistoricalSessionContext context,
        LiveDriverDirectoryModel driverDirectory,
        int? carIdx,
        LiveModelQuality quality,
        string source,
        bool isPlayer,
        bool isFocus,
        bool isOverallLeader,
        bool isClassLeader,
        int? overallPosition,
        int? classPosition,
        int? carClass,
        int? lapCompleted,
        double? lapDistPct,
        double? f2TimeSeconds,
        double? estimatedTimeSeconds,
        double? lastLapTimeSeconds,
        double? bestLapTimeSeconds,
        int? trackSurface,
        bool? onPitRoad,
        IReadOnlySet<int>? griddedCarIdxs)
    {
        if (carIdx is null)
        {
            return;
        }

        rows.Add(CreateTimingRow(
            context,
            driverDirectory,
            carIdx.Value,
            quality,
            source,
            isPlayer,
            isFocus,
            isOverallLeader,
            isClassLeader,
            overallPosition,
            classPosition,
            carClass,
            lapCompleted,
            lapDistPct,
            f2TimeSeconds,
            estimatedTimeSeconds,
            lastLapTimeSeconds,
            bestLapTimeSeconds,
            gapSecondsToClassLeader: null,
            gapLapsToClassLeader: null,
            intervalSecondsToPreviousClassRow: null,
            deltaSecondsToFocus: null,
            trackSurface: trackSurface,
            onPitRoad: onPitRoad,
            hasTakenGrid: HasTakenGrid(carIdx.Value, trackSurface, onPitRoad, griddedCarIdxs)));
    }

    private static void AddProximityRows(
        List<LiveTimingRow> rows,
        HistoricalSessionContext context,
        LiveDriverDirectoryModel driverDirectory,
        IReadOnlyList<HistoricalCarProximity>? cars,
        string source,
        int? focusCarIdx,
        int? playerCarIdx,
        int? overallLeaderCarIdx,
        int? classLeaderCarIdx,
        IReadOnlySet<int>? griddedCarIdxs)
    {
        if (cars is not { Count: > 0 })
        {
            return;
        }

        foreach (var car in cars)
        {
            rows.Add(CreateTimingRow(
                context,
                driverDirectory,
                car.CarIdx,
                LiveModelQuality.Inferred,
                source,
                isPlayer: car.CarIdx == playerCarIdx,
                isFocus: car.CarIdx == focusCarIdx,
                isOverallLeader: car.CarIdx == overallLeaderCarIdx,
                isClassLeader: car.CarIdx == classLeaderCarIdx,
                overallPosition: car.Position,
                classPosition: car.ClassPosition,
                carClass: car.CarClass,
                lapCompleted: car.LapCompleted,
                lapDistPct: car.LapDistPct,
                f2TimeSeconds: car.F2TimeSeconds,
                estimatedTimeSeconds: car.EstimatedTimeSeconds,
                lastLapTimeSeconds: null,
                bestLapTimeSeconds: null,
                gapSecondsToClassLeader: null,
                gapLapsToClassLeader: null,
                intervalSecondsToPreviousClassRow: null,
                deltaSecondsToFocus: null,
                trackSurface: car.TrackSurface,
                onPitRoad: car.OnPitRoad,
                hasTakenGrid: HasTakenGrid(car.CarIdx, car.TrackSurface, car.OnPitRoad, griddedCarIdxs)));
        }
    }

    private static LiveTimingRow CreateTimingRow(
        HistoricalSessionContext context,
        LiveDriverDirectoryModel driverDirectory,
        int carIdx,
        LiveModelQuality quality,
        string source,
        bool isPlayer,
        bool isFocus,
        bool isOverallLeader,
        bool isClassLeader,
        int? overallPosition,
        int? classPosition,
        int? carClass,
        int? lapCompleted,
        double? lapDistPct,
        double? f2TimeSeconds,
        double? estimatedTimeSeconds,
        double? lastLapTimeSeconds,
        double? bestLapTimeSeconds,
        double? gapSecondsToClassLeader,
        double? gapLapsToClassLeader,
        double? intervalSecondsToPreviousClassRow,
        double? deltaSecondsToFocus,
        int? trackSurface,
        bool? onPitRoad,
        bool hasTakenGrid)
    {
        var driver = driverDirectory.Drivers.FirstOrDefault(candidate => candidate.CarIdx == carIdx);
        var validLapDistPct = ValidLapDistPct(lapDistPct);
        var progressLaps = Progress(lapCompleted, validLapDistPct);
        var hasTiming = HasTimingSignal(
            overallPosition,
            classPosition,
            f2TimeSeconds,
            estimatedTimeSeconds,
            lastLapTimeSeconds,
            bestLapTimeSeconds);
        var hasSpatialProgress = validLapDistPct is not null;
        var hasTrackLength = ValidPositive(context.Track.TrackLengthKm) is not null;
        var canUseForRadarPlacement = hasSpatialProgress && hasTrackLength && !IsPitRoadLike(trackSurface, onPitRoad);

        return new LiveTimingRow(
            CarIdx: carIdx,
            Quality: quality,
            Source: source,
            IsPlayer: isPlayer,
            IsFocus: isFocus,
            IsOverallLeader: isOverallLeader,
            IsClassLeader: isClassLeader,
            HasTiming: hasTiming,
            HasSpatialProgress: hasSpatialProgress,
            CanUseForRadarPlacement: canUseForRadarPlacement,
            TimingEvidence: hasTiming
                ? new LiveSignalEvidence(source, quality, IsUsable: true, MissingReason: null)
                : LiveSignalEvidence.Unavailable(source, "timing_fields_missing"),
            SpatialEvidence: hasSpatialProgress
                ? new LiveSignalEvidence(source, quality, IsUsable: true, MissingReason: null)
                : LiveSignalEvidence.Unavailable(source, "lap_progress_missing"),
            RadarPlacementEvidence: canUseForRadarPlacement
                ? new LiveSignalEvidence(source, quality, IsUsable: true, MissingReason: null)
                : LiveSignalEvidence.Unavailable(
                    source,
                    RadarPlacementMissingReason(hasSpatialProgress, hasTrackLength, trackSurface, onPitRoad)),
            GapEvidence: LiveSignalEvidence.Unavailable("class-gap", "gap_not_calculated_for_row"),
            DriverName: driver?.DriverName,
            TeamName: driver?.TeamName,
            CarNumber: driver?.CarNumber,
            CarClassName: driver?.CarClassName ?? context.Car.CarClassShortName,
            CarClassColorHex: driver?.CarClassColorHex,
            OverallPosition: overallPosition is > 0 ? overallPosition : null,
            ClassPosition: classPosition is > 0 ? classPosition : null,
            CarClass: carClass ?? driver?.CarClassId,
            LapCompleted: lapCompleted is >= 0 ? lapCompleted : null,
            LapDistPct: validLapDistPct,
            ProgressLaps: progressLaps,
            F2TimeSeconds: ValidNonNegative(f2TimeSeconds),
            EstimatedTimeSeconds: ValidNonNegative(estimatedTimeSeconds),
            LastLapTimeSeconds: ValidPositive(lastLapTimeSeconds),
            BestLapTimeSeconds: ValidPositive(bestLapTimeSeconds),
            GapSecondsToClassLeader: gapSecondsToClassLeader,
            GapLapsToClassLeader: gapLapsToClassLeader,
            IntervalSecondsToPreviousClassRow: intervalSecondsToPreviousClassRow,
            DeltaSecondsToFocus: deltaSecondsToFocus,
            TrackSurface: trackSurface,
            OnPitRoad: onPitRoad,
            HasTakenGrid: hasTakenGrid);
    }

    private static LiveTimingRow MergeRows(IEnumerable<LiveTimingRow> group)
    {
        var rows = group.ToArray();
        var primary = rows
            .OrderByDescending(row => row.IsFocus)
            .ThenByDescending(row => row.IsPlayer)
            .ThenByDescending(row => row.IsClassLeader)
            .ThenByDescending(row => row.IsOverallLeader)
            .ThenByDescending(row => row.Quality)
            .First();

        return primary with
        {
            Quality = rows.Max(row => row.Quality),
            Source = string.Join("+", rows.Select(row => row.Source).Distinct(StringComparer.OrdinalIgnoreCase)),
            IsPlayer = rows.Any(row => row.IsPlayer),
            IsFocus = rows.Any(row => row.IsFocus),
            IsOverallLeader = rows.Any(row => row.IsOverallLeader),
            IsClassLeader = rows.Any(row => row.IsClassLeader),
            DriverName = FirstNonEmpty(rows.Select(row => row.DriverName)),
            TeamName = FirstNonEmpty(rows.Select(row => row.TeamName)),
            CarNumber = FirstNonEmpty(rows.Select(row => row.CarNumber)),
            CarClassName = FirstNonEmpty(rows.Select(row => row.CarClassName)),
            CarClassColorHex = FirstNonEmpty(rows.Select(row => row.CarClassColorHex)),
            OverallPosition = FirstValue(rows.Select(row => row.OverallPosition)),
            ClassPosition = FirstValue(rows.Select(row => row.ClassPosition)),
            CarClass = FirstValue(rows.Select(row => row.CarClass)),
            LapCompleted = FirstValue(rows.Select(row => row.LapCompleted)),
            LapDistPct = FirstValue(rows.Select(row => row.LapDistPct)),
            ProgressLaps = FirstValue(rows.Select(row => row.ProgressLaps)),
            F2TimeSeconds = FirstValue(rows.Select(row => row.F2TimeSeconds)),
            EstimatedTimeSeconds = FirstValue(rows.Select(row => row.EstimatedTimeSeconds)),
            LastLapTimeSeconds = FirstValue(rows.Select(row => row.LastLapTimeSeconds)),
            BestLapTimeSeconds = FirstValue(rows.Select(row => row.BestLapTimeSeconds)),
            GapSecondsToClassLeader = FirstValue(rows.Select(row => row.GapSecondsToClassLeader)),
            GapLapsToClassLeader = FirstValue(rows.Select(row => row.GapLapsToClassLeader)),
            IntervalSecondsToPreviousClassRow = FirstValue(rows.Select(row => row.IntervalSecondsToPreviousClassRow)),
            DeltaSecondsToFocus = FirstValue(rows.Select(row => row.DeltaSecondsToFocus)),
            TrackSurface = FirstValue(rows.Select(row => row.TrackSurface)),
            OnPitRoad = FirstValue(rows.Select(row => row.OnPitRoad)),
            HasTiming = rows.Any(row => row.HasTiming),
            HasSpatialProgress = rows.Any(row => row.HasSpatialProgress),
            CanUseForRadarPlacement = rows.Any(row => row.CanUseForRadarPlacement),
            TimingEvidence = MergeEvidence(rows.Select(row => row.TimingEvidence)),
            SpatialEvidence = MergeEvidence(rows.Select(row => row.SpatialEvidence)),
            RadarPlacementEvidence = MergeEvidence(rows.Select(row => row.RadarPlacementEvidence)),
            GapEvidence = MergeEvidence(rows.Select(row => row.GapEvidence)),
            HasTakenGrid = HasTakenGrid(rows)
        };
    }

    private static bool HasTakenGrid(IReadOnlyList<LiveTimingRow> rows)
    {
        var directRows = rows
            .Where(row => row.Source is "focus" or "player-team")
            .ToArray();
        if (directRows.Length > 0)
        {
            var directTrackRows = directRows
                .Where(row => row.TrackSurface is not null || row.OnPitRoad == true)
                .ToArray();
            return directTrackRows.Length > 0
                ? directTrackRows.Any(row => row.TrackSurface == OnTrackSurface && row.OnPitRoad != true)
                : directRows.Any(row => row.HasTakenGrid);
        }

        var focusedRows = rows
            .Where(row => row.IsFocus || row.IsPlayer)
            .ToArray();

        return focusedRows.Length > 0
            ? focusedRows.Any(row => row.HasTakenGrid)
            : rows.Any(row => row.HasTakenGrid);
    }

    private static LiveTimingRow ApplyClassGap(
        LiveTimingRow row,
        IReadOnlyDictionary<int, LiveClassGapCar> classGapByCarIdx,
        LiveSignalEvidence classGapEvidence,
        bool isRaceSession)
    {
        if (!classGapByCarIdx.TryGetValue(row.CarIdx, out var gap))
        {
            return row;
        }

        var gapSeconds = gap.GapSecondsToClassLeader;
        var deltaSecondsToReference = gap.DeltaSecondsToReference;
        if (isRaceSession
            && gapSeconds is not null
            && gapSeconds > 0d
            && IsRaceF2Placeholder(row))
        {
            gapSeconds = null;
            deltaSecondsToReference = null;
        }

        return row with
        {
            IsClassLeader = row.IsClassLeader || gap.IsClassLeader,
            ClassPosition = row.ClassPosition ?? gap.ClassPosition,
            GapSecondsToClassLeader = row.GapSecondsToClassLeader ?? gapSeconds,
            GapLapsToClassLeader = row.GapLapsToClassLeader ?? gap.GapLapsToClassLeader,
            DeltaSecondsToFocus = row.DeltaSecondsToFocus ?? deltaSecondsToReference,
            GapEvidence = BuildClassGapRowEvidence(gap with { GapSecondsToClassLeader = gapSeconds }, classGapEvidence)
        };
    }

    private static LiveTimingRow[] ApplyDerivedClassGaps(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        IReadOnlyList<LiveTimingRow> rows)
    {
        var isRaceSession = IsRaceSession(context);
        var allowRaceTiming = !isRaceSession || AllowsLiveRaceGaps(sample);
        var lapTimeSeconds = RaceEstimatedLapTimeSeconds(context, sample);
        var focusRow = rows.FirstOrDefault(row => row.IsFocus);
        var focusF2 = focusRow is null ? null : UsableF2ForTiming(focusRow, isRaceSession);
        var leadersByClass = rows
            .Where(row => row.CarClass is not null)
            .GroupBy(row => row.CarClass!.Value)
            .Select(group => new ClassTimingLeaders(
                group.Key,
                SelectScoringClassLeader(group),
                SelectDerivedClassLeader(group, isRaceSession)))
            .Where(item => item.ScoringLeader is not null || item.F2Leader is not null)
            .ToDictionary(item => item.CarClass);
        var previousRowsByClass = rows
            .Where(row => row.CarClass is not null && row.ClassPosition is not null)
            .GroupBy(row => row.CarClass!.Value)
            .SelectMany(group =>
            {
                var ordered = group
                    .OrderBy(row => row.ClassPosition ?? int.MaxValue)
                    .ThenBy(row => row.OverallPosition ?? int.MaxValue)
                    .ThenBy(row => row.CarIdx)
                    .ToArray();
                return ordered.Select((row, index) => new
                {
                    row.CarIdx,
                    Previous = index > 0 ? ordered[index - 1] : null
                });
            })
            .Where(item => item.Previous is not null)
            .ToDictionary(item => item.CarIdx, item => item.Previous!);

        return rows
            .Select(row =>
            {
                var updated = row;
                var classLeader = row.CarClass is { } carClass && leadersByClass.TryGetValue(carClass, out var leaders)
                    ? isRaceSession
                        ? leaders.ScoringLeader ?? leaders.F2Leader
                        : leaders.F2Leader ?? leaders.ScoringLeader
                    : null;

                if (classLeader is not null)
                {
                    var hadGapSeconds = updated.GapSecondsToClassLeader is not null;
                    var gap = DerivedClassGapSeconds(
                        updated,
                        classLeader,
                        isRaceSession,
                        allowRaceTiming,
                        lapTimeSeconds);
                    if (gap is not null)
                    {
                        updated = updated with
                        {
                            IsClassLeader = updated.IsClassLeader || row.CarIdx == classLeader.CarIdx || row.ClassPosition == 1,
                            GapSecondsToClassLeader = updated.GapSecondsToClassLeader ?? gap.Value.Seconds,
                            GapEvidence = hadGapSeconds && updated.GapEvidence.IsUsable
                                ? updated.GapEvidence
                                : gap.Value.Evidence
                        };
                    }
                }

                if (previousRowsByClass.TryGetValue(row.CarIdx, out var previousRow))
                {
                    var interval = DerivedIntervalSeconds(
                        updated,
                        previousRow,
                        isRaceSession,
                        allowRaceTiming,
                        lapTimeSeconds);
                    if (interval is not null)
                    {
                        updated = updated with
                        {
                            IntervalSecondsToPreviousClassRow = updated.IntervalSecondsToPreviousClassRow ?? interval.Value.Seconds
                        };
                    }
                }

                if (focusRow is not null
                    && ValidNonNegative(focusF2) is { } focus
                    && UsableF2ForTiming(row, isRaceSession) is { } current
                    && current > 0d)
                {
                    updated = updated with
                    {
                        DeltaSecondsToFocus = updated.DeltaSecondsToFocus ?? current - focus
                    };
                }

                return updated;
            })
            .ToArray();
    }

    private static double? EstimatedRelativeSeconds(
        LiveTimingRow row,
        LiveTimingRow reference,
        double? lapTimeSeconds)
    {
        var rowEstimated = ValidPositive(row.EstimatedTimeSeconds);
        var referenceEstimated = ValidPositive(reference.EstimatedTimeSeconds);
        if (rowEstimated is null
            || referenceEstimated is null
            || row.LapDistPct is not { } rowLapDistPct
            || RelativeLapsFromLapDistance(rowLapDistPct, reference.LapDistPct) is not { } relativeLaps)
        {
            return null;
        }

        var delta = rowEstimated.Value - referenceEstimated.Value;
        if (lapTimeSeconds is { } lapSeconds && ValidPositive(lapSeconds) is not null)
        {
            if (delta > lapSeconds / 2d)
            {
                delta -= lapSeconds;
            }
            else if (delta < -lapSeconds / 2d)
            {
                delta += lapSeconds;
            }
        }

        return IsPlausibleEstimatedTiming(delta, relativeLaps, lapTimeSeconds)
            ? delta
            : null;
    }

    private static bool CanUseEstimatedTiming(LiveTimingRow row)
    {
        return ValidPositive(row.EstimatedTimeSeconds) is not null
            && row.LapDistPct is not null;
    }

    private static bool IsPlausibleEstimatedTiming(
        double seconds,
        double relativeLaps,
        double? lapTimeSeconds)
    {
        if (!IsFinite(seconds))
        {
            return false;
        }

        if (IsSuspiciousEstimatedTiming(seconds, relativeLaps, lapTimeSeconds))
        {
            return false;
        }

        var timingSign = Math.Sign(seconds);
        var lapSign = Math.Sign(relativeLaps);
        if (timingSign != 0 && lapSign != 0 && timingSign != lapSign)
        {
            return false;
        }

        if (lapTimeSeconds is { } lapSeconds && ValidPositive(lapSeconds) is not null)
        {
            var lapBasedSeconds = Math.Abs(relativeLaps * lapSeconds);
            var maximumDelta = Math.Max(5d, Math.Min(lapSeconds / 2d, lapBasedSeconds + 10d));
            return Math.Abs(seconds) <= maximumDelta;
        }

        return Math.Abs(seconds) <= 60d;
    }

    private static bool IsSuspiciousEstimatedTiming(
        double seconds,
        double relativeLaps,
        double? lapTimeSeconds)
    {
        if (Math.Abs(seconds) > SuspiciousEstimatedTimingSeconds)
        {
            return false;
        }

        if (lapTimeSeconds is { } lapSeconds && ValidPositive(lapSeconds) is not null)
        {
            return Math.Abs(relativeLaps * lapSeconds) >= SuspiciousEstimatedTimingLapEstimateSeconds;
        }

        return Math.Abs(relativeLaps) >= SuspiciousEstimatedTimingLapsWithoutLapTime;
    }

    private static double? PreGreenEstimatedLapTimeSeconds(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample)
    {
        return FirstValue(
            new[]
            {
                FocusLastLapTimeSeconds(sample),
                FocusBestLapTimeSeconds(sample),
                ValidPositive(sample.TeamLastLapTimeSeconds),
                ValidPositive(sample.TeamBestLapTimeSeconds),
                ValidPositive(context.Car.DriverCarEstLapTimeSeconds),
                ValidPositive(context.Car.CarClassEstLapTimeSeconds)
            });
    }

    private readonly record struct ClassTimingLeaders(
        int CarClass,
        LiveTimingRow? ScoringLeader,
        LiveTimingRow? F2Leader);

    private readonly record struct DerivedTimingSeconds(
        double Seconds,
        LiveSignalEvidence Evidence);

    private static LiveTimingRow? SelectScoringClassLeader(IEnumerable<LiveTimingRow> rows)
    {
        var candidates = rows.ToArray();
        var explicitLeader = candidates
            .Where(row => row.IsClassLeader || row.ClassPosition == 1)
            .OrderBy(row => row.ClassPosition == 1 ? 0 : 1)
            .ThenBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.CarIdx)
            .FirstOrDefault();

        if (explicitLeader is not null)
        {
            return explicitLeader;
        }

        return candidates.Any(row => row.ClassPosition is > 0)
            ? candidates
                .OrderBy(row => row.ClassPosition ?? int.MaxValue)
                .ThenBy(row => row.OverallPosition ?? int.MaxValue)
                .ThenBy(row => row.CarIdx)
                .FirstOrDefault()
            : null;
    }

    private static LiveTimingRow? SelectDerivedClassLeader(IEnumerable<LiveTimingRow> rows, bool isRaceSession)
    {
        var candidates = rows
            .Where(row => HasUsableF2ForClassGap(row, isRaceSession))
            .ToArray();
        var explicitLeader = candidates
            .Where(row => row.IsClassLeader || row.ClassPosition == 1)
            .OrderBy(row => row.ClassPosition == 1 ? 0 : 1)
            .ThenBy(row => UsableF2ForTiming(row, isRaceSession) ?? double.MaxValue)
            .FirstOrDefault();

        if (explicitLeader is not null)
        {
            return explicitLeader;
        }

        return candidates.Length > 1
            ? candidates.OrderBy(row => UsableF2ForTiming(row, isRaceSession) ?? double.MaxValue).First()
            : null;
    }

    private static bool HasUsableF2ForClassGap(LiveTimingRow row, bool isRaceSession)
    {
        return ValidPositive(UsableF2ForTiming(row, isRaceSession)) is not null
            || ((row.IsClassLeader || row.ClassPosition == 1)
                && ValidNonNegative(UsableF2ForTiming(row, isRaceSession)) is not null);
    }

    private static DerivedTimingSeconds? DerivedClassGapSeconds(
        LiveTimingRow row,
        LiveTimingRow classLeader,
        bool isRaceSession,
        bool allowRaceTiming,
        double? lapTimeSeconds)
    {
        if (row.CarIdx == classLeader.CarIdx || row.ClassPosition == 1)
        {
            return new DerivedTimingSeconds(0d, LiveSignalEvidence.Reliable("class-leader-row"));
        }

        if (allowRaceTiming
            && UsableF2ForTiming(row, isRaceSession) is { } rowF2
            && UsableF2ForTiming(classLeader, isRaceSession) is { } leaderF2
            && rowF2 >= leaderF2)
        {
            return new DerivedTimingSeconds(rowF2 - leaderF2, LiveSignalEvidence.Inferred("CarIdxF2Time"));
        }

        if (isRaceSession
            && allowRaceTiming
            && EstimatedSecondsBehind(row, classLeader, lapTimeSeconds) is { } estimated)
        {
            return new DerivedTimingSeconds(estimated, LiveSignalEvidence.Inferred("CarIdxEstTime+wrap"));
        }

        return null;
    }

    private static DerivedTimingSeconds? DerivedIntervalSeconds(
        LiveTimingRow row,
        LiveTimingRow previousRow,
        bool isRaceSession,
        bool allowRaceTiming,
        double? lapTimeSeconds)
    {
        if (allowRaceTiming
            && UsableF2ForTiming(row, isRaceSession) is { } rowF2
            && UsableF2ForTiming(previousRow, isRaceSession) is { } previousF2
            && rowF2 >= previousF2)
        {
            return new DerivedTimingSeconds(rowF2 - previousF2, LiveSignalEvidence.Inferred("CarIdxF2Time"));
        }

        if (isRaceSession
            && allowRaceTiming
            && EstimatedSecondsBehind(row, previousRow, lapTimeSeconds) is { } estimated)
        {
            return new DerivedTimingSeconds(estimated, LiveSignalEvidence.Inferred("CarIdxEstTime+wrap"));
        }

        return null;
    }

    private static double? UsableF2ForTiming(LiveTimingRow row, bool isRaceSession)
    {
        return UsableF2ForTiming(row.F2TimeSeconds, row.OverallPosition, isRaceSession, row.IsOverallLeader || row.ClassPosition == 1);
    }

    private static double? UsableF2ForTiming(
        double? f2TimeSeconds,
        int? overallPosition,
        bool isRaceSession,
        bool isLeader = false)
    {
        if (ValidNonNegative(f2TimeSeconds) is not { } f2)
        {
            return null;
        }

        if (!isRaceSession)
        {
            if (f2 == 0d && isLeader)
            {
                return 0d;
            }

            return ValidPositive(f2TimeSeconds);
        }

        if (f2 == 0d)
        {
            return isLeader || overallPosition == 1 ? 0d : null;
        }

        return IsRaceF2Placeholder(f2, overallPosition) ? null : f2;
    }

    private static bool IsRaceF2Placeholder(LiveTimingRow row)
    {
        return IsRaceF2Placeholder(row.F2TimeSeconds, row.OverallPosition);
    }

    private static bool IsRaceF2Placeholder(double? f2TimeSeconds, int? overallPosition)
    {
        if (overallPosition is not > 1
            || ValidNonNegative(f2TimeSeconds) is not { } f2)
        {
            return false;
        }

        return Math.Abs(f2 - ((overallPosition.Value - 1) / 1000d)) <= 0.00002d;
    }

    private static double? EstimatedSecondsBehind(
        LiveTimingRow row,
        LiveTimingRow referenceAhead,
        double? lapTimeSeconds)
    {
        if (IsPitRoadLike(row.TrackSurface, row.OnPitRoad)
            || IsPitRoadLike(referenceAhead.TrackSurface, referenceAhead.OnPitRoad))
        {
            return null;
        }

        var rowEstimated = ValidPositive(row.EstimatedTimeSeconds);
        var referenceEstimated = ValidPositive(referenceAhead.EstimatedTimeSeconds);
        var lapSeconds = ValidPositive(lapTimeSeconds);
        if (rowEstimated is null || referenceEstimated is null || lapSeconds is null)
        {
            return null;
        }

        var delta = referenceEstimated.Value - rowEstimated.Value;
        if (delta < -lapSeconds.Value / 2d)
        {
            delta += lapSeconds.Value;
        }
        else if (delta > lapSeconds.Value / 2d)
        {
            delta -= lapSeconds.Value;
        }

        if (delta < -0.05d)
        {
            return null;
        }

        return Math.Max(0d, delta);
    }

    private static double? RaceEstimatedLapTimeSeconds(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample)
    {
        return FirstValue(
            new[]
            {
                FocusLastLapTimeSeconds(sample),
                FocusBestLapTimeSeconds(sample),
                ValidPositive(sample.TeamLastLapTimeSeconds),
                ValidPositive(sample.TeamBestLapTimeSeconds),
                ValidPositive(context.Car.DriverCarEstLapTimeSeconds),
                ValidPositive(context.Car.CarClassEstLapTimeSeconds)
            });
    }

    private static LiveSignalEvidence BuildLeaderGapEvidence(
        string source,
        int? position,
        int? leaderCarIdx,
        int? referenceCarIdx,
        double? referenceF2TimeSeconds,
        double? leaderF2TimeSeconds,
        double? referenceProgress,
        double? leaderProgress)
    {
        if (position == 1 || (leaderCarIdx is not null && leaderCarIdx == referenceCarIdx))
        {
            return LiveSignalEvidence.Reliable("position");
        }

        if (ValidPositive(referenceF2TimeSeconds) is not null)
        {
            return ValidNonNegative(leaderF2TimeSeconds) is not null
                ? LiveSignalEvidence.Reliable("CarIdxF2Time")
                : LiveSignalEvidence.Partial("CarIdxF2Time", "leader_f2_time_missing");
        }

        if (referenceProgress is not null && leaderProgress is not null)
        {
            return LiveSignalEvidence.Inferred("CarIdxLapDistPct");
        }

        if (referenceCarIdx is null)
        {
            return LiveSignalEvidence.Unavailable(source, "reference_car_missing");
        }

        return LiveSignalEvidence.Unavailable(source, "gap_signals_missing");
    }

    private static LiveSignalEvidence BuildClassGapRowEvidence(
        LiveClassGapCar gap,
        LiveSignalEvidence classGapEvidence)
    {
        if (gap.IsClassLeader)
        {
            return LiveSignalEvidence.Reliable("class-leader-row");
        }

        if (gap.IsReferenceCar && !classGapEvidence.IsUsable)
        {
            return classGapEvidence;
        }

        if (gap.GapSecondsToClassLeader is not null)
        {
            return classGapEvidence;
        }

        if (gap.GapLapsToClassLeader is not null)
        {
            return LiveSignalEvidence.Inferred("CarIdxLapDistPct");
        }

        return LiveSignalEvidence.Unavailable("class-gap", "gap_signals_missing");
    }

    private static LiveSignalEvidence BuildFuelLevelEvidence(HistoricalTelemetrySample sample)
    {
        return ValidPositive(sample.FuelLevelLiters) is not null
            ? LiveSignalEvidence.Reliable("FuelLevel")
            : LiveSignalEvidence.Unavailable("FuelLevel", "missing_or_zero_fuel_level");
    }

    private static LiveSignalEvidence BuildInstantaneousBurnEvidence(
        HistoricalTelemetrySample sample,
        LiveFuelSnapshot fuel)
    {
        if (ValidPositive(sample.FuelUsePerHourKg) is null)
        {
            return LiveSignalEvidence.Unavailable("FuelUsePerHour", "missing_or_zero_fuel_use");
        }

        return fuel.HasValidFuel
            ? LiveSignalEvidence.DiagnosticOnly("FuelUsePerHour", "instantaneous_burn_requires_smoothing")
            : LiveSignalEvidence.DiagnosticOnly("FuelUsePerHour", "fuel_level_invalid");
    }

    private static LiveSignalEvidence BuildBaselineEligibilityEvidence(
        HistoricalTelemetrySample sample,
        LiveFuelSnapshot fuel)
    {
        if (!fuel.HasValidFuel)
        {
            return LiveSignalEvidence.Unavailable("measured-local-fuel-baseline", "valid_local_fuel_level_missing");
        }

        if (sample.OnPitRoad || sample.PitstopActive || sample.PlayerCarInPitStall)
        {
            return LiveSignalEvidence.Unavailable("measured-local-fuel-baseline", "pit_or_service_context");
        }

        if (Progress(sample.LapCompleted, sample.LapDistPct) is null)
        {
            return LiveSignalEvidence.Unavailable("measured-local-fuel-baseline", "local_lap_progress_missing");
        }

        return LiveSignalEvidence.Partial(
            "measured-local-fuel-baseline",
            "requires_previous_green_distance_sample");
    }

    private static bool HasTimingSignal(
        int? overallPosition,
        int? classPosition,
        double? f2TimeSeconds,
        double? estimatedTimeSeconds,
        double? lastLapTimeSeconds,
        double? bestLapTimeSeconds)
    {
        return overallPosition is > 0
            || classPosition is > 0
            || ValidPositive(f2TimeSeconds) is not null
            || ValidPositive(estimatedTimeSeconds) is not null
            || ValidPositive(lastLapTimeSeconds) is not null
            || ValidPositive(bestLapTimeSeconds) is not null;
    }

    private static string RadarPlacementMissingReason(
        bool hasSpatialProgress,
        bool hasTrackLength,
        int? trackSurface,
        bool? onPitRoad)
    {
        if (!hasSpatialProgress)
        {
            return "lap_progress_missing";
        }

        if (!hasTrackLength)
        {
            return "track_length_missing";
        }

        return IsPitRoadLike(trackSurface, onPitRoad)
            ? "pit_or_off_track_surface"
            : "radar_placement_unavailable";
    }

    private static double? RelativeLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        return FirstValue(
            new double?[]
            {
                FocusLastLapTimeSeconds(sample),
                FocusBestLapTimeSeconds(sample)
            });
    }

    private static bool IsPitRoadLike(int? trackSurface, bool? onPitRoad)
    {
        return onPitRoad == true || IsPitRoadTrackSurface(trackSurface);
    }

    private static bool HasTakenGrid(
        int carIdx,
        int? trackSurface,
        bool? onPitRoad,
        IReadOnlySet<int>? griddedCarIdxs)
    {
        return griddedCarIdxs?.Contains(carIdx) == true
            || (trackSurface == OnTrackSurface && onPitRoad != true);
    }

    private static bool IsPitRoadTrackSurface(int? trackSurface)
    {
        return trackSurface is 1 or 2;
    }

    private static LiveSignalEvidence MergeEvidence(IEnumerable<LiveSignalEvidence> evidence)
    {
        var items = evidence.ToArray();
        if (items.Length == 0)
        {
            return LiveSignalEvidence.Unavailable("unknown", "no_evidence");
        }

        var usable = items
            .Where(item => item.IsUsable)
            .OrderByDescending(item => item.Quality)
            .ToArray();
        if (usable.Length > 0)
        {
            return new LiveSignalEvidence(
                Source: JoinSources(usable),
                Quality: usable.Max(item => item.Quality),
                IsUsable: true,
                MissingReason: null);
        }

        var strongest = items
            .OrderByDescending(item => item.Quality)
            .First();
        return new LiveSignalEvidence(
            Source: JoinSources(items),
            Quality: strongest.Quality,
            IsUsable: false,
            MissingReason: strongest.MissingReason);
    }

    private static string JoinSources(IEnumerable<LiveSignalEvidence> evidence)
    {
        return string.Join(
            "+",
            evidence
                .Select(item => item.Source)
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<int, string> BuildClassNamesByClassId(
        IReadOnlyList<HistoricalSessionDriver> drivers)
    {
        return drivers
            .Where(driver => driver.CarClassId is not null)
            .GroupBy(driver => driver.CarClassId!.Value)
            .Select(group => new
            {
                CarClassId = group.Key,
                ClassName = SelectClassName(group)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ClassName))
            .ToDictionary(item => item.CarClassId, item => item.ClassName!);
    }

    private static string? SelectClassName(IEnumerable<HistoricalSessionDriver> drivers)
    {
        var driverRows = drivers.ToArray();
        var explicitName = FirstNonEmpty(driverRows.Select(driver => driver.CarClassShortName));
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        var carNames = driverRows
            .Select(driver => FirstNonEmpty(driver.CarScreenNameShort, driver.CarScreenName, driver.CarPath))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (carNames.Length == 1)
        {
            return carNames[0];
        }

        return SelectCommonClassToken(carNames);
    }

    private static string? SelectCommonClassToken(IReadOnlyList<string> carNames)
    {
        if (carNames.Count == 0)
        {
            return null;
        }

        var commonTokens = TokenizeCarName(carNames[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var carName in carNames.Skip(1))
        {
            commonTokens.IntersectWith(TokenizeCarName(carName));
        }

        return commonTokens
            .Where(IsLikelyClassToken)
            .OrderByDescending(token => token.Any(char.IsDigit))
            .ThenBy(token => token.Length)
            .FirstOrDefault();
    }

    private static IEnumerable<string> TokenizeCarName(string carName)
    {
        var token = new List<char>();
        foreach (var character in carName)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Add(character);
                continue;
            }

            if (token.Count > 0)
            {
                yield return new string(token.ToArray()).ToUpperInvariant();
                token.Clear();
            }
        }

        if (token.Count > 0)
        {
            yield return new string(token.ToArray()).ToUpperInvariant();
        }
    }

    private static bool IsLikelyClassToken(string token)
    {
        return token.Length >= 2
            && !token.All(character => char.IsDigit(character))
            && (token.Any(character => char.IsDigit(character))
                || string.Equals(token, "TCR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "GTP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "CUP", StringComparison.OrdinalIgnoreCase));
    }

    private static LiveDriverIdentity ToLiveDriver(
        HistoricalSessionDriver driver,
        IReadOnlyDictionary<int, string> classNamesByClassId)
    {
        var carClassName = driver.CarClassShortName;
        if (string.IsNullOrWhiteSpace(carClassName)
            && driver.CarClassId is { } carClassId
            && classNamesByClassId.TryGetValue(carClassId, out var derivedClassName))
        {
            carClassName = derivedClassName;
        }

        return new LiveDriverIdentity(
            CarIdx: driver.CarIdx!.Value,
            DriverName: driver.UserName,
            AbbrevName: driver.AbbrevName,
            Initials: driver.Initials,
            UserId: driver.UserId,
            TeamId: driver.TeamId,
            TeamName: driver.TeamName,
            CarNumber: driver.CarNumber,
            CarClassId: driver.CarClassId,
            CarClassName: carClassName,
            CarClassColorHex: driver.CarClassColorHex,
            IsSpectator: driver.IsSpectator);
    }

    private static int? FocusCarIdx(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx;
    }

    private static int? ReferenceCarClass(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusCarClass;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusCarClass ?? sample.TeamCarClass
            : sample.FocusCarClass;
    }

    private static int? FocusPosition(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusPosition;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusPosition ?? sample.TeamPosition
            : sample.FocusPosition;
    }

    private static int? FocusClassPosition(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusClassPosition;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusClassPosition ?? sample.TeamClassPosition
            : sample.FocusClassPosition;
    }

    private static int? FocusLapCompleted(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusLapCompleted;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusLapCompleted ?? sample.TeamLapCompleted ?? sample.LapCompleted
            : sample.FocusLapCompleted;
    }

    private static double? FocusLapDistPct(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidLapDistPct(sample.FocusLapDistPct);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidLapDistPct(sample.FocusLapDistPct)
                ?? ValidLapDistPct(sample.TeamLapDistPct)
                ?? ValidLapDistPct(sample.LapDistPct)
            : ValidLapDistPct(sample.FocusLapDistPct);
    }

    private static double? FocusF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusF2TimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidNonNegative(sample.FocusF2TimeSeconds)
                ?? ValidNonNegative(sample.TeamF2TimeSeconds)
            : ValidNonNegative(sample.FocusF2TimeSeconds);
    }

    private static double? FocusEstimatedTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusEstimatedTimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidNonNegative(sample.FocusEstimatedTimeSeconds)
                ?? ValidNonNegative(sample.TeamEstimatedTimeSeconds)
            : ValidNonNegative(sample.FocusEstimatedTimeSeconds);
    }

    private static double? FocusLastLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusLastLapTimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidPositive(sample.FocusLastLapTimeSeconds)
                ?? ValidPositive(sample.TeamLastLapTimeSeconds)
                ?? ValidPositive(sample.LapLastLapTimeSeconds)
            : ValidPositive(sample.FocusLastLapTimeSeconds);
    }

    private static double? FocusBestLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusBestLapTimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidPositive(sample.FocusBestLapTimeSeconds)
                ?? ValidPositive(sample.TeamBestLapTimeSeconds)
                ?? ValidPositive(sample.LapBestLapTimeSeconds)
            : ValidPositive(sample.FocusBestLapTimeSeconds);
    }

    private static bool? FocusOnPitRoad(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusOnPitRoad;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusOnPitRoad ?? sample.TeamOnPitRoad ?? sample.OnPitRoad
            : sample.FocusOnPitRoad;
    }

    private static int? FocusClassLeaderCarIdx(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusClassLeaderCarIdx;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusClassLeaderCarIdx ?? sample.ClassLeaderCarIdx
            : sample.FocusClassLeaderCarIdx;
    }

    private static int? FocusClassLeaderLapCompleted(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusClassLeaderLapCompleted;
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? sample.FocusClassLeaderLapCompleted ?? sample.ClassLeaderLapCompleted
            : sample.FocusClassLeaderLapCompleted;
    }

    private static double? FocusClassLeaderLapDistPct(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidLapDistPct(sample.FocusClassLeaderLapDistPct);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidLapDistPct(sample.FocusClassLeaderLapDistPct)
                ?? ValidLapDistPct(sample.ClassLeaderLapDistPct)
            : ValidLapDistPct(sample.FocusClassLeaderLapDistPct);
    }

    private static double? FocusClassLeaderF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusClassLeaderF2TimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidNonNegative(sample.FocusClassLeaderF2TimeSeconds)
                ?? ValidNonNegative(sample.ClassLeaderF2TimeSeconds)
            : ValidNonNegative(sample.FocusClassLeaderF2TimeSeconds);
    }

    private static double? FocusClassLeaderEstimatedTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusClassLeaderEstimatedTimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidNonNegative(sample.FocusClassLeaderEstimatedTimeSeconds)
                ?? ValidNonNegative(sample.ClassLeaderEstimatedTimeSeconds)
            : ValidNonNegative(sample.FocusClassLeaderEstimatedTimeSeconds);
    }

    private static double? FocusClassLeaderLastLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusClassLeaderLastLapTimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidPositive(sample.FocusClassLeaderLastLapTimeSeconds)
                ?? ValidPositive(sample.ClassLeaderLastLapTimeSeconds)
            : ValidPositive(sample.FocusClassLeaderLastLapTimeSeconds);
    }

    private static double? FocusClassLeaderBestLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (!HasFocus(sample))
        {
            return null;
        }

        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusClassLeaderBestLapTimeSeconds);
        }

        return FocusUsesPlayerLocalFallback(sample)
            ? ValidPositive(sample.FocusClassLeaderBestLapTimeSeconds)
                ?? ValidPositive(sample.ClassLeaderBestLapTimeSeconds)
            : ValidPositive(sample.FocusClassLeaderBestLapTimeSeconds);
    }

    private static bool HasFocus(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is not null;
    }

    private static bool FocusUsesPlayerLocalFallback(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is not null
            && sample.PlayerCarIdx is not null
            && sample.FocusCarIdx == sample.PlayerCarIdx;
    }

    private static bool HasExplicitNonPlayerFocus(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx is not null
            && sample.PlayerCarIdx is not null
            && sample.FocusCarIdx != sample.PlayerCarIdx;
    }

    private static double? Progress(int? lapCompleted, double? lapDistPct)
    {
        var validPct = ValidLapDistPct(lapDistPct);
        return lapCompleted is >= 0 && validPct is not null
            ? lapCompleted.Value + validPct.Value
            : null;
    }

    private static double? ValidLapDistPct(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d
            ? Math.Clamp(number, 0d, 1d)
            : null;
    }

    private static double? ValidPositive(double? value)
    {
        return value is { } number && IsFinite(number) && number > 0d ? number : null;
    }

    private static double? ValidNonNegative(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d ? number : null;
    }

    private static double? ValidFinite(double? value)
    {
        return value is { } number && IsFinite(number) ? number : null;
    }

    private static double? ValidPercent(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d ? Math.Min(number, 100d) : null;
    }

    private static double? ValidUnitInterval(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d && number <= 1d ? number : null;
    }

    private static IReadOnlyList<TrackMapSectorSlice> NormalizeSectors(IReadOnlyList<HistoricalTrackSector> sectors)
    {
        var ordered = sectors
            .Where(sector => IsFinite(sector.SectorStartPct) && sector.SectorStartPct >= 0d && sector.SectorStartPct < 1d)
            .GroupBy(sector => sector.SectorNum)
            .Select(group => group.OrderBy(sector => sector.SectorStartPct).First())
            .OrderBy(sector => sector.SectorStartPct)
            .ThenBy(sector => sector.SectorNum)
            .ToArray();
        if (ordered.Length < 2)
        {
            return [];
        }

        return ordered
            .Select((sector, index) => new TrackMapSectorSlice(
                sector.SectorNum,
                Math.Round(sector.SectorStartPct, 6),
                Math.Round(index + 1 < ordered.Length ? ordered[index + 1].SectorStartPct : 1d, 6)))
            .ToArray();
    }

    private static bool IsNonNegativeFinite(double value)
    {
        return IsFinite(value) && value >= 0d;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static T? FirstValue<T>(IEnumerable<T?> values)
        where T : struct
    {
        foreach (var value in values)
        {
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static bool? FirstValue(IEnumerable<bool?> values)
    {
        foreach (var value in values)
        {
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static string FormatTrackWetness(int trackWetness)
    {
        return trackWetness switch
        {
            0 => "unknown",
            1 => "dry",
            2 => "mostly dry",
            3 => "very lightly wet",
            4 => "lightly wet",
            5 => "moderately wet",
            6 => "very wet",
            7 => "extremely wet",
            _ => $"value {trackWetness}"
        };
    }

    private static bool DetermineDeclaredWetSurfaceMismatch(bool declaredWet, int? trackWetness)
    {
        if (trackWetness is null)
        {
            return false;
        }

        return declaredWet
            ? trackWetness <= 1
            : trackWetness >= 3;
    }

    private static string? FormatSkiesLabel(string? trackSkies)
    {
        return string.IsNullOrWhiteSpace(trackSkies) ? null : trackSkies.Trim();
    }

    private static string FormatSkiesLabel(int skies)
    {
        return skies switch
        {
            0 => "clear",
            1 => "partly cloudy",
            2 => "mostly cloudy",
            3 => "overcast",
            _ => $"skies {skies.ToString(CultureInfo.InvariantCulture)}"
        };
    }

    private sealed record ScoringResultSelection(
        LiveScoringSource Source,
        HistoricalSessionResultPosition[] Results);

    private sealed record RaceProgressMetric(double? Value, string Source);

    private sealed record TrackMapSectorSlice(
        int SectorNum,
        double StartPct,
        double EndPct);
}
