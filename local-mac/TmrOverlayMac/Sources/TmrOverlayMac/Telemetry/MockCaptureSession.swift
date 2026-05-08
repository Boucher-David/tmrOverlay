import Foundation

final class MockCaptureSession {
    private static let telemetryFileName = "telemetry.bin"
    private static let schemaFileName = "telemetry-schema.json"
    private static let manifestFileName = "capture-manifest.json"
    private static let latestSessionInfoFileName = "latest-session.yaml"
    private static let sessionInfoDirectoryName = "session-info"
    private static let tickRate = 60
    private static let bufferLength = 64
    private static let schema = [
        TelemetryVariableSchema(name: "SessionTime", typeName: "Double", typeCode: 5, count: 1, offset: 0, byteSize: 8, length: 8, unit: "s", description: "Seconds since session start"),
        TelemetryVariableSchema(name: "SessionTick", typeName: "Int", typeCode: 3, count: 1, offset: 8, byteSize: 4, length: 4, unit: "", description: "Mock session tick"),
        TelemetryVariableSchema(name: "FuelLevel", typeName: "Double", typeCode: 5, count: 1, offset: 16, byteSize: 8, length: 8, unit: "l", description: "Mock fuel level"),
        TelemetryVariableSchema(name: "FuelLevelPct", typeName: "Double", typeCode: 5, count: 1, offset: 24, byteSize: 8, length: 8, unit: "%", description: "Mock fuel percentage"),
        TelemetryVariableSchema(name: "Speed", typeName: "Double", typeCode: 5, count: 1, offset: 32, byteSize: 8, length: 8, unit: "m/s", description: "Mock vehicle speed"),
        TelemetryVariableSchema(name: "Gear", typeName: "Int", typeCode: 3, count: 1, offset: 40, byteSize: 4, length: 4, unit: "", description: "Mock current gear"),
        TelemetryVariableSchema(name: "LapDistPct", typeName: "Double", typeCode: 5, count: 1, offset: 48, byteSize: 8, length: 8, unit: "%", description: "Mock lap progress")
    ]

    let directoryURL: URL
    let startedAtUtc: Date

    var captureId: String {
        manifest.captureId
    }

    var frameCount: Int {
        frameIndex
    }

    private let historyRoot: URL
    private let telemetryFileHandle: FileHandle
    private let telemetryURL: URL
    private let manifestURL: URL
    private let sessionStartMonotonic = Date()
    private var manifest: CaptureManifest
    private var frameIndex = 0
    private var previousHistorySample: MockHistorySample?
    private var validGreenTimeSeconds = 0.0
    private var validDistanceLaps = 0.0
    private var fuelUsedLiters = 0.0
    private var startingFuelLiters: Double?
    private var endingFuelLiters: Double?
    private var minimumFuelLiters: Double?
    private var maximumFuelLiters: Double?
    private var finished = false

    init(rootDirectory: URL, historyRoot: URL) throws {
        startedAtUtc = Date()
        self.historyRoot = historyRoot
        let captureId = "capture-\(Self.captureIdFormatter.string(from: startedAtUtc))"
        directoryURL = rootDirectory.appendingPathComponent(captureId, isDirectory: true)
        manifestURL = directoryURL.appendingPathComponent(Self.manifestFileName)

        let fileManager = FileManager.default
        try fileManager.createDirectory(at: directoryURL, withIntermediateDirectories: true)
        try fileManager.createDirectory(
            at: directoryURL.appendingPathComponent(Self.sessionInfoDirectoryName, isDirectory: true),
            withIntermediateDirectories: true
        )

        manifest = CaptureManifest(
            formatVersion: 1,
            captureId: captureId,
            startedAtUtc: startedAtUtc,
            finishedAtUtc: nil,
            telemetryFile: Self.telemetryFileName,
            schemaFile: Self.schemaFileName,
            latestSessionInfoFile: Self.latestSessionInfoFileName,
            sessionInfoDirectory: Self.sessionInfoDirectoryName,
            sdkVersion: 0,
            tickRate: Self.tickRate,
            bufferLength: Self.bufferLength,
            variableCount: Self.schema.count,
            frameCount: 0,
            droppedFrameCount: 0,
            sessionInfoSnapshotCount: 1
        )

        try Self.writeJson(Self.schema, to: directoryURL.appendingPathComponent(Self.schemaFileName))
        try Self.writeSessionInfo(to: directoryURL)
        try Self.writeJson(manifest, to: manifestURL)

        telemetryURL = directoryURL.appendingPathComponent(Self.telemetryFileName)
        fileManager.createFile(atPath: telemetryURL.path, contents: nil)
        telemetryFileHandle = try FileHandle(forWritingTo: telemetryURL)
        try telemetryFileHandle.write(contentsOf: fileHeader())
    }

    func writeNextFrame() throws -> Date {
        frameIndex += 1

        let capturedAtUtc = Date()
        let sessionTime = capturedAtUtc.timeIntervalSince(sessionStartMonotonic)
        let payload = makePayload(frameIndex: frameIndex, sessionTime: sessionTime)
        var frame = Data(capacity: 32 + payload.count)
        frame.appendInt64LE(capturedAtUtc.unixTimeMilliseconds)
        frame.appendInt32LE(Int32(frameIndex))
        frame.appendInt32LE(Int32(frameIndex))
        frame.appendInt32LE(1)
        frame.appendDoubleLE(sessionTime)
        frame.appendInt32LE(Int32(payload.count))
        frame.append(payload)

        try telemetryFileHandle.write(contentsOf: frame)
        manifest.frameCount = frameIndex
        return capturedAtUtc
    }

    func telemetryFileBytes() -> Int64? {
        guard let attributes = try? FileManager.default.attributesOfItem(atPath: telemetryURL.path),
              let size = attributes[.size] as? NSNumber else {
            return nil
        }

        return size.int64Value
    }

    @discardableResult
    func finish() -> HistoricalSessionSummary? {
        guard !finished else {
            return nil
        }

        finished = true

        do {
            try telemetryFileHandle.synchronize()
            try telemetryFileHandle.close()
            let finishedAtUtc = Date()
            manifest.finishedAtUtc = finishedAtUtc
            try Self.writeJson(manifest, to: manifestURL)
            let summary = makeHistorySummary(finishedAtUtc: finishedAtUtc)
            try SessionHistoryWriter(historyRoot: historyRoot).save(summary: summary)
            return summary
        } catch {
            NSLog("Failed to finalize mock capture: \(error)")
            return nil
        }
    }

    private func fileHeader() -> Data {
        var header = Data(capacity: 32)
        header.append("TMRCAP01".data(using: .ascii)!)
        header.appendInt32LE(0)
        header.appendInt32LE(Int32(Self.tickRate))
        header.appendInt32LE(Int32(Self.bufferLength))
        header.appendInt32LE(Int32(Self.schema.count))
        header.appendInt64LE(startedAtUtc.unixTimeMilliseconds)
        return header
    }

    private func makePayload(frameIndex: Int, sessionTime: TimeInterval) -> Data {
        let fuelLevel = max(0, 106.0 - sessionTime * 0.04)
        let speed = 32.0 + sin(sessionTime * 1.2) * 18.0
        let gear = min(6, max(1, Int(speed / 12.0)))
        let lapDistance = sessionTime.truncatingRemainder(dividingBy: 96.0) / 96.0
        recordHistorySample(MockHistorySample(
            sessionTime: sessionTime,
            fuelLevelLiters: fuelLevel,
            speedMetersPerSecond: speed,
            lapDistancePct: lapDistance
        ))

        var payload = Data(count: Self.bufferLength)
        payload.replaceDoubleLE(sessionTime, at: 0)
        payload.replaceInt32LE(Int32(frameIndex), at: 8)
        payload.replaceDoubleLE(fuelLevel, at: 16)
        payload.replaceDoubleLE(fuelLevel / 106.0, at: 24)
        payload.replaceDoubleLE(speed, at: 32)
        payload.replaceInt32LE(Int32(gear), at: 40)
        payload.replaceDoubleLE(lapDistance, at: 48)
        return payload
    }

    private func recordHistorySample(_ sample: MockHistorySample) {
        startingFuelLiters = startingFuelLiters ?? sample.fuelLevelLiters
        endingFuelLiters = sample.fuelLevelLiters
        minimumFuelLiters = minimumFuelLiters.map { min($0, sample.fuelLevelLiters) } ?? sample.fuelLevelLiters
        maximumFuelLiters = maximumFuelLiters.map { max($0, sample.fuelLevelLiters) } ?? sample.fuelLevelLiters

        defer {
            previousHistorySample = sample
        }

        guard let previousHistorySample else {
            return
        }

        let deltaSeconds = sample.sessionTime - previousHistorySample.sessionTime
        guard deltaSeconds > 0, deltaSeconds < 1 else {
            return
        }

        validGreenTimeSeconds += deltaSeconds
        validDistanceLaps += Self.lapDistanceDelta(from: previousHistorySample.lapDistancePct, to: sample.lapDistancePct)

        let fuelDelta = previousHistorySample.fuelLevelLiters - sample.fuelLevelLiters
        if fuelDelta > 0, fuelDelta < 0.05 {
            fuelUsedLiters += fuelDelta
        }
    }

    private func makeHistorySummary(finishedAtUtc: Date) -> HistoricalSessionSummary {
        let fuelPerHour = validGreenTimeSeconds >= 30 && fuelUsedLiters > 0
            ? fuelUsedLiters / validGreenTimeSeconds * 3600
            : nil
        let fuelPerLap = validDistanceLaps >= 0.25 && fuelUsedLiters > 0
            ? fuelUsedLiters / validDistanceLaps
            : nil
        let hasBaselineValue = fuelPerLap != nil

        return HistoricalSessionSummary.mock(
            sourceCaptureId: manifest.captureId,
            startedAtUtc: startedAtUtc,
            finishedAtUtc: finishedAtUtc,
            frameCount: frameIndex,
            captureDurationSeconds: max(0, finishedAtUtc.timeIntervalSince(startedAtUtc)),
            validGreenTimeSeconds: validGreenTimeSeconds,
            validDistanceLaps: validDistanceLaps,
            fuelUsedLiters: fuelUsedLiters,
            fuelPerHourLiters: fuelPerHour,
            fuelPerLapLiters: fuelPerLap,
            startingFuelLiters: startingFuelLiters,
            endingFuelLiters: endingFuelLiters,
            minimumFuelLiters: minimumFuelLiters,
            maximumFuelLiters: maximumFuelLiters,
            contributesToBaseline: hasBaselineValue
        )
    }

    private static func lapDistanceDelta(from previous: Double, to current: Double) -> Double {
        current >= previous ? current - previous : (1.0 - previous) + current
    }

    private static func writeJson<T: Encodable>(_ value: T, to url: URL) throws {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted]
        let data = try encoder.encode(value)
        try data.write(to: url, options: .atomic)
    }

    private static func writeSessionInfo(to directoryURL: URL) throws {
        let yaml = """
        WeekendInfo:
          TrackID: 0
          TrackName: macOS Local Dev
          TrackDisplayName: macOS Local Dev
          TrackConfigName: Mock Overlay Loop
          EventType: Local Mock
          Category: road
          BuildVersion: mac-dev
        SessionInfo:
          CurrentSessionNum: 0
          Sessions:
            - SessionNum: 0
              SessionType: Practice
              SessionName: Practice
              SessionTime: unlimited
              SessionLaps: unlimited
        DriverInfo:
          DriverCarIdx: 0
          DriverCarFuelKgPerLtr: 0.750
          DriverCarFuelMaxLtr: 106.000
          DriverCarEstLapTime: 96.0000
          Drivers:
            - CarIdx: 0
              UserName: Local Developer
              CarPath: mock-gt3
              CarID: 0
              CarClassID: 0
              CarScreenName: Mock GT3
              CarScreenNameShort: Mock
              CarIsElectric: 0
        """

        let latestURL = directoryURL.appendingPathComponent(latestSessionInfoFileName)
        let snapshotURL = directoryURL
            .appendingPathComponent(sessionInfoDirectoryName, isDirectory: true)
            .appendingPathComponent("session-0001.yaml")

        try yaml.write(to: latestURL, atomically: true, encoding: .utf8)
        try yaml.write(to: snapshotURL, atomically: true, encoding: .utf8)
    }

    private static let captureIdFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyyMMdd-HHmmss-SSS"
        return formatter
    }()
}
