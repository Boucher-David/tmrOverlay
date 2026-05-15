// Designer-facing preview content for the mac-only design v2 proving ground.
// Edit this file to tune labels, example values, state mix, and scenario coverage.
extension DesignV2PreviewScenario {
    static let standingsTelemetry = DesignV2PreviewScenario(
        title: "Standings",
        subtitle: "A simple timing-board window into iRacing telemetry.",
        badges: [
            DesignV2Badge(title: "Race", evidence: .live),
            DesignV2Badge(title: "GT3", evidence: .measured)
        ],
        metrics: [
            DesignV2Metric(title: "Position", value: "12", detail: "overall timing row", evidence: .live),
            DesignV2Metric(title: "Class", value: "7", detail: "class timing row", evidence: .measured),
            DesignV2Metric(title: "Pit", value: "clear", detail: "not on pit road", evidence: .live)
        ],
        rows: [],
        footer: "Normal telemetry overlays should not explain confidence unless data is stale, unavailable, or derived.",
        mode: .standingsTable,
        table: DesignV2Table(
            columns: ["POS", "CLS", "#", "DRIVER", "GAP", "INT", "PIT"],
            rows: [
                ["10", "5", "71", "Maya Keller", "+1L", "+8.4", ""],
                ["11", "6", "24", "Avery Rowan", "+1L", "+3.1", ""],
                ["12", "7", "44", "Tech Mates Racing", "+1L", "--", ""],
                ["13", "8", "18", "Luca Nordin", "+1L", "-2.7", "IN"],
                ["14", "9", "52", "Ravi Singh", "+1L", "-9.8", ""]
            ],
            highlightedRowIndex: 2
        )
    )

    static let relativeTelemetry = DesignV2PreviewScenario(
        title: "Relative",
        subtitle: "Focus-centered rows using the current production content columns.",
        badges: [
            DesignV2Badge(title: "Live timing", evidence: .live),
            DesignV2Badge(title: "Current content", evidence: .measured)
        ],
        metrics: [
            DesignV2Metric(title: "Ahead", value: "-1.4s", detail: "nearest lapping context", evidence: .measured),
            DesignV2Metric(title: "Behind", value: "+2.7s", detail: "nearest car being lapped", evidence: .live),
            DesignV2Metric(title: "Focus", value: "Team", detail: "camera / player car", evidence: .measured)
        ],
        rows: [],
        footer: "Relative should be a direct timing surface first; focus-safe caveats only appear in degraded states.",
        mode: .relativeTable,
        table: DesignV2Table(
            columns: ["POS", "DRIVER", "DELTA"],
            rows: [
                ["5", "#24 Avery Rowan", "-3.100"],
                ["6", "#71 Maya Keller", "-1.400"],
                ["7", "#44 Tech Mates Racing", "0.000"],
                ["8", "#18 Luca Nordin", "+2.700"],
                ["9", "#63 Sofia Blake", "+5.800"]
            ],
            highlightedRowIndex: 2
        )
    )

    static let sectorComparison = DesignV2PreviewScenario(
        title: "Sector Comparison",
        subtitle: "Ahead, focus, and behind splits in one timing-table shape.",
        badges: [
            DesignV2Badge(title: "Model target", evidence: .partial),
            DesignV2Badge(title: "Column form", evidence: .measured)
        ],
        metrics: [
            DesignV2Metric(title: "Ahead", value: "+0.16", detail: "car #71 vs focus lap", evidence: .partial),
            DesignV2Metric(title: "Behind", value: "+0.07", detail: "car #18 vs focus lap", evidence: .partial),
            DesignV2Metric(title: "Focus", value: "8:19.742", detail: "latest completed lap", evidence: .measured)
        ],
        rows: [],
        footer: "The visual is simple, but production needs model-v2 sector split inputs before this becomes a real overlay.",
        mode: .sectorComparison,
        table: DesignV2Table(
            columns: ["CAR", "S1", "S2", "S3", "LAP"],
            rows: [
                ["Ahead #71", "+0.08", "-0.03", "+0.11", "+0.16"],
                ["Focus #44", "2:42.31", "2:48.06", "2:49.37", "8:19.74"],
                ["Behind #18", "-0.04", "+0.09", "+0.02", "+0.07"]
            ],
            highlightedRowIndex: 1
        )
    )

    static let blindspotSignal = DesignV2PreviewScenario(
        title: "Blindspot Signal",
        subtitle: "Local in-car side occupancy from the player-scoped signal.",
        badges: [
            DesignV2Badge(title: "Live", evidence: .live),
            DesignV2Badge(title: "Local car", evidence: .measured)
        ],
        metrics: [
            DesignV2Metric(title: "Left", value: "clear", detail: "no side occupancy", evidence: .live),
            DesignV2Metric(title: "Right", value: "overlap", detail: "CarLeftRight right", evidence: .live),
            DesignV2Metric(title: "Mode", value: "driving", detail: "hidden outside local car", evidence: .measured)
        ],
        rows: [],
        footer: "This can stay direct and quiet: visible only while the local player is in the car.",
        mode: .blindspotSignal
    )

    static let lapDelta = DesignV2PreviewScenario(
        title: "Laptime Delta",
        subtitle: "A compact pace readout once a live delta source is promoted.",
        badges: [
            DesignV2Badge(title: "Model target", evidence: .partial),
            DesignV2Badge(title: "Lap pace", evidence: .measured)
        ],
        metrics: [
            DesignV2Metric(title: "Current", value: "+0.24", detail: "vs selected target", evidence: .partial),
            DesignV2Metric(title: "Last", value: "8:19.742", detail: "completed lap", evidence: .measured),
            DesignV2Metric(title: "Target", value: "8:19.50", detail: "session best / user target", evidence: .measured)
        ],
        rows: [],
        footer: "Keep this simple, but add a clear live delta source or local baseline before wiring it in Windows.",
        mode: .lapDelta,
        graph: DesignV2LineGraph(
            title: "Lap Progress",
            valueLabel: "+0.24",
            unitLabel: "s vs target",
            points: [0.03, 0.10, 0.06, 0.18, 0.15, 0.24],
            referenceValue: 0,
            minValue: -0.35,
            maxValue: 0.55
        )
    )

    static let stintLapLog = DesignV2PreviewScenario(
        title: "Stint Laptime Log",
        subtitle: "Completed local laps as a quiet stint trend.",
        badges: [
            DesignV2Badge(title: "Measured", evidence: .measured),
            DesignV2Badge(title: "Stint", evidence: .history)
        ],
        metrics: [
            DesignV2Metric(title: "Last", value: "8:19.7", detail: "lap 17", evidence: .measured),
            DesignV2Metric(title: "Best", value: "8:17.9", detail: "current stint", evidence: .measured),
            DesignV2Metric(title: "Trend", value: "+0.8", detail: "last 5 laps", evidence: .history)
        ],
        rows: [],
        footer: "This fits simple if it only plots completed local laps and leaves strategy interpretation elsewhere.",
        mode: .stintLapGraph,
        graph: DesignV2LineGraph(
            title: "Local Stint Laps",
            valueLabel: "8:19.7",
            unitLabel: "completed laps",
            points: [499.2, 498.6, 497.9, 498.4, 499.0, 500.2, 499.7],
            referenceValue: 499.0,
            minValue: 497.5,
            maxValue: 501.0
        )
    )

    static let flagDisplay = DesignV2PreviewScenario(
        title: "Flag Display",
        subtitle: "Minimal race-control state with high contrast and low explanation.",
        badges: [
            DesignV2Badge(title: "Green", evidence: .live)
        ],
        metrics: [
            DesignV2Metric(title: "Flag", value: "GREEN", detail: "session running", evidence: .live),
            DesignV2Metric(title: "Incidents", value: "12x", detail: "team session count", evidence: .measured),
            DesignV2Metric(title: "Remaining", value: "2:14", detail: "race clock", evidence: .live)
        ],
        rows: [
            DesignV2SourceRow(label: "Race control", value: "clear", detail: "no active message", evidence: .live),
            DesignV2SourceRow(label: "Local state", value: "on track", detail: "not in garage or tow", evidence: .live),
            DesignV2SourceRow(label: "Next action", value: "none", detail: "surface stays quiet until state changes", evidence: .measured)
        ],
        footer: "Simple overlays can be mostly raw telemetry with strong visual priority and almost no explanatory chrome.",
        mode: .flagStrip
    )

    static let analysisException = DesignV2PreviewScenario(
        title: "Analysis Exception",
        subtitle: "Source labels appear only for interpreted telemetry.",
        badges: [
            DesignV2Badge(title: "Modeled", evidence: .modeled),
            DesignV2Badge(title: "Partial", evidence: .partial)
        ],
        metrics: [
            DesignV2Metric(title: "Fuel target", value: "7 laps", detail: "team stint model", evidence: .modeled),
            DesignV2Metric(title: "Local fuel", value: "38.4 L", detail: "measured scalar", evidence: .measured),
            DesignV2Metric(title: "Rejected", value: "5 laps", detail: "fails stint evidence", evidence: .error)
        ],
        rows: [
            DesignV2SourceRow(label: "Measured", value: "2.92 L/lap", detail: "rolling local window", evidence: .measured),
            DesignV2SourceRow(label: "Modeled", value: "7-lap rhythm", detail: "teammate stint history", evidence: .modeled),
            DesignV2SourceRow(label: "Unavailable", value: "side scalar", detail: "hidden when focus is not local player", evidence: .unavailable)
        ],
        footer: "Fuel, radar, gap, and strategy are analysis products; they earn source labels because they can mislead.",
        mode: .sourceTable
    )

    static let all: [DesignV2PreviewScenario] = [
        .standingsTelemetry,
        .relativeTelemetry,
        .sectorComparison,
        .blindspotSignal,
        .lapDelta,
        .stintLapLog,
        .flagDisplay,
        .analysisException
    ]
}
