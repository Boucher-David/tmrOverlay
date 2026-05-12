@testable import TmrOverlayMacCore
import XCTest

final class AppBoilerplateTests: XCTestCase {
    func testSettingsStorePersistsOverlaySettings() throws {
        let root = temporaryRoot("settings")
        defer {
            try? FileManager.default.removeItem(at: root)
        }

        let store = AppSettingsStore(settingsRoot: root)
        var settings = store.load()
        var overlay = settings.overlay(id: "status", defaultSize: CGSize(width: 304, height: 92))
        overlay.x = 128
        overlay.y = 256
        settings.updateOverlay(overlay)

        store.save(settings)

        let reloaded = AppSettingsStore(settingsRoot: root).load()
        XCTAssertEqual(reloaded.overlays.first?.id, "status")
        XCTAssertEqual(reloaded.overlays.first?.x, 128)
        XCTAssertEqual(reloaded.overlays.first?.y, 256)
    }

    func testSettingsStoreCompactsLegacyFullScreenFlagsOverlay() throws {
        let root = temporaryRoot("settings")
        defer {
            try? FileManager.default.removeItem(at: root)
        }

        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let settingsURL = root.appendingPathComponent("settings.json")
        try """
        {
          "settingsVersion": 4,
          "overlays": [
            {
              "id": "flags",
              "enabled": true,
              "scale": 1.7,
              "x": 0,
              "y": 0,
              "width": 1920,
              "height": 1440,
              "screenId": "primary-screen-default"
            }
          ]
        }
        """.write(to: settingsURL, atomically: true, encoding: .utf8)

        let reloaded = AppSettingsStore(settingsRoot: root).load()
        let flags = try XCTUnwrap(reloaded.overlays.first)
        XCTAssertEqual(flags.id, "flags")
        XCTAssertTrue(flags.enabled)
        XCTAssertEqual(flags.scale, 1)
        XCTAssertEqual(flags.width, 360)
        XCTAssertEqual(flags.height, 170)
        XCTAssertEqual(flags.screenId, "primary-screen-default")
    }

    func testSettingsStoreCanonicalizesStreamChatSharedDefaultsIntoOptions() throws {
        let root = temporaryRoot("settings")
        defer {
            try? FileManager.default.removeItem(at: root)
        }

        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        let settingsURL = root.appendingPathComponent("settings.json")
        try """
        {
          "settingsVersion": 5,
          "overlays": [
            {
              "id": "stream-chat",
              "streamChatProvider": "",
              "streamChatTwitchChannel": "",
              "options": {}
            }
          ]
        }
        """.write(to: settingsURL, atomically: true, encoding: .utf8)

        let reloaded = AppSettingsStore(settingsRoot: root).load()
        let streamChat = try XCTUnwrap(reloaded.overlays.first)
        XCTAssertEqual(reloaded.settingsVersion, SharedOverlayContract.current.settingsVersion)
        XCTAssertEqual(streamChat.streamChatProvider, "twitch")
        XCTAssertEqual(streamChat.streamChatTwitchChannel, "techmatesracing")
        XCTAssertEqual(streamChat.options[SharedOverlayContract.streamChatProviderKey], "twitch")
        XCTAssertEqual(streamChat.options[SharedOverlayContract.streamChatTwitchChannelKey], "techmatesracing")
    }

    func testSharedContractDrivesStreamChatDefaultsAndDesignTokens() {
        XCTAssertEqual(StreamChatProviderOptions.defaultProvider, "twitch")
        XCTAssertEqual(StreamChatProviderOptions.defaultTwitchChannel, "techmatesracing")
        XCTAssertEqual(SharedOverlayContract.current.settingsVersion, 10)
        XCTAssertEqual(SharedOverlayContract.current.designV2ColorHex("cyan", fallback: ""), "#00E8FF")
    }

    func testOverlaySettingsAreIndependentById() {
        var settings = ApplicationSettings()
        var status = settings.overlay(id: "status", defaultSize: CGSize(width: 520, height: 150))
        var fuel = settings.overlay(id: "fuel-calculator", defaultSize: CGSize(width: 360, height: 180))
        status.x = 64
        fuel.x = 512
        settings.updateOverlay(status)
        settings.updateOverlay(fuel)

        let reloadedStatus = settings.overlay(id: "status", defaultSize: CGSize(width: 100, height: 100))

        XCTAssertEqual(settings.overlays.count, 2)
        XCTAssertEqual(reloadedStatus.x, 64)
        XCTAssertEqual(settings.overlays.first(where: { $0.id == "fuel-calculator" })?.x, 512)
        XCTAssertEqual(reloadedStatus.width, 520)
    }

    func testRetentionRemovesOldCapturesAndDiagnostics() throws {
        let root = temporaryRoot("retention")
        defer {
            try? FileManager.default.removeItem(at: root)
        }

        let captureRoot = root.appendingPathComponent("captures", isDirectory: true)
        let diagnosticsRoot = root.appendingPathComponent("diagnostics", isDirectory: true)
        let keepCapture = captureRoot.appendingPathComponent("capture-keep", isDirectory: true)
        let deleteCapture = captureRoot.appendingPathComponent("capture-delete", isDirectory: true)
        let keepBundle = diagnosticsRoot.appendingPathComponent("keep.diagnostics", isDirectory: true)
        let deleteBundle = diagnosticsRoot.appendingPathComponent("delete.diagnostics", isDirectory: true)

        for url in [keepCapture, deleteCapture, keepBundle, deleteBundle] {
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        }

        let oldDate = Date().addingTimeInterval(-10 * 24 * 60 * 60)
        try FileManager.default.setAttributes([.modificationDate: oldDate], ofItemAtPath: deleteCapture.path)
        try FileManager.default.setAttributes([.modificationDate: oldDate], ofItemAtPath: deleteBundle.path)

        RetentionService().clean(
            captureRoot: captureRoot,
            diagnosticsRoot: diagnosticsRoot,
            captureRetentionDays: 1,
            maxCaptureDirectories: 10,
            diagnosticsRetentionDays: 1,
            maxDiagnosticsBundles: 10
        )

        XCTAssertTrue(FileManager.default.fileExists(atPath: keepCapture.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: deleteCapture.path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: keepBundle.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: deleteBundle.path))
    }

    func testDiagnosticsBundleIncludesTriageFilesAndExcludesTelemetryBin() throws {
        let root = temporaryRoot("diagnostics")
        defer {
            try? FileManager.default.removeItem(at: root)
        }

        let captureRoot = root.appendingPathComponent("captures", isDirectory: true)
        let historyRoot = root.appendingPathComponent("history", isDirectory: true)
        let diagnosticsRoot = root.appendingPathComponent("diagnostics", isDirectory: true)
        let logsRoot = root.appendingPathComponent("logs", isDirectory: true)
        let settingsRoot = root.appendingPathComponent("settings", isDirectory: true)
        let eventsRoot = logsRoot.appendingPathComponent("events", isDirectory: true)
        let overlayDiagnosticsRoot = logsRoot.appendingPathComponent("overlay-diagnostics", isDirectory: true)
        let runtimeStateURL = root.appendingPathComponent("runtime-state.json")
        let capture = captureRoot.appendingPathComponent("capture-test", isDirectory: true)

        for url in [capture, historyRoot, diagnosticsRoot, logsRoot, settingsRoot, eventsRoot, overlayDiagnosticsRoot] {
            try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        }

        try "{}".write(to: runtimeStateURL, atomically: true, encoding: .utf8)
        try """
        {
          "overlays": [
            {
              "id": "stream-chat",
              "streamChatProvider": "streamlabs",
              "streamChatStreamlabsUrl": "https://streamlabs.com/widgets/chat-box/private-token",
              "streamChatTwitchChannel": "tmracing",
              "options": {
                "stream-chat.provider": "streamlabs",
                "stream-chat.streamlabs-url": "https://streamlabs.com/widgets/chat-box/private-token",
                "stream-chat.twitch-channel": "tmracing"
              }
            }
          ]
        }
        """.write(to: settingsRoot.appendingPathComponent("settings.json"), atomically: true, encoding: .utf8)
        try "log".write(to: logsRoot.appendingPathComponent("tmroverlay-test.log"), atomically: true, encoding: .utf8)
        try "{}".write(to: eventsRoot.appendingPathComponent("events-test.jsonl"), atomically: true, encoding: .utf8)
        try "{}".write(to: overlayDiagnosticsRoot.appendingPathComponent("session-test-live-overlay-diagnostics.json"), atomically: true, encoding: .utf8)
        try "{}".write(to: capture.appendingPathComponent("capture-manifest.json"), atomically: true, encoding: .utf8)
        try "[]".write(to: capture.appendingPathComponent("telemetry-schema.json"), atomically: true, encoding: .utf8)
        try "WeekendInfo: {}".write(to: capture.appendingPathComponent("latest-session.yaml"), atomically: true, encoding: .utf8)
        try "{}".write(to: capture.appendingPathComponent("live-overlay-diagnostics.json"), atomically: true, encoding: .utf8)
        try "raw".write(to: capture.appendingPathComponent("telemetry.bin"), atomically: true, encoding: .utf8)

        let bundle = try DiagnosticsBundleWriter(
            appDataRoot: root,
            captureRoot: captureRoot,
            historyRoot: historyRoot,
            diagnosticsRoot: diagnosticsRoot,
            logsRoot: logsRoot,
            settingsRoot: settingsRoot,
            eventsRoot: eventsRoot,
            runtimeStateURL: runtimeStateURL,
            makeTelemetrySnapshot: {
                TelemetryCaptureStatusSnapshot.idleWithCaptureRoot(captureRoot)
            }
        ).createBundle()

        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("metadata").appendingPathComponent("app-version.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("metadata").appendingPathComponent("storage.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("metadata").appendingPathComponent("shared-settings-contract.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("metadata").appendingPathComponent("ui-freeze-watch.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("shared").appendingPathComponent("tmr-overlay-contract.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("shared").appendingPathComponent("tmr-overlay-contract.schema.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("runtime").appendingPathComponent("runtime-state.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("settings").appendingPathComponent("settings.json").path))
        let bundledSettings = try Data(contentsOf: bundle.appendingPathComponent("settings").appendingPathComponent("settings.json"))
        let settingsJson = try JSONSerialization.jsonObject(with: bundledSettings) as? [String: Any]
        let overlays = settingsJson?["overlays"] as? [[String: Any]]
        XCTAssertEqual(overlays?.first?["streamChatStreamlabsUrl"] as? String, "<redacted>")
        XCTAssertEqual(overlays?.first?["streamChatTwitchChannel"] as? String, "tmracing")
        let options = overlays?.first?["options"] as? [String: Any]
        XCTAssertEqual(options?["stream-chat.streamlabs-url"] as? String, "<redacted>")
        XCTAssertEqual(options?["stream-chat.twitch-channel"] as? String, "tmracing")
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("logs").appendingPathComponent("tmroverlay-test.log").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("overlay-diagnostics").appendingPathComponent("session-test-live-overlay-diagnostics.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("events").appendingPathComponent("events-test.jsonl").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("latest-capture").appendingPathComponent("capture-manifest.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("latest-capture").appendingPathComponent("telemetry-schema.json").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("latest-capture").appendingPathComponent("latest-session.yaml").path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("latest-capture").appendingPathComponent("live-overlay-diagnostics.json").path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: bundle.appendingPathComponent("latest-capture").appendingPathComponent("telemetry.bin").path))
    }

    private func temporaryRoot(_ purpose: String) -> URL {
        FileManager.default.temporaryDirectory
            .appendingPathComponent("tmr-overlay-mac-\(purpose)-tests", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
    }
}
