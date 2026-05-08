import AppKit

enum DesignV2EvidenceKind {
    case live
    case measured
    case modeled
    case history
    case partial
    case stale
    case unavailable
    case error

    var color: NSColor {
        DesignV2Theme.color(for: self)
    }
}

struct DesignV2Badge {
    var title: String
    var evidence: DesignV2EvidenceKind
}

struct DesignV2Metric {
    var title: String
    var value: String
    var detail: String
    var evidence: DesignV2EvidenceKind
}

struct DesignV2SourceRow {
    var label: String
    var value: String
    var detail: String
    var evidence: DesignV2EvidenceKind
}

struct DesignV2Table {
    var columns: [String]
    var rows: [[String]]
    var highlightedRowIndex: Int?
}

struct DesignV2LineGraph {
    var title: String
    var valueLabel: String
    var unitLabel: String
    var points: [Double]
    var referenceValue: Double?
    var minValue: Double?
    var maxValue: Double?
}

struct DesignV2PreviewScenario {
    enum BodyMode {
        case standingsTable
        case relativeTable
        case sectorComparison
        case blindspotSignal
        case lapDelta
        case stintLapGraph
        case flagStrip
        case sourceTable
        case fuelMatrix
        case gapGraph
        case unavailable
    }

    var title: String
    var subtitle: String
    var badges: [DesignV2Badge]
    var metrics: [DesignV2Metric]
    var rows: [DesignV2SourceRow]
    var footer: String
    var mode: BodyMode
    var table: DesignV2Table? = nil
    var graph: DesignV2LineGraph? = nil
}
