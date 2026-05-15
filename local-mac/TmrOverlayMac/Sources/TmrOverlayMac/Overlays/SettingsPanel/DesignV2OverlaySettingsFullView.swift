import AppKit

final class DesignV2OverlaySettingsFullView: NSView, NSTextFieldDelegate {
    private static let matrixControlContentWidth: CGFloat = 768
    private static let matrixControlLabelWidth: CGFloat = 244
    private static let matrixControlVisibleWidth: CGFloat = 104
    private static let matrixControlGap: CGFloat = 12
    private static let matrixControlCellPadding: CGFloat = 12
    private static let matrixControlStepperWidth: CGFloat = 220

    private struct ContentMatrixRow {
        var label: String
        var enabled: Bool
    }

    private struct ChromeSettingsRow {
        var label: String
        var keys: [String]
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
        alignBoundsToMatchedWindow()
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

    func selectRegion(identifier: String) {
        guard let region = DesignV2SettingsRegion(rawValue: identifier),
              availableRegions.contains(region) else {
            return
        }

        selectedRegion = region
        rebuildRegionControls()
        needsDisplay = true
    }

    override func layout() {
        super.layout()
        alignBoundsToMatchedWindow()
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

    private func alignBoundsToMatchedWindow() {
        bounds = NSRect(origin: DesignV2SettingsChrome.matchedWindowBoundsOrigin, size: frame.size)
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
            buildChromeControls(rows: Self.headerChromeRows)
        case .footer:
            buildChromeControls(rows: Self.footerChromeRows(for: definition.id))
        case .preview:
            break
        case .twitch:
            addStreamChatMetadataControls()
        case .streamlabs:
            break
        }
    }

    private func buildGeneralControls() {
        let isGarageCover = definition.id == "garage-cover"
        if !isGarageCover {
            addDynamic(DesignV2SettingsToggleControl(
                frame: NSRect(x: 600, y: 328, width: 56, height: 28),
                isOn: overlay.enabled,
                theme: theme,
                onChange: { [weak self] isOn in
                    self?.overlay.enabled = isOn
                    self?.saveOverlay()
                }
            ))
        }

        if definition.showScaleControl {
            addDynamic(DesignV2SettingsPercentSliderControl(
                frame: NSRect(x: 454, y: isGarageCover ? 328 : 368, width: 180, height: 28),
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

        if isGarageCover {
            addDynamic(DesignV2SettingsActionButtonControl(
                frame: NSRect(x: 454, y: 368, width: 112, height: 30),
                title: "Import",
                font: font(size: 12, weight: .heavy),
                onClick: { [weak self] in
                    self?.importGarageCoverImage()
                }
            ))
            addDynamic(DesignV2SettingsActionButtonControl(
                frame: NSRect(x: 580, y: 368, width: 86, height: 30),
                title: "Clear",
                font: font(size: 12, weight: .heavy),
                onClick: { [weak self] in
                    self?.clearGarageCoverImage()
                }
            ))
        }

        if definition.showOpacityControl {
            addDynamic(DesignV2SettingsPercentSliderControl(
                frame: NSRect(x: 454, y: 408, width: 180, height: 28),
                value: closestPercent(overlay.opacity, allowedValues: [20, 30, 40, 50, 60, 70, 80, 90, 100]),
                allowedValues: [20, 30, 40, 50, 60, 70, 80, 90, 100],
                activeColor: theme.colors.accentSecondary,
                theme: theme,
                onChange: { [weak self] percent in
                    self?.overlay.opacity = min(max(Double(percent) / 100.0, 0.2), 1.0)
                    self?.saveOverlay()
                }
            ))
        }

        addDynamic(DesignV2SettingsActionButtonControl(
            frame: NSRect(x: 1048, y: 382, width: 70, height: 30),
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
            let relativeRect = NSRect(x: 306, y: 272, width: 834, height: 280)
            let relativeRows = columnContentRows(definition: OverlayContentColumns.relative)
            addColumnToggleControls(definition: OverlayContentColumns.relative, rect: relativeRect)
            addDynamic(DesignV2SettingsStepperControl(
                frame: matrixControlCountStepperFrame(rect: relativeRect, rowIndex: relativeRows.count),
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
                rect: NSRect(x: 306, y: 272, width: 834, height: 344),
                rowHeight: 22,
                rowGap: 3
            )
            if let block = OverlayContentColumns.standings.blocks.first {
                let standingsRect = NSRect(x: 306, y: 272, width: 834, height: 344)
                let rowIndex = standingsContentRows().count
                addDynamic(DesignV2SettingsCheckControl(
                    frame: matrixControlVisibleCheckFrame(rect: standingsRect, rowIndex: rowIndex, precedingRowHeight: 22, precedingRowGap: 3),
                    title: "",
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
                        frame: matrixControlCountStepperFrame(rect: standingsRect, rowIndex: rowIndex, precedingRowHeight: 22, precedingRowGap: 3, hasVisibleColumn: true),
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
            let gapRect = NSRect(x: 306, y: 272, width: 834, height: 126)
            addDynamic(DesignV2SettingsStepperControl(
                frame: matrixControlCountStepperFrame(rect: gapRect, rowIndex: 0, followsMatrixRows: false),
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
        case "track-map":
            addMatrixCheckControl(
                rowIndex: 0,
                rect: NSRect(x: 306, y: 272, width: 834, height: 150),
                isOn: optionBool(key: "track-map.sector-boundaries.enabled", defaultValue: true),
                onChange: { [weak self] isOn in
                    self?.overlay.options["track-map.sector-boundaries.enabled"] = isOn ? "true" : "false"
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
        case "session-weather":
            addContentBlockGridToggleControls(
                definition: OverlayContentColumns.sessionWeather,
                rect: NSRect(x: 306, y: 272, width: 834, height: 344),
                columns: 2,
                rowHeight: 16,
                rowGap: 2
            )
        case "pit-service":
            addContentBlockGridToggleControls(
                definition: OverlayContentColumns.pitService,
                rect: NSRect(x: 306, y: 272, width: 834, height: 344),
                columns: 2,
                rowHeight: 18,
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
            addStreamChatControls()
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
            frame: NSRect(x: 454, y: 330, width: 258, height: 30),
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
            frame: NSRect(x: 454, y: 368, width: 420, height: 28),
            value: overlay.streamChatStreamlabsUrl,
            identifier: "streamChatStreamlabsUrl",
            enabled: StreamChatProviderOptions.normalize(overlay.streamChatProvider) == "streamlabs"
        ))
        addDynamic(textField(
            frame: NSRect(x: 454, y: 406, width: 210, height: 28),
            value: overlay.streamChatTwitchChannel,
            identifier: "streamChatTwitchChannel",
            enabled: StreamChatProviderOptions.normalize(overlay.streamChatProvider) == "twitch"
        ))
        addDynamic(DesignV2SettingsActionButtonControl(
            frame: NSRect(x: 682, y: 404, width: 92, height: 30),
            title: "Save",
            font: font(size: 12, weight: .heavy),
            onClick: { [weak self] in
                self?.saveStreamChatFields()
            }
        ))
    }

    private func addStreamChatMetadataControls() {
        let rect = NSRect(x: 306, y: 272, width: 834, height: 200)
        let columns = 2
        let rowHeight: CGFloat = 16
        let rowGap: CGFloat = 2
        let rowsPerColumn = Int(ceil(Double(OverlayContentColumns.streamChat.blocks.count) / Double(columns)))
        let columnGap: CGFloat = 18
        let contentLeft = rect.minX + 22
        let columnWidth = (rect.width - 44 - columnGap * CGFloat(columns - 1)) / CGFloat(columns)
        for (index, block) in OverlayContentColumns.streamChat.blocks.enumerated() {
            let column = index / max(1, rowsPerColumn)
            let row = index % max(1, rowsPerColumn)
            let rowX = contentLeft + CGFloat(column) * (columnWidth + columnGap)
            let rowY = rect.minY + 78 + CGFloat(row) * (rowHeight + rowGap)
            addDynamic(DesignV2SettingsCheckControl(
                frame: NSRect(x: rowX + 10, y: rowY + max(1, (rowHeight - 19) / 2), width: columnWidth - 20, height: 22),
                title: block.label,
                isOn: OverlayContentColumns.blockEnabled(block, settings: overlay),
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.options[block.enabledOptionKey] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            ))
        }
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

    private func addContentBlockGridToggleControls(
        definition contentDefinition: OverlayContentDefinition,
        rect: NSRect,
        columns: Int,
        rowHeight: CGFloat,
        rowGap: CGFloat
    ) {
        let rowsPerColumn = Int(ceil(Double(contentDefinition.blocks.count) / Double(max(1, columns))))
        for (index, block) in contentDefinition.blocks.enumerated() {
            addDynamic(DesignV2SettingsCheckControl(
                frame: blockGridCheckFrame(index: index, rect: rect, columns: columns, rowsPerColumn: rowsPerColumn, rowHeight: rowHeight, rowGap: rowGap),
                title: "",
                isOn: OverlayContentColumns.blockEnabled(block, settings: overlay),
                theme: theme,
                font: font(size: 12, weight: .semibold),
                onChange: { [weak self] isOn in
                    self?.overlay.options[block.enabledOptionKey] = isOn ? "true" : "false"
                    self?.saveOverlay()
                }
            ))
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

    private func buildChromeControls(rows: [ChromeSettingsRow]) {
        guard DesignV2SettingsOverlaySpecs.supportsSharedChromeSettings(definition.id) else {
            return
        }

        for (rowIndex, row) in rows.enumerated() {
            for (index, key) in chromeDisplayKeys(row.keys).enumerated() {
                let x = 454 + CGFloat(index) * 116
                addDynamic(DesignV2SettingsCheckControl(
                    frame: NSRect(x: x, y: 370 + CGFloat(rowIndex) * 48, width: 38, height: 22),
                    title: "",
                    isOn: optionBool(key: key, defaultValue: true),
                    theme: theme,
                    font: font(size: 12, weight: .semibold),
                    onChange: { [weak self] isOn in
                        self?.overlay.options[key] = isOn ? "true" : "false"
                        if let mirroredTestKey = self?.mirroredTestOptionKey(for: key) {
                            self?.overlay.options[mirroredTestKey] = isOn ? "true" : "false"
                        }
                        self?.saveOverlay()
                    }
                ))
            }
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
            drawChromeRegion(title: "Header", rows: Self.headerChromeRows)
        case .footer:
            drawChromeRegion(title: "Footer", rows: Self.footerChromeRows(for: definition.id))
        case .preview:
            drawGarageCoverPreviewRegion()
        case .twitch:
            drawStreamChatTwitchRegion()
        case .streamlabs:
            drawStreamChatStreamlabsRegion()
        }
    }

    private func drawGeneralRegion() {
        let isGarageCover = definition.id == "garage-cover"
        drawPanel(NSRect(x: 306, y: 272, width: 392, height: isGarageCover ? 166 : 226), title: "Overlay Controls")
        if isGarageCover {
            drawText("Scale", in: NSRect(x: 328, y: 334, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            drawText("\(Int((overlay.scale * 100).rounded()))%", in: NSRect(x: 642, y: 331, width: 40, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
            drawText("Cover image", in: NSRect(x: 328, y: 374, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        } else {
            drawText("Visible", in: NSRect(x: 328, y: 334, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            if definition.showScaleControl {
                drawText("Scale", in: NSRect(x: 328, y: 374, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
                drawText("\(Int((overlay.scale * 100).rounded()))%", in: NSRect(x: 642, y: 371, width: 40, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
            }
            if definition.showOpacityControl {
                drawText(definition.id == "track-map" ? "Map fill" : "Opacity", in: NSRect(x: 328, y: 414, width: 100, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
                drawText("\(Int((overlay.opacity * 100).rounded()))%", in: NSRect(x: 642, y: 411, width: 40, height: 18), size: 12, weight: .bold, color: DesignV2SettingsPalette.text, alignment: .right)
            }
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
        case "session-weather":
            drawSessionWeatherContentRegion()
        case "pit-service":
            drawPitServiceContentRegion()
        case "car-radar":
            drawCarRadarContentRegion()
        case "flags":
            drawFlagsContentRegion()
        default:
            drawEmptyContentRegion()
        }
    }

    private func drawRelativeContentRegion() {
        let relativeRect = NSRect(x: 306, y: 272, width: 834, height: 280)
        let rows = columnContentRows(definition: OverlayContentColumns.relative)
        drawContentMatrix(
            title: "Content Display",
            rows: rows,
            rect: relativeRect
        )
        let eachSide = max(overlay.relativeCarsAhead, overlay.relativeCarsBehind)
        drawMatrixControlRow(label: "Rows around focus", enabled: eachSide > 0, rect: relativeRect, rowIndex: rows.count, countLabel: "Cars each side")
        let labelFrame = matrixControlLabelFrame(rect: relativeRect, rowIndex: rows.count)
        drawText("\(eachSide * 2 + 1) rows", in: NSRect(x: labelFrame.maxX - 86, y: labelFrame.minY + (labelFrame.height - 16) / 2, width: 70, height: 16), size: 11, weight: .bold, color: DesignV2SettingsPalette.muted, alignment: .right)
    }

    private func drawStandingsContentRegion() {
        let standingsRect = NSRect(x: 306, y: 272, width: 834, height: 344)
        let rows = standingsContentRows()
        drawContentMatrix(
            title: "Content Display",
            rows: rows,
            rect: standingsRect,
            rowHeight: 22,
            rowGap: 3
        )
        if let block = OverlayContentColumns.standings.blocks.first {
            drawMatrixControlRow(
                label: block.label,
                enabled: OverlayContentColumns.blockEnabled(block, settings: overlay),
                rect: standingsRect,
                rowIndex: rows.count,
                precedingRowHeight: 22,
                precedingRowGap: 3,
                visibleLabel: "Visible",
                countLabel: block.countLabel ?? "Other-class cars"
            )
            drawCheckBox(in: matrixControlVisibleCheckFrame(rect: standingsRect, rowIndex: rows.count, precedingRowHeight: 22, precedingRowGap: 3), checked: OverlayContentColumns.blockEnabled(block, settings: overlay))
        }
    }

    private func drawGapContentRegion() {
        let gapRect = NSRect(x: 306, y: 272, width: 834, height: 126)
        let eachSide = max(overlay.classGapCarsAhead, overlay.classGapCarsBehind)
        drawPanel(gapRect, title: "Content Display")
        drawMatrixControlRow(
            label: "Class gap window",
            enabled: eachSide > 0,
            rect: gapRect,
            rowIndex: 0,
            countLabel: "Cars each side",
            followsMatrixRows: false
        )
    }

    private func drawFuelContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Advice column", enabled: overlay.showFuelAdvice)
            ],
            rect: NSRect(x: 306, y: 272, width: 834, height: 150)
        )
    }

    private func drawTrackMapContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Sector boundaries", enabled: optionBool(key: "track-map.sector-boundaries.enabled", defaultValue: true))
            ],
            rect: NSRect(x: 306, y: 272, width: 834, height: 150)
        )
    }

    private func drawStreamChatContentRegion() {
        drawPanel(NSRect(x: 306, y: 272, width: 834, height: 170), title: "Chat Source")
        drawText("Mode", in: NSRect(x: 328, y: 336, width: 90, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Streamlabs URL", in: NSRect(x: 328, y: 374, width: 120, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
        drawText("Twitch channel", in: NSRect(x: 328, y: 412, width: 120, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
    }

    private func drawGarageCoverPreviewRegion() {
        let previewRect = NSRect(x: 423, y: 272, width: 600, height: 338)
        fillRounded(previewRect, radius: 10, color: NSColor(red255: 3, green: 8, blue: 18))
        strokeRounded(previewRect, radius: 10, color: DesignV2SettingsPalette.cyan.withAlphaComponent(0.65), lineWidth: 1)
        if let image = garageCoverImage {
            drawAspectFit(image, in: previewRect.insetBy(dx: 12, dy: 10))
        }
    }

    private func drawStreamChatTwitchRegion() {
        drawBlockToggleGrid(
            title: "Twitch Metadata",
            rows: contentBlockRows(definition: OverlayContentColumns.streamChat),
            rect: NSRect(x: 306, y: 272, width: 834, height: 200),
            columns: 2,
            rowHeight: 16,
            rowGap: 2
        )
    }

    private func drawStreamChatStreamlabsRegion() {
        drawPanel(NSRect(x: 306, y: 272, width: 834, height: 150), title: "Streamlabs")
        drawText("No Streamlabs-specific message controls yet.", in: NSRect(x: 328, y: 334, width: 440, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
        drawText("This page is reserved for provider-specific controls after Streamlabs payloads are verified.", in: NSRect(x: 328, y: 362, width: 640, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
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

    private func drawPitServiceContentRegion() {
        drawBlockToggleGrid(
            title: "Pit Service Cells",
            rows: contentBlockRows(definition: OverlayContentColumns.pitService),
            rect: NSRect(x: 306, y: 272, width: 834, height: 344),
            columns: 2,
            rowHeight: 18,
            rowGap: 3
        )
    }

    private func drawSessionWeatherContentRegion() {
        drawBlockToggleGrid(
            title: "Session / Weather Cells",
            rows: contentBlockRows(definition: OverlayContentColumns.sessionWeather),
            rect: NSRect(x: 306, y: 272, width: 834, height: 344),
            columns: 2,
            rowHeight: 16,
            rowGap: 2
        )
    }

    private func drawCarRadarContentRegion() {
        drawContentMatrix(
            title: "Content Display",
            rows: [
                ContentMatrixRow(label: "Radar proximity", enabled: true),
                ContentMatrixRow(label: "Faster-class warning", enabled: overlay.showRadarMulticlassWarning)
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

    private func drawChromeRegion(title: String, rows: [ChromeSettingsRow]) {
        drawPanel(NSRect(x: 306, y: 272, width: 834, height: 188), title: title)
        guard DesignV2SettingsOverlaySpecs.supportsSharedChromeSettings(definition.id) else {
            drawText("No \(title.lowercased()) controls yet.", in: NSRect(x: 328, y: 334, width: 420, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            drawText("This matches the current production settings surface for this overlay.", in: NSRect(x: 328, y: 372, width: 560, height: 18), size: 12, color: DesignV2SettingsPalette.muted)
            return
        }

        if rows.isEmpty {
            drawText("No \(title.lowercased()) controls for this overlay.", in: NSRect(x: 328, y: 334, width: 420, height: 18), size: 13, color: DesignV2SettingsPalette.secondary)
            return
        }

        drawText("Item", in: NSRect(x: 328, y: 330, width: 110, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        for (index, session) in ["Practice", "Qualifying", "Race"].enumerated() {
            drawText(session, in: NSRect(x: 454 + CGFloat(index) * 116, y: 330, width: 104, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        }
        for (rowIndex, row) in rows.enumerated() {
            let rowY = 360 + CGFloat(rowIndex) * 48
            fillRounded(NSRect(x: 328, y: rowY, width: 768, height: 44), radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
            strokeRounded(NSRect(x: 328, y: rowY, width: 768, height: 44), radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
            drawText(row.label, in: NSRect(x: 346, y: rowY + 13, width: 110, height: 18), size: 13, weight: .semibold, color: DesignV2SettingsPalette.secondary)
        }
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
        for (index, session) in ["Practice", "Qualifying", "Race"].enumerated() {
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

    private func drawBlockToggleGrid(
        title: String,
        rows: [ContentMatrixRow],
        rect: NSRect,
        columns: Int,
        rowHeight: CGFloat,
        rowGap: CGFloat
    ) {
        drawPanel(rect, title: title)
        let columnGap: CGFloat = 18
        let contentLeft = rect.minX + 22
        let columnWidth = (rect.width - 44 - columnGap * CGFloat(columns - 1)) / CGFloat(columns)
        for column in 0..<columns {
            drawText("Item", in: NSRect(x: contentLeft + CGFloat(column) * (columnWidth + columnGap), y: rect.minY + 58, width: 110, height: 16), size: 10, weight: .bold, color: DesignV2SettingsPalette.muted)
        }

        let rowsPerColumn = Int(ceil(Double(rows.count) / Double(max(1, columns))))
        for (index, row) in rows.enumerated() {
            let column = index / max(1, rowsPerColumn)
            let rowIndex = index % max(1, rowsPerColumn)
            let rowX = contentLeft + CGFloat(column) * (columnWidth + columnGap)
            let rowY = rect.minY + 78 + CGFloat(rowIndex) * (rowHeight + rowGap)
            guard rowY + rowHeight <= rect.maxY - 10 else {
                break
            }

            fillRounded(NSRect(x: rowX, y: rowY, width: columnWidth, height: rowHeight), radius: 7, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
            strokeRounded(NSRect(x: rowX, y: rowY, width: columnWidth, height: rowHeight), radius: 7, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
            drawCheckBox(in: blockGridCheckFrame(index: index, rect: rect, columns: columns, rowsPerColumn: rowsPerColumn, rowHeight: rowHeight, rowGap: rowGap), checked: row.enabled)
            drawText(row.label, in: NSRect(x: rowX + 34, y: rowY + max(2, (rowHeight - 13) / 2), width: columnWidth - 44, height: 14), size: 10.5, weight: .regular, color: row.enabled ? DesignV2SettingsPalette.secondary : DesignV2SettingsPalette.dim)
        }
    }

    private func matrixCheckFrame(rowIndex: Int, rect: NSRect, rowHeight: CGFloat = 24, rowGap: CGFloat = 5) -> NSRect {
        let rowY = rect.minY + 78 + CGFloat(rowIndex) * (rowHeight + rowGap)
        return NSRect(x: 344, y: rowY + max(2, (rowHeight - 19) / 2), width: 19, height: 19)
    }

    private func drawMatrixControlRow(
        label: String,
        enabled: Bool,
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat = 24,
        precedingRowGap: CGFloat = 5,
        visibleLabel: String? = nil,
        countLabel: String? = nil,
        followsMatrixRows: Bool = true
    ) {
        let labelFrame = matrixControlLabelFrame(
            rect: rect,
            rowIndex: rowIndex,
            precedingRowHeight: precedingRowHeight,
            precedingRowGap: precedingRowGap,
            hasVisibleColumn: visibleLabel != nil,
            followsMatrixRows: followsMatrixRows
        )
        fillRounded(labelFrame, radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
        strokeRounded(labelFrame, radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
        drawText(label, in: NSRect(x: labelFrame.minX + 18, y: labelFrame.minY + (labelFrame.height - 16) / 2, width: labelFrame.width - 36, height: 16), size: 12, color: enabled ? DesignV2SettingsPalette.secondary : DesignV2SettingsPalette.dim)

        if let visibleLabel {
            let visibleFrame = matrixControlVisibleFrame(
                rect: rect,
                rowIndex: rowIndex,
                precedingRowHeight: precedingRowHeight,
                precedingRowGap: precedingRowGap,
                followsMatrixRows: followsMatrixRows
            )
            fillRounded(visibleFrame, radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
            strokeRounded(visibleFrame, radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
            drawText(visibleLabel, in: NSRect(x: visibleFrame.minX + 8, y: visibleFrame.minY + 7, width: visibleFrame.width - 16, height: 14), size: 9.5, weight: .bold, color: DesignV2SettingsPalette.muted, alignment: .center)
        }

        if let countLabel {
            let countFrame = matrixControlCountFrame(
                rect: rect,
                rowIndex: rowIndex,
                precedingRowHeight: precedingRowHeight,
                precedingRowGap: precedingRowGap,
                hasVisibleColumn: visibleLabel != nil,
                followsMatrixRows: followsMatrixRows
            )
            fillRounded(countFrame, radius: 8, color: DesignV2SettingsPalette.panelRaised.withAlphaComponent(0.78))
            strokeRounded(countFrame, radius: 8, color: DesignV2SettingsPalette.borderDim, lineWidth: 1)
            drawText(countLabel, in: NSRect(x: countFrame.minX + Self.matrixControlCellPadding, y: countFrame.minY + 7, width: countFrame.width - Self.matrixControlCellPadding * 2, height: 14), size: 9.5, weight: .bold, color: DesignV2SettingsPalette.muted)
        }
    }

    private func matrixControlLabelFrame(
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat = 24,
        precedingRowGap: CGFloat = 5,
        hasVisibleColumn: Bool = false,
        followsMatrixRows: Bool = true
    ) -> NSRect {
        let rowY = matrixControlRowY(rect: rect, rowIndex: rowIndex, precedingRowHeight: precedingRowHeight, precedingRowGap: precedingRowGap, followsMatrixRows: followsMatrixRows)
        return NSRect(x: rect.minX + 22, y: rowY, width: Self.matrixControlLabelWidth, height: 56)
    }

    private func matrixControlVisibleFrame(
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat = 24,
        precedingRowGap: CGFloat = 5,
        followsMatrixRows: Bool = true
    ) -> NSRect {
        let labelFrame = matrixControlLabelFrame(rect: rect, rowIndex: rowIndex, precedingRowHeight: precedingRowHeight, precedingRowGap: precedingRowGap, hasVisibleColumn: true, followsMatrixRows: followsMatrixRows)
        return NSRect(x: labelFrame.maxX + Self.matrixControlGap, y: labelFrame.minY, width: Self.matrixControlVisibleWidth, height: labelFrame.height)
    }

    private func matrixControlVisibleCheckFrame(
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat = 24,
        precedingRowGap: CGFloat = 5,
        followsMatrixRows: Bool = true
    ) -> NSRect {
        let visibleFrame = matrixControlVisibleFrame(rect: rect, rowIndex: rowIndex, precedingRowHeight: precedingRowHeight, precedingRowGap: precedingRowGap, followsMatrixRows: followsMatrixRows)
        return NSRect(x: visibleFrame.minX + (visibleFrame.width - 19) / 2, y: visibleFrame.minY + (visibleFrame.height - 19) / 2, width: 19, height: 19)
    }

    private func matrixControlCountFrame(
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat = 24,
        precedingRowGap: CGFloat = 5,
        hasVisibleColumn: Bool = false,
        followsMatrixRows: Bool = true
    ) -> NSRect {
        let labelFrame = matrixControlLabelFrame(rect: rect, rowIndex: rowIndex, precedingRowHeight: precedingRowHeight, precedingRowGap: precedingRowGap, hasVisibleColumn: hasVisibleColumn, followsMatrixRows: followsMatrixRows)
        var left = labelFrame.maxX + Self.matrixControlGap
        if hasVisibleColumn {
            left += Self.matrixControlVisibleWidth + Self.matrixControlGap
        }
        let width = Self.matrixControlContentWidth
            - Self.matrixControlLabelWidth
            - Self.matrixControlGap
            - (hasVisibleColumn ? Self.matrixControlVisibleWidth + Self.matrixControlGap : 0)
        return NSRect(x: left, y: labelFrame.minY, width: width, height: labelFrame.height)
    }

    private func matrixControlCountStepperFrame(
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat = 24,
        precedingRowGap: CGFloat = 5,
        hasVisibleColumn: Bool = false,
        followsMatrixRows: Bool = true
    ) -> NSRect {
        let countFrame = matrixControlCountFrame(rect: rect, rowIndex: rowIndex, precedingRowHeight: precedingRowHeight, precedingRowGap: precedingRowGap, hasVisibleColumn: hasVisibleColumn, followsMatrixRows: followsMatrixRows)
        return NSRect(x: countFrame.maxX - Self.matrixControlCellPadding - Self.matrixControlStepperWidth, y: countFrame.minY + 20, width: Self.matrixControlStepperWidth, height: 32)
    }

    private func matrixControlRowY(
        rect: NSRect,
        rowIndex: Int,
        precedingRowHeight: CGFloat,
        precedingRowGap: CGFloat,
        followsMatrixRows: Bool
    ) -> CGFloat {
        return followsMatrixRows
            ? rect.minY + 78 + CGFloat(rowIndex) * (precedingRowHeight + precedingRowGap)
            : rect.minY + 58
    }

    private func blockGridCheckFrame(
        index: Int,
        rect: NSRect,
        columns: Int,
        rowsPerColumn: Int,
        rowHeight: CGFloat,
        rowGap: CGFloat
    ) -> NSRect {
        let columnGap: CGFloat = 18
        let contentLeft = rect.minX + 22
        let columnWidth = (rect.width - 44 - columnGap * CGFloat(columns - 1)) / CGFloat(columns)
        let column = index / max(1, rowsPerColumn)
        let row = index % max(1, rowsPerColumn)
        let rowX = contentLeft + CGFloat(column) * (columnWidth + columnGap)
        let rowY = rect.minY + 78 + CGFloat(row) * (rowHeight + rowGap)
        return NSRect(x: rowX + 10, y: rowY + max(1, (rowHeight - 15) / 2), width: 15, height: 15)
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
            return [false, false, false]
        }

        if definition.id == "gap-to-leader" {
            return [false, false, true]
        }

        guard definition.showSessionFilters else {
            return [true, true, true]
        }

        return [
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
        return "OBS browser size \(Int(browserSize.width)) x \(Int(browserSize.height))"
    }

    private var previewImage: NSImage? {
        DesignV2SettingsReferenceImages.load(relativePath: "mocks/application-redesign/overlays/\(definition.id).png")
    }

    private var garageCoverImage: NSImage? {
        if !overlay.garageCoverImagePath.isEmpty,
           let image = NSImage(contentsOfFile: overlay.garageCoverImagePath) {
            return image
        }

        return TmrBrandAssets.loadGarageCoverDefaultImage() ?? previewImage ?? TmrBrandAssets.loadLogoImage()
    }

    private var garageCoverImageLabel: String {
        guard !overlay.garageCoverImagePath.isEmpty else {
            return "Stock fallback cover"
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

    private func chromeDisplayKeys(_ keys: [String]) -> [String] {
        guard keys.count == 4 else {
            return keys
        }

        return [keys[1], keys[2], keys[3]]
    }

    private func mirroredTestOptionKey(for key: String) -> String? {
        guard key.hasSuffix(".practice") else {
            return nil
        }

        return String(key.dropLast(".practice".count)) + ".test"
    }

    private func font(size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        DesignV2Drawing.font(family: fontFamily, size: size, weight: weight)
    }

    private static let headerChromeRows = [
        ChromeSettingsRow(label: "Status", keys: [
            "chrome.header.status.test",
            "chrome.header.status.practice",
            "chrome.header.status.qualifying",
            "chrome.header.status.race"
        ]),
        ChromeSettingsRow(label: "Time remaining", keys: [
            "chrome.header.time-remaining.test",
            "chrome.header.time-remaining.practice",
            "chrome.header.time-remaining.qualifying",
            "chrome.header.time-remaining.race"
        ])
    ]

    private static let footerChromeRows = [
        ChromeSettingsRow(label: "Source", keys: [
            "chrome.footer.source.test",
            "chrome.footer.source.practice",
            "chrome.footer.source.qualifying",
            "chrome.footer.source.race"
        ])
    ]

    private static func footerChromeRows(for overlayId: String) -> [ChromeSettingsRow] {
        overlayId.lowercased() == "session-weather" ? [] : footerChromeRows
    }
}
