import AppKit

enum DesignV2OverlayMockKind: CaseIterable {
    case standings
    case gapToLeader
    case fuelCalculator
    case pitService
    case sessionWeather
    case streamChat
    case garageCover
    case carRadar
    case inputState
    case trackMap

    var sourceDefinition: OverlayDefinition {
        switch self {
        case .standings:
            return StandingsOverlayDefinition.definition
        case .gapToLeader:
            return GapToLeaderOverlayDefinition.definition
        case .fuelCalculator:
            return FuelCalculatorOverlayDefinition.definition
        case .pitService:
            return PitServiceOverlayDefinition.definition
        case .sessionWeather:
            return SessionWeatherOverlayDefinition.definition
        case .streamChat:
            return StreamChatOverlayDefinition.definition
        case .garageCover:
            return GarageCoverOverlayDefinition.definition
        case .carRadar:
            return CarRadarOverlayDefinition.definition
        case .inputState:
            return InputStateOverlayDefinition.definition
        case .trackMap:
            return TrackMapOverlayDefinition.definition
        }
    }

    var demoId: String {
        "\(sourceDefinition.id)-design-v2"
    }

    var title: String {
        switch self {
        case .standings:
            return "Standings"
        case .gapToLeader:
            return "Gap To Leader"
        case .fuelCalculator:
            return "Fuel Calculator"
        case .pitService:
            return "Pit Service"
        case .sessionWeather:
            return "Session / Weather"
        case .streamChat:
            return "Stream Chat"
        case .garageCover:
            return "Garage Cover"
        case .carRadar:
            return "Car Radar"
        case .inputState:
            return "Inputs"
        case .trackMap:
            return "Track Map"
        }
    }

    var defaultSize: NSSize {
        switch self {
        case .standings:
            return NSSize(width: 650, height: 360)
        case .gapToLeader:
            return NSSize(width: 610, height: 300)
        case .fuelCalculator:
            return NSSize(width: 620, height: 340)
        case .pitService:
            return NSSize(width: 430, height: 280)
        case .sessionWeather:
            return NSSize(width: 450, height: 300)
        case .streamChat:
            return NSSize(width: 390, height: 520)
        case .garageCover:
            return NSSize(width: 960, height: 540)
        case .carRadar:
            return NSSize(width: 300, height: 300)
        case .inputState:
            return NSSize(width: 520, height: 220)
        case .trackMap:
            return TrackMapOverlayDefinition.definition.defaultSize
        }
    }

    init?(reviewAlias: String) {
        let normalized = reviewAlias
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
            .replacingOccurrences(of: "_", with: "-")
        switch normalized {
        case "standings", "standing":
            self = .standings
        case "gap", "gtl", "gap-to-leader", "gaptoleader":
            self = .gapToLeader
        case "fuel", "fuel-calculator", "fuelcalculator":
            self = .fuelCalculator
        case "pit", "pit-service", "pitservice":
            self = .pitService
        case "session", "weather", "session-weather", "sessionweather":
            self = .sessionWeather
        case "chat", "stream-chat", "streamchat":
            self = .streamChat
        case "garage", "garage-cover", "garagecover":
            self = .garageCover
        case "radar", "car-radar", "carradar":
            self = .carRadar
        case "inputs", "input", "input-state", "inputstate":
            self = .inputState
        case "track", "track-map", "trackmap", "map":
            self = .trackMap
        default:
            return nil
        }
    }
}

enum DesignV2OverlayBody {
    case table(columns: [DesignV2OverlayColumn], rows: [DesignV2OverlayRow])
    case metricRows([DesignV2OverlayMetricRow])
    case graph(points: [Double])
    case chat([DesignV2ChatRow])
    case garageCover(DesignV2GarageCoverModel)
    case radar(DesignV2RadarModel)
    case inputs(DesignV2InputModel)
    case trackMap(DesignV2TrackMapModel)
}

struct DesignV2OverlayColumn {
    var label: String
    var width: CGFloat
    var alignment: NSTextAlignment
}

struct DesignV2OverlayRow {
    var values: [String]
    var evidence: DesignV2EvidenceKind
    var isReference: Bool = false
    var isClassHeader: Bool = false
    var classColorHex: String? = nil
    var classHeaderTitle: String = ""
    var classHeaderDetail: String = ""
}

struct DesignV2OverlayMetricRow {
    var label: String
    var value: String
    var detail: String = ""
    var evidence: DesignV2EvidenceKind = .measured
}

struct DesignV2ChatRow {
    var author: String
    var message: String
    var evidence: DesignV2EvidenceKind = .live
}

struct DesignV2FuelRow {
    var label: String
    var value: String
    var advice: String
}

struct DesignV2GarageCoverModel {
    var state: String
    var detail: String
    var isGarageVisible: Bool
    var shouldFailClosed: Bool
}

struct DesignV2RadarModel {
    var proximity: LiveProximitySnapshot
    var isAvailable: Bool
}

struct DesignV2InputPoint {
    var throttle: Double
    var brake: Double
    var clutch: Double
    var brakeAbsActive: Bool
}

struct DesignV2InputModel {
    var throttle: Double
    var brake: Double
    var clutch: Double
    var steeringDegrees: Double
    var speedMetersPerSecond: Double
    var gear: Int
    var brakeAbsActive: Bool
    var trace: [DesignV2InputPoint]
    var isAvailable: Bool
}

struct DesignV2TrackMapModel {
    var snapshot: LiveTelemetrySnapshot
    var isAvailable: Bool
}

struct DesignV2TrackMarker {
    var carIdx: Int
    var lapDistPct: Double
    var isFocus: Bool
    var color: NSColor
    var positionLabel: String?
}

struct DesignV2OverlayModel {
    var title: String
    var status: String
    var footer: String
    var evidence: DesignV2EvidenceKind
    var body: DesignV2OverlayBody
}
