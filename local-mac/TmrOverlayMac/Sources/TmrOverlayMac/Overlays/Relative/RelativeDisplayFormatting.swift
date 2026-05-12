import Foundation

enum RelativeDisplayDirection {
    case ahead
    case behind
}

enum RelativeDisplayFormatting {
    static func gap(seconds: Double?, meters: Double?, laps: Double, direction: RelativeDisplayDirection) -> String {
        let sign = direction == .ahead ? "-" : "+"
        if let seconds, seconds.isFinite {
            return String(format: "%@%.3f", sign, abs(seconds))
        }

        if let meters, meters.isFinite {
            return String(format: "%@%.0fm", sign, abs(meters))
        }

        return "--"
    }
}
