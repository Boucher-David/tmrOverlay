import Foundation

enum StandingsDisplayFormatting {
    static func gap(isClassLeader: Bool, seconds: Double?, laps: Double? = nil) -> String {
        if isClassLeader {
            return "Leader"
        }

        if let seconds, seconds.isFinite {
            return String(format: "+%.1f", seconds)
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
}
