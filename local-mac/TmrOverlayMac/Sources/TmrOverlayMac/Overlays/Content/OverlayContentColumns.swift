import AppKit

enum OverlayContentColumnAlignment {
    case left
    case center
    case right
}

struct OverlayContentColumnDefinition {
    var id: String
    var label: String
    var dataKey: String
    var defaultEnabled: Bool
    var defaultOrder: Int
    var defaultWidth: Int
    var minimumWidth: Int
    var maximumWidth: Int
    var widthOptionKey: String? = nil
    var alignment: OverlayContentColumnAlignment = .right

    func enabledKey(overlayId: String) -> String {
        "\(overlayId).content.\(id).enabled"
    }

    func orderKey(overlayId: String) -> String {
        "\(overlayId).content.\(id).order"
    }

    func widthKey(overlayId: String) -> String {
        widthOptionKey ?? "\(overlayId).content.\(id).width"
    }
}

struct OverlayContentDefinition {
    var overlayId: String
    var columns: [OverlayContentColumnDefinition]
    var browserWidthPadding: Int
    var browserMinimumHeight: Int
    var nativeMinimumTableHeight: Int
    var fallbackColumnId: String
    var blocks: [OverlayContentBlockDefinition] = []
}

struct OverlayContentColumnState {
    var definition: OverlayContentColumnDefinition
    var enabled: Bool
    var order: Int
    var width: Int
}

struct OverlayContentBlockDefinition {
    var id: String
    var label: String
    var description: String
    var enabledOptionKey: String
    var defaultEnabled: Bool
    var countOptionKey: String?
    var countLabel: String?
    var defaultCount: Int
    var minimumCount: Int
    var maximumCount: Int
}

enum OverlayContentColumns {
    static let dataClassPosition = "class-position"
    static let dataCarNumber = "car-number"
    static let dataDriver = "driver"
    static let dataGap = "gap"
    static let dataInterval = "interval"
    static let dataPit = "pit"
    static let dataRelativePosition = "relative-position"
    static let standingsClassPositionColumnId = "standings.class-position"
    static let standingsCarNumberColumnId = "standings.car-number"
    static let standingsDriverColumnId = "standings.driver"
    static let standingsGapColumnId = "standings.gap"
    static let standingsIntervalColumnId = "standings.interval"
    static let standingsPitColumnId = "standings.pit"
    static let relativePositionColumnId = "relative.position"
    static let relativeDriverColumnId = "relative.driver"
    static let relativeGapColumnId = "relative.gap"
    static let relativePitColumnId = "relative.pit"
    static let standingsClassSeparatorBlockId = "standings.class-separators"
    static let inputThrottleTraceBlockId = "input-state.trace-throttle"
    static let inputBrakeTraceBlockId = "input-state.trace-brake"
    static let inputClutchTraceBlockId = "input-state.trace-clutch"
    static let inputThrottleBlockId = "input-state.throttle"
    static let inputBrakeBlockId = "input-state.brake"
    static let inputClutchBlockId = "input-state.clutch"
    static let inputSteeringBlockId = "input-state.steering"
    static let inputGearBlockId = "input-state.gear"
    static let inputSpeedBlockId = "input-state.speed"
    static let pitServiceTireCompoundBlockId = "pit-service.tire-compound"
    static let pitServiceTireChangeBlockId = "pit-service.tire-change"
    static let pitServiceTireSetLimitBlockId = "pit-service.tire-set-limit"
    static let pitServiceTireSetsAvailableBlockId = "pit-service.tire-sets-available"
    static let pitServiceTireSetsUsedBlockId = "pit-service.tire-sets-used"
    static let pitServiceTirePressureBlockId = "pit-service.tire-pressure"
    static let pitServiceTireTemperatureBlockId = "pit-service.tire-temperature"
    static let pitServiceTireWearBlockId = "pit-service.tire-wear"
    static let pitServiceTireDistanceBlockId = "pit-service.tire-distance"

    static let standings = OverlayContentDefinition(
        overlayId: StandingsOverlayDefinition.definition.id,
        columns: [
            OverlayContentColumnDefinition(id: standingsClassPositionColumnId, label: "CLS", dataKey: dataClassPosition, defaultEnabled: true, defaultOrder: 1, defaultWidth: 35, minimumWidth: 30, maximumWidth: 110, widthOptionKey: "standings.column.cls-width"),
            OverlayContentColumnDefinition(id: standingsCarNumberColumnId, label: "CAR", dataKey: dataCarNumber, defaultEnabled: true, defaultOrder: 2, defaultWidth: 50, minimumWidth: 42, maximumWidth: 130, widthOptionKey: "standings.column.car-width"),
            OverlayContentColumnDefinition(id: standingsDriverColumnId, label: "Driver", dataKey: dataDriver, defaultEnabled: true, defaultOrder: 3, defaultWidth: 250, minimumWidth: 180, maximumWidth: 520, widthOptionKey: "standings.column.driver-width", alignment: .left),
            OverlayContentColumnDefinition(id: standingsGapColumnId, label: "GAP", dataKey: dataGap, defaultEnabled: true, defaultOrder: 4, defaultWidth: 60, minimumWidth: 50, maximumWidth: 160, widthOptionKey: "standings.column.gap-width"),
            OverlayContentColumnDefinition(id: standingsIntervalColumnId, label: "INT", dataKey: dataInterval, defaultEnabled: true, defaultOrder: 5, defaultWidth: 60, minimumWidth: 50, maximumWidth: 160, widthOptionKey: "standings.column.interval-width"),
            OverlayContentColumnDefinition(id: standingsPitColumnId, label: "PIT", dataKey: dataPit, defaultEnabled: true, defaultOrder: 6, defaultWidth: 30, minimumWidth: 24, maximumWidth: 90, widthOptionKey: "standings.column.pit-width")
        ],
        browserWidthPadding: 66,
        browserMinimumHeight: 520,
        nativeMinimumTableHeight: 390,
        fallbackColumnId: standingsDriverColumnId,
        blocks: [
            OverlayContentBlockDefinition(
                id: standingsClassSeparatorBlockId,
                label: "Class separators",
                description: "Show iRacing class-colored separators and a limited sample of other multiclass rows.",
                enabledOptionKey: "standings.class-separators.enabled",
                defaultEnabled: true,
                countOptionKey: "standings.other-class-rows",
                countLabel: "Other-class cars",
                defaultCount: 2,
                minimumCount: 0,
                maximumCount: 6
            )
        ]
    )

    static let relative = OverlayContentDefinition(
        overlayId: RelativeOverlayDefinition.definition.id,
        columns: [
            OverlayContentColumnDefinition(id: relativePositionColumnId, label: "Pos", dataKey: dataRelativePosition, defaultEnabled: true, defaultOrder: 1, defaultWidth: 38, minimumWidth: 32, maximumWidth: 100),
            OverlayContentColumnDefinition(id: relativeDriverColumnId, label: "Driver", dataKey: dataDriver, defaultEnabled: true, defaultOrder: 2, defaultWidth: 250, minimumWidth: 180, maximumWidth: 520, alignment: .left),
            OverlayContentColumnDefinition(id: relativeGapColumnId, label: "Delta", dataKey: dataGap, defaultEnabled: true, defaultOrder: 3, defaultWidth: 70, minimumWidth: 60, maximumWidth: 160),
            OverlayContentColumnDefinition(id: relativePitColumnId, label: "Pit", dataKey: dataPit, defaultEnabled: false, defaultOrder: 4, defaultWidth: 30, minimumWidth: 24, maximumWidth: 90)
        ],
        browserWidthPadding: 66,
        browserMinimumHeight: 360,
        nativeMinimumTableHeight: 180,
        fallbackColumnId: relativeDriverColumnId
    )

    static let inputState = OverlayContentDefinition(
        overlayId: InputStateOverlayDefinition.definition.id,
        columns: [],
        browserWidthPadding: 42,
        browserMinimumHeight: 220,
        nativeMinimumTableHeight: 160,
        fallbackColumnId: "",
        blocks: [
            OverlayContentBlockDefinition(
                id: inputThrottleTraceBlockId,
                label: "Throttle trace",
                description: "Show the throttle line in the input graph.",
                enabledOptionKey: "input-state.trace.throttle",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputBrakeTraceBlockId,
                label: "Brake trace",
                description: "Show the brake line and ABS segments in the input graph.",
                enabledOptionKey: "input-state.trace.brake",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputClutchTraceBlockId,
                label: "Clutch trace",
                description: "Show the clutch line in the input graph.",
                enabledOptionKey: "input-state.trace.clutch",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputThrottleBlockId,
                label: "Throttle %",
                description: "Show the live throttle percentage in the right-side input rail.",
                enabledOptionKey: "input-state.current.throttle",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputBrakeBlockId,
                label: "Brake %",
                description: "Show the live brake percentage in the right-side input rail.",
                enabledOptionKey: "input-state.current.brake",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputClutchBlockId,
                label: "Clutch %",
                description: "Show the live clutch percentage in the right-side input rail.",
                enabledOptionKey: "input-state.current.clutch",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputSteeringBlockId,
                label: "Steering wheel",
                description: "Show the live steering wheel visualization in the right-side input rail.",
                enabledOptionKey: "input-state.current.steering",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputGearBlockId,
                label: "Gear",
                description: "Show the live gear readout in the right-side input rail.",
                enabledOptionKey: "input-state.current.gear",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: inputSpeedBlockId,
                label: "Speed",
                description: "Show the live speed readout in the right-side input rail.",
                enabledOptionKey: "input-state.current.speed",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            )
        ]
    )

    static let pitService = OverlayContentDefinition(
        overlayId: PitServiceOverlayDefinition.definition.id,
        columns: [],
        browserWidthPadding: 42,
        browserMinimumHeight: 360,
        nativeMinimumTableHeight: 260,
        fallbackColumnId: "",
        blocks: [
            OverlayContentBlockDefinition(
                id: pitServiceTireCompoundBlockId,
                label: "Compound",
                description: "Show current and requested tire compound when compound telemetry is available.",
                enabledOptionKey: "pit-service.tire-analysis.compound",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireChangeBlockId,
                label: "Change request",
                description: "Show requested tire changes by corner when pit-service tire commands are available.",
                enabledOptionKey: "pit-service.tire-analysis.change",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireSetLimitBlockId,
                label: "Set limit",
                description: "Show the session tire-set limit when the SDK reports a representative limit.",
                enabledOptionKey: "pit-service.tire-analysis.set-limit",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireSetsAvailableBlockId,
                label: "Sets available",
                description: "Show remaining tire sets by corner or axle when representative availability data exists.",
                enabledOptionKey: "pit-service.tire-analysis.sets-available",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireSetsUsedBlockId,
                label: "Sets used",
                description: "Show tire sets or corner tires used when counters have moved from zero.",
                enabledOptionKey: "pit-service.tire-analysis.sets-used",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTirePressureBlockId,
                label: "Pressure",
                description: "Show tire pressure from tire condition or pit-service pressure channels.",
                enabledOptionKey: "pit-service.tire-analysis.pressure",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireTemperatureBlockId,
                label: "Temperature",
                description: "Show tire temperatures when tire condition telemetry is populated.",
                enabledOptionKey: "pit-service.tire-analysis.temperature",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireWearBlockId,
                label: "Wear",
                description: "Show tire wear percentages when tire condition telemetry is populated.",
                enabledOptionKey: "pit-service.tire-analysis.wear",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            ),
            OverlayContentBlockDefinition(
                id: pitServiceTireDistanceBlockId,
                label: "Distance",
                description: "Show tire odometer values when tire distance telemetry is populated.",
                enabledOptionKey: "pit-service.tire-analysis.distance",
                defaultEnabled: true,
                countOptionKey: nil,
                countLabel: nil,
                defaultCount: 0,
                minimumCount: 0,
                maximumCount: 0
            )
        ]
    )

    static func definition(for overlayId: String) -> OverlayContentDefinition? {
        [standings, relative, inputState, pitService].first { $0.overlayId == overlayId }
    }

    static func columnStates(
        for definition: OverlayContentDefinition,
        settings: OverlaySettings
    ) -> [OverlayContentColumnState] {
        definition.columns
            .map { column in
                OverlayContentColumnState(
                    definition: column,
                    enabled: boolOption(
                        settings.options[column.enabledKey(overlayId: definition.overlayId)],
                        defaultValue: column.defaultEnabled
                    ),
                    order: intOption(
                        settings.options[column.orderKey(overlayId: definition.overlayId)],
                        defaultValue: column.defaultOrder,
                        minimum: 1,
                        maximum: max(1, definition.columns.count)
                    ),
                    width: intOption(
                        settings.options[column.widthKey(overlayId: definition.overlayId)],
                        defaultValue: column.defaultWidth,
                        minimum: column.minimumWidth,
                        maximum: column.maximumWidth,
                        clamp: !debugWidthEditorEnabled
                    )
                )
            }
            .sorted { left, right in
                left.order == right.order
                    ? left.definition.defaultOrder < right.definition.defaultOrder
                    : left.order < right.order
            }
    }

    static func visibleColumnStates(
        for definition: OverlayContentDefinition,
        settings: OverlaySettings
    ) -> [OverlayContentColumnState] {
        let states = columnStates(for: definition, settings: settings)
        let enabled = states.filter(\.enabled)
        return enabled.isEmpty
            ? states.filter { $0.definition.id == definition.fallbackColumnId }.prefix(1).map { $0 }
            : enabled
    }

    static func recommendedBrowserSize(
        overlay definition: OverlayDefinition,
        settings: OverlaySettings
    ) -> NSSize {
        let baseHeight = settings.height > 0 ? settings.height : definition.defaultSize.height
        guard let content = self.definition(for: definition.id),
              !content.columns.isEmpty else {
            let baseWidth = settings.width > 0 ? settings.width : definition.defaultSize.width
            return NSSize(width: baseWidth, height: baseHeight)
        }

        let columns = visibleColumnStates(for: content, settings: settings)
        let contentWidth = columns.reduce(0) { $0 + $1.width }
        let columnGaps = max(0, columns.count - 1) * 8
        return NSSize(
            width: max(1, CGFloat(contentWidth + columnGaps + content.browserWidthPadding)),
            height: max(baseHeight, CGFloat(content.browserMinimumHeight))
        )
    }

    static func blockEnabled(_ block: OverlayContentBlockDefinition, settings: OverlaySettings) -> Bool {
        boolOption(settings.options[block.enabledOptionKey], defaultValue: block.defaultEnabled)
    }

    static func blockCount(_ block: OverlayContentBlockDefinition, settings: OverlaySettings) -> Int {
        guard let countOptionKey = block.countOptionKey else {
            return block.defaultCount
        }

        return intOption(
            settings.options[countOptionKey],
            defaultValue: block.defaultCount,
            minimum: block.minimumCount,
            maximum: block.maximumCount
        )
    }

    private static func boolOption(_ value: String?, defaultValue: Bool) -> Bool {
        guard let value else {
            return defaultValue
        }

        return (value as NSString).boolValue
    }

    private static func intOption(_ value: String?, defaultValue: Int, minimum: Int, maximum: Int, clamp: Bool = true) -> Int {
        guard let value, let parsed = Int(value.trimmingCharacters(in: .whitespacesAndNewlines)) else {
            return min(max(defaultValue, minimum), maximum)
        }

        if !clamp {
            return max(1, parsed)
        }

        return min(max(parsed, minimum), maximum)
    }

    private static var debugWidthEditorEnabled: Bool {
        guard let value = ProcessInfo.processInfo.environment["TMR_MAC_COLUMN_WIDTH_DEBUG"]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
            return false
        }

        return ["true", "1", "yes", "on"].contains(value)
    }
}
