import AppKit

final class SettingsOverlayView: NSView, NSTabViewDelegate, NSTextFieldDelegate {
    private static let designV2Theme = DesignV2Theme.outrun
    private static let preferredOverlayTabOrder = [
        "standings",
        "relative",
        "gap-to-leader",
        "track-map",
        "stream-chat",
        "garage-cover",
        "fuel-calculator",
        "input-state",
        "car-radar",
        "flags",
        "session-weather",
        "pit-service"
    ]

    private static let streamChatProviders: [(label: String, value: String)] = [
        ("Not configured", "none"),
        ("Streamlabs Chat Box URL", "streamlabs"),
        ("Twitch channel", "twitch")
    ]

    private var settings: ApplicationSettings
    private var captureSnapshot: TelemetryCaptureStatusSnapshot
    private let overlayDefinitions: [OverlayDefinition]
    private let onSettingsChanged: (ApplicationSettings) -> Void
    private let rawCaptureChanged: (Bool) -> Bool
    private let selectedOverlayChanged: (String?) -> Void
    private let titleBar = NSView()
    private let brandLogoView = NSImageView()
    private let titleLabel = NSTextField(labelWithString: "Tech Mates Racing Overlay")
    private let subtitleLabel = NSTextField(labelWithString: "TMR Overlay")
    private let sidebarView = NSView()
    private let tabView = NSTabView()
    private var tabButtons: [String: NSButton] = [:]
    private var designV2SurfaceViews: [String: NSView] = [:]
    private var designV2OverlaySurfaces: [String: DesignV2OverlaySettingsFullView] = [:]
    private weak var rawCaptureCheckbox: NSButton?
    private weak var appVersionValueLabel: NSTextField?
    private weak var appStatusValueLabel: NSTextField?
    private weak var sessionStateValueLabel: NSTextField?
    private weak var currentIssueValueLabel: NSTextField?
    private weak var latestBundleLabel: NSTextField?
    private weak var performanceSnapshotLabel: NSTextField?
    private weak var supportStatusLabel: NSTextField?
    private var syncingRawCaptureCheckbox = false

    init(
        settings: ApplicationSettings,
        captureSnapshot: TelemetryCaptureStatusSnapshot,
        overlayDefinitions: [OverlayDefinition],
        onSettingsChanged: @escaping (ApplicationSettings) -> Void,
        rawCaptureChanged: @escaping (Bool) -> Bool,
        selectedOverlayChanged: @escaping (String?) -> Void
    ) {
        self.settings = settings
        self.captureSnapshot = captureSnapshot
        self.overlayDefinitions = overlayDefinitions
        self.onSettingsChanged = onSettingsChanged
        self.rawCaptureChanged = rawCaptureChanged
        self.selectedOverlayChanged = selectedOverlayChanged
        super.init(frame: NSRect(origin: .zero, size: SettingsOverlayDefinition.definition.defaultSize))
        wantsLayer = true
        layer?.backgroundColor = Self.designV2Theme.colors.surface.cgColor
        build()
    }

    required init?(coder: NSCoder) {
        nil
    }

    func applySettings(_ updatedSettings: ApplicationSettings) {
        settings = updatedSettings
        for (overlayId, surface) in designV2OverlaySurfaces {
            if let overlay = settings.overlays.first(where: { $0.id == overlayId }) {
                surface.applyOverlay(overlay)
            }
        }
    }

    func updateCaptureStatus(_ snapshot: TelemetryCaptureStatusSnapshot) {
        captureSnapshot = snapshot
        syncRawCaptureCheckbox()
        syncSupportSnapshot()
    }

    func selectTab(identifier: String) {
        guard let item = tabView.tabViewItems.first(where: { $0.identifier as? String == identifier }) else {
            return
        }

        tabView.selectTabViewItem(item)
    }

    override func layout() {
        super.layout()
        titleBar.frame = NSRect(x: 0, y: bounds.height - 42, width: bounds.width, height: 42)
        brandLogoView.frame = NSRect(x: 14, y: 8, width: 48, height: 27)
        titleLabel.frame = NSRect(x: 72, y: 20, width: max(160, titleBar.bounds.width - 132), height: 17)
        subtitleLabel.frame = NSRect(x: 72, y: 7, width: max(160, titleBar.bounds.width - 132), height: 14)
        sidebarView.frame = NSRect(x: 12, y: 12, width: 174, height: max(320, bounds.height - 66))
        tabView.frame = NSRect(x: 198, y: 12, width: max(360, bounds.width - 210), height: max(320, bounds.height - 66))
        for surface in designV2SurfaceViews.values {
            surface.frame = bounds
        }
        layoutSideTabButtons()
    }

    private func build() {
        titleBar.wantsLayer = true
        titleBar.layer?.backgroundColor = Self.designV2Theme.colors.titleBar.cgColor
        brandLogoView.image = TmrBrandAssets.loadLogoImage()
        brandLogoView.imageScaling = .scaleProportionallyUpOrDown
        titleBar.addSubview(brandLogoView)
        titleLabel.textColor = .white
        titleLabel.font = overlayFont(ofSize: 15, weight: .semibold)
        titleBar.addSubview(titleLabel)
        subtitleLabel.textColor = Self.designV2Theme.colors.textMuted
        subtitleLabel.font = overlayFont(ofSize: 10)
        titleBar.addSubview(subtitleLabel)
        addSubview(titleBar)

        sidebarView.wantsLayer = true
        sidebarView.layer?.backgroundColor = Self.designV2Theme.colors.surfaceInset.cgColor
        addSubview(sidebarView)

        tabView.frame = NSRect(x: 198, y: 12, width: bounds.width - 210, height: bounds.height - 66)
        tabView.appearance = NSAppearance(named: .darkAqua)
        tabView.tabViewType = .noTabsNoBorder
        tabView.delegate = self
        addSubview(tabView)
        addTab(generalTab())
        for definition in orderedSettingsDefinitions() {
            addTab(overlayTab(definition))
        }
        addTab(errorLoggingTab())
        addDesignV2OverlaySurfaces()
        tabView.selectTabViewItem(at: 0)
        updateSideTabSelection()
        selectedOverlayChanged(tabView.selectedTabViewItem?.identifier as? String)
    }

    private func addDesignV2OverlaySurfaces() {
        for definition in orderedSettingsDefinitions() where DesignV2SettingsOverlaySpecs.usesFullSurface(definition.id) {
            let overlay = settings.overlay(
                id: definition.id,
                defaultSize: definition.defaultSize,
                defaultEnabled: false
            )
            let surface = DesignV2OverlaySettingsFullView(
                frame: bounds,
                definition: definition,
                overlay: overlay,
                fontFamily: OverlayTheme.defaultFontFamily,
                onOverlayChanged: { [weak self] updatedOverlay in
                    guard let self else {
                        return
                    }
                    self.settings.updateOverlay(updatedOverlay)
                    self.onSettingsChanged(self.settings)
                },
                onSelectTab: { [weak self] identifier in
                    self?.selectTab(identifier: identifier)
                }
            )
            surface.isHidden = true
            addSubview(surface)
            designV2SurfaceViews[definition.id] = surface
            designV2OverlaySurfaces[definition.id] = surface
        }
    }

    func tabView(_ tabView: NSTabView, didSelect tabViewItem: NSTabViewItem?) {
        updateSideTabSelection()
        selectedOverlayChanged(tabViewItem?.identifier as? String)
    }

    private func addTab(_ item: NSTabViewItem) {
        tabView.addTabViewItem(item)
        guard let identifier = item.identifier as? String else {
            return
        }

        let button = NSButton(title: item.label, target: self, action: #selector(sideTabClicked(_:)))
        button.identifier = NSUserInterfaceItemIdentifier(identifier)
        button.isBordered = false
        button.alignment = .left
        button.font = overlayFont(ofSize: 12, weight: .semibold)
        button.contentTintColor = NSColor(red: 0.84, green: 0.87, blue: 0.90, alpha: 1)
        button.wantsLayer = true
        button.layer?.cornerRadius = 5
        button.layer?.borderWidth = 1
        sidebarView.addSubview(button)
        tabButtons[identifier] = button
        layoutSideTabButtons()
    }

    @objc private func sideTabClicked(_ sender: NSButton) {
        guard let identifier = sender.identifier?.rawValue else {
            return
        }

        selectTab(identifier: identifier)
    }

    private func layoutSideTabButtons() {
        let buttonHeight: CGFloat = 34
        let spacing: CGFloat = 4
        var y = sidebarView.bounds.height - 10
        for item in tabView.tabViewItems {
            guard let identifier = item.identifier as? String,
                  let button = tabButtons[identifier] else {
                continue
            }

            y -= buttonHeight
            button.frame = NSRect(x: 8, y: y, width: max(80, sidebarView.bounds.width - 16), height: buttonHeight)
            y -= spacing
        }
    }

    private func updateSideTabSelection() {
        let selectedIdentifier = tabView.selectedTabViewItem?.identifier as? String
        let selectedDesignV2Surface = selectedIdentifier.flatMap { designV2SurfaceViews[$0] }
        let designV2Selected = selectedDesignV2Surface != nil
        titleBar.isHidden = designV2Selected
        sidebarView.isHidden = designV2Selected
        tabView.isHidden = designV2Selected
        for surface in designV2SurfaceViews.values {
            surface.isHidden = true
        }
        if let selectedDesignV2Surface {
            selectedDesignV2Surface.isHidden = false
            addSubview(selectedDesignV2Surface, positioned: .above, relativeTo: nil)
        }

        for (identifier, button) in tabButtons {
            let selected = identifier == selectedIdentifier
            button.layer?.backgroundColor = selected
                ? Self.designV2Theme.colors.accentPrimary.withAlphaComponent(0.18).cgColor
                : Self.designV2Theme.colors.surface.cgColor
            button.layer?.borderColor = selected
                ? Self.designV2Theme.colors.accentPrimary.withAlphaComponent(0.72).cgColor
                : Self.designV2Theme.colors.borderMuted.cgColor
            button.contentTintColor = selected
                ? Self.designV2Theme.colors.textPrimary
                : Self.designV2Theme.colors.textSecondary
        }
    }

    private func orderedSettingsDefinitions() -> [OverlayDefinition] {
        let userFacing = overlayDefinitions.filter { $0.id != "status" }
        let preferred = Self.preferredOverlayTabOrder.compactMap { preferredId in
            userFacing.first { $0.id.caseInsensitiveCompare(preferredId) == .orderedSame }
        }
        let remaining = userFacing.filter { definition in
            !Self.preferredOverlayTabOrder.contains { $0.caseInsensitiveCompare(definition.id) == .orderedSame }
        }
        return preferred + remaining
    }

    private func addSupportCaptureControls(to content: NSView, x: CGFloat, top: CGFloat, width: CGFloat) {
        content.addSubview(label("Diagnostic telemetry", frame: NSRect(x: x, y: top, width: width, height: 24), bold: true))
        content.addSubview(label("If we ask for a repro, enable before driving, then create diagnostics after.", frame: NSRect(x: x + 4, y: top - 28, width: width, height: 24)))
        let rawCapture = NSButton(checkboxWithTitle: "Capture diagnostic telemetry", target: self, action: #selector(rawCaptureClicked(_:)))
        rawCapture.frame = NSRect(x: x + 4, y: top - 68, width: min(320, width), height: 24)
        rawCapture.font = overlayFont(ofSize: 13)
        rawCaptureCheckbox = rawCapture
        syncRawCaptureCheckbox()
        content.addSubview(rawCapture)
    }

    private func addAdvancedCollectionControls(to content: NSView, x: CGFloat, top: CGFloat, width: CGFloat) {
        content.addSubview(label("Advanced collection", frame: NSRect(x: x, y: top, width: width, height: 24), bold: true))
        content.addSubview(label("Compact diagnostics run by default.", frame: NSRect(x: x + 4, y: top - 28, width: width, height: 24)))
        content.addSubview(multiLineValueLabel(advancedCollectionText(), frame: NSRect(x: x + 4, y: top - 136, width: width, height: 96)))
    }

    private func addStorageControls(to content: NSView, x: CGFloat, top: CGFloat, width: CGFloat) {
        content.addSubview(label("Storage", frame: NSRect(x: x, y: top, width: width, height: 24), bold: true))
        content.addSubview(label("Open local support folders.", frame: NSRect(x: x + 4, y: top - 28, width: width, height: 24)))
        content.addSubview(actionButton("Logs", frame: NSRect(x: x + 4, y: top - 66, width: 88, height: 28), action: #selector(openMockLogsFolder)))
        content.addSubview(actionButton("Diagnostics", frame: NSRect(x: x + 100, y: top - 66, width: 116, height: 28), action: #selector(openMockDiagnosticsFolder)))
        content.addSubview(actionButton("Captures", frame: NSRect(x: x + 4, y: top - 104, width: 88, height: 28), action: #selector(openMockCapturesFolder)))
        content.addSubview(actionButton("History", frame: NSRect(x: x + 100, y: top - 104, width: 116, height: 28), action: #selector(openMockHistoryFolder)))
    }

    private func generalTab() -> NSTabViewItem {
        let item = NSTabViewItem(identifier: "general")
        item.label = "General"
        let content = tabContentView()
        content.addSubview(label("General", frame: NSRect(x: 18, y: 500, width: 520, height: 24), bold: true))
        content.addSubview(label("Units", frame: NSRect(x: 22, y: 452, width: 140, height: 24)))
        let units = NSPopUpButton(frame: NSRect(x: 170, y: 448, width: 160, height: 28), pullsDown: false)
        units.addItems(withTitles: ["Metric", "Imperial"])
        units.selectItem(withTitle: settings.general.unitSystem == "Imperial" ? "Imperial" : "Metric")
        units.target = self
        units.action = #selector(unitsChanged(_:))
        content.addSubview(units)

        item.view = content
        return item
    }

    private func errorLoggingTab() -> NSTabViewItem {
        let item = NSTabViewItem(identifier: "error-logging")
        item.label = "Support"
        let content = tabContentView()

        content.addSubview(label("Support", frame: NSRect(x: 18, y: 500, width: 520, height: 24), bold: true))
        content.addSubview(label("Use this tab when we ask for diagnostics or version details.", frame: NSRect(x: 22, y: 470, width: 640, height: 24)))

        content.addSubview(label("App version", frame: NSRect(x: 22, y: 432, width: 120, height: 22)))
        let appVersion = valueLabel(appVersionText(), frame: NSRect(x: 150, y: 428, width: 380, height: 28))
        appVersionValueLabel = appVersion
        content.addSubview(appVersion)

        addSupportCaptureControls(to: content, x: 18, top: 386, width: 650)

        content.addSubview(label("Diagnostics bundle", frame: NSRect(x: 18, y: 276, width: 650, height: 24), bold: true))
        content.addSubview(label("After testing or reproducing an issue, create a bundle and send back the zip path.", frame: NSRect(x: 22, y: 248, width: 650, height: 24)))
        content.addSubview(actionButton("Create Bundle", frame: NSRect(x: 22, y: 210, width: 118, height: 28), action: #selector(openMockDiagnosticsFolder)))
        content.addSubview(actionButton("Copy Path", frame: NSRect(x: 150, y: 210, width: 118, height: 28), action: #selector(copyMockDiagnosticsBundlePath)))
        content.addSubview(actionButton("Open Diagnostics", frame: NSRect(x: 278, y: 210, width: 136, height: 28), action: #selector(openMockDiagnosticsFolder)))
        let latestBundle = label(latestBundleDisplayText(), frame: NSRect(x: 22, y: 178, width: 650, height: 22))
        latestBundle.textColor = NSColor(red: 0.70, green: 0.76, blue: 0.79, alpha: 1)
        latestBundleLabel = latestBundle
        content.addSubview(latestBundle)

        let status = label("", frame: NSRect(x: 22, y: 152, width: 650, height: 24))
        status.textColor = NSColor(red: 0.44, green: 0.88, blue: 0.57, alpha: 1)
        supportStatusLabel = status
        content.addSubview(status)

        content.addSubview(label("Current state", frame: NSRect(x: 18, y: 116, width: 650, height: 24), bold: true))
        content.addSubview(label("App status", frame: NSRect(x: 22, y: 84, width: 92, height: 22)))
        let appStatus = valueLabel(appStatusText(), frame: NSRect(x: 116, y: 80, width: 180, height: 28))
        appStatusValueLabel = appStatus
        content.addSubview(appStatus)

        content.addSubview(label("Session", frame: NSRect(x: 320, y: 84, width: 70, height: 22)))
        let sessionState = valueLabel(sessionStateText(), frame: NSRect(x: 390, y: 80, width: 280, height: 28))
        sessionStateValueLabel = sessionState
        content.addSubview(sessionState)

        content.addSubview(label("Issue", frame: NSRect(x: 22, y: 44, width: 92, height: 22)))
        let currentIssue = multiLineValueLabel(currentIssueText(), frame: NSRect(x: 116, y: 28, width: 554, height: 44))
        currentIssueValueLabel = currentIssue
        content.addSubview(currentIssue)

        item.view = content
        return item
    }

    private func overlayTab(_ definition: OverlayDefinition) -> NSTabViewItem {
        let overlay = settings.overlay(
            id: definition.id,
            defaultSize: definition.defaultSize,
            defaultEnabled: false
        )
        settings.updateOverlay(overlay)

        if DesignV2SettingsOverlaySpecs.usesFullSurface(definition.id) {
            return designV2OverlayTab(definition: definition)
        }

        let item = NSTabViewItem(identifier: definition.id)
        item.label = definition.displayName
        let content = tabContentView()
        content.addSubview(label(definition.displayName, frame: NSRect(x: 18, y: 500, width: 520, height: 24), bold: true))
        content.addSubview(overlayRegionTabs(definition: definition, overlay: overlay))
        item.view = content
        return item
    }

    private func designV2OverlayTab(definition: OverlayDefinition) -> NSTabViewItem {
        let item = NSTabViewItem(identifier: definition.id)
        item.label = definition.displayName
        item.view = NSView(frame: NSRect(x: 0, y: 0, width: 1100, height: 614))
        return item
    }

    private func overlayRegionTabs(definition: OverlayDefinition, overlay: OverlaySettings) -> NSTabView {
        let tabs = NSTabView(frame: NSRect(x: 18, y: 18, width: 1040, height: 458))
        tabs.appearance = NSAppearance(named: .darkAqua)
        tabs.tabPosition = .left
        tabs.addTabViewItem(regionTab("General", view: overlayGeneralRegion(definition: definition, overlay: overlay)))
        tabs.addTabViewItem(regionTab("Content", view: overlayContentRegion(definition: definition, overlay: overlay)))
        if !suppressHeaderFooterTabs(definition.id) {
            tabs.addTabViewItem(regionTab("Header", view: overlayChromeRegion(
                overlayId: definition.id,
                title: "Header",
                itemLabel: "Status",
                keys: [
                    "chrome.header.status.test",
                    "chrome.header.status.practice",
                    "chrome.header.status.qualifying",
                    "chrome.header.status.race"
                ]
            )))
            tabs.addTabViewItem(regionTab("Footer", view: overlayChromeRegion(
                overlayId: definition.id,
                title: "Footer",
                itemLabel: "Source",
                keys: [
                    "chrome.footer.source.test",
                    "chrome.footer.source.practice",
                    "chrome.footer.source.qualifying",
                    "chrome.footer.source.race"
                ]
            )))
        }
        return tabs
    }

    private func regionTab(_ title: String, view: NSView) -> NSTabViewItem {
        let item = NSTabViewItem(identifier: title.lowercased())
        item.label = title
        item.view = view
        return item
    }

    private func regionContentView() -> NSView {
        let view = NSView(frame: NSRect(x: 0, y: 0, width: 1040, height: 420))
        view.wantsLayer = true
        view.layer?.backgroundColor = NSColor(red: 0.078, green: 0.098, blue: 0.114, alpha: 1).cgColor
        return view
    }

    private func overlayGeneralRegion(definition: OverlayDefinition, overlay: OverlaySettings) -> NSView {
        let content = regionContentView()
        if definition.id == "garage-cover" {
            let nextTop = addScaleAndSessionOptions(
                to: content,
                definition: definition,
                overlay: overlay,
                top: 376,
                includeVisibility: false,
                includeSessionFilters: false
            )
            addLocalhostOptions(to: content, definition: definition, overlay: overlay, top: nextTop)
            return content
        }

        let nextTop = addScaleAndSessionOptions(
            to: content,
            definition: definition,
            overlay: overlay,
            top: 376,
            includeVisibility: true,
            includeSessionFilters: definition.showSessionFilters
        )
        addLocalhostOptions(to: content, definition: definition, overlay: overlay, top: nextTop)
        return content
    }

    private func overlayContentRegion(definition: OverlayDefinition, overlay: OverlaySettings) -> NSView {
        let content = regionContentView()
        var nextTop: CGFloat = 376
        var hasContent = false

        switch definition.id {
        case "stream-chat":
            addStreamChatOptions(to: content, overlay: overlay, top: nextTop)
            hasContent = true
        case "garage-cover":
            addGarageCoverOptions(to: content, overlay: overlay, top: nextTop)
            hasContent = true
        default:
            if let contentDefinition = OverlayContentColumns.definition(for: definition.id) {
                nextTop = addContentColumnSettings(to: content, definition: contentDefinition, top: nextTop)
                hasContent = true
            }
        }

        if definition.id != "garage-cover",
           addOverlaySpecificOptions(to: content, definition: definition, overlay: overlay, top: hasContent ? nextTop - 20 : nextTop) {
            hasContent = true
        }

        if !hasContent {
            content.addSubview(label("Content", frame: NSRect(x: 18, y: 376, width: 520, height: 24), bold: true))
            content.addSubview(label("No content controls yet.", frame: NSRect(x: 22, y: 334, width: 420, height: 24)))
        }

        return content
    }

    private func overlayChromeRegion(
        overlayId: String,
        title: String,
        itemLabel: String,
        keys: [String]
    ) -> NSView {
        let content = regionContentView()
        guard supportsSharedChromeSettings(overlayId) else {
            content.addSubview(label(title, frame: NSRect(x: 18, y: 376, width: 520, height: 24), bold: true))
            content.addSubview(label("No \(title.lowercased()) controls yet.", frame: NSRect(x: 22, y: 334, width: 420, height: 24)))
            return content
        }

        content.addSubview(label(title, frame: NSRect(x: 18, y: 376, width: 520, height: 24), bold: true))
        content.addSubview(label("Item", frame: NSRect(x: 22, y: 334, width: 120, height: 24)))
        content.addSubview(label("Test", frame: NSRect(x: 196, y: 334, width: 90, height: 24)))
        content.addSubview(label("Practice", frame: NSRect(x: 296, y: 334, width: 110, height: 24)))
        content.addSubview(label("Qualifying", frame: NSRect(x: 416, y: 334, width: 120, height: 24)))
        content.addSubview(label("Race", frame: NSRect(x: 548, y: 334, width: 90, height: 24)))
        content.addSubview(label(itemLabel, frame: NSRect(x: 22, y: 292, width: 150, height: 24)))
        let overlay = settings.overlay(id: overlayId, defaultSize: .zero)
        for (index, key) in keys.enumerated() {
            let x = [196, 296, 416, 548][index]
            let checked = optionBool(overlay, key: key, defaultValue: true)
            content.addSubview(checkbox(
                title: "",
                state: checked,
                frame: NSRect(x: CGFloat(x), y: 292, width: 32, height: 24),
                identifier: "\(overlayId)|\(key)"
            ))
        }

        return content
    }

    private func supportsSharedChromeSettings(_ overlayId: String) -> Bool {
        ["standings", "relative", "fuel-calculator", "gap-to-leader"].contains(overlayId)
    }

    private func suppressHeaderFooterTabs(_ overlayId: String) -> Bool {
        overlayId == "input-state"
    }

    private func addScaleAndSessionOptions(
        to content: NSView,
        definition: OverlayDefinition,
        overlay: OverlaySettings,
        top: CGFloat,
        includeVisibility: Bool,
        includeSessionFilters: Bool
    ) -> CGFloat {
        var controlsY = top
        if includeVisibility {
            let visible = checkbox(
                title: "Visible",
                state: overlay.enabled,
                frame: NSRect(x: 22, y: controlsY, width: 180, height: 24),
                identifier: "\(definition.id)|enabled"
            )
            content.addSubview(visible)
            controlsY -= 46
        }

        if definition.showScaleControl {
            content.addSubview(label("Scale", frame: NSRect(x: 22, y: controlsY, width: 120, height: 24)))
            let scale = NSPopUpButton(frame: NSRect(x: 170, y: controlsY - 4, width: 120, height: 28), pullsDown: false)
            let scaleValues = [60, 75, 100, 125, 150, 175, 200]
            scale.addItems(withTitles: scaleValues.map { "\($0)%" })
            let selectedScale = closestScaleValue(to: overlay.scale, allowedValues: scaleValues)
            scale.selectItem(withTitle: "\(selectedScale)%")
            scale.identifier = NSUserInterfaceItemIdentifier("\(definition.id)|scale")
            scale.target = self
            scale.action = #selector(scaleChanged(_:))
            content.addSubview(scale)
            controlsY -= 46
        }

        if definition.showOpacityControl {
            content.addSubview(label(definition.id == "track-map" ? "Map fill" : "Opacity", frame: NSRect(x: 22, y: controlsY, width: 120, height: 24)))
            let opacity = NSPopUpButton(frame: NSRect(x: 170, y: controlsY - 4, width: 120, height: 28), pullsDown: false)
            let opacityValues = [20, 30, 40, 50, 60, 70, 80, 88, 90, 100]
            opacity.addItems(withTitles: opacityValues.map { "\($0)%" })
            let selectedOpacity = closestOpacityValue(to: overlay.opacity, allowedValues: opacityValues)
            opacity.selectItem(withTitle: "\(selectedOpacity)%")
            opacity.identifier = NSUserInterfaceItemIdentifier("\(definition.id)|opacity")
            opacity.target = self
            opacity.action = #selector(opacityChanged(_:))
            content.addSubview(opacity)
            controlsY -= 46
        }

        if includeSessionFilters {
            let sessionBox = NSBox(frame: NSRect(x: 22, y: controlsY - 76, width: 430, height: 76))
            sessionBox.title = "Display in sessions"
            sessionBox.contentView?.addSubview(checkbox(
                title: "Test",
                state: overlay.showInTest,
                frame: NSRect(x: 14, y: 22, width: 78, height: 24),
                identifier: "\(definition.id)|test"
            ))
            sessionBox.contentView?.addSubview(checkbox(
                title: "Practice",
                state: overlay.showInPractice,
                frame: NSRect(x: 104, y: 22, width: 98, height: 24),
                identifier: "\(definition.id)|practice"
            ))
            sessionBox.contentView?.addSubview(checkbox(
                title: "Qualifying",
                state: overlay.showInQualifying,
                frame: NSRect(x: 214, y: 22, width: 112, height: 24),
                identifier: "\(definition.id)|qualifying"
            ))
            sessionBox.contentView?.addSubview(checkbox(
                title: "Race",
                state: overlay.showInRace,
                frame: NSRect(x: 338, y: 22, width: 76, height: 24),
                identifier: "\(definition.id)|race"
            ))
            content.addSubview(sessionBox)
            controlsY -= 98
        }

        return controlsY - 12
    }

    private func addLocalhostOptions(to content: NSView, definition: OverlayDefinition, overlay: OverlaySettings, top: CGFloat) {
        let x: CGFloat = 22
        content.addSubview(label("Localhost browser source", frame: NSRect(x: x, y: top, width: 560, height: 24), bold: true))
        guard let route = localhostRoute(for: definition.id) else {
            content.addSubview(label("No localhost route is available for this overlay yet.", frame: NSRect(x: x + 4, y: top - 42, width: 560, height: 24)))
            return
        }

        let url = "http://localhost:8765\(route)"
        content.addSubview(label("URL", frame: NSRect(x: x + 4, y: top - 42, width: 42, height: 24)))
        content.addSubview(commandField(url, frame: NSRect(x: x + 52, y: top - 46, width: 360, height: 28)))
        let copyButton = NSButton(title: "Copy", target: self, action: #selector(copyLocalhostURL(_:)))
        copyButton.frame = NSRect(x: x + 420, y: top - 46, width: 64, height: 28)
        copyButton.identifier = NSUserInterfaceItemIdentifier(url)
        copyButton.font = overlayFont(ofSize: 12)
        content.addSubview(copyButton)
        let browserSize = OverlayContentColumns.recommendedBrowserSize(overlay: definition, settings: overlay)
        content.addSubview(label("OBS", frame: NSRect(x: x + 500, y: top - 42, width: 36, height: 24)))
        content.addSubview(commandField("\(Int(browserSize.width))x\(Int(browserSize.height))", frame: NSRect(x: x + 538, y: top - 46, width: 86, height: 28)))
        content.addSubview(label("This route does not require the native overlay to be visible.", frame: NSRect(x: x + 4, y: top - 78, width: 620, height: 24)))
    }

    private func localhostRoute(for overlayId: String) -> String? {
        BrowserOverlayCatalog.route(for: overlayId)
    }

    private func addContentColumnSettings(
        to content: NSView,
        definition: OverlayContentDefinition,
        top: CGFloat
    ) -> CGFloat {
        let x: CGFloat = 18
        let overlay = settings.overlay(id: definition.overlayId, defaultSize: .zero)
        if definition.columns.isEmpty {
            return addContentBlockSettings(
                to: content,
                definition: definition,
                overlay: overlay,
                top: top
            )
        }

        content.addSubview(label("Content columns", frame: NSRect(x: x, y: top, width: 560, height: 24), bold: true))
        content.addSubview(label("Order", frame: NSRect(x: x + 4, y: top - 42, width: 70, height: 22)))
        content.addSubview(label("Column", frame: NSRect(x: x + 84, y: top - 42, width: 150, height: 22)))
        content.addSubview(label("Show", frame: NSRect(x: x + 298, y: top - 42, width: 54, height: 22)))
        content.addSubview(label("Width", frame: NSRect(x: x + 368, y: top - 42, width: 70, height: 22)))
        content.addSubview(label("Range", frame: NSRect(x: x + 470, y: top - 42, width: 120, height: 22)))

        let listHeight = CGFloat(max(1, definition.columns.count) * 40 + 2)
        let list = OverlayContentColumnListView(
            frame: NSRect(x: x, y: top - 74 - listHeight + 34, width: 650, height: listHeight),
            definition: definition,
            overlay: overlay,
            fontProvider: overlayFont(ofSize:weight:),
            onChange: { [weak self] states in
                self?.saveContentColumnStates(states, definition: definition)
            }
        )
        content.addSubview(list)
        return addContentBlockSettings(
            to: content,
            definition: definition,
            overlay: overlay,
            top: top - 80 - listHeight
        )
    }

    private func addContentBlockSettings(
        to content: NSView,
        definition: OverlayContentDefinition,
        overlay: OverlaySettings,
        top: CGFloat
    ) -> CGFloat {
        guard !definition.blocks.isEmpty else {
            return top
        }

        let x: CGFloat = 18
        var rowTop = top - 12
        content.addSubview(label("Content blocks", frame: NSRect(x: x, y: rowTop, width: 560, height: 24), bold: true))
        rowTop -= 42
        for block in definition.blocks {
            let row = NSView(frame: NSRect(x: x, y: rowTop - 52, width: 650, height: 76))
            row.wantsLayer = true
            let enabled = OverlayContentColumns.blockEnabled(block, settings: overlay)
            row.layer?.backgroundColor = enabled
                ? NSColor(red: 0.094, green: 0.118, blue: 0.133, alpha: 1).cgColor
                : NSColor(red: 0.070, green: 0.090, blue: 0.105, alpha: 1).cgColor
            row.layer?.cornerRadius = 4

            row.addSubview(checkbox(
                title: block.label,
                state: enabled,
                frame: NSRect(x: 12, y: 44, width: 220, height: 24),
                identifier: "\(definition.overlayId)|\(block.enabledOptionKey)"
            ))

            if let countOptionKey = block.countOptionKey,
               let countLabel = block.countLabel {
                row.addSubview(label(countLabel, frame: NSRect(x: 316, y: 44, width: 124, height: 24)))
                let count = countPopup(
                    value: OverlayContentColumns.blockCount(block, settings: overlay),
                    frame: NSRect(x: 446, y: 42, width: 76, height: 28),
                    identifier: "\(definition.overlayId)|\(countOptionKey)",
                    maximum: block.maximumCount
                )
                row.addSubview(count)
            }

            row.addSubview(label(block.description, frame: NSRect(x: 36, y: 10, width: 580, height: 24)))
            content.addSubview(row)
            rowTop -= 86
        }

        return rowTop
    }

    private func addOverlaySpecificOptions(to content: NSView, definition: OverlayDefinition, overlay: OverlaySettings, top: CGFloat) -> Bool {
        switch definition.id {
        case "flags":
            addFlagsOptions(to: content, overlay: overlay, top: top)
            return true
        case "status":
            content.addSubview(checkbox(
                title: "Show capture path",
                state: overlay.showStatusCaptureDetails,
                frame: NSRect(x: 22, y: top, width: 180, height: 24),
                identifier: "\(definition.id)|statusCapture"
            ))
            content.addSubview(checkbox(
                title: "Show health details",
                state: overlay.showStatusHealthDetails,
                frame: NSRect(x: 220, y: top, width: 180, height: 24),
                identifier: "\(definition.id)|statusHealth"
            ))
            return true
        case "fuel-calculator":
            content.addSubview(checkbox(
                title: "Show advice column",
                state: overlay.showFuelAdvice,
                frame: NSRect(x: 22, y: top, width: 190, height: 24),
                identifier: "\(definition.id)|fuelAdvice"
            ))
            content.addSubview(checkbox(
                title: "Show source row",
                state: overlay.showFuelSource,
                frame: NSRect(x: 220, y: top, width: 190, height: 24),
                identifier: "\(definition.id)|fuelSource"
            ))
            return true
        case "car-radar":
            content.addSubview(checkbox(
                title: "Show multiclass warning",
                state: overlay.showRadarMulticlassWarning,
                frame: NSRect(x: 22, y: top, width: 220, height: 24),
                identifier: "\(definition.id)|radarMulticlass"
            ))
            return true
        case "relative":
            content.addSubview(label("Cars each side", frame: NSRect(x: 22, y: top, width: 130, height: 24)))
            let eachSide = max(overlay.relativeCarsAhead, overlay.relativeCarsBehind)
            content.addSubview(countPopup(
                value: eachSide,
                frame: NSRect(x: 158, y: top - 2, width: 76, height: 28),
                identifier: "\(definition.id)|relativeEachSide",
                maximum: 8
            ))
            return true
        case "track-map":
            content.addSubview(label("Map sources", frame: NSRect(x: 18, y: top, width: 520, height: 24), bold: true))
            content.addSubview(label("Source", frame: NSRect(x: 22, y: top - 42, width: 120, height: 24)))
            content.addSubview(valueLabel("Best bundled or local map; circle fallback", frame: NSRect(x: 150, y: top - 46, width: 390, height: 28)))
            content.addSubview(label("Generation", frame: NSRect(x: 22, y: top - 84, width: 120, height: 24)))
            content.addSubview(valueLabel("Automatic after sessions; complete maps are skipped", frame: NSRect(x: 150, y: top - 88, width: 390, height: 28)))
            content.addSubview(checkbox(
                title: "Show sector boundaries",
                state: optionBool(overlay, key: "track-map.sector-boundaries.enabled", defaultValue: true),
                frame: NSRect(x: 22, y: top - 128, width: 230, height: 24),
                identifier: "\(definition.id)|track-map.sector-boundaries.enabled"
            ))
            content.addSubview(checkbox(
                title: "Build local maps from IBT telemetry",
                state: overlay.trackMapBuildFromTelemetry,
                frame: NSRect(x: 22, y: top - 164, width: 310, height: 24),
                identifier: "\(definition.id)|trackMapBuildFromTelemetry"
            ))
            content.addSubview(label("Derived geometry stays local. Bundled maps still work when this is off.", frame: NSRect(x: 22, y: top - 200, width: 520, height: 24)))
            content.addSubview(label("Bundled coverage", frame: NSRect(x: 560, y: 278, width: 520, height: 24), bold: true))
            content.addSubview(label("Reviewed app maps load automatically for matching tracks.", frame: NSRect(x: 564, y: 236, width: 430, height: 24)))
            return true
        case "garage-cover":
            addGarageCoverOptions(to: content, overlay: overlay, top: top)
            return true
        case "gap-to-leader":
            content.addSubview(label("Cars ahead", frame: NSRect(x: 22, y: top, width: 110, height: 24)))
            let ahead = countPopup(value: overlay.classGapCarsAhead, frame: NSRect(x: 136, y: top - 2, width: 76, height: 28), identifier: "\(definition.id)|gapAhead", maximum: 12)
            content.addSubview(ahead)
            content.addSubview(label("Cars behind", frame: NSRect(x: 238, y: top, width: 110, height: 24)))
            let behind = countPopup(value: overlay.classGapCarsBehind, frame: NSRect(x: 356, y: top - 2, width: 76, height: 28), identifier: "\(definition.id)|gapBehind", maximum: 12)
            content.addSubview(behind)
            return true
        default:
            return false
        }
    }

    private func addStreamChatOptions(to content: NSView, overlay: OverlaySettings, top: CGFloat) {
        let provider = normalizedStreamChatProvider(overlay.streamChatProvider)
        content.addSubview(label("Chat provider", frame: NSRect(x: 18, y: top, width: 520, height: 24), bold: true))
        content.addSubview(label("Mode", frame: NSRect(x: 22, y: top - 42, width: 120, height: 24)))
        let providerPopup = NSPopUpButton(frame: NSRect(x: 150, y: top - 46, width: 220, height: 28), pullsDown: false)
        for item in Self.streamChatProviders {
            providerPopup.addItem(withTitle: item.label)
            providerPopup.lastItem?.representedObject = item.value
        }
        providerPopup.select(providerPopup.menu?.items.first { ($0.representedObject as? String) == provider })
        providerPopup.identifier = NSUserInterfaceItemIdentifier("\(overlay.id)|streamChatProvider")
        providerPopup.target = self
        providerPopup.action = #selector(streamChatProviderChanged(_:))
        providerPopup.font = overlayFont(ofSize: 12)
        content.addSubview(providerPopup)

        content.addSubview(label("Streamlabs URL", frame: NSRect(x: 22, y: top - 90, width: 120, height: 24)))
        content.addSubview(editableField(
            overlay.streamChatStreamlabsUrl,
            frame: NSRect(x: 150, y: top - 94, width: 520, height: 28),
            identifier: "\(overlay.id)|streamChatStreamlabsUrl"
        ))
        content.addSubview(label("Paste the Streamlabs Chat Box widget URL, for example https://streamlabs.com/widgets/chat-box/...", frame: NSRect(x: 150, y: top - 126, width: 620, height: 24)))

        content.addSubview(label("Twitch channel", frame: NSRect(x: 22, y: top - 170, width: 120, height: 24)))
        content.addSubview(editableField(
            overlay.streamChatTwitchChannel,
            frame: NSRect(x: 150, y: top - 174, width: 220, height: 28),
            identifier: "\(overlay.id)|streamChatTwitchChannel"
        ))
        content.addSubview(label("Use the public channel name only. Streamlabs is the preferred no-login option for this first pass.", frame: NSRect(x: 150, y: top - 206, width: 620, height: 24)))

        let saveButton = actionButton("Save Chat", frame: NSRect(x: 150, y: top - 252, width: 110, height: 28), action: #selector(saveStreamChatSettings(_:)))
        saveButton.identifier = NSUserInterfaceItemIdentifier(overlay.id)
        content.addSubview(saveButton)
        content.addSubview(label("Open the localhost URL in a browser or OBS after saving.", frame: NSRect(x: 274, y: top - 246, width: 560, height: 24)))
        syncStreamChatFields(in: content, overlayId: overlay.id, provider: provider)
    }

    private func addGarageCoverOptions(to content: NSView, overlay: OverlaySettings, top: CGFloat) {
        content.addSubview(label("Cover image", frame: NSRect(x: 18, y: top, width: 520, height: 24), bold: true))
        content.addSubview(label("Image", frame: NSRect(x: 22, y: top - 42, width: 90, height: 24)))
        let imagePath = overlay.garageCoverImagePath.isEmpty ? "No image imported" : overlay.garageCoverImagePath
        content.addSubview(commandField(imagePath, frame: NSRect(x: 116, y: top - 46, width: 360, height: 28)))

        let importButton = actionButton("Import Image", frame: NSRect(x: 488, y: top - 46, width: 120, height: 28), action: #selector(importGarageCoverImage))
        content.addSubview(importButton)
        let clearButton = actionButton("Clear", frame: NSRect(x: 618, y: top - 46, width: 72, height: 28), action: #selector(clearGarageCoverImage))
        content.addSubview(clearButton)

        content.addSubview(label("Set the browser-source size in OBS. The app does not create a desktop Garage Cover window.", frame: NSRect(x: 22, y: top - 104, width: 720, height: 24)))
    }

    private func addFlagsOptions(to content: NSView, overlay: OverlaySettings, top: CGFloat) {
        content.addSubview(label("Displayed flags", frame: NSRect(x: 18, y: top, width: 520, height: 24), bold: true))
        addFlagDisplayRow(
            to: content,
            label: "Green start/resume",
            enabled: overlay.flagsShowGreen,
            enabledIdentifier: "\(overlay.id)|flagsShowGreen",
            top: top - 38
        )
        addFlagDisplayRow(
            to: content,
            label: "Blue",
            enabled: overlay.flagsShowBlue,
            enabledIdentifier: "\(overlay.id)|flagsShowBlue",
            top: top - 74
        )
        addFlagDisplayRow(
            to: content,
            label: "Yellow",
            enabled: overlay.flagsShowYellow,
            enabledIdentifier: "\(overlay.id)|flagsShowYellow",
            top: top - 110
        )
        addFlagDisplayRow(
            to: content,
            label: "Red / black",
            enabled: overlay.flagsShowCritical,
            enabledIdentifier: "\(overlay.id)|flagsShowCritical",
            top: top - 146
        )
        addFlagDisplayRow(
            to: content,
            label: "White / checkered",
            enabled: overlay.flagsShowFinish,
            enabledIdentifier: "\(overlay.id)|flagsShowFinish",
            top: top - 182
        )

        let sizeTop = top - 236
        content.addSubview(label("Overlay size", frame: NSRect(x: 18, y: sizeTop, width: 520, height: 24), bold: true))
        content.addSubview(label("Width", frame: NSRect(x: 22, y: sizeTop - 44, width: 80, height: 24)))
        content.addSubview(integerField(
            value: Int(FlagsOverlayDefinition.resolveSize(overlay).width.rounded()),
            frame: NSRect(x: 108, y: sizeTop - 48, width: 96, height: 28),
            identifier: "\(overlay.id)|flagsWidth"
        ))
        content.addSubview(label("Height", frame: NSRect(x: 238, y: sizeTop - 44, width: 80, height: 24)))
        content.addSubview(integerField(
            value: Int(FlagsOverlayDefinition.resolveSize(overlay).height.rounded()),
            frame: NSRect(x: 324, y: sizeTop - 48, width: 96, height: 28),
            identifier: "\(overlay.id)|flagsHeight"
        ))
    }

    private func addFlagDisplayRow(
        to content: NSView,
        label title: String,
        enabled: Bool,
        enabledIdentifier: String,
        top: CGFloat
    ) {
        content.addSubview(checkbox(
            title: title,
            state: enabled,
            frame: NSRect(x: 22, y: top, width: 220, height: 24),
            identifier: enabledIdentifier
        ))
    }

    @objc private func unitsChanged(_ sender: NSPopUpButton) {
        guard let unitSystem = sender.selectedItem?.title else {
            return
        }

        settings.general.unitSystem = unitSystem
        onSettingsChanged(settings)
    }

    @objc private func openMockLogsFolder() {
        openSupportURL(AppPaths.logsRoot(), status: "Opened logs folder.")
    }

    @objc private func openMockDiagnosticsFolder() {
        openSupportURL(AppPaths.diagnosticsRoot(), status: "Opened diagnostics folder.")
    }

    @objc private func openMockCapturesFolder() {
        openSupportURL(AppPaths.captureRoot(), status: "Opened captures folder.")
    }

    @objc private func openMockHistoryFolder() {
        openSupportURL(AppPaths.historyRoot(), status: "Opened history folder.")
    }

    @objc private func copyMockDiagnosticsBundlePath() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(mockDiagnosticsBundleURL.path, forType: .string)
        setSupportStatus("Copied diagnostics bundle path.")
    }

    @objc private func copyLocalhostURL(_ sender: NSButton) {
        guard let url = sender.identifier?.rawValue else {
            return
        }

        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(url, forType: .string)
        setSupportStatus("Copied localhost URL.")
    }

    @objc private func streamChatProviderChanged(_ sender: NSPopUpButton) {
        guard let parts = sender.identifier?.rawValue.split(separator: "|"), parts.count == 2,
              let content = sender.superview else {
            return
        }

        syncStreamChatFields(
            in: content,
            overlayId: String(parts[0]),
            provider: selectedStreamChatProvider(sender)
        )
    }

    @objc private func saveStreamChatSettings(_ sender: NSButton) {
        guard let overlayId = sender.identifier?.rawValue,
              let content = sender.superview,
              var overlay = settings.overlays.first(where: { $0.id == overlayId }) else {
            return
        }

        if let providerPopup: NSPopUpButton = findSubview(
            in: content,
            identifier: "\(overlayId)|streamChatProvider"
        ) {
            overlay.streamChatProvider = selectedStreamChatProvider(providerPopup)
        }
        if let streamlabsField: NSTextField = findSubview(
            in: content,
            identifier: "\(overlayId)|streamChatStreamlabsUrl"
        ) {
            overlay.streamChatStreamlabsUrl = streamlabsField.stringValue
        }
        if let twitchField: NSTextField = findSubview(
            in: content,
            identifier: "\(overlayId)|streamChatTwitchChannel"
        ) {
            overlay.streamChatTwitchChannel = twitchField.stringValue
        }

        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
        setSupportStatus("Saved stream chat settings.")
    }

    @objc private func rawCaptureClicked(_ sender: NSButton) {
        guard !syncingRawCaptureCheckbox else {
            return
        }

        let accepted = rawCaptureChanged(sender.state == .on)
        if !accepted {
            syncRawCaptureCheckbox()
        }
    }

    @objc private func overlayCheckboxChanged(_ sender: NSButton) {
        guard let parts = sender.identifier?.rawValue.split(separator: "|"), parts.count == 2 else {
            return
        }

        let id = String(parts[0])
        let key = String(parts[1])
        guard var overlay = settings.overlays.first(where: { $0.id == id }) else {
            return
        }

        let isOn = sender.state == .on
        switch key {
        case "enabled":
            overlay.enabled = isOn
        case "test":
            overlay.showInTest = isOn
        case "practice":
            overlay.showInPractice = isOn
        case "qualifying":
            overlay.showInQualifying = isOn
        case "race":
            overlay.showInRace = isOn
        case "statusCapture":
            overlay.showStatusCaptureDetails = isOn
        case "statusHealth":
            overlay.showStatusHealthDetails = isOn
        case "fuelAdvice":
            overlay.showFuelAdvice = isOn
        case "fuelSource":
            overlay.showFuelSource = isOn
        case "radarMulticlass":
            overlay.showRadarMulticlassWarning = isOn
        case "flagsShowGreen":
            overlay.flagsShowGreen = isOn
        case "flagsShowBlue":
            overlay.flagsShowBlue = isOn
        case "flagsShowYellow":
            overlay.flagsShowYellow = isOn
        case "flagsShowCritical":
            overlay.flagsShowCritical = isOn
        case "flagsShowFinish":
            overlay.flagsShowFinish = isOn
        case "trackMapBuildFromTelemetry":
            overlay.trackMapBuildFromTelemetry = isOn
        default:
            guard key.contains(".") else {
                return
            }

            overlay.options[key] = isOn ? "true" : "false"
        }

        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    @objc private func countChanged(_ sender: NSStepper) {
        guard let parts = sender.identifier?.rawValue.split(separator: "|"), parts.count == 2,
              var overlay = settings.overlays.first(where: { $0.id == String(parts[0]) }) else {
            return
        }

        switch String(parts[1]) {
        case "relativeEachSide":
            let value = min(max(Int(sender.integerValue), 0), 8)
            overlay.relativeCarsAhead = value
            overlay.relativeCarsBehind = value
        case "gapAhead":
            overlay.classGapCarsAhead = Int(sender.integerValue)
        case "gapBehind":
            overlay.classGapCarsBehind = Int(sender.integerValue)
        default:
            return
        }

        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    @objc private func countPopupChanged(_ sender: NSPopUpButton) {
        guard let parts = sender.identifier?.rawValue.split(separator: "|"), parts.count == 2,
              let selected = sender.selectedItem?.title,
              let selectedValue = Int(selected),
              var overlay = settings.overlays.first(where: { $0.id == String(parts[0]) }) else {
            return
        }

        switch String(parts[1]) {
        case "relativeEachSide":
            let value = min(max(selectedValue, 0), 8)
            overlay.relativeCarsAhead = value
            overlay.relativeCarsBehind = value
        case "gapAhead":
            overlay.classGapCarsAhead = min(max(selectedValue, 0), 12)
        case "gapBehind":
            overlay.classGapCarsBehind = min(max(selectedValue, 0), 12)
        default:
            let key = String(parts[1])
            guard key.contains(".") else {
                return
            }

            overlay.options[key] = String(selectedValue)
        }

        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    @objc private func integerFieldChanged(_ sender: NSTextField) {
        applyIntegerField(sender)
    }

    func controlTextDidEndEditing(_ obj: Notification) {
        guard let field = obj.object as? NSTextField else {
            return
        }

        applyIntegerField(field)
    }

    private func applyIntegerField(_ field: NSTextField) {
        guard let parts = field.identifier?.rawValue.split(separator: "|"), parts.count == 2,
              let selectedValue = Int(field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)),
              var overlay = settings.overlays.first(where: { $0.id == String(parts[0]) }) else {
            return
        }

        switch String(parts[1]) {
        case "flagsWidth":
            overlay.width = Double(min(max(selectedValue, Int(FlagsOverlayDefinition.minimumWidth)), Int(FlagsOverlayDefinition.maximumWidth)))
            overlay.screenId = nil
        case "flagsHeight":
            overlay.height = Double(min(max(selectedValue, Int(FlagsOverlayDefinition.minimumHeight)), Int(FlagsOverlayDefinition.maximumHeight)))
            overlay.screenId = nil
        default:
            return
        }

        field.stringValue = String(Int((String(parts[1]).hasSuffix("Width") ? overlay.width : overlay.height).rounded()))
        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    @objc private func importGarageCoverImage() {
        guard var overlay = settings.overlays.first(where: { $0.id == GarageCoverOverlayDefinition.definition.id }) else {
            return
        }

        let panel = NSOpenPanel()
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        panel.allowedFileTypes = ["png", "jpg", "jpeg", "bmp", "gif"]
        panel.title = "Import garage cover image"

        guard panel.runModal() == .OK, let sourceURL = panel.url else {
            return
        }

        do {
            overlay.garageCoverImagePath = try copyGarageCoverImage(from: sourceURL).path
            settings.updateOverlay(overlay)
            onSettingsChanged(settings)
            setSupportStatus("Imported garage cover image.")
        } catch {
            setSupportStatus("Garage cover import failed: \(error.localizedDescription)")
        }
    }

    @objc private func clearGarageCoverImage() {
        guard var overlay = settings.overlays.first(where: { $0.id == GarageCoverOverlayDefinition.definition.id }) else {
            return
        }

        overlay.garageCoverImagePath = ""
        clearGarageCoverImportedImages()
        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
        setSupportStatus("Cleared garage cover image.")
    }

    private func copyGarageCoverImage(from sourceURL: URL) throws -> URL {
        let allowedExtensions: Set<String> = ["png", "jpg", "jpeg", "bmp", "gif"]
        let fileExtension = sourceURL.pathExtension.lowercased()
        guard allowedExtensions.contains(fileExtension) else {
            throw NSError(domain: "TmrOverlayMac", code: 1, userInfo: [NSLocalizedDescriptionKey: "Garage cover images must be PNG, JPG, BMP, or GIF files."])
        }

        let directory = AppPaths.settingsRoot().appendingPathComponent("garage-cover", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let destination = directory.appendingPathComponent("cover.\(fileExtension)")
        if sourceURL.standardizedFileURL.path == destination.standardizedFileURL.path {
            return destination
        }

        let temporaryDestination = directory.appendingPathComponent("cover-import.\(fileExtension)")
        try? FileManager.default.removeItem(at: temporaryDestination)
        try FileManager.default.copyItem(at: sourceURL, to: temporaryDestination)
        if let existing = try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) {
            for url in existing where url.deletingPathExtension().lastPathComponent == "cover" {
                try? FileManager.default.removeItem(at: url)
            }
        }
        try? FileManager.default.removeItem(at: destination)
        try FileManager.default.moveItem(at: temporaryDestination, to: destination)
        return destination
    }

    private func clearGarageCoverImportedImages() {
        let directory = AppPaths.settingsRoot().appendingPathComponent("garage-cover", isDirectory: true)
        guard let existing = try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) else {
            return
        }

        for url in existing where url.deletingPathExtension().lastPathComponent == "cover" {
            try? FileManager.default.removeItem(at: url)
        }
    }

    @objc private func opacityChanged(_ sender: NSPopUpButton) {
        guard let parts = sender.identifier?.rawValue.split(separator: "|"), parts.count == 2,
              let selected = sender.selectedItem?.title.replacingOccurrences(of: "%", with: ""),
              let percent = Double(selected),
              var overlay = settings.overlays.first(where: { $0.id == String(parts[0]) }) else {
            return
        }

        overlay.opacity = min(max(percent / 100.0, 0.2), 1.0)
        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    @objc private func scaleChanged(_ sender: NSPopUpButton) {
        guard let parts = sender.identifier?.rawValue.split(separator: "|"), parts.count == 2,
              let selected = sender.selectedItem?.title.replacingOccurrences(of: "%", with: ""),
              let percent = Double(selected),
              var overlay = settings.overlays.first(where: { $0.id == String(parts[0]) }),
              let definition = overlayDefinitions.first(where: { $0.id == overlay.id }) else {
            return
        }

        overlay.scale = min(max(percent / 100.0, 0.6), 2.0)
        overlay.width = definition.defaultSize.width * overlay.scale
        overlay.height = definition.defaultSize.height * overlay.scale
        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    private func tabContentView() -> NSView {
        let view = NSView(frame: NSRect(x: 0, y: 0, width: 1100, height: 614))
        view.wantsLayer = true
        view.layer?.backgroundColor = NSColor(red: 0.078, green: 0.098, blue: 0.114, alpha: 1).cgColor
        return view
    }

    private func label(_ text: String, frame: NSRect, bold: Bool = false) -> NSTextField {
        let field = NSTextField(labelWithString: text)
        field.frame = frame
        field.textColor = bold ? .white : NSColor(red: 0.84, green: 0.87, blue: 0.90, alpha: 1)
        field.font = overlayFont(ofSize: bold ? 15 : 13, weight: bold ? .semibold : .regular)
        return field
    }

    private func valueLabel(_ text: String, frame: NSRect) -> NSTextField {
        let field = NSTextField(labelWithString: text)
        field.frame = frame
        field.textColor = NSColor(red: 0.84, green: 0.87, blue: 0.90, alpha: 1)
        field.font = overlayFont(ofSize: 13)
        field.backgroundColor = NSColor(red: 0.094, green: 0.118, blue: 0.133, alpha: 1)
        field.drawsBackground = true
        field.isBordered = false
        return field
    }

    private func warningLabel(_ text: String, frame: NSRect) -> NSTextField {
        let field = NSTextField(labelWithString: text)
        field.frame = frame
        field.textColor = OverlayTheme.Colors.warningIndicator
        field.font = overlayFont(ofSize: 12, weight: .semibold)
        field.backgroundColor = NSColor(red: 0.165, green: 0.137, blue: 0.071, alpha: 1)
        field.drawsBackground = true
        field.isBordered = true
        field.maximumNumberOfLines = 0
        field.lineBreakMode = .byWordWrapping
        field.cell?.wraps = true
        return field
    }

    private func commandField(_ text: String, frame: NSRect) -> NSTextField {
        let field = valueLabel(text, frame: frame)
        field.isSelectable = true
        field.maximumNumberOfLines = 1
        field.lineBreakMode = .byTruncatingMiddle
        field.cell?.lineBreakMode = .byTruncatingMiddle
        return field
    }

    private func editableField(_ text: String, frame: NSRect, identifier: String) -> NSTextField {
        let field = NSTextField(string: text)
        field.frame = frame
        field.identifier = NSUserInterfaceItemIdentifier(identifier)
        field.font = overlayFont(ofSize: 12)
        field.textColor = OverlayTheme.Colors.textPrimary
        field.backgroundColor = NSColor(red: 0.094, green: 0.118, blue: 0.133, alpha: 1)
        field.isBordered = true
        field.maximumNumberOfLines = 1
        field.lineBreakMode = .byTruncatingMiddle
        field.cell?.lineBreakMode = .byTruncatingMiddle
        return field
    }

    private func multiLineValueLabel(_ text: String, frame: NSRect) -> NSTextField {
        let field = NSTextField(labelWithString: text)
        field.frame = frame
        field.textColor = NSColor(red: 0.84, green: 0.87, blue: 0.90, alpha: 1)
        field.font = overlayFont(ofSize: 12)
        field.backgroundColor = NSColor(red: 0.094, green: 0.118, blue: 0.133, alpha: 1)
        field.drawsBackground = true
        field.isBordered = true
        field.isEditable = false
        field.maximumNumberOfLines = 0
        field.lineBreakMode = .byWordWrapping
        field.cell?.wraps = true
        field.cell?.isScrollable = false
        return field
    }

    private func actionButton(_ title: String, frame: NSRect, action: Selector) -> NSButton {
        let button = NSButton(title: title, target: self, action: action)
        button.frame = frame
        button.font = overlayFont(ofSize: 12)
        return button
    }

    private func checkbox(title: String, state: Bool, frame: NSRect, identifier: String) -> NSButton {
        let button = NSButton(checkboxWithTitle: title, target: self, action: #selector(overlayCheckboxChanged(_:)))
        button.frame = frame
        button.state = state ? .on : .off
        button.identifier = NSUserInterfaceItemIdentifier(identifier)
        button.font = overlayFont(ofSize: 13)
        return button
    }

    private func integerField(value: Int, frame: NSRect, identifier: String) -> NSTextField {
        let field = NSTextField(string: String(value))
        field.frame = frame
        field.identifier = NSUserInterfaceItemIdentifier(identifier)
        field.delegate = self
        field.target = self
        field.action = #selector(integerFieldChanged(_:))
        field.alignment = .right
        field.font = OverlayTheme.monospacedFont(size: 12)
        return field
    }

    private func stepper(value: Int, frame: NSRect, identifier: String, maximum: Double = 12) -> NSStepper {
        let stepper = NSStepper(frame: frame)
        stepper.minValue = 0
        stepper.maxValue = maximum
        stepper.increment = 1
        stepper.integerValue = min(max(value, 0), Int(maximum))
        stepper.identifier = NSUserInterfaceItemIdentifier(identifier)
        stepper.target = self
        stepper.action = #selector(countChanged(_:))
        return stepper
    }

    private func countPopup(value: Int, frame: NSRect, identifier: String, maximum: Int) -> NSPopUpButton {
        let popup = NSPopUpButton(frame: frame, pullsDown: false)
        let clamped = min(max(value, 0), maximum)
        popup.addItems(withTitles: (0...maximum).map { String($0) })
        popup.selectItem(withTitle: String(clamped))
        popup.identifier = NSUserInterfaceItemIdentifier(identifier)
        popup.target = self
        popup.action = #selector(countPopupChanged(_:))
        popup.font = overlayFont(ofSize: 12)
        return popup
    }

    private func syncRawCaptureCheckbox() {
        guard let rawCaptureCheckbox else {
            return
        }

        syncingRawCaptureCheckbox = true
        rawCaptureCheckbox.state = captureSnapshot.rawCaptureEnabled || captureSnapshot.rawCaptureActive ? .on : .off
        rawCaptureCheckbox.isEnabled = !captureSnapshot.rawCaptureActive
        rawCaptureCheckbox.title = captureSnapshot.rawCaptureActive
            ? "Diagnostic telemetry capture active"
            : "Capture diagnostic telemetry"
        syncingRawCaptureCheckbox = false
    }

    private func syncSupportSnapshot() {
        appVersionValueLabel?.stringValue = appVersionText()
        appStatusValueLabel?.stringValue = appStatusText()
        sessionStateValueLabel?.stringValue = sessionStateText()
        currentIssueValueLabel?.stringValue = currentIssueText()
        latestBundleLabel?.stringValue = latestBundleDisplayText()
        performanceSnapshotLabel?.stringValue = performanceSnapshotText()
    }

    private var mockDiagnosticsBundleURL: URL {
        AppPaths.diagnosticsRoot().appendingPathComponent("tmroverlay-diagnostics-mock-20260428-120413.zip")
    }

    private func openSupportURL(_ url: URL, status: String) {
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        NSWorkspace.shared.open(url)
        setSupportStatus(status)
    }

    private func setSupportStatus(_ status: String) {
        supportStatusLabel?.stringValue = status
    }

    private func appStatusText() -> String {
        if let lastError = captureSnapshot.lastError, !lastError.isEmpty {
            return "Error"
        }

        if let lastWarning = captureSnapshot.lastWarning, !lastWarning.isEmpty {
            return "Warning"
        }

        if let appWarning = captureSnapshot.appWarning, !appWarning.isEmpty {
            return "Warning"
        }

        if captureSnapshot.droppedFrameCount > 0 {
            return "Warning"
        }

        if captureSnapshot.isCapturing {
            return "Live telemetry"
        }

        if captureSnapshot.isConnected {
            return "Connected"
        }

        return "Waiting for iRacing"
    }

    private func sessionStateText() -> String {
        if captureSnapshot.rawCaptureActive {
            return "Diagnostic telemetry active (\(captureSnapshot.writtenFrameCount) frames)"
        }

        if captureSnapshot.rawCaptureEnabled {
            return "Diagnostic telemetry requested; starts with live data"
        }

        if captureSnapshot.isCapturing {
            return "Receiving live telemetry (\(captureSnapshot.frameCount) frames)"
        }

        if captureSnapshot.isConnected {
            return "iRacing connected; waiting for live session data"
        }

        return "Not connected; start iRacing when ready"
    }

    private func advancedCollectionText() -> String {
        """
        edge clips: enabled
        model v2 parity: enabled
        overlay signals: enabled
        post-race analysis: enabled
        localhost: listening, 0 requests
        """
    }

    private func latestBundleDisplayText() -> String {
        "Latest bundle: mock bundle ready"
    }

    private func currentIssueText() -> String {
        if let lastError = captureSnapshot.lastError, !lastError.isEmpty {
            return "Error: \(lastError)"
        }

        if let lastWarning = captureSnapshot.lastWarning, !lastWarning.isEmpty {
            return "Warning: \(lastWarning)"
        }

        if let appWarning = captureSnapshot.appWarning, !appWarning.isEmpty {
            return "App warning: \(appWarning)"
        }

        if !captureSnapshot.isConnected {
            return "No active issue. Waiting is expected before iRacing is running."
        }

        if !captureSnapshot.isCapturing {
            return "No active issue. Live telemetry starts after session data arrives."
        }

        return "No active issue recorded."
    }

    private func appVersionText() -> String {
        "v\(AppVersionInfo.current.version)"
    }

    private func performanceSnapshotText() -> String {
        let fps = captureSnapshot.isCapturing ? "60" : "0"
        let telemetry: String
        let iracing: String
        if captureSnapshot.isCapturing {
            telemetry = "\(captureSnapshot.frameCount) frames, \(fps) fps"
            iracing = "mock quality 0.94, latency 0.067s"
        } else {
            telemetry = "waiting for live telemetry"
            iracing = "n/a"
        }

        let raw: String
        if captureSnapshot.rawCaptureEnabled || captureSnapshot.rawCaptureActive {
            raw = "\(captureSnapshot.writtenFrameCount) written, \(captureSnapshot.droppedFrameCount) dropped, \(formatBytes(captureSnapshot.telemetryFileBytes))"
        } else {
            raw = "diagnostic capture off"
        }

        return """
        telemetry: \(telemetry)
        iRacing: \(iracing)
        raw: \(raw)
        process: mock harness
        """
    }

    private func formatBytes(_ bytes: Int64?) -> String {
        guard var value = bytes.map(Double.init) else {
            return "n/a"
        }

        let units = ["B", "KB", "MB", "GB"]
        var unitIndex = 0
        while value >= 1024, unitIndex < units.count - 1 {
            value /= 1024
            unitIndex += 1
        }

        return String(format: "%.1f %@", value, units[unitIndex])
    }

    private func closestScaleValue(to scale: Double, allowedValues: [Int]) -> Int {
        let percent = Int((scale * 100).rounded())
        return allowedValues.min(by: { abs($0 - percent) < abs($1 - percent) }) ?? 100
    }

    private func closestOpacityValue(to opacity: Double, allowedValues: [Int]) -> Int {
        let percent = Int((min(max(opacity, 0.2), 1.0) * 100).rounded())
        return allowedValues.min(by: { abs($0 - percent) < abs($1 - percent) }) ?? 88
    }

    private func selectedStreamChatProvider(_ popup: NSPopUpButton) -> String {
        normalizedStreamChatProvider(popup.selectedItem?.representedObject as? String ?? "none")
    }

    private func optionBool(_ overlay: OverlaySettings, key: String, defaultValue: Bool) -> Bool {
        guard let configured = overlay.options[key]?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() else {
            return defaultValue
        }

        return ["true", "1", "yes"].contains(configured)
            ? true
            : ["false", "0", "no"].contains(configured)
                ? false
                : defaultValue
    }

    private func saveContentColumnStates(
        _ states: [OverlayContentColumnState],
        definition: OverlayContentDefinition
    ) {
        guard var overlay = settings.overlays.first(where: { $0.id == definition.overlayId }) else {
            return
        }

        for (index, state) in states.enumerated() {
            let column = state.definition
            overlay.options[column.enabledKey(overlayId: definition.overlayId)] = state.enabled ? "true" : "false"
            overlay.options[column.orderKey(overlayId: definition.overlayId)] = String(index + 1)
            overlay.options[column.widthKey(overlayId: definition.overlayId)] = String(state.width)
        }

        settings.updateOverlay(overlay)
        onSettingsChanged(settings)
    }

    private func syncStreamChatFields(in content: NSView, overlayId: String, provider: String) {
        let normalized = normalizedStreamChatProvider(provider)
        let streamlabsField: NSTextField? = findSubview(
            in: content,
            identifier: "\(overlayId)|streamChatStreamlabsUrl"
        )
        let twitchField: NSTextField? = findSubview(
            in: content,
            identifier: "\(overlayId)|streamChatTwitchChannel"
        )
        streamlabsField?.isEnabled = normalized == "streamlabs"
        twitchField?.isEnabled = normalized == "twitch"
    }

    private func normalizedStreamChatProvider(_ value: String) -> String {
        let normalized = value.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return normalized == "streamlabs" || normalized == "twitch" ? normalized : "none"
    }

    private func findSubview<T: NSView>(in view: NSView, identifier: String) -> T? {
        for subview in view.subviews {
            if subview.identifier?.rawValue == identifier {
                return subview as? T
            }
            if let found: T = findSubview(in: subview, identifier: identifier) {
                return found
            }
        }

        return nil
    }

    private func overlayFont(ofSize size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        OverlayTheme.font(family: OverlayTheme.defaultFontFamily, size: size, weight: weight)
    }
}

private final class OverlayContentColumnListView: NSView, NSTextFieldDelegate {
    private let definition: OverlayContentDefinition
    private var states: [OverlayContentColumnState]
    private let fontProvider: (CGFloat, NSFont.Weight) -> NSFont
    private let onChange: ([OverlayContentColumnState]) -> Void
    private var rowViews: [String: NSView] = [:]
    private var draggingColumnId: String?
    private let rowHeight: CGFloat = 34
    private let rowSpacing: CGFloat = 6

    init(
        frame: NSRect,
        definition: OverlayContentDefinition,
        overlay: OverlaySettings,
        fontProvider: @escaping (CGFloat, NSFont.Weight) -> NSFont,
        onChange: @escaping ([OverlayContentColumnState]) -> Void
    ) {
        self.definition = definition
        self.states = OverlayContentColumns.columnStates(for: definition, settings: overlay)
        self.fontProvider = fontProvider
        self.onChange = onChange
        super.init(frame: frame)
        wantsLayer = true
        layer?.backgroundColor = NSColor.clear.cgColor
        buildRows()
    }

    required init?(coder: NSCoder) {
        nil
    }

    private func buildRows() {
        subviews.forEach { $0.removeFromSuperview() }
        rowViews.removeAll()

        for state in states {
            let row = NSView(frame: NSRect(x: 0, y: 0, width: bounds.width, height: rowHeight))
            row.wantsLayer = true
            row.layer?.cornerRadius = 4

            let handle = OverlayContentColumnDragHandleView(
                frame: NSRect(x: 8, y: 5, width: 44, height: 24),
                columnId: state.definition.id,
                owner: self,
                font: fontProvider(12, .regular)
            )
            row.addSubview(handle)

            row.addSubview(rowLabel(state.definition.label, frame: NSRect(x: 84, y: 5, width: 190, height: 24)))

            let enabled = NSButton(checkboxWithTitle: "", target: self, action: #selector(enabledChanged(_:)))
            enabled.frame = NSRect(x: 318, y: 5, width: 32, height: 24)
            enabled.state = state.enabled ? .on : .off
            enabled.identifier = NSUserInterfaceItemIdentifier(state.definition.id)
            row.addSubview(enabled)

            let width = NSTextField(string: String(state.width))
            width.frame = NSRect(x: 386, y: 3, width: 78, height: 28)
            width.identifier = NSUserInterfaceItemIdentifier(state.definition.id)
            width.alignment = .right
            width.font = NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .regular)
            width.delegate = self
            width.target = self
            width.action = #selector(widthChanged(_:))
            row.addSubview(width)

            row.addSubview(rowLabel(
                "\(state.definition.minimumWidth)-\(state.definition.maximumWidth)",
                frame: NSRect(x: 488, y: 5, width: 90, height: 24)
            ))

            addSubview(row)
            rowViews[state.definition.id] = row
            applyRowStyle(row, enabled: state.enabled)
        }

        layoutRows()
    }

    private func rowLabel(_ text: String, frame: NSRect) -> NSTextField {
        let field = NSTextField(labelWithString: text)
        field.frame = frame
        field.textColor = NSColor(red: 0.84, green: 0.87, blue: 0.90, alpha: 1)
        field.font = fontProvider(13, .regular)
        return field
    }

    private func layoutRows() {
        for (index, state) in states.enumerated() {
            guard let row = rowViews[state.definition.id] else {
                continue
            }

            row.frame = NSRect(
                x: 0,
                y: rowY(for: index),
                width: bounds.width,
                height: rowHeight
            )
        }
    }

    private func rowY(for index: Int) -> CGFloat {
        bounds.height - rowHeight - CGFloat(index) * (rowHeight + rowSpacing)
    }

    private func targetIndex(for point: NSPoint) -> Int {
        guard !states.isEmpty else {
            return 0
        }

        let topDistance = max(0, bounds.height - point.y)
        return min(states.count - 1, max(0, Int(topDistance / (rowHeight + rowSpacing))))
    }

    @objc private func enabledChanged(_ sender: NSButton) {
        guard let id = sender.identifier?.rawValue,
              let index = states.firstIndex(where: { $0.definition.id == id }) else {
            return
        }

        states[index].enabled = sender.state == .on
        if let row = rowViews[id] {
            applyRowStyle(row, enabled: states[index].enabled)
        }
        onChange(states)
    }

    @objc private func widthChanged(_ sender: NSTextField) {
        applyWidth(sender)
    }

    func controlTextDidEndEditing(_ obj: Notification) {
        guard let field = obj.object as? NSTextField else {
            return
        }

        applyWidth(field)
    }

    private func applyWidth(_ field: NSTextField) {
        guard let id = field.identifier?.rawValue,
              let index = states.firstIndex(where: { $0.definition.id == id }) else {
            return
        }

        let definition = states[index].definition
        let parsed = Int(field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)) ?? states[index].width
        let clamped = min(max(parsed, definition.minimumWidth), definition.maximumWidth)
        states[index].width = clamped
        field.stringValue = String(clamped)
        onChange(states)
    }

    fileprivate func beginDragging(columnId: String) {
        draggingColumnId = columnId
    }

    fileprivate func dragColumn(with event: NSEvent) {
        guard let draggingColumnId,
              let sourceIndex = states.firstIndex(where: { $0.definition.id == draggingColumnId }) else {
            return
        }

        let target = targetIndex(for: convert(event.locationInWindow, from: nil))
        guard target != sourceIndex else {
            return
        }

        var moved = states.remove(at: sourceIndex)
        moved.order = target + 1
        states.insert(moved, at: target)
        normalizeOrder()
        layoutRows()
    }

    fileprivate func endDragging() {
        guard draggingColumnId != nil else {
            return
        }

        draggingColumnId = nil
        normalizeOrder()
        onChange(states)
    }

    private func normalizeOrder() {
        for index in states.indices {
            states[index].order = index + 1
        }
    }

    private func applyRowStyle(_ row: NSView, enabled: Bool) {
        row.layer?.backgroundColor = enabled
            ? NSColor(red: 0.094, green: 0.118, blue: 0.133, alpha: 1).cgColor
            : NSColor(red: 0.070, green: 0.090, blue: 0.105, alpha: 1).cgColor
        for subview in row.subviews {
            if let field = subview as? NSTextField {
                field.textColor = enabled
                    ? NSColor(red: 0.84, green: 0.87, blue: 0.90, alpha: 1)
                    : NSColor(red: 0.48, green: 0.54, blue: 0.58, alpha: 1)
            }
        }
    }
}

private final class OverlayContentColumnDragHandleView: NSView {
    private let columnId: String
    private weak var owner: OverlayContentColumnListView?
    private let font: NSFont

    init(frame: NSRect, columnId: String, owner: OverlayContentColumnListView, font: NSFont) {
        self.columnId = columnId
        self.owner = owner
        self.font = font
        super.init(frame: frame)
    }

    required init?(coder: NSCoder) {
        nil
    }

    override func draw(_ dirtyRect: NSRect) {
        super.draw(dirtyRect)
        let attributes: [NSAttributedString.Key: Any] = [
            .foregroundColor: NSColor(red: 0.70, green: 0.76, blue: 0.79, alpha: 1),
            .font: font
        ]
        "::".draw(in: bounds.insetBy(dx: 0, dy: 4), withAttributes: attributes)
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: .openHand)
    }

    override func mouseDown(with event: NSEvent) {
        owner?.beginDragging(columnId: columnId)
    }

    override func mouseDragged(with event: NSEvent) {
        owner?.dragColumn(with: event)
    }

    override func mouseUp(with event: NSEvent) {
        owner?.endDragging()
    }
}
