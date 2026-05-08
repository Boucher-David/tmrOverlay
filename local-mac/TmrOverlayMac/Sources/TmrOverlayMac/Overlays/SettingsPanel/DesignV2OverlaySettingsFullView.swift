import AppKit

final class DesignV2OverlaySettingsFullView: NSView {
    private struct ContentMatrixRow {
        var label: String
        var enabled: Bool
    }

    private let theme = DesignV2Theme.outrun
    private let definition: OverlayDefinition
    private var overlay: OverlaySettings
    private let fontFamily: String
    private let onOverlayChanged: (OverlaySettings) -> Void
    private let onSelectTab: (String) -> Void
    private var sidebarButtons: [String: NSButton] = [:]
    private var regionButtons: [DesignV2SettingsRegion: NSButton] = [:]
    private var dynamicControls: [NSView] = []
    private var selectedRegion = DesignV2SettingsRegion.general
    private var settingsRenderer: DesignV2SettingsRenderer {
        DesignV2SettingsRenderer(fontFamily: fontFamily)
    }

    override var isFlipped: Bool {
        true
    }

    init(
        frame: NSRect,
        definition: OverlayDefinition,
        overlay: OverlaySettings,
        fontFamily: String = OverlayTheme.defaultFontFamily,
        onOverlayChanged: @escaping (OverlaySettings) -> Void,
        onSelectTab: @escaping (String) -> Void
    ) {
        self.definition = definition
        self.overlay = overlay
        self.fontFamily = fontFamily
        self.onOverlayChanged = onOverlayChanged
        self.onSelectTab = onSelectTab
        super.init(frame: frame)
        if let initialRegion = Self.initialRegionFromEnvironment() {
            selectedRegion = initialRegion
        }
        wantsLayer = true
        layer?.backgroundColor = theme.colors.surface.cgColor
        buildStaticControls()
        rebuildRegionControls()
    }

    required init?(coder: NSCoder) {
        nil
    }

    private static func initialRegionFromEnvironment() -> DesignV2SettingsRegion? {
        guard let rawValue = ProcessInfo.processInfo.environment["TMR_MAC_SELECTED_SETTINGS_REGION"] else {
            return nil
        }
        return DesignV2SettingsRegion(rawValue: rawValue.trimmingCharacters(in: .whitespacesAndNewlines).lowercased())
    }

    func applyOverlay(_ updatedOverlay: OverlaySettings) {
        overlay = updatedOverlay
        rebuildRegionControls()
        needsDisplay = true
    }

    override func layout() {
        super.layout()
        for (index, tab) in DesignV2SettingsChrome.sidebarTabs.enumerated() {
            sidebarButtons[tab.id]?.frame = DesignV2SettingsChrome.sidebarButtonFrame(index: index)
        }
        for (segment, rect) in DesignV2SettingsChrome.segmentFrames(for: DesignV2SettingsRegion.standardSegments) {
            guard let region = DesignV2SettingsRegion(rawValue: segment.id) else {
                continue
            }
            regionButtons[region]?.frame = rect
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        settingsRenderer.drawBackdrop(in: bounds)
        settingsRenderer.drawWindowShell()
        settingsRenderer.drawTitleBar()
        settingsRenderer.drawSidebar(activeTabId: definition.id)
        settingsRenderer.drawContentContainer()
        settingsRenderer.drawContentHeader(
            title: definition.displayName,
            subtitle: DesignV2SettingsOverlaySpecs.subtitle(for: definition.id)
        )
        settingsRenderer.drawSegments(DesignV2SettingsRegion.standardSegments, selectedId: selectedRegion.rawValue)
        drawSelectedRegion()
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

        for region in DesignV2SettingsRegion.allCases {
            let button = NSButton(title: "", target: self, action: #selector(regionClicked(_:)))
            button.identifier = NSUserInterfaceItemIdentifier(region.rawValue)
            button.isBordered = false
            button.toolTip = region.title
            button.wantsLayer = false
            addSubview(button)
            regionButtons[region] = button
        }
    }

    @objc private func sidebarClicked(_ sender: NSButton) {
        guard let id = sender.identifier?.rawValue else {
            return
        }

        if id != definition.id {
            onSelectTab(id)
        }
    }

    @objc private func regionClicked(_ sender: NSButton) {
        guard let rawValue = sender.identifier?.rawValue,
              let region = DesignV2SettingsRegion(rawValue: rawValue) else {
            return
        }

        selectedRegion = region
        rebuildRegionControls()
        needsDisplay = true
    }

    private func rebuildRegionControls() {
        dynamicControls.forEach { $0.removeFromSuperview() }
        dynamicControls.removeAll()

        switch selectedRegion {
        case .general:
            buildGeneralControls()
        case .content:
            buildContentControls()
        case .header:
            buildChromeControls(keys: Self.headerChromeKeys)
        case .footer:
            buildChromeControls(keys: Self.footerChromeKeys)
        }
    }

    private func buildGeneralControls() {
        addDynamic(DesignV2SettingsToggleControl(
            frame: NSRect(x: 600, y: 328, width: 56, height: 28),
            isOn: overlay.enabled,
            theme: theme,
            onChange: { [weak self] isOn in
                self?.overlay.enabled = isOn
                self?.saveOverlay()
            }
        ))

        if definition.showScaleControl {
            addDynamic(DesignV2SettingsPercentSliderControl(
                frame: NSRect(x: 454, y: 368, width: 180, height: 28),
                value: closestPercent(overlay.scale, allowedValues: [60, 75, 100, 125, 150, 175, 200]),
                allowedValues: [60, 75, 100, 125, 150, 175, 200],
                activeColor: theme.colors.accentPrimary,
                theme: theme,
                onChange: { [weak self] percent in
                    guard let self else {
                        return
                    }
                    overlay.scale = Double(percent) / 100.0
                    overlay.width = definition.defaultSize.width * overlay.scale
                    overlay.height = definition.defaultSize.height * overlay.scale
                    saveOverlay()
                }
            ))
        }

        if definition.showOpacityControl {
            addDynamic(DesignV2SettingsPercentSliderControl(
                frame: NSRect(x: 454, y: 408, width: 180, height: 28),
                value: closestPercent(overlay.opacity, allowedValues: [20, 30, 40, 50, 60, 70, 80, 88, 90, 100]),
                allowedValues: [20, 30, 40, 50, 60, 70, 80, 88, 90, 100],
                activeColor: theme.colors.accentSecondary,
                theme: theme,
                onChange: { [weak self] percent in
                    self?.overlay.opacity = min(max(Double(percent) / 100.0, 0.2), 1.0)
                    self?.saveOverlay()
                }
            ))
        }

        if definition.showSessionFilters {
            let sessions: [(String, WritableKeyPath<OverlaySettings, Bool>, CGFloat, CGFloat)] = [
                ("Test", \.showInTest, 454, 62),
                ("Practice", \.showInPractice, 548, 94),
                ("Qual", \.showInQualifying, 454, 62),
                ("Race", \.showInRace, 548, 76)
            ]
            for (index, session) in sessions.enumerated() {
                addDynamic(DesignV2SettingsCheckControl(
                    frame: NSRect(x: session.2, y: index < 2 ? 442 : 468, width: session.3, height: 20),
                    title: session.0,
                    isOn: overlay[keyPath: session.1],
                    theme: theme,
                    font: font(size: 12, weight: .semibold),
                    onChange: { [weak self] isOn in
                        self?.overlay[keyPath: session.1] = isOn
                        self?.saveOverlay()
                    }
                ))
            }
        }

        addDynamic(DesignV2SettingsActionButtonControl(
            frame: NSRect(x: 950, y: 542, width: 70, height: 30),
            title: "Copy",
            font: font(size: 12, weight: .heavy),
            onClick: { [weak self] in
                self?.copyLocalhostURL()
            }
        ))
    }

    private func buildContentControls() {
        switch definition.id {
        case "relative":
            addCountPopup(
                frame: NSRect(x: 454, y: 568, width: 86, height: 28),
                value: min(max(max(overlay.relativeCarsAhead, overlay.relativeCarsBehind), 0), 8),
                maximum: 8,
                action: #selector(relativeEachSideChanged(_:))
            )
        case "standings":
            if let block = OverlayContentColumns.standings.blocks.first {
                addDynamic(DesignV2SettingsCheckControl(
                    frame: NSRect(x: 328, y: 590, width: 190, height: 20),
                    title: block.label,
                    isOn: OverlayContentColumns.blockEnabled(block, settings: overlay),
                    theme: theme,
                    font: font(size: 12, weight: .semibold),
                    onChange: { [weak self] isOn in
                        self?.overlay.options[block.enabledOptionKey] = isOn ? "true" : "false"
                        self?.saveOverlay()
                    }
                ))
                if let key = block.countOptionKey {
                    addCountPopup(
                        frame: NSRect(x: 660, y: 586, width: 76, height: 28),
                        value: OverlayContentColumns.blockCount(block, settings: overlay),
                        maximum: block.maximumCount,
                        action: #selector(standingsOtherClassRowsChanged(_:)),
                        identifier: key
                    )
                }
            }
        case "gap-to-leader":
            addCountPopup(frame: NSRect(x: 454, y: 492, width: 86, height: 28), value: overlay.classGapCarsAhead, maximum: 12, action: #selector(gapCarsAheadChanged(_:)))
            addCountPopup(frame: NSRect(x: 662, y: 492, width: 86, height: 28), value: overlay.classGapCarsBehind, maximum: 12, action: #selector(gapCarsBehindChanged(_:)))
        case "fuel-calculator":
            addDynamic(DesignV2SettingsCheckControl(
                frame: NSRect(x: 454, y: 500, width: 170, height: 22),
                title: "Advice column",
                isOn: overlay.showFuelAdvice,
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.showFuelAdvice = isOn
                    self?.saveOverlay()
                }
            ))
            addDynamic(DesignV2SettingsCheckControl(
                frame: NSRect(x: 454, y: 532, width: 150, height: 22),
                title: "Source row",
                isOn: overlay.showFuelSource,
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.showFuelSource = isOn
                    self?.saveOverlay()
                }
            ))
        case "track-map":
            addDynamic(DesignV2SettingsCheckControl(
                frame: NSRect(x: 454, y: 558, width: 220, height: 22),
                title: "Sector boundaries",
                isOn: optionBool(key: "track-map.sector-boundaries.enabled", defaultValue: true),
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.options["track-map.sector-boundaries.enabled"] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            ))
            addDynamic(DesignV2SettingsCheckControl(
                frame: NSRect(x: 454, y: 590, width: 280, height: 22),
                title: "Build maps from IBT telemetry",
                isOn: overlay.trackMapBuildFromTelemetry,
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.trackMapBuildFromTelemetry = isOn
                    self?.saveOverlay()
                }
            ))
        default:
            break
        }
    }

    private func buildChromeControls(keys: [String]) {
        guard DesignV2SettingsOverlaySpecs.supportsSharedChromeSettings(definition.id) else {
            return
        }

        for (index, key) in keys.enumerated() {
            let x = 454 + CGFloat(index) * 116
            addDynamic(DesignV2SettingsCheckControl(
                frame: NSRect(x: x, y: 370, width: 38, height: 22),
                title: "",
                isOn: optionBool(key: key, defaultValue: true),
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.options[key] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            ))
        }
    }

    private func addCountPopup(
        frame: NSRect,
        value: Int,
        maximum: Int,
        action: Selector,
        identifier: String? = nil
    ) {
        let popup = NSPopUpButton(frame: frame, pullsDown: false)
        popup.addItems(withTitles: (0...maximum).map(String.init))
        popup.selectItem(withTitle: String(min(max(value, 0), maximum)))
        if let identifier {
            popup.identifier = NSUserInterfaceItemIdentifier(identifier)
        }
        popup.target = self
        popup.action = action
        popup.font = font(size: 12, weight: .semibold)
        addDynamic(popup)
    }

    private func addDynamic(_ view: NSView) {
        dynamicControls.append(view)
        addSubview(view)
    }

    @objc private func relativeEachSideChanged(_ sender: NSPopUpButton) {
        guard let value = selectedInt(sender) else {
            return
        }
        let clamped = min(max(value, 0), 8)
        overlay.relativeCarsAhead = clamped
        overlay.relativeCarsBehind = clamped
        saveOverlay()
    }

    @objc private func standingsOtherClassRowsChanged(_ sender: NSPopUpButton) {
        guard let key = sender.identifier?.rawValue,
              let value = selectedInt(sender) else {
            return
        }
        overlay.options[key] = String(min(max(value, 0), 6))
        saveOverlay()
    }

    @objc private func gapCarsAheadChanged(_ sender: NSPopUpButton) {
        guard let value = selectedInt(sender) else {
            return
        }
        overlay.classGapCarsAhead = min(max(value, 0), 12)
        saveOverlay()
    }

    @objc private func gapCarsBehindChanged(_ sender: NSPopUpButton) {
        guard let value = selectedInt(sender) else {
            return
        }
        overlay.classGapCarsBehind = min(max(value, 0), 12)
        saveOverlay()
    }

    @objc private func copyLocalhostURL() {
        guard let route = BrowserOverlayCatalog.route(for: definition.id) else {
            return
        }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString("http://localhost:8765\(route)", forType: .string)
    }

    private func saveOverlay() {
        onOverlayChanged(overlay)
        needsDisplay = true
    }

    private func drawSelectedRegion() {
        switch selectedRegion {
        case .general:
            drawGeneralRegion()
        case .content:
            drawContentRegion()
        case .header:
            drawChromeRegion(title: "Header", itemLabel: "Status")
        case .footer:
            drawChromeRegion(title: "Footer", itemLabel: "Source")
        }
    }

    private func drawGeneralRegion() {
        drawPanel(NSRect(x: 306, y: 272, width: 392, height: 226), title: "Overlay Controls")
        drawText("Visible", in: NSRect(x: 328, y: 334, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        if definition.showScaleControl {
            drawText("Scale", in: NSRect(x: 328, y: 374, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            drawText("\(Int((overlay.scale * 100).rounded()))%", in: NSRect(x: 642, y: 371, width: 40, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
        }
        if definition.showOpacityControl {
            drawText(definition.id == "track-map" ? "Map fill" : "Opacity", in: NSRect(x: 328, y: 414, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            drawText("\(Int((overlay.opacity * 100).rounded()))%", in: NSRect(x: 642, y: 411, width: 40, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
        }
        if definition.showSessionFilters {
            drawText("Sessions", in: NSRect(x: 328, y: 454, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        } else {
            drawText("Sessions", in: NSRect(x: 328, y: 454, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            drawText("Managed by overlay logic", in: NSRect(x: 454, y: 454, width: 180, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
        }

        drawPanel(NSRect(x: 726, y: 272, width: 414, height: 226), title: "\(definition.displayName) Preview")
        let previewRect = NSRect(x: 750, y: 324, width: 366, height: 132)
        fillRounded(previewRect, radius: 10, color: NSColor(red255: 3, green: 8, blue: 18))
        strokeRounded(previewRect, radius: 10, color: DesignV2SettingsPalette.cyan.withAlphaComponent(0.65), lineWidth: 1)
        if let image = previewImage {
            drawAspectFit(image, in: NSRect(x: 762, y: 334, width: 342, height: 112))
        }
        drawText("Default size", in: NSRect(x: 750, y: 468, width: 100, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
        drawText(
            "\(Int(definition.defaultSize.width)) x \(Int(definition.defaultSize.height))",
            in: NSRect(x: 852, y: 468, width: 120, height: 18),
            size: 12,
            weight: .bold,
            color: DesignV2SettingsPalette.secondary,
            monospaced: true
        )

        drawBrowserSourcePanel(summary: browserSummary)
    }

    private func drawContentRegion() {
        switch definition.id {
        case "relative":
            drawRelativeContentRegion()
        case "standings":
            drawStandingsContentRegion()
        case "gap-to-leader":
            drawGapContentRegion()
        case "fuel-calculator":
            drawFuelContentRegion()
        case "track-map":
            drawTrackMapContentRegion()
        default:
            drawEmptyContentRegion()
        }
    }

    private func drawRelativeContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: columnContentRows(definition: OverlayContentColumns.relative),
            rect: NSRect(x: 306, y: 272, width: 834, height: 222)
        )

        drawPanel(NSRect(x: 306, y: 512, width: 834, height: 104), title: "Relative Rows")
        drawText("Cars each side", in: NSRect(x: 328, y: 574, width: 130, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("\(overlay.relativeCarsAhead + overlay.relativeCarsBehind + 1) visible rows", in: NSRect(x: 558, y: 574, width: 112, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
    }

    private func drawStandingsContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: standingsContentRows(),
            rect: NSRect(x: 306, y: 272, width: 834, height: 258)
        )
        if let block = OverlayContentColumns.standings.blocks.first {
            drawPanel(NSRect(x: 306, y: 536, width: 834, height: 88), title: "Content Details")
            drawText(block.description, in: NSRect(x: 328, y: 566, width: 760, height: 18), size: 11, color: DesignV2SettingsPalette.muted)
            drawText(block.countLabel ?? "Count", in: NSRect(x: 540, y: 592, width: 112, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        }
    }

    private func drawGapContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [ContentMatrixRow(label: "Class gap window", enabled: true)],
            rect: NSRect(x: 306, y: 272, width: 834, height: 126)
        )
        drawPanel(NSRect(x: 306, y: 426, width: 834, height: 126), title: "Class Gap Window")
        drawText("Cars ahead", in: NSRect(x: 328, y: 498, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Cars behind", in: NSRect(x: 536, y: 498, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Keeps the focused class gap trend bounded around the team car.", in: NSRect(x: 328, y: 532, width: 560, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
    }

    private func drawFuelContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Advice column", enabled: overlay.showFuelAdvice),
                ContentMatrixRow(label: "Source row", enabled: overlay.showFuelSource)
            ],
            rect: NSRect(x: 306, y: 272, width: 834, height: 150)
        )
        drawPanel(NSRect(x: 306, y: 438, width: 834, height: 170), title: "Fuel Rows")
        drawText("Show", in: NSRect(x: 328, y: 506, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Advice adds stint/rhythm guidance where modeled data is available.", in: NSRect(x: 328, y: 584, width: 560, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
    }

    private func drawTrackMapContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Map source", enabled: true),
                ContentMatrixRow(label: "Sector boundaries", enabled: optionBool(key: "track-map.sector-boundaries.enabled", defaultValue: true)),
                ContentMatrixRow(label: "Local map building", enabled: overlay.trackMapBuildFromTelemetry)
            ],
            rect: NSRect(x: 306, y: 272, width: 834, height: 190)
        )

        drawPanel(NSRect(x: 306, y: 484, width: 392, height: 126), title: "Map Sources")
        drawText("Source", in: NSRect(x: 328, y: 542, width: 90, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Best bundled or local map; circle fallback", in: NSRect(x: 454, y: 542, width: 210, height: 18), size: 12, color: DesignV2SettingsPalette.text)
        drawText("Display", in: NSRect(x: 328, y: 562, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Local maps", in: NSRect(x: 328, y: 594, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)

        drawPanel(NSRect(x: 726, y: 484, width: 414, height: 126), title: "Bundled Coverage")
        drawText("Reviewed app maps load automatically for matching tracks.", in: NSRect(x: 750, y: 542, width: 340, height: 18), size: 12, color: DesignV2SettingsPalette.secondary)
        drawText("Circle fallback remains available when no reviewed/local geometry exists.", in: NSRect(x: 750, y: 582, width: 330, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
    }

    private func drawEmptyContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [ContentMatrixRow(label: "Content", enabled: true)],
            rect: NSRect(x: 306, y: 272, width: 834, height: 126)
        )
        drawText("This matches the current production settings surface for this overlay.", in: NSRect(x: 328, y: 410, width: 560, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
    }

    private func drawChromeRegion(title: String, itemLabel: String) {
        drawPanel(NSRect(x: 306, y: 272, width: 834, height: 188), title: title)
        guard DesignV2SettingsOverlaySpecs.supportsSharedChromeSettings(definition.id) else {
            drawText("No \(title.lowercased()) controls yet.", in: NSRect(x: 328, y: 334, width: 420, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            drawText("This matches the current production settings surface for this overlay.", in: NSRect(x: 328, y: 372, width: 560, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
            return
        }

        drawText("Item", in: NSRect(x: 328, y: 330, width: 110, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        for (index, session) in ["Test", "Practice", "Qualifying", "Race"].enumerated() {
            drawText(session, in: NSRect(x: 454 + CGFloat(index) * 116, y: 330, width: 104, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        }
        fillRounded(NSRect(x: 328, y: 360, width: 768, height: 44), radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
        strokeRounded(NSRect(x: 328, y: 360, width: 768, height: 44), radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
        drawText(itemLabel, in: NSRect(x: 346, y: 373, width: 110, height: 18), size: 13, weight: .semibold, color: DesignV2SettingsPalette.secondary)
    }

    private func drawContentMatrix(title: String, rows: [ContentMatrixRow], rect: NSRect) {
        drawPanel(rect, title: title)
        drawText("Item", in: NSRect(x: 328, y: rect.minY + 58, width: 110, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        for (index, session) in ["Test", "Practice", "Qualifying", "Race"].enumerated() {
            drawText(session, in: NSRect(x: 454 + CGFloat(index) * 116, y: rect.minY + 58, width: 104, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        }

        let rowHeight: CGFloat = 24
        let rowGap: CGFloat = 5
        let rowWidth: CGFloat = 768
        for (index, row) in rows.enumerated() {
            let rowY = rect.minY + 78 + CGFloat(index) * (rowHeight + rowGap)
            guard rowY + rowHeight <= rect.maxY - 10 else {
                break
            }

            fillRounded(NSRect(x: 328, y: rowY, width: rowWidth, height: rowHeight), radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
            strokeRounded(NSRect(x: 328, y: rowY, width: rowWidth, height: rowHeight), radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
            drawText(row.label, in: NSRect(x: 346, y: rowY + 5, width: 110, height: 16), size: 12, weight: .semibold, color: row.enabled ? DesignV2SettingsPalette.secondary : DesignV2SettingsPalette.dim)

            for (sessionIndex, enabled) in situationStates(rowEnabled: row.enabled).enumerated() {
                drawSituationBox(
                    in: NSRect(x: 462 + CGFloat(sessionIndex) * 116, y: rowY + 3, width: 22, height: 18),
                    enabled: enabled
                )
            }
        }
    }

    private func drawSituationBox(in rect: NSRect, enabled: Bool) {
        fillRounded(
            rect,
            radius: 5,
            color: enabled ? NSColor(red255: 6, green: 46, blue: 55) : DesignV2SettingsPalette.panelRaised
        )
        strokeRounded(
            rect,
            radius: 5,
            color: enabled ? DesignV2SettingsPalette.cyan : DesignV2SettingsPalette.border,
            lineWidth: 1
        )
        if enabled {
            fillRounded(NSRect(x: rect.minX + 5, y: rect.minY + 7, width: rect.width - 10, height: 4), radius: 2, color: DesignV2SettingsPalette.green)
        }
    }

    private func situationStates(rowEnabled: Bool) -> [Bool] {
        guard rowEnabled else {
            return [false, false, false, false]
        }

        if definition.id == "gap-to-leader" {
            return [false, false, false, true]
        }

        guard definition.showSessionFilters else {
            return [true, true, true, true]
        }

        return [
            overlay.showInTest,
            overlay.showInPractice,
            overlay.showInQualifying,
            overlay.showInRace
        ]
    }

    private func columnContentRows(definition contentDefinition: OverlayContentDefinition) -> [ContentMatrixRow] {
        OverlayContentColumns.columnStates(for: contentDefinition, settings: overlay).map {
            ContentMatrixRow(label: $0.definition.label, enabled: $0.enabled)
        }
    }

    private func standingsContentRows() -> [ContentMatrixRow] {
        columnContentRows(definition: OverlayContentColumns.standings)
    }

    private func drawBrowserSourcePanel(summary: String) {
        drawPanel(NSRect(x: 306, y: 518, width: 834, height: 70), title: "Browser Source")
        let route = localhostURLText()
        fillRounded(NSRect(x: 462, y: 542, width: 470, height: 30), radius: 8, color: NSColor(red255: 4, green: 9, blue: 20))
        strokeRounded(NSRect(x: 462, y: 542, width: 470, height: 30), radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
        drawText(route, in: NSRect(x: 478, y: 550, width: 430, height: 18), size: 12, color: NSColor(red255: 159, green: 220, blue: 255), monospaced: true)
        drawText(summary, in: NSRect(x: 328, y: 594, width: 620, height: 18), size: 11, color: DesignV2SettingsPalette.dim)
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

    private func drawAspectFit(_ image: NSImage, in rect: NSRect) {
        settingsRenderer.drawAspectFit(image, in: rect)
    }

    private func localhostURLText() -> String {
        BrowserOverlayCatalog.route(for: definition.id).map { "http://localhost:8765\($0)" } ?? "No localhost route"
    }

    private var browserSummary: String {
        let browserSize = OverlayContentColumns.recommendedBrowserSize(overlay: definition, settings: overlay)
        return "OBS browser size \(Int(browserSize.width)) x \(Int(browserSize.height)); native visibility is controlled separately."
    }

    private var previewImage: NSImage? {
        DesignV2SettingsReferenceImages.load(relativePath: "mocks/application-redesign/overlays/\(definition.id).png")
    }

    private func optionBool(key: String, defaultValue: Bool) -> Bool {
        guard let configured = overlay.options[key]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
            return defaultValue
        }
        switch configured {
        case "true", "1", "yes", "on":
            return true
        case "false", "0", "no", "off":
            return false
        default:
            return defaultValue
        }
    }

    private func closestPercent(_ value: Double, allowedValues: [Int]) -> Int {
        let percent = Int((value * 100).rounded())
        return allowedValues.min(by: { abs($0 - percent) < abs($1 - percent) }) ?? 100
    }

    private func selectedInt(_ sender: NSPopUpButton) -> Int? {
        guard let selected = sender.selectedItem?.title else {
            return nil
        }
        return Int(selected)
    }

    private func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
    }

    private static let headerChromeKeys = [
        "chrome.header.status.test",
        "chrome.header.status.practice",
        "chrome.header.status.qualifying",
        "chrome.header.status.race"
    ]

    private static let footerChromeKeys = [
        "chrome.footer.source.test",
        "chrome.footer.source.practice",
        "chrome.footer.source.qualifying",
        "chrome.footer.source.race"
    ]
}
