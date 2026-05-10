import AppKit

final class DesignV2ApplicationSettingsFullView: NSView {
    enum Page: String {
        case general
        case support = "error-logging"

        var title: String {
            switch self {
            case .general:
                return "General"
            case .support:
                return "Support"
            }
        }

        var subtitle: String {
            switch self {
            case .general:
                return "Shared units."
            case .support:
                return "Diagnostics and teammate handoff controls stay task-oriented."
            }
        }

        var status: String? {
            switch self {
            case .general:
                return nil
            case .support:
                return "READY"
            }
        }
    }

    private let theme = DesignV2Theme.outrun
    private let page: Page
    private var settings: ApplicationSettings
    private var captureSnapshot: TelemetryCaptureStatusSnapshot
    private let onSettingsChanged: (ApplicationSettings) -> Void
    private let rawCaptureChanged: (Bool) -> Bool
    private let onSelectTab: (String) -> Void
    private var sidebarButtons: [String: NSButton] = [:]
    private var dynamicControls: [NSView] = []
    private var supportStatus = ""
    private var settingsRenderer: DesignV2SettingsRenderer {
        DesignV2SettingsRenderer(fontFamily: OverlayTheme.defaultFontFamily)
    }

    override var isFlipped: Bool {
        true
    }

    init(
        frame: NSRect,
        page: Page,
        settings: ApplicationSettings,
        captureSnapshot: TelemetryCaptureStatusSnapshot,
        onSettingsChanged: @escaping (ApplicationSettings) -> Void,
        rawCaptureChanged: @escaping (Bool) -> Bool,
        onSelectTab: @escaping (String) -> Void
    ) {
        self.page = page
        self.settings = settings
        self.captureSnapshot = captureSnapshot
        self.onSettingsChanged = onSettingsChanged
        self.rawCaptureChanged = rawCaptureChanged
        self.onSelectTab = onSelectTab
        super.init(frame: frame)
        alignBoundsToMatchedWindow()
        wantsLayer = true
        layer?.backgroundColor = theme.colors.surface.cgColor
        buildStaticControls()
        rebuildDynamicControls()
    }

    required init?(coder: NSCoder) {
        nil
    }

    func applySettings(_ updatedSettings: ApplicationSettings) {
        settings = updatedSettings
        rebuildDynamicControls()
        needsDisplay = true
    }

    func updateCaptureStatus(_ snapshot: TelemetryCaptureStatusSnapshot) {
        captureSnapshot = snapshot
        rebuildDynamicControls()
        needsDisplay = true
    }

    override func layout() {
        super.layout()
        alignBoundsToMatchedWindow()
        for (index, tab) in DesignV2SettingsChrome.sidebarTabs.enumerated() {
            sidebarButtons[tab.id]?.frame = DesignV2SettingsChrome.sidebarButtonFrame(index: index)
        }
    }

    private func alignBoundsToMatchedWindow() {
        bounds = NSRect(origin: DesignV2SettingsChrome.matchedWindowBoundsOrigin, size: frame.size)
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        settingsRenderer.drawBackdrop(in: bounds)
        settingsRenderer.drawWindowShell()
        settingsRenderer.drawTitleBar()
        settingsRenderer.drawSidebar(activeTabId: page.rawValue)
        settingsRenderer.drawContentContainer()
        settingsRenderer.drawContentHeader(
            title: page.title,
            subtitle: page.subtitle,
            status: page.status
        )

        switch page {
        case .general:
            drawGeneralPage()
        case .support:
            drawSupportPage()
        }
    }

    private func buildStaticControls() {
        for tab in DesignV2SettingsChrome.sidebarTabs {
            let button = NSButton(title: "", target: self, action: #selector(sidebarClicked(_:)))
            button.identifier = NSUserInterfaceItemIdentifier(tab.id)
            button.isBordered = false
            button.toolTip = tab.label
            button.wantsLayer = false
            addSubview(button)
            sidebarButtons[tab.id] = button
        }
    }

    private func rebuildDynamicControls() {
        dynamicControls.forEach { $0.removeFromSuperview() }
        dynamicControls.removeAll()

        switch page {
        case .general:
            addDynamic(DesignV2SettingsChoiceControl(
                frame: NSRect(x: 506, y: 270, width: 154, height: 30),
                options: ["Metric", "Imperial"],
                selected: settings.general.unitSystem,
                font: font(size: 12, weight: .heavy),
                onChange: { [weak self] unitSystem in
                    guard let self else {
                        return
                    }
                    settings.general.unitSystem = unitSystem
                    onSettingsChanged(settings)
                    needsDisplay = true
                }
            ))
        case .support:
            addDynamic(DesignV2SettingsToggleControl(
                frame: NSRect(x: 328, y: 276, width: 56, height: 28),
                isOn: captureSnapshot.rawCaptureEnabled || captureSnapshot.rawCaptureActive,
                theme: theme,
                onChange: { [weak self] isOn in
                    guard let self else {
                        return
                    }
                    if captureSnapshot.rawCaptureActive {
                        supportStatus = "Diagnostic telemetry is active for this session."
                        rebuildDynamicControls()
                        needsDisplay = true
                        return
                    }

                    let accepted = rawCaptureChanged(isOn)
                    supportStatus = accepted
                        ? (isOn ? "Diagnostic telemetry will start with live data." : "Diagnostic telemetry capture disabled.")
                        : "Diagnostic telemetry change was rejected while capture is active."
                    if !accepted {
                        rebuildDynamicControls()
                    }
                    needsDisplay = true
                }
            ))
            addActionButton(frame: NSRect(x: 328, y: 510, width: 138, height: 34), title: "Create Bundle") { [weak self] in
                self?.openSupportURL(AppPaths.diagnosticsRoot(), status: "Opened diagnostics folder.")
            }
            addActionButton(frame: NSRect(x: 482, y: 510, width: 120, height: 34), title: "Open Logs") { [weak self] in
                self?.openSupportURL(AppPaths.logsRoot(), status: "Opened logs folder.")
            }
            addActionButton(frame: NSRect(x: 618, y: 510, width: 116, height: 34), title: "Copy Path") { [weak self] in
                self?.copyDiagnosticsBundlePath()
            }
        }
    }

    private func addActionButton(frame: NSRect, title: String, onClick: @escaping () -> Void) {
        addDynamic(DesignV2SettingsActionButtonControl(
            frame: frame,
            title: title,
            font: font(size: 12, weight: .heavy),
            onClick: onClick
        ))
    }

    private func addDynamic(_ view: NSView) {
        dynamicControls.append(view)
        addSubview(view)
    }

    @objc private func sidebarClicked(_ sender: NSButton) {
        guard let id = sender.identifier?.rawValue else {
            return
        }

        if id != page.rawValue {
            onSelectTab(id)
        }
    }

    private func drawGeneralPage() {
        drawPanel(NSRect(x: 306, y: 214, width: 392, height: 132), title: "Units")
        drawText("Measurement system", in: NSRect(x: 328, y: 281, width: 160, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
    }

    private func drawSupportPage() {
        drawPanel(NSRect(x: 306, y: 214, width: 392, height: 170), title: "Diagnostic Capture")
        drawText(
            captureSnapshot.rawCaptureActive ? "Raw diagnostic telemetry active" : "Raw diagnostic telemetry",
            in: NSRect(x: 400, y: 282, width: 250, height: 18),
            size: 13,
            weight: .heavy,
            color: DesignV2SettingsPalette.text
        )
        drawBodyLines(
            [
                "Capture writes raw frames only when explicitly requested.",
                "Live overlay diagnostics remain lightweight by default."
            ],
            x: 328,
            y: 320,
            width: 326
        )

        drawPanel(NSRect(x: 726, y: 214, width: 414, height: 170), title: "Current State")
        drawStatusRow(label: "iRacing", value: captureSnapshot.isConnected ? "Connected" : "Waiting", y: 280, active: captureSnapshot.isConnected)
        drawStatusRow(label: "Session", value: sessionStateText(), y: 314, active: captureSnapshot.isCapturing, accent: DesignV2SettingsPalette.cyan)
        drawStatusRow(label: "Issue", value: shortIssueText(), y: 348, active: shortIssueText() == "No active warnings")

        drawPanel(NSRect(x: 306, y: 410, width: 834, height: 156), title: "Support Bundle")
        drawText("Latest bundle", in: NSRect(x: 328, y: 478, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.muted)
        drawText(mockDiagnosticsBundleURL.lastPathComponent, in: NSRect(x: 456, y: 477, width: 360, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, monospaced: true)
        drawBodyLines(
            [
                "Storage shortcuts and release/update state stay here,",
                "not inside normal overlay tabs."
            ],
            x: 754,
            y: 520,
            width: 330,
            size: 11
        )
        if !supportStatus.isEmpty {
            drawText(supportStatus, in: NSRect(x: 328, y: 548, width: 520, height: 18), size: 11, color: DesignV2SettingsPalette.green)
        }
    }

    private func drawStatusRow(label: String, value: String, y: CGFloat, active: Bool, accent: NSColor = DesignV2SettingsPalette.green) {
        drawText(label, in: NSRect(x: 750, y: y, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.muted)
        fillRounded(NSRect(x: 884, y: y + 5, width: 8, height: 8), radius: 4, color: active ? accent : DesignV2SettingsPalette.dim)
        drawText(value, in: NSRect(x: 904, y: y, width: 190, height: 18), size: 13, weight: .heavy, color: active ? DesignV2SettingsPalette.text : DesignV2SettingsPalette.secondary)
    }

    private func drawBodyLines(
        _ lines: [String],
        x: CGFloat,
        y: CGFloat,
        width: CGFloat,
        size: CGFloat = 12,
        color: NSColor = DesignV2SettingsPalette.muted
    ) {
        for (index, line) in lines.enumerated() {
            drawText(
                line,
                in: NSRect(x: x, y: y + CGFloat(index) * (size + 6), width: width, height: size + 4),
                size: size,
                color: color
            )
        }
    }

    private func openSupportURL(_ url: URL, status: String) {
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        NSWorkspace.shared.open(url)
        supportStatus = status
        needsDisplay = true
    }

    private func copyDiagnosticsBundlePath() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(mockDiagnosticsBundleURL.path, forType: .string)
        supportStatus = "Copied diagnostics bundle path."
        needsDisplay = true
    }

    private var mockDiagnosticsBundleURL: URL {
        AppPaths.diagnosticsRoot().appendingPathComponent("mock-macos-local-dev-20260428-120413-000.diagnostics")
    }

    private func sessionStateText() -> String {
        if captureSnapshot.rawCaptureActive {
            return "Diagnostic capture"
        }
        if captureSnapshot.isCapturing {
            return "Live telemetry"
        }
        if captureSnapshot.isConnected {
            return "Connected"
        }
        return "Not connected"
    }

    private func shortIssueText() -> String {
        if let lastError = captureSnapshot.lastError, !lastError.isEmpty {
            return "Error"
        }
        if let lastWarning = captureSnapshot.lastWarning, !lastWarning.isEmpty {
            return "Warning"
        }
        if let appWarning = captureSnapshot.appWarning, !appWarning.isEmpty {
            return "App warning"
        }
        if captureSnapshot.droppedFrameCount > 0 {
            return "Dropped frames"
        }
        return "No active warnings"
    }

    private func drawPanel(_ rect: NSRect, title: String) {
        settingsRenderer.drawPanel(rect, title: title)
    }

    private func drawText(
        _ text: String,
        in rect: NSRect,
        size: CGFloat,
        weight: NSFont.Weight = .regular,
        color: NSColor,
        alignment: NSTextAlignment = .left,
        monospaced: Bool = false
    ) {
        settingsRenderer.drawText(text, in: rect, size: size, weight: weight, color: color, alignment: alignment, monospaced: monospaced)
    }

    private func fillRounded(_ rect: NSRect, radius: CGFloat, color: NSColor) {
        settingsRenderer.fillRounded(rect, radius: radius, color: color)
    }

    private func strokeRounded(_ rect: NSRect, radius: CGFloat, color: NSColor, lineWidth: CGFloat) {
        settingsRenderer.strokeRounded(rect, radius: radius, color: color, lineWidth: lineWidth)
    }

    private func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        DesignV2Drawing.font(family: OverlayTheme.defaultFontFamily, size: size, weight: weight)
    }
}
