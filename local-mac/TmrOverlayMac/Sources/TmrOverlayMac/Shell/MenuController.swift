import AppKit

final class MenuController: NSObject {
    private let state: TelemetryCaptureState
    private let captureRoot: URL
    private let logsRoot: URL
    private let diagnosticsBundleWriter: DiagnosticsBundleWriter
    private let events: AppEventRecorder
    private let logger: LocalLogWriter
    private let openSettingsAction: () -> Void
    private let demoActions: [(String, () -> Void)]
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let statusMenuItem = NSMenuItem(title: "Waiting for iRacing", action: nil, keyEquivalent: "")
    private let captureMenuItem = NSMenuItem(title: "Open Latest Capture", action: #selector(openCapture), keyEquivalent: "")
    private let rootMenuItem = NSMenuItem(title: "Open Capture Root", action: #selector(openCaptureRoot), keyEquivalent: "")
    private let logsMenuItem = NSMenuItem(title: "Open Logs", action: #selector(openLogs), keyEquivalent: "")
    private let settingsMenuItem = NSMenuItem(title: "Open Settings", action: #selector(openSettings), keyEquivalent: "")
    private let diagnosticsMenuItem = NSMenuItem(title: "Create Diagnostics Bundle", action: #selector(createDiagnosticsBundle), keyEquivalent: "")

    init(
        state: TelemetryCaptureState,
        captureRoot: URL,
        logsRoot: URL,
        diagnosticsBundleWriter: DiagnosticsBundleWriter,
        events: AppEventRecorder,
        logger: LocalLogWriter,
        openSettings: @escaping () -> Void,
        demoActions: [(String, () -> Void)] = []
    ) {
        self.state = state
        self.captureRoot = captureRoot
        self.logsRoot = logsRoot
        self.diagnosticsBundleWriter = diagnosticsBundleWriter
        self.events = events
        self.logger = logger
        self.openSettingsAction = openSettings
        self.demoActions = demoActions
        super.init()

        statusItem.button?.title = "TMR"
        statusItem.button?.toolTip = "Tech Mates Racing Overlay"

        captureMenuItem.target = self
        rootMenuItem.target = self
        logsMenuItem.target = self
        settingsMenuItem.target = self
        diagnosticsMenuItem.target = self

        let menu = NSMenu()
        statusMenuItem.isEnabled = false
        menu.addItem(statusMenuItem)
        menu.addItem(.separator())
        menu.addItem(captureMenuItem)
        menu.addItem(rootMenuItem)
        menu.addItem(logsMenuItem)
        menu.addItem(settingsMenuItem)
        menu.addItem(diagnosticsMenuItem)
        if !demoActions.isEmpty {
            menu.addItem(.separator())
            for (index, action) in demoActions.enumerated() {
                let item = NSMenuItem(title: action.0, action: #selector(runDemoAction), keyEquivalent: "")
                item.target = self
                item.tag = index
                menu.addItem(item)
            }
        }
        menu.addItem(.separator())
        let exitItem = NSMenuItem(title: "Exit", action: #selector(exitApplication), keyEquivalent: "q")
        exitItem.target = self
        menu.addItem(exitItem)

        statusItem.menu = menu
        refresh()
    }

    func refresh() {
        let snapshot = state.snapshot()

        if snapshot.isCapturing {
            if snapshot.rawCaptureEnabled {
                statusMenuItem.title = "Capturing \(snapshot.frameCount.formatted()) frames"
                captureMenuItem.isEnabled = snapshot.currentCaptureDirectory != nil
                captureMenuItem.title = "Open Current Capture"
            } else {
                statusMenuItem.title = "Analyzing \(snapshot.frameCount.formatted()) frames"
                captureMenuItem.isEnabled = snapshot.lastCaptureDirectory != nil
                captureMenuItem.title = "Open Latest Raw Capture"
            }
            return
        }

        statusMenuItem.title = snapshot.isConnected ? "Connected to iRacing" : "Waiting for iRacing"
        captureMenuItem.isEnabled = snapshot.lastCaptureDirectory != nil
        captureMenuItem.title = snapshot.rawCaptureEnabled ? "Open Latest Capture" : "Open Latest Raw Capture"
    }

    @objc private func openCapture() {
        let snapshot = state.snapshot()
        let url = snapshot.currentCaptureDirectory ?? snapshot.lastCaptureDirectory ?? captureRoot
        openDirectory(url)
    }

    @objc private func openCaptureRoot() {
        openDirectory(captureRoot)
    }

    @objc private func openLogs() {
        openDirectory(logsRoot)
    }

    @objc private func openSettings() {
        openSettingsAction()
    }

    @objc private func createDiagnosticsBundle() {
        do {
            let bundleURL = try diagnosticsBundleWriter.createBundle()
            events.record("diagnostics_bundle_created", properties: ["bundlePath": bundleURL.path])
            logger.info("Created diagnostics bundle \(bundleURL.path).")
            openDirectory(bundleURL)
        } catch {
            events.record("diagnostics_bundle_failed", properties: ["error": String(describing: error)])
            logger.error("Failed to create diagnostics bundle: \(error)")
        }
    }

    @objc private func exitApplication() {
        NSApp.terminate(nil)
    }

    @objc private func runDemoAction(_ sender: NSMenuItem) {
        guard sender.tag >= 0, sender.tag < demoActions.count else {
            return
        }

        demoActions[sender.tag].1()
    }

    private func openDirectory(_ url: URL) {
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        NSWorkspace.shared.open(url)
    }
}
