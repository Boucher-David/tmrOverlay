import Foundation

enum FourHourRacePreview {
    static let sessionLengthSeconds = 14_400.0
    static let fuelMaxLiters = 106.0
    static let fuelPerLapLiters = 13.36344
    static let medianLapSeconds = 482.092804
    static let averageLapSeconds = 482.194874
    static let leaderPreviewLapSeconds = 482.092804
    static let fuelUsePerHourLiters = fuelPerLapLiters * 3600.0 / medianLapSeconds
    static let mockSpeedMultiplier = 4.0
    static let teamCarIdx = 15
    static let classLeaderCarIdx = 2
    static let teamClassColorHex = "#FFDA59"
    static let dafyddToSimonHandoffSeconds = 3_642.583
    static let mockStartRaceSeconds = 0.0
    static let firstPitEntrySeconds = 3_626.150
    static let firstPitExitSeconds = 3_690.017
    static let firstPitLaneLossSeconds = 63.867

    static func classFieldSpanSeconds(sessionTime: TimeInterval) -> Double {
        let progress = min(max(sessionTime / sessionLengthSeconds, 0), 1)
        let startSpan = 11.0
        let endSpan = medianLapSeconds * 1.35
        return startSpan + pow(progress, 1.35) * (endSpan - startSpan)
    }

    static func classGapSeconds(classPosition: Int, sessionTime: TimeInterval) -> Double {
        guard classPosition > 1 else {
            return 0
        }

        let spacing = classFieldSpanSeconds(sessionTime: sessionTime) / 11.0
        let base = Double(classPosition - 1) * spacing
        let wave = sin(sessionTime / 210.0 + Double(classPosition) * 0.65) * min(4.0, spacing * 0.08)
        return max(0, base + wave)
    }

    static func teamClassGapSeconds(sessionTime: TimeInterval) -> Double {
        classGapSeconds(classPosition: 6, sessionTime: sessionTime) + firstPitLossSeconds(sessionTime: sessionTime)
    }

    static func teamClassPosition(sessionTime: TimeInterval) -> Int {
        let teamGap = teamClassGapSeconds(sessionTime: sessionTime)
        var carsAhead = 0
        for classPosition in 1...18 where classPosition != 6 {
            if classGapSeconds(classPosition: classPosition, sessionTime: sessionTime) < teamGap {
                carsAhead += 1
            }
        }

        return max(1, carsAhead + 1)
    }

    static func firstPitLossSeconds(sessionTime: TimeInterval) -> Double {
        guard sessionTime >= firstPitEntrySeconds else {
            return 0
        }

        if sessionTime <= firstPitExitSeconds {
            let progress = (sessionTime - firstPitEntrySeconds) / (firstPitExitSeconds - firstPitEntrySeconds)
            return max(0, min(1, progress)) * firstPitLaneLossSeconds
        }

        return firstPitLaneLossSeconds
    }

    static func fuelLevelLiters(sessionTime: TimeInterval) -> Double {
        if sessionTime < firstPitEntrySeconds {
            let lapsUsed = max(0, sessionTime / averageLapSeconds)
            return max(0, fuelMaxLiters - lapsUsed * fuelPerLapLiters)
        }

        if sessionTime <= firstPitExitSeconds {
            let entryFuel = max(0, fuelMaxLiters - (firstPitEntrySeconds / averageLapSeconds) * fuelPerLapLiters)
            let progress = (sessionTime - firstPitEntrySeconds) / (firstPitExitSeconds - firstPitEntrySeconds)
            return entryFuel + max(0, min(1, progress)) * (fuelMaxLiters - entryFuel)
        }

        let lapsUsed = max(0, (sessionTime - firstPitExitSeconds) / averageLapSeconds)
        return max(0, fuelMaxLiters - lapsUsed * fuelPerLapLiters)
    }

    static func trackWetness(sessionTime: TimeInterval) -> Int {
        switch sessionTime {
        case ..<3_500:
            return 0
        case ..<3_720:
            return 2
        case ..<4_020:
            return 4
        default:
            return 1
        }
    }

    static func weatherDeclaredWet(sessionTime: TimeInterval) -> Bool {
        sessionTime >= 3_820 && sessionTime < 4_020
    }

    static func teamDriver(sessionTime: TimeInterval) -> MockTeamDriver {
        sessionTime < dafyddToSimonHandoffSeconds
            ? MockTeamDriver(key: "dafydd", name: "Dafydd", initials: "Daf", driversSoFar: 1)
            : MockTeamDriver(key: "simon", name: "Simon", initials: "Sim", driversSoFar: 2)
    }
}

struct MockTeamDriver {
    let key: String
    let name: String
    let initials: String
    let driversSoFar: Int
}
