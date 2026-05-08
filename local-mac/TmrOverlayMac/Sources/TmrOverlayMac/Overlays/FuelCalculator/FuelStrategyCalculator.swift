import Foundation

struct FuelStrategySnapshot {
    var hasData: Bool
    var status: String
    var currentFuelLiters: Double?
    var fuelPerLapLiters: Double?
    var fuelPerLapSource: String
    var fuelPerLapMinimumLiters: Double?
    var fuelPerLapMaximumLiters: Double?
    var lapTimeSeconds: Double?
    var lapTimeSource: String
    var raceLapsRemaining: Double?
    var plannedRaceLaps: Int?
    var fullTankStintLaps: Double?
    var plannedStintCount: Int?
    var plannedStopCount: Int?
    var finalStintTargetLaps: Int?
    var additionalFuelNeededLiters: Double?
    var requiredFuelSavingLitersPerLap: Double?
    var requiredFuelSavingPercent: Double?
    var stopOptimization: FuelStopOptimization?
    var rhythmComparison: FuelRhythmComparison?
    var teammateStintTargetLaps: Int?
    var teammateStintTargetSource: String?
    var tireModelSource: String
    var fuelFillRateLitersPerSecond: Double?
    var tireChangeServiceSeconds: Double?
    var stints: [FuelStintEstimate]
}

struct FuelStintEstimate {
    var number: Int
    var lengthLaps: Double
    var source: String
    var targetLaps: Int? = nil
    var targetFuelPerLapLiters: Double? = nil
    var currentFuelPerLapLiters: Double? = nil
    var currentFuelPerLapSource: String? = nil
    var requiredFuelSavingLitersPerLap: Double? = nil
    var requiredFuelSavingPercent: Double? = nil
    var tireAdvice: TireChangeAdvice? = nil
}

struct TireChangeAdvice {
    var text: String
    var fuelToAddLiters: Double? = nil
    var refuelSeconds: Double? = nil
    var tireServiceSeconds: Double? = nil
    var timeLossSeconds: Double? = nil

    static let pending = TireChangeAdvice(text: "tire data pending")
    static let noStop = TireChangeAdvice(text: "no tire stop")
}

struct FuelStopOptimization {
    var isRealistic: Bool
    var candidateStintCount: Int
    var savedStopCount: Int
    var requiredFuelPerLapLiters: Double
    var requiredSavingLitersPerLap: Double
    var requiredSavingPercent: Double
    var estimatedSavedSeconds: Double?
    var breakEvenLossSecondsPerLap: Double?
    var message: String
}

struct FuelRhythmComparison {
    var shortTargetLaps: Int
    var longTargetLaps: Int
    var shortStintCount: Int
    var longStintCount: Int
    var shortStopCount: Int
    var longStopCount: Int
    var additionalStopCount: Int
    var requiredFuelPerLapLiters: Double
    var requiredSavingLitersPerLap: Double
    var requiredSavingPercent: Double
    var estimatedTimeLossSeconds: Double?
    var isRealistic: Bool
    var message: String
}

private struct FuelPerLapSelection {
    var value: Double?
    var source: String
    var minimum: Double?
    var maximum: Double?
}

private struct MetricSelection {
    var value: Double?
    var source: String
}

private struct StintPlan {
    var stints: [FuelStintEstimate]
    var plannedRaceLaps: Int?
    var plannedStintCount: Int?
    var plannedStopCount: Int?
    var finalStintTargetLaps: Int?
    var requiredFuelSavingLitersPerLap: Double?
    var requiredFuelSavingPercent: Double?
    var stopOptimization: FuelStopOptimization?
    var rhythmComparison: FuelRhythmComparison?

    static let empty = StintPlan(
        stints: [],
        plannedRaceLaps: nil,
        plannedStintCount: nil,
        plannedStopCount: nil,
        finalStintTargetLaps: nil,
        requiredFuelSavingLitersPerLap: nil,
        requiredFuelSavingPercent: nil,
        stopOptimization: nil,
        rhythmComparison: nil
    )
}

private struct PitStrategyEstimate {
    var fuelFillRateLitersPerSecond: Double?
    var pitLaneSeconds: Double?
    var tireChangeServiceSeconds: Double?
    var noTireServiceSeconds: Double?
    var source: String

    static let empty = PitStrategyEstimate(
        fuelFillRateLitersPerSecond: nil,
        pitLaneSeconds: nil,
        tireChangeServiceSeconds: nil,
        noTireServiceSeconds: nil,
        source: "pit history unavailable"
    )
}

enum FuelStrategyCalculator {
    private static let realisticFuelSaveThresholdPercent = 0.05

    static func make(from snapshot: LiveTelemetrySnapshot, history: SessionHistoryLookupResult) -> FuelStrategySnapshot {
        let aggregate = history.preferredAggregate
        let aggregateSource = history.preferredAggregateSource
        let frame = snapshot.latestFrame
        let fuel = snapshot.fuel
        let currentFuel = validPositive(fuel.fuelLevelLiters)
        let maxFuelLiters = firstValidPositive(frame?.fuelMaxLiters, aggregate?.car?.driverCarFuelMaxLiters)
        let fuelPerLap = selectFuelPerLap(fuel, aggregate: aggregate, aggregateSource: aggregateSource)
        let lapTime = selectLapTime(fuel, aggregate: aggregate)
        let raceLapsRemaining = estimateRaceLapsRemaining(frame: frame, lapTimeSeconds: lapTime.value)
        let teammateStintTarget = selectTeammateStintTarget(
            aggregate: aggregate,
            aggregateSource: aggregateSource,
            maxFuelLiters: maxFuelLiters,
            fuelPerLapLiters: fuelPerLap.value
        )
        let completedStintCount = estimateCompletedStintCount(
            snapshot: snapshot,
            maxFuelLiters: maxFuelLiters,
            fuelPerLapLiters: fuelPerLap.value,
            preferredLongTargetLaps: teammateStintTarget.targetLaps
        )
        let pitStrategy = selectPitStrategy(aggregate: aggregate, aggregateSource: aggregateSource)
        let fuelToFinish = fuelPerLap.value.flatMap { perLap in
            raceLapsRemaining.map { perLap * $0 }
        }
        let additionalFuelNeeded = fuelToFinish.flatMap { finishFuel in
            currentFuel.map { max(0, finishFuel - $0) }
        }
        let stintPlan = buildStintPlan(
            currentFuelLiters: currentFuel,
            maxFuelLiters: maxFuelLiters,
            fuelPerLapLiters: fuelPerLap.value,
            fuelPerLapSource: fuelPerLap.source,
            raceLapsRemaining: raceLapsRemaining,
            teammateStintTargetLaps: teammateStintTarget.targetLaps,
            pitStrategy: pitStrategy,
            completedStintCount: completedStintCount
        )

        return FuelStrategySnapshot(
            hasData: currentFuel != nil || fuelPerLap.value != nil,
            status: status(
                currentFuel: currentFuel,
                fuelPerLap: fuelPerLap.value,
                raceLapsRemaining: raceLapsRemaining,
                additionalFuelNeeded: additionalFuelNeeded,
                stintPlan: stintPlan
            ),
            currentFuelLiters: currentFuel,
            fuelPerLapLiters: fuelPerLap.value,
            fuelPerLapSource: fuelPerLap.source,
            fuelPerLapMinimumLiters: fuelPerLap.minimum,
            fuelPerLapMaximumLiters: fuelPerLap.maximum,
            lapTimeSeconds: lapTime.value,
            lapTimeSource: lapTime.source,
            raceLapsRemaining: raceLapsRemaining,
            plannedRaceLaps: stintPlan.plannedRaceLaps,
            fullTankStintLaps: validPositive(maxFuelLiters).flatMap { maxFuel in
                fuelPerLap.value.map { maxFuel / $0 }
            },
            plannedStintCount: stintPlan.plannedStintCount,
            plannedStopCount: stintPlan.plannedStopCount,
            finalStintTargetLaps: stintPlan.finalStintTargetLaps,
            additionalFuelNeededLiters: additionalFuelNeeded,
            requiredFuelSavingLitersPerLap: stintPlan.requiredFuelSavingLitersPerLap,
            requiredFuelSavingPercent: stintPlan.requiredFuelSavingPercent,
            stopOptimization: stintPlan.stopOptimization,
            rhythmComparison: stintPlan.rhythmComparison,
            teammateStintTargetLaps: teammateStintTarget.targetLaps,
            teammateStintTargetSource: teammateStintTarget.source,
            tireModelSource: pitStrategy.source,
            fuelFillRateLitersPerSecond: pitStrategy.fuelFillRateLitersPerSecond,
            tireChangeServiceSeconds: pitStrategy.tireChangeServiceSeconds,
            stints: stintPlan.stints
        )
    }

    static func format(_ value: Double?, suffix: String, fallback: String = "--") -> String {
        guard let value, value.isFinite else {
            return fallback
        }

        return String(format: "%.1f%@", value, suffix)
    }

    private static func estimateRaceLapsRemaining(frame: MockLiveTelemetryFrame?, lapTimeSeconds: Double?) -> Double? {
        guard let frame else {
            return nil
        }

        if frame.sessionState >= 5 {
            return 0
        }

        guard let lapTimeSeconds, lapTimeSeconds > 0 else {
            return nil
        }

        let leaderProgress = Double(frame.leaderLapCompleted) + min(max(frame.leaderLapDistPct, 0), 1)
        let teamProgress = Double(frame.teamLapCompleted) + min(max(frame.teamLapDistPct, 0), 1)
        let finishLap = ceil(leaderProgress + frame.sessionTimeRemain / lapTimeSeconds)
        return max(0, finishLap - teamProgress)
    }

    private static func selectFuelPerLap(
        _ fuel: LiveFuelSnapshot,
        aggregate: HistoricalSessionAggregate?,
        aggregateSource: String?
    ) -> FuelPerLapSelection {
        let historicalRange = aggregate?.fuelPerLapLiters
        if let liveFuelPerLap = validPositive(fuel.fuelPerLapLiters) {
            return FuelPerLapSelection(
                value: liveFuelPerLap,
                source: "live burn",
                minimum: validPositive(historicalRange?.minimum),
                maximum: validPositive(historicalRange?.maximum)
            )
        }

        if let historicalFuelPerLap = validPositive(aggregate?.fuelPerLapLiters.mean) {
            return FuelPerLapSelection(
                value: historicalFuelPerLap,
                source: historySourceLabel(aggregateSource, metric: "history"),
                minimum: validPositive(historicalRange?.minimum),
                maximum: validPositive(historicalRange?.maximum)
            )
        }

        return FuelPerLapSelection(
            value: nil,
            source: "unavailable",
            minimum: nil,
            maximum: nil
        )
    }

    private static func selectLapTime(
        _ fuel: LiveFuelSnapshot,
        aggregate: HistoricalSessionAggregate?
    ) -> MetricSelection {
        if let liveLapTime = validLapTime(fuel.lapTimeSeconds) {
            return MetricSelection(value: liveLapTime, source: fuel.lapTimeSource)
        }

        if let historicalMedian = validLapTime(aggregate?.medianLapSeconds.mean) {
            return MetricSelection(value: historicalMedian, source: "history median")
        }

        if let historicalAverage = validLapTime(aggregate?.averageLapSeconds.mean) {
            return MetricSelection(value: historicalAverage, source: "history average")
        }

        if let historyEstimate = validLapTime(aggregate?.car?.driverCarEstLapTimeSeconds) {
            return MetricSelection(value: historyEstimate, source: "history estimate")
        }

        return MetricSelection(value: nil, source: "unavailable")
    }

    private static func selectTeammateStintTarget(
        aggregate: HistoricalSessionAggregate?,
        aggregateSource: String?,
        maxFuelLiters: Double?,
        fuelPerLapLiters: Double?
    ) -> (targetLaps: Int?, source: String?) {
        guard let teammateStintLaps = validPositive(aggregate?.teammateDriverStintLaps.mean) else {
            return (nil, nil)
        }

        let targetLaps = Int(teammateStintLaps.rounded(.toNearestOrAwayFromZero))
        guard targetLaps > 1, targetLaps <= 20 else {
            return (nil, nil)
        }

        if let maxFuelLiters,
           let fuelPerLapLiters,
           fuelPerLapLiters > 0,
           let requiredSaving = calculateRequiredSavingPerLap(
                targetLaps: targetLaps,
                fuelPerLapLiters: fuelPerLapLiters,
                availableFuelLiters: maxFuelLiters
           ),
           requiredSaving / fuelPerLapLiters > realisticFuelSaveThresholdPercent {
            return (nil, nil)
        }

        return (targetLaps, historySourceLabel(aggregateSource, metric: "teammate stints"))
    }

    private static func selectPitStrategy(
        aggregate: HistoricalSessionAggregate?,
        aggregateSource: String?
    ) -> PitStrategyEstimate {
        guard let aggregate else {
            return .empty
        }

        return PitStrategyEstimate(
            fuelFillRateLitersPerSecond: validPositive(aggregate.observedFuelFillRateLitersPerSecond.mean),
            pitLaneSeconds: validPositive(aggregate.averagePitLaneSeconds.mean),
            tireChangeServiceSeconds: firstValidPositive(
                aggregate.averageTireChangePitServiceSeconds.mean,
                aggregate.averagePitServiceSeconds.mean
            ),
            noTireServiceSeconds: validPositive(aggregate.averageNoTirePitServiceSeconds.mean),
            source: historySourceLabel(aggregateSource, metric: "pit history")
        )
    }

    private static func buildStintPlan(
        currentFuelLiters: Double?,
        maxFuelLiters: Double?,
        fuelPerLapLiters: Double?,
        fuelPerLapSource: String,
        raceLapsRemaining: Double?,
        teammateStintTargetLaps: Int?,
        pitStrategy: PitStrategyEstimate,
        completedStintCount: Int
    ) -> StintPlan {
        guard let fuelPerLapLiters, fuelPerLapLiters > 0 else {
            return .empty
        }

        var stints: [FuelStintEstimate] = []
        let currentStintLaps = currentFuelLiters.map { $0 / fuelPerLapLiters }
        let fullTankStintLaps = maxFuelLiters.map { $0 / fuelPerLapLiters }

        if let raceLapsRemaining, raceLapsRemaining > 0 {
            let plannedRaceLaps = Int(ceil(raceLapsRemaining))
            let firstProjectedStintIsTeammate = currentFuelLiters == nil
            if let plannedStintCount = calculatePlannedStintCount(
                plannedRaceLaps: plannedRaceLaps,
                currentStintLaps: currentStintLaps,
                fullTankStintLaps: fullTankStintLaps
            ), plannedStintCount > 0 {
                let targets = distributeWholeLapTargets(
                    plannedRaceLaps: plannedRaceLaps,
                    plannedStintCount: plannedStintCount,
                    teammateStintTargetLaps: teammateStintTargetLaps,
                    firstProjectedStintIsTeammate: firstProjectedStintIsTeammate
                )
                var requiredSavings: [Double] = []
                var displayedLapsConsumed = 0.0

                for (index, targetLaps) in targets.enumerated() {
                    let availableFuel = index == 0 && currentFuelLiters != nil ? currentFuelLiters : maxFuelLiters
                    let requiredSaving = calculateRequiredSavingPerLap(
                        targetLaps: targetLaps,
                        fuelPerLapLiters: fuelPerLapLiters,
                        availableFuelLiters: availableFuel
                    )
                    if let requiredSaving, requiredSaving > 0 {
                        requiredSavings.append(requiredSaving)
                    }

                    let remainingForDisplay = max(0, raceLapsRemaining - displayedLapsConsumed)
                    let displayedLength = min(Double(targetLaps), remainingForDisplay)
                    displayedLapsConsumed += Double(targetLaps)
                    stints.append(FuelStintEstimate(
                        number: completedStintCount + index + 1,
                        lengthLaps: displayedLength,
                        source: targets.count == 1 ? "finish" : (index == targets.count - 1 ? "final" : "target"),
                        targetLaps: targetLaps,
                        targetFuelPerLapLiters: availableFuel.map { $0 / Double(targetLaps) },
                        currentFuelPerLapLiters: fuelPerLapLiters,
                        currentFuelPerLapSource: fuelPerLapSource,
                        requiredFuelSavingLitersPerLap: requiredSaving,
                        requiredFuelSavingPercent: requiredSaving.map { $0 / fuelPerLapLiters },
                        tireAdvice: buildTireAdvice(
                            index: index,
                            stintCount: targets.count,
                            targetLaps: targetLaps,
                            availableFuelLiters: availableFuel,
                            maxFuelLiters: maxFuelLiters,
                            fuelPerLapLiters: fuelPerLapLiters,
                            pitStrategy: pitStrategy
                        )
                    ))
                }

                let requiredSaving = requiredSavings.max()
                return StintPlan(
                    stints: stints,
                    plannedRaceLaps: plannedRaceLaps,
                    plannedStintCount: plannedStintCount,
                    plannedStopCount: max(0, plannedStintCount - 1),
                    finalStintTargetLaps: targets.last,
                    requiredFuelSavingLitersPerLap: requiredSaving,
                    requiredFuelSavingPercent: requiredSaving.map { $0 / fuelPerLapLiters },
                    stopOptimization: buildStopOptimization(
                        plannedRaceLaps: plannedRaceLaps,
                        plannedStintCount: plannedStintCount,
                        currentFuelLiters: currentFuelLiters,
                        maxFuelLiters: maxFuelLiters,
                        fuelPerLapLiters: fuelPerLapLiters,
                        pitStrategy: pitStrategy
                    ),
                    rhythmComparison: buildRhythmComparison(
                        plannedRaceLaps: plannedRaceLaps,
                        fuelPerLapLiters: fuelPerLapLiters,
                        maxFuelLiters: maxFuelLiters,
                        preferredLongTargetLaps: teammateStintTargetLaps,
                        pitStrategy: pitStrategy
                    )
                )
            }
        }

        if let currentStintLaps {
            let stintLength = raceLapsRemaining.map { min(currentStintLaps, $0) } ?? currentStintLaps
            stints.append(FuelStintEstimate(
                number: completedStintCount + 1,
                lengthLaps: max(0, stintLength),
                source: "current fuel",
                targetFuelPerLapLiters: fuelPerLapLiters,
                currentFuelPerLapLiters: fuelPerLapLiters,
                currentFuelPerLapSource: fuelPerLapSource,
                tireAdvice: .noStop
            ))
        }

        var remainingAfterCurrent = raceLapsRemaining.flatMap { raceLaps in
            currentStintLaps.map { max(0, raceLaps - $0) }
        }
        var stintNumber = completedStintCount + stints.count + 1

        while stints.count < 5, let remaining = remainingAfterCurrent, remaining > 0, let fullTankStintLaps, fullTankStintLaps > 0 {
            let stintLength = min(fullTankStintLaps, remaining)
            stints.append(FuelStintEstimate(
                number: stintNumber,
                lengthLaps: stintLength,
                source: "full tank",
                targetFuelPerLapLiters: fuelPerLapLiters,
                currentFuelPerLapLiters: fuelPerLapLiters,
                currentFuelPerLapSource: fuelPerLapSource,
                tireAdvice: .pending
            ))
            stintNumber += 1
            remainingAfterCurrent = remaining - stintLength
        }

        if stints.isEmpty, let fullTankStintLaps {
            stints.append(FuelStintEstimate(
                number: completedStintCount + 1,
                lengthLaps: fullTankStintLaps,
                source: "full tank",
                targetFuelPerLapLiters: fuelPerLapLiters,
                currentFuelPerLapLiters: fuelPerLapLiters,
                currentFuelPerLapSource: fuelPerLapSource,
                tireAdvice: .pending
            ))
        }

        var plan = StintPlan.empty
        plan.stints = stints
        return plan
    }

    private static func status(
        currentFuel: Double?,
        fuelPerLap: Double?,
        raceLapsRemaining: Double?,
        additionalFuelNeeded: Double?,
        stintPlan: StintPlan
    ) -> String {
        guard currentFuel != nil else {
            if fuelPerLap == nil || raceLapsRemaining == nil || stintPlan.plannedStintCount == nil {
                return "waiting for fuel"
            }
            return "model: \(stintPlan.plannedStintCount ?? 0) stints / \(stintPlan.plannedStopCount ?? 0) \((stintPlan.plannedStopCount ?? 0) == 1 ? "stop" : "stops")"
        }
        guard fuelPerLap != nil else {
            return "waiting for burn"
        }
        guard raceLapsRemaining != nil else {
            return "stint estimate"
        }

        if let comparison = stintPlan.rhythmComparison, comparison.isRealistic, comparison.additionalStopCount > 0 {
            return comparison.message
        }

        if let optimization = stintPlan.stopOptimization, optimization.isRealistic {
            return optimization.message
        }

        if let saving = stintPlan.requiredFuelSavingLitersPerLap,
           let savingPercent = stintPlan.requiredFuelSavingPercent,
           saving > 0.01,
           savingPercent <= realisticFuelSaveThresholdPercent {
            let target = stintPlan.stints.compactMap { stint in
                (stint.requiredFuelSavingLitersPerLap ?? 0) > 0 ? stint.targetLaps : nil
            }.max() ?? 0
            return String(format: "%d-lap target: save %.1f L/lap", target, saving)
        }

        if let stintCount = stintPlan.plannedStintCount, let stopCount = stintPlan.plannedStopCount {
            return stintCount <= 1 ? "fuel covers finish" : "\(stintCount) stints / \(stopCount) \(stopCount == 1 ? "stop" : "stops")"
        }

        if let additionalFuelNeeded, additionalFuelNeeded > 0.1 {
            return String(format: "+%.1f L needed", additionalFuelNeeded)
        }
        return "fuel covers finish"
    }

    private static func calculatePlannedStintCount(
        plannedRaceLaps: Int,
        currentStintLaps: Double?,
        fullTankStintLaps: Double?
    ) -> Int? {
        guard plannedRaceLaps > 0 else {
            return 0
        }

        if let currentStintLaps, currentStintLaps > 0 {
            let remainingAfterCurrent = Double(plannedRaceLaps) - currentStintLaps
            if remainingAfterCurrent <= 0 {
                return 1
            }

            guard let fullTankStintLaps, fullTankStintLaps > 0 else {
                return 1
            }

            return 1 + Int(ceil(remainingAfterCurrent / fullTankStintLaps))
        }

        guard let fullTankStintLaps, fullTankStintLaps > 0 else {
            return nil
        }

        return Int(ceil(Double(plannedRaceLaps) / fullTankStintLaps))
    }

    private static func distributeWholeLapTargets(
        plannedRaceLaps: Int,
        plannedStintCount: Int,
        teammateStintTargetLaps: Int?,
        firstProjectedStintIsTeammate: Bool
    ) -> [Int] {
        guard plannedRaceLaps > 0, plannedStintCount > 0 else {
            return []
        }

        let baseLaps = plannedRaceLaps / plannedStintCount
        let extraLaps = plannedRaceLaps % plannedStintCount
        var targets = (0..<plannedStintCount).map { index in
            baseLaps + (index < extraLaps ? 1 : 0)
        }
        applyTeammateStintTarget(
            targets: &targets,
            teammateStintTargetLaps: teammateStintTargetLaps,
            firstProjectedStintIsTeammate: firstProjectedStintIsTeammate
        )
        return targets
    }

    private static func applyTeammateStintTarget(
        targets: inout [Int],
        teammateStintTargetLaps: Int?,
        firstProjectedStintIsTeammate: Bool
    ) {
        guard let targetLaps = teammateStintTargetLaps, targetLaps > 1, targets.count > 1 else {
            return
        }

        let donorFloor = max(1, targetLaps - 1)
        for index in targets.indices {
            guard isProjectedTeammateStint(index: index, firstProjectedStintIsTeammate: firstProjectedStintIsTeammate) else {
                continue
            }

            while targets[index] < targetLaps {
                guard let donorIndex = selectTeammateTargetDonor(
                    targets: targets,
                    donorFloor: donorFloor,
                    recipientIndex: index,
                    firstProjectedStintIsTeammate: firstProjectedStintIsTeammate
                ) else {
                    break
                }

                targets[donorIndex] -= 1
                targets[index] += 1
            }
        }
    }

    private static func selectTeammateTargetDonor(
        targets: [Int],
        donorFloor: Int,
        recipientIndex: Int,
        firstProjectedStintIsTeammate: Bool
    ) -> Int? {
        targets.indices
            .filter { $0 != recipientIndex }
            .filter { !isProjectedTeammateStint(index: $0, firstProjectedStintIsTeammate: firstProjectedStintIsTeammate) }
            .filter { targets[$0] > donorFloor }
            .sorted {
                if targets[$0] != targets[$1] {
                    return targets[$0] > targets[$1]
                }

                return abs($0 - recipientIndex) < abs($1 - recipientIndex)
            }
            .first
    }

    private static func isProjectedTeammateStint(index: Int, firstProjectedStintIsTeammate: Bool) -> Bool {
        firstProjectedStintIsTeammate ? index.isMultiple(of: 2) : !index.isMultiple(of: 2)
    }

    private static func calculateRequiredSavingPerLap(
        targetLaps: Int,
        fuelPerLapLiters: Double,
        availableFuelLiters: Double?
    ) -> Double? {
        guard targetLaps > 0, let availableFuelLiters, availableFuelLiters > 0 else {
            return nil
        }

        let extraFuelRequired = Double(targetLaps) * fuelPerLapLiters - availableFuelLiters
        return extraFuelRequired > 0 ? extraFuelRequired / Double(targetLaps) : nil
    }

    private static func buildTireAdvice(
        index: Int,
        stintCount: Int,
        targetLaps: Int,
        availableFuelLiters: Double?,
        maxFuelLiters: Double?,
        fuelPerLapLiters: Double,
        pitStrategy: PitStrategyEstimate
    ) -> TireChangeAdvice {
        guard stintCount > 1, index < stintCount - 1 else {
            return .noStop
        }

        guard let maxFuelLiters, maxFuelLiters > 0, let availableFuelLiters, availableFuelLiters > 0 else {
            return .pending
        }

        let fuelAtStop = max(0, availableFuelLiters - Double(targetLaps) * fuelPerLapLiters)
        let fuelToAdd = max(0, maxFuelLiters - fuelAtStop)
        let refuelSeconds = pitStrategy.fuelFillRateLitersPerSecond.flatMap { fillRate in
            fillRate > 0 ? fuelToAdd / fillRate : nil
        }

        guard let tireServiceSeconds = pitStrategy.tireChangeServiceSeconds, tireServiceSeconds > 0 else {
            var pending = TireChangeAdvice.pending
            pending.fuelToAddLiters = fuelToAdd
            pending.refuelSeconds = refuelSeconds
            return pending
        }

        guard let refuelSeconds else {
            return TireChangeAdvice(
                text: String(format: "tires ~%.0fs", tireServiceSeconds),
                fuelToAddLiters: fuelToAdd,
                tireServiceSeconds: tireServiceSeconds
            )
        }

        let noTireServiceSeconds = pitStrategy.noTireServiceSeconds ?? 0
        let noTireStopSeconds = max(refuelSeconds, noTireServiceSeconds)
        let tireStopSeconds = max(refuelSeconds, tireServiceSeconds)
        let timeLoss = max(0, tireStopSeconds - noTireStopSeconds)
        let text = timeLoss <= 1
            ? String(format: "tires free (%.0f L)", fuelToAdd)
            : String(format: "tires +%.0fs", timeLoss)

        return TireChangeAdvice(
            text: text,
            fuelToAddLiters: fuelToAdd,
            refuelSeconds: refuelSeconds,
            tireServiceSeconds: tireServiceSeconds,
            timeLossSeconds: timeLoss
        )
    }

    private static func buildStopOptimization(
        plannedRaceLaps: Int,
        plannedStintCount: Int,
        currentFuelLiters: Double?,
        maxFuelLiters: Double?,
        fuelPerLapLiters: Double,
        pitStrategy: PitStrategyEstimate
    ) -> FuelStopOptimization? {
        guard plannedRaceLaps > 0,
              plannedStintCount > 1,
              let maxFuelLiters,
              maxFuelLiters > 0 else {
            return nil
        }

        let candidateStintCount = plannedStintCount - 1
        let savedStopCount = 1
        let estimatedPitStopSeconds = firstValidPositive(
            pitStrategy.pitLaneSeconds,
            pitStrategy.tireChangeServiceSeconds,
            pitStrategy.noTireServiceSeconds
        )
        let estimatedSavedSeconds = estimatedPitStopSeconds.map { $0 * Double(savedStopCount) }
        let breakEvenLossSecondsPerLap = estimatedSavedSeconds.map { $0 / Double(plannedRaceLaps) }
        let availableFuelLiters: Double
        if let currentFuelLiters {
            availableFuelLiters = currentFuelLiters + Double(max(0, candidateStintCount - 1)) * maxFuelLiters
        } else {
            availableFuelLiters = Double(candidateStintCount) * maxFuelLiters
        }

        guard availableFuelLiters > 0 else {
            return nil
        }

        let requiredFuelPerLap = availableFuelLiters / Double(plannedRaceLaps)
        let requiredSaving = fuelPerLapLiters - requiredFuelPerLap
        if requiredSaving <= 0 {
            return FuelStopOptimization(
                isRealistic: true,
                candidateStintCount: candidateStintCount,
                savedStopCount: savedStopCount,
                requiredFuelPerLapLiters: requiredFuelPerLap,
                requiredSavingLitersPerLap: 0,
                requiredSavingPercent: 0,
                estimatedSavedSeconds: estimatedSavedSeconds,
                breakEvenLossSecondsPerLap: breakEvenLossSecondsPerLap,
                message: stopOptimizationMessage(requiredSavingLitersPerLap: 0, savedStopCount: savedStopCount, estimatedSavedSeconds: estimatedSavedSeconds)
            )
        }

        let requiredSavingPercent = requiredSaving / fuelPerLapLiters
        return FuelStopOptimization(
            isRealistic: requiredSavingPercent <= realisticFuelSaveThresholdPercent,
            candidateStintCount: candidateStintCount,
            savedStopCount: savedStopCount,
            requiredFuelPerLapLiters: requiredFuelPerLap,
            requiredSavingLitersPerLap: requiredSaving,
            requiredSavingPercent: requiredSavingPercent,
            estimatedSavedSeconds: estimatedSavedSeconds,
            breakEvenLossSecondsPerLap: breakEvenLossSecondsPerLap,
            message: stopOptimizationMessage(requiredSavingLitersPerLap: requiredSaving, savedStopCount: savedStopCount, estimatedSavedSeconds: estimatedSavedSeconds)
        )
    }

    private static func stopOptimizationMessage(
        requiredSavingLitersPerLap: Double,
        savedStopCount: Int,
        estimatedSavedSeconds: Double?
    ) -> String {
        let stopText = savedStopCount == 1 ? "stop" : "stops"
        let timeText = estimatedSavedSeconds.map { String(format: ", saves ~%.0fs", $0) } ?? ""
        if requiredSavingLitersPerLap <= 0.01 {
            return "skip \(savedStopCount) \(stopText)\(timeText)"
        }

        return String(format: "skip %d %@: save %.1f L/lap%@", savedStopCount, stopText, requiredSavingLitersPerLap, timeText)
    }

    private static func buildRhythmComparison(
        plannedRaceLaps: Int,
        fuelPerLapLiters: Double,
        maxFuelLiters: Double?,
        preferredLongTargetLaps: Int?,
        pitStrategy: PitStrategyEstimate
    ) -> FuelRhythmComparison? {
        guard plannedRaceLaps > 0,
              fuelPerLapLiters > 0,
              let maxFuelLiters,
              maxFuelLiters > 0 else {
            return nil
        }

        let longTarget = longestRealisticStintTarget(
            maxFuelLiters: maxFuelLiters,
            fuelPerLapLiters: fuelPerLapLiters,
            preferredLongTargetLaps: preferredLongTargetLaps
        )
        let longTargetLaps = longTarget.targetLaps
        let requiredSaving = longTarget.requiredSavingLitersPerLap
        let requiredSavingPercent = longTarget.requiredSavingPercent

        let shortTargetLaps = longTargetLaps - 1
        guard shortTargetLaps > 0 else {
            return nil
        }

        let shortStintCount = Int(ceil(Double(plannedRaceLaps) / Double(shortTargetLaps)))
        let longStintCount = Int(ceil(Double(plannedRaceLaps) / Double(longTargetLaps)))
        guard longStintCount > 1 else {
            return nil
        }

        let shortStopCount = max(0, shortStintCount - 1)
        let longStopCount = max(0, longStintCount - 1)
        let additionalStopCount = shortStopCount - longStopCount
        guard additionalStopCount > 0 else {
            return nil
        }

        let estimatedPitStopSeconds = firstValidPositive(
            pitStrategy.pitLaneSeconds,
            pitStrategy.tireChangeServiceSeconds,
            pitStrategy.noTireServiceSeconds
        )
        let estimatedTimeLossSeconds = estimatedPitStopSeconds.map { $0 * Double(additionalStopCount) }
        let timeText = estimatedTimeLossSeconds.map { String(format: " (~%.0fs)", $0) } ?? ""
        let message = "\(longTargetLaps)-lap rhythm avoids +\(additionalStopCount) \(additionalStopCount == 1 ? "stop" : "stops")\(timeText)"

        return FuelRhythmComparison(
            shortTargetLaps: shortTargetLaps,
            longTargetLaps: longTargetLaps,
            shortStintCount: shortStintCount,
            longStintCount: longStintCount,
            shortStopCount: shortStopCount,
            longStopCount: longStopCount,
            additionalStopCount: additionalStopCount,
            requiredFuelPerLapLiters: maxFuelLiters / Double(longTargetLaps),
            requiredSavingLitersPerLap: requiredSaving,
            requiredSavingPercent: requiredSavingPercent,
            estimatedTimeLossSeconds: estimatedTimeLossSeconds,
            isRealistic: requiredSavingPercent <= realisticFuelSaveThresholdPercent,
            message: message
        )
    }

    private static func firstValidPositive(_ values: Double?...) -> Double? {
        values.lazy.compactMap { validPositive($0) }.first
    }

    private static func historySourceLabel(_ aggregateSource: String?, metric: String) -> String {
        aggregateSource == "baseline" ? "baseline \(metric)" : "user \(metric)"
    }

    private static func estimateCompletedStintCount(
        snapshot: LiveTelemetrySnapshot,
        maxFuelLiters: Double?,
        fuelPerLapLiters: Double?,
        preferredLongTargetLaps: Int?
    ) -> Int {
        if snapshot.completedStintCount > 0 {
            return snapshot.completedStintCount
        }

        guard let frame = snapshot.latestFrame,
              let fuelPerLapLiters,
              fuelPerLapLiters > 0 else {
            return 0
        }

        let progress = Double(frame.teamLapCompleted) + min(max(frame.teamLapDistPct, 0), 1)
        guard progress > 0 else {
            return 0
        }

        let target = longestRealisticStintTarget(
            maxFuelLiters: maxFuelLiters,
            fuelPerLapLiters: fuelPerLapLiters,
            preferredLongTargetLaps: preferredLongTargetLaps
        ).targetLaps
        return max(0, Int(floor(progress / Double(target))))
    }

    private static func longestRealisticStintTarget(
        maxFuelLiters: Double?,
        fuelPerLapLiters: Double,
        preferredLongTargetLaps: Int?
    ) -> (targetLaps: Int, requiredSavingLitersPerLap: Double, requiredSavingPercent: Double) {
        guard let maxFuelLiters, maxFuelLiters > 0, fuelPerLapLiters > 0 else {
            return (1, 0, 0)
        }

        let naturalLongTarget = Int(ceil(maxFuelLiters / fuelPerLapLiters))
        var targetLaps = max(2, preferredLongTargetLaps ?? naturalLongTarget)
        var requiredSaving = calculateRequiredSavingPerLap(
            targetLaps: targetLaps,
            fuelPerLapLiters: fuelPerLapLiters,
            availableFuelLiters: maxFuelLiters
        ) ?? 0
        var requiredSavingPercent = requiredSaving / fuelPerLapLiters
        if requiredSavingPercent > realisticFuelSaveThresholdPercent {
            targetLaps = max(2, Int(floor(maxFuelLiters / fuelPerLapLiters)))
            requiredSaving = calculateRequiredSavingPerLap(
                targetLaps: targetLaps,
                fuelPerLapLiters: fuelPerLapLiters,
                availableFuelLiters: maxFuelLiters
            ) ?? 0
            requiredSavingPercent = requiredSaving / fuelPerLapLiters
        }

        return (targetLaps, requiredSaving, requiredSavingPercent)
    }

    private static func validPositive(_ value: Double?) -> Double? {
        guard let value, value.isFinite, value > 0 else {
            return nil
        }

        return value
    }

    private static func validLapTime(_ value: Double?) -> Double? {
        guard let value, value.isFinite, value > 20, value < 1800 else {
            return nil
        }

        return value
    }

}
