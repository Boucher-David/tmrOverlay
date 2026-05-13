import Foundation

enum StandingsDisplayFormatting {
    static func gap(isClassLeader: Bool, seconds: Double?, laps: Double? = nil, lapCompleted: Int? = nil, lapDistPct: Double? = nil) -> String {
        if isClassLeader {
            return leaderProgress(lapCompleted: lapCompleted, lapDistPct: lapDistPct) ?? "Leader"
        }

        if let seconds, seconds.isFinite {
            return String(format: "+%.1f", seconds)
        }

        if let laps, laps.isFinite, laps >= 0.9999 {
            return abs(laps - laps.rounded()) <= 0.0001
                ? String(format: "+%.0fL", laps)
                : String(format: "+%.3fL", laps)
        }

        return "--"
    }

    static func interval(_ delta: Double?, referenceGap: Double, isReference: Bool) -> String {
        if isReference {
            return "0.0"
        }

        guard let delta, delta.isFinite else {
            return "--"
        }

        return delta > 0 ? String(format: "+%.1f", delta) : String(format: "%.1f", delta)
    }

    private static func leaderProgress(lapCompleted: Int?, lapDistPct: Double?) -> String? {
        guard let lapCompleted, lapCompleted >= 0 else {
            return nil
        }

        let isInLap = lapDistPct.map { $0 > 0 && $0 < 1 } ?? false
        let displayLap = lapCompleted + (isInLap ? 1 : 0)
        return displayLap > 0 ? "Lap \(displayLap)" : nil
    }
}
