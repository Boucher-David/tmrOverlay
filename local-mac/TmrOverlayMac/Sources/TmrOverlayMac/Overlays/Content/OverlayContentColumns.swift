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
    static let pitServiceSessionTimeBlockId = "pit-service.session.time"
    static let pitServiceSessionLapsBlockId = "pit-service.session.laps"
    static let pitServiceReleaseBlockId = "pit-service.signal.release"
    static let pitServicePitStatusBlockId = "pit-service.signal.status"
    static let pitServiceFuelRequestedBlockId = "pit-service.service.fuel-requested"
    static let pitServiceFuelSelectedBlockId = "pit-service.service.fuel-selected"
    static let pitServiceTearoffRequestedBlockId = "pit-service.service.tearoff-requested"
    static let pitServiceRepairRequiredBlockId = "pit-service.service.repair-required"
    static let pitServiceRepairOptionalBlockId = "pit-service.service.repair-optional"
    static let pitServiceFastRepairSelectedBlockId = "pit-service.service.fast-repair-selected"
    static let pitServiceFastRepairAvailableBlockId = "pit-service.service.fast-repair-available"
    static let sessionWeatherSessionTypeBlockId = "session-weather.session.type"
    static let sessionWeatherSessionNameBlockId = "session-weather.session.name"
    static let sessionWeatherSessionModeBlockId = "session-weather.session.mode"
    static let sessionWeatherClockElapsedBlockId = "session-weather.clock.elapsed"
    static let sessionWeatherClockRemainingBlockId = "session-weather.clock.remaining"
    static let sessionWeatherClockTotalBlockId = "session-weather.clock.total"
    static let sessionWeatherEventTypeBlockId = "session-weather.event.type"
    static let sessionWeatherEventCarBlockId = "session-weather.event.car"
    static let sessionWeatherTrackNameBlockId = "session-weather.track.name"
    static let sessionWeatherTrackLengthBlockId = "session-weather.track.length"
    static let sessionWeatherLapsRemainingBlockId = "session-weather.laps.remaining"
    static let sessionWeatherLapsTotalBlockId = "session-weather.laps.total"
    static let sessionWeatherSurfaceWetnessBlockId = "session-weather.surface.wetness"
    static let sessionWeatherSurfaceDeclaredBlockId = "session-weather.surface.declared"
    static let sessionWeatherSurfaceRubberBlockId = "session-weather.surface.rubber"
    static let sessionWeatherSkySkiesBlockId = "session-weather.sky.skies"
    static let sessionWeatherSkyWeatherBlockId = "session-weather.sky.weather"
    static let sessionWeatherSkyRainBlockId = "session-weather.sky.rain"
    static let sessionWeatherWindDirectionBlockId = "session-weather.wind.direction"
    static let sessionWeatherWindSpeedBlockId = "session-weather.wind.speed"
    static let sessionWeatherWindFacingBlockId = "session-weather.wind.facing"
    static let sessionWeatherTempsAirBlockId = "session-weather.temps.air"
    static let sessionWeatherTempsTrackBlockId = "session-weather.temps.track"
    static let sessionWeatherAtmosphereHumidityBlockId = "session-weather.atmosphere.humidity"
    static let sessionWeatherAtmosphereFogBlockId = "session-weather.atmosphere.fog"
    static let sessionWeatherAtmospherePressureBlockId = "session-weather.atmosphere.pressure"
    static let streamChatAuthorColorBlockId = "stream-chat.twitch.author-color"
    static let streamChatBadgesBlockId = "stream-chat.twitch.badges"
    static let streamChatBitsBlockId = "stream-chat.twitch.bits"
    static let streamChatFirstMessageBlockId = "stream-chat.twitch.first-message"
    static let streamChatRepliesBlockId = "stream-chat.twitch.replies"
    static let streamChatTimestampsBlockId = "stream-chat.twitch.timestamps"
    static let streamChatEmotesBlockId = "stream-chat.twitch.emotes"
    static let streamChatAlertsBlockId = "stream-chat.twitch.alerts"
    static let streamChatMessageIdsBlockId = "stream-chat.twitch.message-ids"

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
                label: "Multiclass sections",
                description: "Show class header rows and a limited sample of other multiclass rows.",
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

    static let sessionWeather = OverlayContentDefinition(
        overlayId: SessionWeatherOverlayDefinition.definition.id,
        columns: [],
        browserWidthPadding: 42,
        browserMinimumHeight: 360,
        nativeMinimumTableHeight: 260,
        fallbackColumnId: "",
        blocks: [
            cellBlock(id: sessionWeatherSessionTypeBlockId, label: "Session type", description: "Show the iRacing session type."),
            cellBlock(id: sessionWeatherSessionNameBlockId, label: "Session name", description: "Show a meaningful session name when it differs from the type/event."),
            cellBlock(id: sessionWeatherSessionModeBlockId, label: "Session mode", description: "Show solo/team session mode."),
            cellBlock(id: sessionWeatherClockElapsedBlockId, label: "Elapsed time", description: "Show elapsed session time."),
            cellBlock(id: sessionWeatherClockRemainingBlockId, label: "Remaining time", description: "Show time left or pre-green countdown."),
            cellBlock(id: sessionWeatherClockTotalBlockId, label: "Total time", description: "Show scheduled session length."),
            cellBlock(id: sessionWeatherEventTypeBlockId, label: "Event type", description: "Show event type when session telemetry reports it."),
            cellBlock(id: sessionWeatherEventCarBlockId, label: "Car", description: "Show the focused car display name."),
            cellBlock(id: sessionWeatherTrackNameBlockId, label: "Track name", description: "Show track display name."),
            cellBlock(id: sessionWeatherTrackLengthBlockId, label: "Track length", description: "Show track length using the selected units."),
            cellBlock(id: sessionWeatherLapsRemainingBlockId, label: "Laps remaining", description: "Show remaining race laps when available or estimated."),
            cellBlock(id: sessionWeatherLapsTotalBlockId, label: "Laps total", description: "Show total race laps when available or estimated."),
            cellBlock(id: sessionWeatherSurfaceWetnessBlockId, label: "Wetness", description: "Show current track wetness."),
            cellBlock(id: sessionWeatherSurfaceDeclaredBlockId, label: "Declared surface", description: "Show declared wet/dry surface state."),
            cellBlock(id: sessionWeatherSurfaceRubberBlockId, label: "Rubber", description: "Show session rubber state."),
            cellBlock(id: sessionWeatherSkySkiesBlockId, label: "Skies", description: "Show sky condition."),
            cellBlock(id: sessionWeatherSkyWeatherBlockId, label: "Weather", description: "Show session weather type."),
            cellBlock(id: sessionWeatherSkyRainBlockId, label: "Rain", description: "Show precipitation percentage."),
            cellBlock(id: sessionWeatherWindDirectionBlockId, label: "Wind direction", description: "Show absolute wind direction."),
            cellBlock(id: sessionWeatherWindSpeedBlockId, label: "Wind speed", description: "Show wind speed using the selected units."),
            cellBlock(id: sessionWeatherWindFacingBlockId, label: "Facing wind", description: "Show wind direction relative to the local car heading."),
            cellBlock(id: sessionWeatherTempsAirBlockId, label: "Air temp", description: "Show air temperature using the selected units."),
            cellBlock(id: sessionWeatherTempsTrackBlockId, label: "Track temp", description: "Show track temperature using the selected units."),
            cellBlock(id: sessionWeatherAtmosphereHumidityBlockId, label: "Humidity", description: "Show relative humidity."),
            cellBlock(id: sessionWeatherAtmosphereFogBlockId, label: "Fog", description: "Show fog level."),
            cellBlock(id: sessionWeatherAtmospherePressureBlockId, label: "Pressure", description: "Show air pressure using the selected units.")
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
            cellBlock(id: pitServiceSessionTimeBlockId, label: "Session time", description: "Show remaining session time in the pit-service session row."),
            cellBlock(id: pitServiceSessionLapsBlockId, label: "Session laps", description: "Show remaining/total race laps in the pit-service session row."),
            cellBlock(id: pitServiceReleaseBlockId, label: "Release", description: "Show pit release state."),
            cellBlock(id: pitServicePitStatusBlockId, label: "Pit status", description: "Show iRacing pit-service status."),
            cellBlock(id: pitServiceFuelRequestedBlockId, label: "Fuel requested", description: "Show whether refuel service is requested."),
            cellBlock(id: pitServiceFuelSelectedBlockId, label: "Fuel selected", description: "Show selected refuel amount using the selected units."),
            cellBlock(id: pitServiceTearoffRequestedBlockId, label: "Tearoff requested", description: "Show tearoff service request."),
            cellBlock(id: pitServiceRepairRequiredBlockId, label: "Required repair", description: "Show required repair time."),
            cellBlock(id: pitServiceRepairOptionalBlockId, label: "Optional repair", description: "Show optional repair time."),
            cellBlock(id: pitServiceFastRepairSelectedBlockId, label: "Fast repair selected", description: "Show fast repair selection."),
            cellBlock(id: pitServiceFastRepairAvailableBlockId, label: "Fast repairs available", description: "Show local fast repairs available."),
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

    static let streamChat = OverlayContentDefinition(
        overlayId: StreamChatOverlayDefinition.definition.id,
        columns: [],
        browserWidthPadding: 42,
        browserMinimumHeight: 520,
        nativeMinimumTableHeight: 420,
        fallbackColumnId: "",
        blocks: [
            OverlayContentBlockDefinition(id: streamChatAuthorColorBlockId, label: "Author color", description: "Use Twitch's author color tag when it is present.", enabledOptionKey: SharedOverlayContract.streamChatShowAuthorColorKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatBadgesBlockId, label: "Badges", description: "Show Twitch badge chips such as mod, VIP, subscriber, and broadcaster.", enabledOptionKey: SharedOverlayContract.streamChatShowBadgesKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatBitsBlockId, label: "Bits", description: "Show cheer/bits metadata when Twitch sends it with the message.", enabledOptionKey: SharedOverlayContract.streamChatShowBitsKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatFirstMessageBlockId, label: "First message", description: "Mark a viewer's first message when Twitch reports that signal.", enabledOptionKey: SharedOverlayContract.streamChatShowFirstMessageKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatRepliesBlockId, label: "Replies", description: "Show the replied-to chatter when Twitch sends reply tags.", enabledOptionKey: SharedOverlayContract.streamChatShowRepliesKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatTimestampsBlockId, label: "Timestamps", description: "Show Twitch's sent timestamp as a compact local time.", enabledOptionKey: SharedOverlayContract.streamChatShowTimestampsKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatEmotesBlockId, label: "Emotes", description: "Show compact Twitch emote metadata when emote ranges are available.", enabledOptionKey: SharedOverlayContract.streamChatShowEmotesKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatAlertsBlockId, label: "Alerts", description: "Show Twitch USERNOTICE rows such as subs, gifts, and raids.", enabledOptionKey: SharedOverlayContract.streamChatShowAlertsKey, defaultEnabled: true, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0),
            OverlayContentBlockDefinition(id: streamChatMessageIdsBlockId, label: "Message IDs", description: "Show a short Twitch message ID for debugging chat delivery.", enabledOptionKey: SharedOverlayContract.streamChatShowMessageIdsKey, defaultEnabled: false, countOptionKey: nil, countLabel: nil, defaultCount: 0, minimumCount: 0, maximumCount: 0)
        ]
    )

    static func definition(for overlayId: String) -> OverlayContentDefinition? {
        [standings, relative, inputState, sessionWeather, pitService, streamChat].first { $0.overlayId == overlayId }
    }

    private static func cellBlock(id: String, label: String, description: String, defaultEnabled: Bool = true) -> OverlayContentBlockDefinition {
        OverlayContentBlockDefinition(
            id: id,
            label: label,
            description: description,
            enabledOptionKey: "\(id).enabled",
            defaultEnabled: defaultEnabled,
            countOptionKey: nil,
            countLabel: nil,
            defaultCount: 0,
            minimumCount: 0,
            maximumCount: 0
        )
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
