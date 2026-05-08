import AppKit

final class DesignV2OverlaySettingsFullView: NSView, NSTextFieldDelegate {
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
    private var availableRegions: [DesignV2SettingsRegion] {
        DesignV2SettingsOverlaySpecs.regions(for: definition.id)
    }
    private var availableSegments: [DesignV2SettingsSegment] {
        DesignV2SettingsOverlaySpecs.segments(for: definition.id)
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
        if let initialRegion = Self.initialRegionFromEnvironment(),
           DesignV2SettingsOverlaySpecs.regions(for: definition.id).contains(initialRegion) {
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
        for button in regionButtons.values {
            button.isHidden = true
        }
        for (segment, rect) in DesignV2SettingsChrome.segmentFrames(for: availableSegments) {
            guard let region = DesignV2SettingsRegion(rawValue: segment.id) else {
                continue
            }
            regionButtons[region]?.frame = rect
            regionButtons[region]?.isHidden = false
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
        settingsRenderer.drawSegments(availableSegments, selectedId: selectedRegion.rawValue)
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
        guard availableRegions.contains(region) else {
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

        if definition.id == "garage-cover" {
            addDynamic(DesignV2SettingsActionButtonControl(
                frame: NSRect(x: 750, y: 428, width: 112, height: 30),
                title: "Import",
                font: font(size: 12, weight: .heavy),
                onClick: { [weak self] in
                    self?.importGarageCoverImage()
                }
            ))
            addDynamic(DesignV2SettingsActionButtonControl(
                frame: NSRect(x: 876, y: 428, width: 86, height: 30),
                title: "Clear",
                font: font(size: 12, weight: .heavy),
                onClick: { [weak self] in
                    self?.clearGarageCoverImage()
                }
            ))
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
            addColumnToggleControls(definition: OverlayContentColumns.relative, rect: NSRect(x: 306, y: 272, width: 834, height: 222))
            addDynamic(DesignV2SettingsStepperControl(
                frame: NSRect(x: 454, y: 562, width: 220, height: 38),
                value: min(max(max(overlay.relativeCarsAhead, overlay.relativeCarsBehind), 0), 8),
                minimum: 0,
                maximum: 8,
                valueLabel: { "\($0) each side" },
                font: font(size: 12, weight: .heavy),
                onChange: { [weak self] value in
                    self?.overlay.relativeCarsAhead = value
                    self?.overlay.relativeCarsBehind = value
                    self?.saveOverlay()
                }
            ))
        case "standings":
            addColumnToggleControls(
                definition: OverlayContentColumns.standings,
                rect: NSRect(x: 306, y: 272, width: 834, height: 236),
                rowHeight: 22,
                rowGap: 3
            )
            if let block = OverlayContentColumns.standings.blocks.first {
                addDynamic(DesignV2SettingsCheckControl(
                    frame: NSRect(x: 328, y: 582, width: 190, height: 20),
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
                    addDynamic(DesignV2SettingsStepperControl(
                        frame: NSRect(x: 552, y: 572, width: 220, height: 38),
                        value: OverlayContentColumns.blockCount(block, settings: overlay),
                        minimum: block.minimumCount,
                        maximum: block.maximumCount,
                        valueLabel: { "\($0) other-class rows" },
                        font: font(size: 12, weight: .heavy),
                        onChange: { [weak self] value in
                            self?.overlay.options[key] = String(value)
                            self?.saveOverlay()
                        }
                    ))
                }
            }
        case "gap-to-leader":
            addDynamic(DesignV2SettingsStepperControl(
                frame: NSRect(x: 454, y: 484, width: 220, height: 38),
                value: min(max(max(overlay.classGapCarsAhead, overlay.classGapCarsBehind), 0), 12),
                minimum: 0,
                maximum: 12,
                valueLabel: { "\($0) each side" },
                font: font(size: 12, weight: .heavy),
                onChange: { [weak self] value in
                    self?.overlay.classGapCarsAhead = value
                    self?.overlay.classGapCarsBehind = value
                    self?.saveOverlay()
                }
            ))
        case "fuel-calculator":
            addMatrixCheckControl(
                rowIndex: 0,
                rect: NSRect(x: 306, y: 272, width: 834, height: 150),
                isOn: overlay.showFuelAdvice,
                onChange: { [weak self] isOn in
                    self?.overlay.showFuelAdvice = isOn
                    self?.saveOverlay()
                }
            )
            addMatrixCheckControl(
                rowIndex: 1,
                rect: NSRect(x: 306, y: 272, width: 834, height: 150),
                isOn: overlay.showFuelSource,
                onChange: { [weak self] isOn in
                    self?.overlay.showFuelSource = isOn
                    self?.saveOverlay()
                }
            )
        case "track-map":
            addMatrixCheckControl(
                rowIndex: 1,
                rect: NSRect(x: 306, y: 272, width: 834, height: 190),
                isOn: optionBool(key: "track-map.sector-boundaries.enabled", defaultValue: true),
                onChange: { [weak self] isOn in
                    self?.overlay.options["track-map.sector-boundaries.enabled"] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            )
            addMatrixCheckControl(
                rowIndex: 2,
                rect: NSRect(x: 306, y: 272, width: 834, height: 190),
                isOn: overlay.trackMapBuildFromTelemetry,
                onChange: { [weak self] isOn in
                    self?.overlay.trackMapBuildFromTelemetry = isOn
                    self?.saveOverlay()
                }
            )
        case "input-state":
            addContentBlockToggleControls(
                definition: OverlayContentColumns.inputState,
                rect: NSRect(x: 306, y: 272, width: 834, height: 236),
                rowHeight: 22,
                rowGap: 3
            )
        case "car-radar":
            addMatrixCheckControl(
                rowIndex: 1,
                rect: NSRect(x: 306, y: 272, width: 834, height: 150),
                isOn: overlay.showRadarMulticlassWarning,
                onChange: { [weak self] isOn in
                    self?.overlay.showRadarMulticlassWarning = isOn
                    self?.saveOverlay()
                }
            )
        case "flags":
            addFlagDisplayControls()
        case "stream-chat":
            addDynamic(DesignV2SettingsToggleControl(
                frame: NSRect(x: 454, y: 330, width: 56, height: 28),
                isOn: overlay.enabled,
                theme: theme,
                onChange: { [weak self] isOn in
                    self?.overlay.enabled = isOn
                    self?.saveOverlay()
                }
            ))
            addStreamChatControls()
            addDynamic(DesignV2SettingsActionButtonControl(
                frame: NSRect(x: 950, y: 552, width: 70, height: 30),
                title: "Copy",
                font: font(size: 12, weight: .heavy),
                onClick: { [weak self] in
                    self?.copyLocalhostURL()
                }
            ))
        default:
            break
        }
    }

    private func addFlagDisplayControls() {
        let flags: [(WritableKeyPath<OverlaySettings, Bool>, Bool)] = [
            (\.flagsShowGreen, overlay.flagsShowGreen),
            (\.flagsShowBlue, overlay.flagsShowBlue),
            (\.flagsShowYellow, overlay.flagsShowYellow),
            (\.flagsShowCritical, overlay.flagsShowCritical),
            (\.flagsShowFinish, overlay.flagsShowFinish)
        ]
        for (index, flag) in flags.enumerated() {
            addMatrixCheckControl(
                rowIndex: index,
                rect: NSRect(x: 306, y: 272, width: 834, height: 240),
                isOn: flag.1,
                onChange: { [weak self] isOn in
                    self?.overlay[keyPath: flag.0] = isOn
                    self?.saveOverlay()
                }
            )
        }

        addDynamic(sizeField(
            frame: NSRect(x: 374, y: 578, width: 64, height: 26),
            value: Int(FlagsOverlayDefinition.resolveSize(overlay).width.rounded()),
            identifier: "flagsWidth"
        ))
        addDynamic(sizeField(
            frame: NSRect(x: 488, y: 578, width: 64, height: 26),
            value: Int(FlagsOverlayDefinition.resolveSize(overlay).height.rounded()),
            identifier: "flagsHeight"
        ))
    }

    private func addStreamChatControls() {
        addDynamic(DesignV2SettingsChoiceControl(
            frame: NSRect(x: 454, y: 366, width: 258, height: 30),
            options: StreamChatProviderOptions.compactLabels,
            selected: StreamChatProviderOptions.compactLabel(for: overlay.streamChatProvider),
            font: font(size: 11, weight: .heavy),
            onChange: { [weak self] selected in
                guard let self else {
                    return
                }
                overlay.streamChatProvider = StreamChatProviderOptions.normalize(selected)
                saveOverlay()
                rebuildRegionControls()
            }
        ))
        addDynamic(textField(
            frame: NSRect(x: 454, y: 404, width: 420, height: 28),
            value: overlay.streamChatStreamlabsUrl,
            identifier: "streamChatStreamlabsUrl",
            enabled: StreamChatProviderOptions.normalize(overlay.streamChatProvider) == "streamlabs"
        ))
        addDynamic(textField(
            frame: NSRect(x: 454, y: 442, width: 210, height: 28),
            value: overlay.streamChatTwitchChannel,
            identifier: "streamChatTwitchChannel",
            enabled: StreamChatProviderOptions.normalize(overlay.streamChatProvider) == "twitch"
        ))
        addDynamic(DesignV2SettingsActionButtonControl(
            frame: NSRect(x: 682, y: 440, width: 92, height: 30),
            title: "Save",
            font: font(size: 12, weight: .heavy),
            onClick: { [weak self] in
                self?.saveStreamChatFields()
            }
        ))
    }

    private func addColumnToggleControls(
        definition contentDefinition: OverlayContentDefinition,
        rect: NSRect,
        rowHeight: CGFloat = 24,
        rowGap: CGFloat = 5
    ) {
        for (index, state) in OverlayContentColumns.columnStates(for: contentDefinition, settings: overlay).enumerated() {
            addMatrixCheckControl(
                rowIndex: index,
                rect: rect,
                rowHeight: rowHeight,
                rowGap: rowGap,
                isOn: state.enabled,
                onChange: { [weak self] isOn in
                    self?.overlay.options[state.definition.enabledKey(overlayId: contentDefinition.overlayId)] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            )
        }
    }

    private func addContentBlockToggleControls(
        definition contentDefinition: OverlayContentDefinition,
        rect: NSRect,
        rowHeight: CGFloat = 24,
        rowGap: CGFloat = 5
    ) {
        for (index, block) in contentDefinition.blocks.enumerated() {
            addMatrixCheckControl(
                rowIndex: index,
                rect: rect,
                rowHeight: rowHeight,
                rowGap: rowGap,
                isOn: OverlayContentColumns.blockEnabled(block, settings: overlay),
                onChange: { [weak self] isOn in
                    self?.overlay.options[block.enabledOptionKey] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            )
        }
    }

    private func addMatrixCheckControl(
        rowIndex: Int,
        rect: NSRect,
        rowHeight: CGFloat = 24,
        rowGap: CGFloat = 5,
        isOn: Bool,
        onChange: @escaping (Bool) -> Void
    ) {
        addDynamic(DesignV2SettingsCheckControl(
            frame: matrixCheckFrame(rowIndex: rowIndex, rect: rect, rowHeight: rowHeight, rowGap: rowGap),
            title: "",
            isOn: isOn,
            theme: theme,
            font: font(size: 12, weight: .semibold),
            onChange: onChange
        ))
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

    private func sizeField(frame: NSRect, value: Int, identifier: String) -> NSTextField {
        let field = NSTextField(string: String(value))
        field.frame = frame
        field.identifier = NSUserInterfaceItemIdentifier(identifier)
        field.delegate = self
        field.target = self
        field.action = #selector(sizeFieldChanged(_:))
        field.alignment = .right
        field.font = OverlayTheme.monospacedFont(size: 12, weight: .semibold)
        field.textColor = DesignV2SettingsPalette.text
        field.backgroundColor = NSColor(red255: 4, green: 9, blue: 20)
        field.isBordered = true
        return field
    }

    private func textField(frame: NSRect, value: String, identifier: String, enabled: Bool) -> NSTextField {
        let field = NSTextField(string: value)
        field.frame = frame
        field.identifier = NSUserInterfaceItemIdentifier(identifier)
        field.font = font(size: 12)
        field.textColor = enabled ? DesignV2SettingsPalette.text : DesignV2SettingsPalette.dim
        field.backgroundColor = NSColor(red255: 4, green: 9, blue: 20)
        field.isBordered = true
        field.isEnabled = enabled
        field.maximumNumberOfLines = 1
        field.lineBreakMode = .byTruncatingMiddle
        field.cell?.lineBreakMode = .byTruncatingMiddle
        return field
    }

    private func addDynamic(_ view: NSView) {
        dynamicControls.append(view)
        addSubview(view)
    }

    @objc private func sizeFieldChanged(_ sender: NSTextField) {
        applySizeField(sender)
    }

    func controlTextDidEndEditing(_ obj: Notification) {
        guard let field = obj.object as? NSTextField else {
            return
        }

        applySizeField(field)
    }

    private func applySizeField(_ field: NSTextField) {
        guard definition.id == "flags",
              let identifier = field.identifier?.rawValue,
              let parsed = Int(field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)) else {
            return
        }

        switch identifier {
        case "flagsWidth":
            overlay.width = Double(min(max(parsed, Int(FlagsOverlayDefinition.minimumWidth)), Int(FlagsOverlayDefinition.maximumWidth)))
            field.stringValue = String(Int(overlay.width.rounded()))
        case "flagsHeight":
            overlay.height = Double(min(max(parsed, Int(FlagsOverlayDefinition.minimumHeight)), Int(FlagsOverlayDefinition.maximumHeight)))
            field.stringValue = String(Int(overlay.height.rounded()))
        default:
            return
        }
        overlay.screenId = nil
        saveOverlay()
    }

    private func saveStreamChatFields() {
        guard definition.id == "stream-chat" else {
            return
        }

        overlay.streamChatStreamlabsUrl = textFieldValue(identifier: "streamChatStreamlabsUrl")
        overlay.streamChatTwitchChannel = textFieldValue(identifier: "streamChatTwitchChannel")
        saveOverlay()
    }

    private func importGarageCoverImage() {
        guard definition.id == "garage-cover" else {
            return
        }

        let panel = NSOpenPanel()
        GarageCoverImageStore.configureImportPanel(panel)

        guard panel.runModal() == .OK, let sourceURL = panel.url else {
            return
        }

        do {
            overlay.garageCoverImagePath = try GarageCoverImageStore.copyImage(from: sourceURL).path
            saveOverlay()
            rebuildRegionControls()
        } catch {
            NSSound.beep()
        }
    }

    private func clearGarageCoverImage() {
        guard definition.id == "garage-cover" else {
            return
        }

        overlay.garageCoverImagePath = ""
        GarageCoverImageStore.clearImportedImages()
        saveOverlay()
        rebuildRegionControls()
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

        let previewTitle = definition.id == "garage-cover" ? "Cover Image" : "\(definition.displayName) Preview"
        drawPanel(NSRect(x: 726, y: 272, width: 414, height: 226), title: previewTitle)
        let previewRect = definition.id == "garage-cover"
            ? NSRect(x: 750, y: 324, width: 366, height: 96)
            : NSRect(x: 750, y: 324, width: 366, height: 132)
        fillRounded(previewRect, radius: 10, color: NSColor(red255: 3, green: 8, blue: 18))
        strokeRounded(previewRect, radius: 10, color: DesignV2SettingsPalette.cyan.withAlphaComponent(0.65), lineWidth: 1)
        if let image = definition.id == "garage-cover" ? garageCoverImage : previewImage {
            drawAspectFit(image, in: previewRect.insetBy(dx: 12, dy: 10))
        }
        if definition.id == "garage-cover" {
            drawText("Image", in: NSRect(x: 750, y: 468, width: 60, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
            drawText(garageCoverImageLabel, in: NSRect(x: 814, y: 468, width: 292, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.secondary)
        } else {
            drawText("Default size", in: NSRect(x: 750, y: 468, width: 100, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
            drawText(
                "\(Int(definition.defaultSize.width)) x \(Int(definition.defaultSize.height))",
                in: NSRect(x: 852, y: 468, width: 120, height: 18),
                size: 12,
                weight: .bold,
                color: DesignV2SettingsPalette.secondary,
                monospaced: true
            )
        }

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
        case "stream-chat":
            drawStreamChatContentRegion()
        case "input-state":
            drawInputStateContentRegion()
        case "car-radar":
            drawCarRadarContentRegion()
        case "flags":
            drawFlagsContentRegion()
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
        drawText("\(overlay.relativeCarsAhead + overlay.relativeCarsBehind + 1) visible rows", in: NSRect(x: 700, y: 574, width: 112, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
    }

    private func drawStandingsContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: standingsContentRows(),
            rect: NSRect(x: 306, y: 272, width: 834, height: 236),
            rowHeight: 22,
            rowGap: 3
        )
        if !OverlayContentColumns.standings.blocks.isEmpty {
            drawPanel(NSRect(x: 306, y: 520, width: 834, height: 102), title: "Class Separators")
        }
    }

    private func drawGapContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [ContentMatrixRow(label: "Class gap window", enabled: true)],
            rect: NSRect(x: 306, y: 272, width: 834, height: 126)
        )
        drawPanel(NSRect(x: 306, y: 426, width: 834, height: 126), title: "Class Gap Window")
        drawText("Cars each side", in: NSRect(x: 328, y: 498, width: 130, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
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

        drawPanel(NSRect(x: 306, y: 484, width: 834, height: 110), title: "Map Sources")
        drawText("Source", in: NSRect(x: 328, y: 542, width: 90, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Best bundled or local map; circle fallback", in: NSRect(x: 454, y: 542, width: 320, height: 18), size: 12, color: DesignV2SettingsPalette.text)
        drawText("Local maps", in: NSRect(x: 328, y: 574, width: 110, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Reviewed app maps load automatically for matching tracks.", in: NSRect(x: 454, y: 574, width: 390, height: 18), size: 12, color: DesignV2SettingsPalette.secondary)
    }

    private func drawStreamChatContentRegion() {
        drawPanel(NSRect(x: 306, y: 272, width: 834, height: 204), title: "Chat Source")
        drawText("Visible", in: NSRect(x: 328, y: 336, width: 90, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Mode", in: NSRect(x: 328, y: 374, width: 90, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Streamlabs URL", in: NSRect(x: 328, y: 412, width: 120, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Twitch channel", in: NSRect(x: 328, y: 450, width: 120, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)

        drawPanel(NSRect(x: 306, y: 500, width: 834, height: 92), title: "Localhost")
        fillRounded(NSRect(x: 462, y: 552, width: 470, height: 30), radius: 8, color: NSColor(red255: 4, green: 9, blue: 20))
        strokeRounded(NSRect(x: 462, y: 552, width: 470, height: 30), radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
        drawText(localhostURLText(), in: NSRect(x: 478, y: 560, width: 430, height: 18), size: 12, color: NSColor(red255: 159, green: 220, blue: 255), monospaced: true)
        drawText("OBS browser source", in: NSRect(x: 328, y: 560, width: 120, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
    }

    private func drawInputStateContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: contentBlockRows(definition: OverlayContentColumns.inputState),
            rect: NSRect(x: 306, y: 272, width: 834, height: 236),
            rowHeight: 22,
            rowGap: 3
        )
    }

    private func drawCarRadarContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Radar proximity", enabled: true),
                ContentMatrixRow(label: "Multiclass warning", enabled: overlay.showRadarMulticlassWarning)
            ],
            rect: NSRect(x: 306, y: 272, width: 834, height: 150)
        )
    }

    private func drawFlagsContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Green", enabled: overlay.flagsShowGreen),
                ContentMatrixRow(label: "Blue", enabled: overlay.flagsShowBlue),
                ContentMatrixRow(label: "Yellow", enabled: overlay.flagsShowYellow),
                ContentMatrixRow(label: "Red / black", enabled: overlay.flagsShowCritical),
                ContentMatrixRow(label: "White / checkered", enabled: overlay.flagsShowFinish)
            ],
            rect: NSRect(x: 306, y: 272, width: 834, height: 240)
        )
        drawPanel(NSRect(x: 306, y: 528, width: 414, height: 90), title: "Size")
        drawText("W", in: NSRect(x: 342, y: 586, width: 18, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.muted)
        drawText("H", in: NSRect(x: 456, y: 586, width: 18, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.muted)
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

    private func drawContentMatrix(
        title: String,
        rows: [ContentMatrixRow],
        rect: NSRect,
        rowHeight: CGFloat = 24,
        rowGap: CGFloat = 5
    ) {
        drawPanel(rect, title: title)
        drawText("Item", in: NSRect(x: 328, y: rect.minY + 58, width: 110, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        for (index, session) in ["Test", "Practice", "Qualifying", "Race"].enumerated() {
            drawText(session, in: NSRect(x: 548 + CGFloat(index) * 116, y: rect.minY + 58, width: 104, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        }

        let rowWidth: CGFloat = 768
        for (index, row) in rows.enumerated() {
            let rowY = rect.minY + 78 + CGFloat(index) * (rowHeight + rowGap)
            guard rowY + rowHeight <= rect.maxY - 10 else {
                break
            }

            fillRounded(NSRect(x: 328, y: rowY, width: rowWidth, height: rowHeight), radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
            strokeRounded(NSRect(x: 328, y: rowY, width: rowWidth, height: rowHeight), radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
            drawCheckBox(in: matrixCheckFrame(rowIndex: index, rect: rect, rowHeight: rowHeight, rowGap: rowGap), checked: row.enabled)
            drawText(row.label, in: NSRect(x: 376, y: rowY + 5, width: 150, height: 16), size: 12, weight: .semibold, color: row.enabled ? DesignV2SettingsPalette.secondary : DesignV2SettingsPalette.dim)

            for (sessionIndex, enabled) in situationStates(rowEnabled: row.enabled).enumerated() {
                drawSituationBox(
                    in: NSRect(x: 556 + CGFloat(sessionIndex) * 116, y: rowY + 3, width: 22, height: 18),
                    enabled: enabled
                )
            }
        }
    }

    private func matrixCheckFrame(rowIndex: Int, rect: NSRect, rowHeight: CGFloat = 24, rowGap: CGFloat = 5) -> NSRect {
        let rowY = rect.minY + 78 + CGFloat(rowIndex) * (rowHeight + rowGap)
        return NSRect(x: 344, y: rowY + max(2, (rowHeight - 19) / 2), width: 19, height: 19)
    }

    private func drawSituationBox(in rect: NSRect, enabled: Bool) {
        drawCheckBox(in: rect, checked: enabled)
    }

    private func drawCheckBox(in rect: NSRect, checked: Bool) {
        fillRounded(
            rect,
            radius: 5,
            color: checked ? NSColor(red255: 6, green: 46, blue: 55) : DesignV2SettingsPalette.panelRaised
        )
        strokeRounded(
            rect,
            radius: 5,
            color: checked ? DesignV2SettingsPalette.cyan : DesignV2SettingsPalette.border,
            lineWidth: 1
        )
        if checked {
            DesignV2Drawing.line(from: NSPoint(x: rect.minX + 5, y: rect.minY + 10), to: NSPoint(x: rect.minX + 9, y: rect.minY + 15), color: DesignV2SettingsPalette.green, width: 2)
            DesignV2Drawing.line(from: NSPoint(x: rect.minX + 9, y: rect.minY + 15), to: NSPoint(x: rect.minX + 16, y: rect.minY + 6), color: DesignV2SettingsPalette.green, width: 2)
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

    private func contentBlockRows(definition contentDefinition: OverlayContentDefinition) -> [ContentMatrixRow] {
        contentDefinition.blocks.map {
            ContentMatrixRow(label: $0.label, enabled: OverlayContentColumns.blockEnabled($0, settings: overlay))
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

    private var garageCoverImage: NSImage? {
        if !overlay.garageCoverImagePath.isEmpty,
           let image = NSImage(contentsOfFile: overlay.garageCoverImagePath) {
            return image
        }

        return previewImage ?? TmrBrandAssets.loadLogoImage()
    }

    private var garageCoverImageLabel: String {
        guard !overlay.garageCoverImagePath.isEmpty else {
            return "No image imported"
        }

        return URL(fileURLWithPath: overlay.garageCoverImagePath).lastPathComponent
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

    private func textFieldValue(identifier: String) -> String {
        dynamicControls.compactMap { $0 as? NSTextField }.first {
            $0.identifier?.rawValue == identifier
        }?.stringValue.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
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
