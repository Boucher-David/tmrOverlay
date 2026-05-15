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
                return "Diagnostics"
            }
        }

        var subtitle: String {
            switch self {
            case .general:
                return "Shared units."
            case .support:
                return "Advanced capture and support bundle tools."
            }
        }

        var status: String? {
            switch self {
            case .general:
                return nil
            case .support:
                return nil
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
    private var updateStatus = "Dev run."
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
            addActionButton(frame: NSRect(x: 748, y: 292, width: 76, height: 30), title: "Check") { [weak self] in
                self?.updateStatus = "Checked."
                self?.needsDisplay = true
            }
            addActionButton(frame: NSRect(x: 838, y: 292, width: 88, height: 30), title: "Install") { [weak self] in
                self?.updateStatus = "No installable update."
                self?.needsDisplay = true
            }
            addActionButton(frame: NSRect(x: 940, y: 292, width: 92, height: 30), title: "Releases") { [weak self] in
                self?.updateStatus = "Opened releases."
                self?.needsDisplay = true
            }
        case .support:
            addDynamic(DesignV2SettingsToggleControl(
                frame: NSRect(x: 620, y: 276, width: 56, height: 28),
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
            addDynamic(DesignV2SettingsToggleControl(
                frame: NSRect(x: 620, y: 320, width: 56, height: 28),
                isOn: trackMapSettings().trackMapBuildFromTelemetry,
                theme: theme,
                onChange: { [weak self] isOn in
                    self?.setTrackMapBuildFromTelemetry(isOn)
                }
            ))
            addActionButton(frame: NSRect(x: 748, y: 552, width: 132, height: 32), title: "Create Bundle") { [weak self] in
                self?.openSupportURL(AppPaths.diagnosticsRoot(), status: "Opened diagnostics folder.")
            }
            addActionButton(frame: NSRect(x: 894, y: 552, width: 104, height: 32), title: "Copy Path") { [weak self] in
                self?.copyDiagnosticsBundlePath()
            }
            addActionButton(frame: NSRect(x: 328, y: 514, width: 104, height: 30), title: "Open Logs") { [weak self] in
                self?.openSupportURL(AppPaths.logsRoot(), status: "Opened logs folder.")
            }
            addActionButton(frame: NSRect(x: 446, y: 514, width: 118, height: 30), title: "Diagnostics") { [weak self] in
                self?.openSupportURL(AppPaths.diagnosticsRoot(), status: "Opened diagnostics folder.")
            }
            addActionButton(frame: NSRect(x: 328, y: 552, width: 100, height: 30), title: "Captures") { [weak self] in
                self?.openSupportURL(AppPaths.captureRoot(), status: "Opened captures folder.")
            }
            addActionButton(frame: NSRect(x: 446, y: 552, width: 92, height: 30), title: "History") { [weak self] in
                self?.openSupportURL(AppPaths.historyRoot(), status: "Opened history folder.")
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

        drawPanel(NSRect(x: 726, y: 214, width: 414, height: 132), title: "Updates")
        drawText("Status", in: NSRect(x: 748, y: 281, width: 70, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText(updateStatus, in: NSRect(x: 826, y: 281, width: 290, height: 18), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
    }

    private func drawSupportPage() {
        drawPanel(NSRect(x: 306, y: 214, width: 392, height: 206), title: "Capture Controls")
        drawText(
            captureSnapshot.rawCaptureActive ? "Raw diagnostic telemetry active" : "Raw diagnostic telemetry",
            in: NSRect(x: 328, y: 282, width: 250, height: 18),
            size: 13,
            weight: .heavy,
            color: DesignV2SettingsPalette.text
        )
        drawText("Local map building", in: NSRect(x: 328, y: 326, width: 250, height: 18), size: 13, weight: .heavy, color: DesignV2SettingsPalette.text)
        drawBodyLines(
            [
                "Capture writes raw frames only when explicitly requested.",
                "Local map building derives track geometry from completed telemetry."
            ],
            x: 328,
            y: 364,
            width: 326
        )

        drawPanel(NSRect(x: 726, y: 214, width: 414, height: 206), title: "Automatic History")
        drawStatusRow(label: "Car / track", value: "Session history", y: 280, active: true)
        drawStatusRow(label: "Fuel", value: "History model", y: 314, active: true)
        drawStatusRow(label: "Radar", value: "Calibration analysis", y: 348, active: true)
        drawStatusRow(label: "Post-race", value: "Summary analysis", y: 382, active: true)

        drawPanel(NSRect(x: 726, y: 446, width: 414, height: 142), title: "Support Bundle")
        drawText("Latest bundle", in: NSRect(x: 748, y: 514, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.muted)
        drawText(mockDiagnosticsBundleURL.lastPathComponent, in: NSRect(x: 876, y: 513, width: 220, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, monospaced: true)
        if !supportStatus.isEmpty {
            drawText(supportStatus, in: NSRect(x: 748, y: 562, width: 330, height: 18), size: 11, color: DesignV2SettingsPalette.green)
        }

        drawPanel(NSRect(x: 306, y: 446, width: 392, height: 142), title: "Support Folders")
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

    private func trackMapSettings() -> OverlaySettings {
        settings.overlays.first { $0.id == "track-map" }
            ?? OverlaySettings(id: "track-map", width: 360, height: 360)
    }

    private func setTrackMapBuildFromTelemetry(_ isOn: Bool) {
        var overlay = trackMapSettings()
        overlay.trackMapBuildFromTelemetry = isOn
        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
        supportStatus = isOn ? "Local map building enabled." : "Local map building disabled."
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
