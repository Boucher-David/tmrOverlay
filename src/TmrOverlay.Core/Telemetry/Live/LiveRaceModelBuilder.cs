using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal static class LiveRaceModelBuilder
{
    public static LiveRaceModels From(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveFuelSnapshot fuel,
        LiveProximitySnapshot proximity,
        LiveLeaderGapSnapshot leaderGap)
    {
        var drivers = BuildDriverDirectory(context, sample);
        var timing = BuildTiming(context, sample, leaderGap, drivers);

        return new LiveRaceModels(
            Session: BuildSession(context, sample),
            DriverDirectory: drivers,
            Timing: timing,
            Relative: BuildRelative(sample, proximity, timing),
            Spatial: BuildSpatial(context, sample, proximity),
            Weather: BuildWeather(context, sample),
            FuelPit: BuildFuelPit(sample, fuel),
            RaceEvents: BuildRaceEvents(sample),
            Inputs: BuildInputs(sample));
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

    private static LiveDriverDirectoryModel BuildDriverDirectory(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample)
    {
        var drivers = context.Drivers
            .Where(driver => driver.CarIdx is not null)
            .Select(ToLiveDriver)
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

    private static LiveTimingModel BuildTiming(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveLeaderGapSnapshot leaderGap,
        LiveDriverDirectoryModel driverDirectory)
    {
        var rows = new List<LiveTimingRow>();
        var focusCarIdx = FocusCarIdx(sample);
        var playerCarIdx = sample.PlayerCarIdx;
        var classLeaderCarIdx = leaderGap.ClassLeaderCarIdx ?? FocusClassLeaderCarIdx(sample);
        var overallGapEvidence = BuildLeaderGapEvidence(
            source: "overall-gap",
            position: FocusPosition(sample),
            leaderCarIdx: sample.LeaderCarIdx,
            referenceCarIdx: focusCarIdx,
            referenceF2TimeSeconds: FocusF2TimeSeconds(sample),
            leaderF2TimeSeconds: sample.LeaderF2TimeSeconds,
            referenceProgress: Progress(FocusLapCompleted(sample), FocusLapDistPct(sample)),
            leaderProgress: Progress(sample.LeaderLapCompleted, sample.LeaderLapDistPct));
        var classGapEvidence = BuildLeaderGapEvidence(
            source: "class-gap",
            position: FocusClassPosition(sample),
            leaderCarIdx: classLeaderCarIdx,
            referenceCarIdx: focusCarIdx,
            referenceF2TimeSeconds: FocusF2TimeSeconds(sample),
            leaderF2TimeSeconds: FocusClassLeaderF2TimeSeconds(sample),
            referenceProgress: Progress(FocusLapCompleted(sample), FocusLapDistPct(sample)),
            leaderProgress: Progress(FocusClassLeaderLapCompleted(sample), FocusClassLeaderLapDistPct(sample)));

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
            onPitRoad: FocusOnPitRoad(sample));

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
            onPitRoad: sample.TeamOnPitRoad ?? sample.OnPitRoad);

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
            onPitRoad: null);

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
            onPitRoad: null);

        AddProximityRows(rows, context, driverDirectory, sample.FocusClassCars, "focus-class-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx);
        AddProximityRows(rows, context, driverDirectory, sample.ClassCars, "player-class-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx);
        AddProximityRows(rows, context, driverDirectory, sample.NearbyCars, "nearby-cars", focusCarIdx, playerCarIdx, leaderGap.OverallLeaderCarIdx, classLeaderCarIdx);

        var classGapByCarIdx = leaderGap.ClassCars.ToDictionary(car => car.CarIdx);
        var mergedRows = rows
            .GroupBy(row => row.CarIdx)
            .Select(group => ApplyClassGap(MergeRows(group), classGapByCarIdx, classGapEvidence))
            .OrderBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenByDescending(row => row.ProgressLaps ?? double.MinValue)
            .ThenBy(row => row.CarIdx)
            .ToArray();

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
            rows.Add(new LiveRelativeRow(
                CarIdx: car.CarIdx,
                Quality: car.RelativeSeconds is not null || car.RelativeMeters is not null
                    ? LiveModelQuality.Reliable
                    : LiveModelQuality.Partial,
                Source: "proximity",
                IsAhead: car.RelativeLaps > 0d,
                IsBehind: car.RelativeLaps < 0d,
                IsSameClass: referenceClass is not null && car.CarClass == referenceClass,
                TimingEvidence: car.RelativeSeconds is not null
                    ? LiveSignalEvidence.Reliable("proximity-relative-seconds")
                    : LiveSignalEvidence.Partial("proximity-relative-seconds", "relative_seconds_missing"),
                PlacementEvidence: car.RelativeMeters is not null
                    ? LiveSignalEvidence.Reliable("CarIdxLapDistPct+track-length")
                    : LiveSignalEvidence.Inferred("CarIdxLapDistPct"),
                DriverName: timingRow?.DriverName,
                OverallPosition: car.OverallPosition ?? timingRow?.OverallPosition,
                ClassPosition: car.ClassPosition ?? timingRow?.ClassPosition,
                CarClass: car.CarClass ?? timingRow?.CarClass,
                RelativeSeconds: car.RelativeSeconds,
                RelativeLaps: car.RelativeLaps,
                RelativeMeters: car.RelativeMeters,
                OnPitRoad: car.OnPitRoad ?? timingRow?.OnPitRoad));
        }

        if (rows.Count == 0)
        {
            rows.AddRange(timing.OverallRows
                .Where(row => !row.IsFocus && row.DeltaSecondsToFocus is not null)
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

    private static LiveSpatialModel BuildSpatial(
        HistoricalSessionContext context,
        HistoricalTelemetrySample sample,
        LiveProximitySnapshot proximity)
    {
        var trackLengthMeters = ValidPositive(context.Track.TrackLengthKm) is { } km ? km * 1000d : (double?)null;
        var cars = proximity.NearbyCars
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
            HasData: proximity.HasData || proximity.HasCarLeft || proximity.HasCarRight || cars.Length > 0 || FocusLapDistPct(sample) is not null,
            Quality: cars.Length > 0
                ? cars.Max(car => car.Quality)
                : proximity.HasCarLeft || proximity.HasCarRight || FocusLapDistPct(sample) is not null
                    ? LiveModelQuality.Partial
                    : LiveModelQuality.Unavailable,
            ReferenceCarIdx: FocusCarIdx(sample),
            ReferenceCarClass: proximity.ReferenceCarClass,
            CarLeftRight: proximity.CarLeftRight,
            SideStatus: proximity.SideStatus,
            HasCarLeft: proximity.HasCarLeft,
            HasCarRight: proximity.HasCarRight,
            SideOverlapWindowSeconds: proximity.SideOverlapWindowSeconds,
            TrackLengthMeters: trackLengthMeters,
            ReferenceLapDistPct: FocusLapDistPct(sample),
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
        var hasLiveWeather = IsFinite(sample.AirTempC)
            || IsFinite(sample.TrackTempCrewC)
            || trackWetness is not null
            || sample.WeatherDeclaredWet;
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
            SkiesLabel: FormatSkiesLabel(context.Conditions.TrackSkies),
            PrecipitationPercent: context.Conditions.TrackPrecipitationPercent,
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
        bool? onPitRoad)
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
            deltaSecondsToFocus: null,
            trackSurface: trackSurface,
            onPitRoad: onPitRoad));
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
        int? classLeaderCarIdx)
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
                deltaSecondsToFocus: null,
                trackSurface: car.TrackSurface,
                onPitRoad: car.OnPitRoad));
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
        double? deltaSecondsToFocus,
        int? trackSurface,
        bool? onPitRoad)
    {
        var driver = driverDirectory.Drivers.FirstOrDefault(candidate => candidate.CarIdx == carIdx);
        var progressLaps = Progress(lapCompleted, lapDistPct);
        var hasTiming = HasTimingSignal(
            overallPosition,
            classPosition,
            f2TimeSeconds,
            estimatedTimeSeconds,
            lastLapTimeSeconds,
            bestLapTimeSeconds);
        var hasSpatialProgress = progressLaps is not null;
        var canUseForRadarPlacement = hasSpatialProgress && !IsPitRoadLike(trackSurface, onPitRoad);

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
                    hasSpatialProgress ? "pit_or_off_track_surface" : "lap_progress_missing"),
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
            LapDistPct: ValidLapDistPct(lapDistPct),
            ProgressLaps: progressLaps,
            F2TimeSeconds: ValidNonNegative(f2TimeSeconds),
            EstimatedTimeSeconds: ValidNonNegative(estimatedTimeSeconds),
            LastLapTimeSeconds: ValidPositive(lastLapTimeSeconds),
            BestLapTimeSeconds: ValidPositive(bestLapTimeSeconds),
            GapSecondsToClassLeader: gapSecondsToClassLeader,
            GapLapsToClassLeader: gapLapsToClassLeader,
            DeltaSecondsToFocus: deltaSecondsToFocus,
            TrackSurface: trackSurface,
            OnPitRoad: onPitRoad);
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
            DeltaSecondsToFocus = FirstValue(rows.Select(row => row.DeltaSecondsToFocus)),
            TrackSurface = FirstValue(rows.Select(row => row.TrackSurface)),
            OnPitRoad = FirstValue(rows.Select(row => row.OnPitRoad)),
            HasTiming = rows.Any(row => row.HasTiming),
            HasSpatialProgress = rows.Any(row => row.HasSpatialProgress),
            CanUseForRadarPlacement = rows.Any(row => row.CanUseForRadarPlacement),
            TimingEvidence = MergeEvidence(rows.Select(row => row.TimingEvidence)),
            SpatialEvidence = MergeEvidence(rows.Select(row => row.SpatialEvidence)),
            RadarPlacementEvidence = MergeEvidence(rows.Select(row => row.RadarPlacementEvidence)),
            GapEvidence = MergeEvidence(rows.Select(row => row.GapEvidence))
        };
    }

    private static LiveTimingRow ApplyClassGap(
        LiveTimingRow row,
        IReadOnlyDictionary<int, LiveClassGapCar> classGapByCarIdx,
        LiveSignalEvidence classGapEvidence)
    {
        if (!classGapByCarIdx.TryGetValue(row.CarIdx, out var gap))
        {
            return row;
        }

        return row with
        {
            IsClassLeader = row.IsClassLeader || gap.IsClassLeader,
            ClassPosition = row.ClassPosition ?? gap.ClassPosition,
            GapSecondsToClassLeader = row.GapSecondsToClassLeader ?? gap.GapSecondsToClassLeader,
            GapLapsToClassLeader = row.GapLapsToClassLeader ?? gap.GapLapsToClassLeader,
            DeltaSecondsToFocus = row.DeltaSecondsToFocus ?? gap.DeltaSecondsToReference,
            GapEvidence = BuildClassGapRowEvidence(gap, classGapEvidence)
        };
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

        if (ValidNonNegative(referenceF2TimeSeconds) is not null)
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
            || ValidNonNegative(f2TimeSeconds) is not null
            || ValidNonNegative(estimatedTimeSeconds) is not null
            || ValidPositive(lastLapTimeSeconds) is not null
            || ValidPositive(bestLapTimeSeconds) is not null;
    }

    private static bool IsPitRoadLike(int? trackSurface, bool? onPitRoad)
    {
        return onPitRoad == true || IsPitRoadTrackSurface(trackSurface);
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

    private static LiveDriverIdentity ToLiveDriver(HistoricalSessionDriver driver)
    {
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
            CarClassName: driver.CarClassShortName,
            CarClassColorHex: driver.CarClassColorHex,
            IsSpectator: driver.IsSpectator);
    }

    private static int? FocusCarIdx(HistoricalTelemetrySample sample)
    {
        return sample.FocusCarIdx ?? sample.PlayerCarIdx;
    }

    private static int? ReferenceCarClass(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusCarClass;
        }

        return sample.FocusCarClass ?? sample.TeamCarClass;
    }

    private static int? FocusPosition(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusPosition;
        }

        return sample.FocusPosition ?? sample.TeamPosition;
    }

    private static int? FocusClassPosition(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusClassPosition;
        }

        return sample.FocusClassPosition ?? sample.TeamClassPosition;
    }

    private static int? FocusLapCompleted(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusLapCompleted;
        }

        return sample.FocusLapCompleted ?? sample.TeamLapCompleted ?? sample.LapCompleted;
    }

    private static double? FocusLapDistPct(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidLapDistPct(sample.FocusLapDistPct);
        }

        return ValidLapDistPct(sample.FocusLapDistPct)
            ?? ValidLapDistPct(sample.TeamLapDistPct)
            ?? ValidLapDistPct(sample.LapDistPct);
    }

    private static double? FocusF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusF2TimeSeconds);
        }

        return ValidNonNegative(sample.FocusF2TimeSeconds)
            ?? ValidNonNegative(sample.TeamF2TimeSeconds);
    }

    private static double? FocusEstimatedTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusEstimatedTimeSeconds);
        }

        return ValidNonNegative(sample.FocusEstimatedTimeSeconds)
            ?? ValidNonNegative(sample.TeamEstimatedTimeSeconds);
    }

    private static double? FocusLastLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusLastLapTimeSeconds);
        }

        return ValidPositive(sample.FocusLastLapTimeSeconds)
            ?? ValidPositive(sample.TeamLastLapTimeSeconds)
            ?? ValidPositive(sample.LapLastLapTimeSeconds);
    }

    private static double? FocusBestLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusBestLapTimeSeconds);
        }

        return ValidPositive(sample.FocusBestLapTimeSeconds)
            ?? ValidPositive(sample.TeamBestLapTimeSeconds)
            ?? ValidPositive(sample.LapBestLapTimeSeconds);
    }

    private static bool? FocusOnPitRoad(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusOnPitRoad;
        }

        return sample.FocusOnPitRoad ?? sample.TeamOnPitRoad ?? sample.OnPitRoad;
    }

    private static int? FocusClassLeaderCarIdx(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusClassLeaderCarIdx;
        }

        return sample.FocusClassLeaderCarIdx ?? sample.ClassLeaderCarIdx;
    }

    private static int? FocusClassLeaderLapCompleted(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return sample.FocusClassLeaderLapCompleted;
        }

        return sample.FocusClassLeaderLapCompleted ?? sample.ClassLeaderLapCompleted;
    }

    private static double? FocusClassLeaderLapDistPct(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidLapDistPct(sample.FocusClassLeaderLapDistPct);
        }

        return ValidLapDistPct(sample.FocusClassLeaderLapDistPct)
            ?? ValidLapDistPct(sample.ClassLeaderLapDistPct);
    }

    private static double? FocusClassLeaderF2TimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusClassLeaderF2TimeSeconds);
        }

        return ValidNonNegative(sample.FocusClassLeaderF2TimeSeconds)
            ?? ValidNonNegative(sample.ClassLeaderF2TimeSeconds);
    }

    private static double? FocusClassLeaderEstimatedTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidNonNegative(sample.FocusClassLeaderEstimatedTimeSeconds);
        }

        return ValidNonNegative(sample.FocusClassLeaderEstimatedTimeSeconds)
            ?? ValidNonNegative(sample.ClassLeaderEstimatedTimeSeconds);
    }

    private static double? FocusClassLeaderLastLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusClassLeaderLastLapTimeSeconds);
        }

        return ValidPositive(sample.FocusClassLeaderLastLapTimeSeconds)
            ?? ValidPositive(sample.ClassLeaderLastLapTimeSeconds);
    }

    private static double? FocusClassLeaderBestLapTimeSeconds(HistoricalTelemetrySample sample)
    {
        if (HasExplicitNonPlayerFocus(sample))
        {
            return ValidPositive(sample.FocusClassLeaderBestLapTimeSeconds);
        }

        return ValidPositive(sample.FocusClassLeaderBestLapTimeSeconds)
            ?? ValidPositive(sample.ClassLeaderBestLapTimeSeconds);
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

    private static double? ValidUnitInterval(double? value)
    {
        return value is { } number && IsFinite(number) && number >= 0d && number <= 1d ? number : null;
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
}
