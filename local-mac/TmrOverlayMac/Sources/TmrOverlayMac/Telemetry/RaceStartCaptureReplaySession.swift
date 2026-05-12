import Foundation

final class RaceStartCaptureReplaySession {
    struct Configuration {
        var captureDirectory: URL
        var startFrameIndex: Int
        var endFrameIndex: Int
        var playbackSpeed: Double

        static func fromEnvironment() -> Configuration? {
            let environment = ProcessInfo.processInfo.environment
            let replayRequested = Self.boolValue(environment["TMR_MAC_RACE_START_REPLAY"])
                || environment["TMR_MAC_REPLAY_CAPTURE"] != nil
            guard replayRequested else {
                return nil
            }

            let captureDirectory = environment["TMR_MAC_REPLAY_CAPTURE"].flatMap {
                URL(fileURLWithPath: ($0 as NSString).expandingTildeInPath, isDirectory: true)
            } ?? defaultCaptureDirectory()

            guard let captureDirectory else {
                return nil
            }

            return Configuration(
                captureDirectory: captureDirectory,
                startFrameIndex: Self.intValue(environment["TMR_MAC_REPLAY_START_FRAME"]) ?? 150_035,
                endFrameIndex: Self.intValue(environment["TMR_MAC_REPLAY_END_FRAME"]) ?? 157_235,
                playbackSpeed: max(0.1, Self.doubleValue(environment["TMR_MAC_REPLAY_PLAYBACK_SPEED"]) ?? 1.0)
            )
        }

        private static func defaultCaptureDirectory() -> URL? {
            let relativePath = "captures/capture-20260426-130334-932"
            var candidates: [URL] = []
            var current = URL(fileURLWithPath: FileManager.default.currentDirectoryPath, isDirectory: true)
            for _ in 0..<8 {
                candidates.append(current.appendingPathComponent(relativePath, isDirectory: true))
                let parent = current.deletingLastPathComponent()
                if parent.path == current.path {
                    break
                }
                current = parent
            }
            candidates.append(URL(fileURLWithPath: "/Users/davidboucher/Code/tmrOverlay/\(relativePath)", isDirectory: true))

            return candidates.first {
                FileManager.default.fileExists(atPath: $0.appendingPathComponent("telemetry.bin").path)
            }
        }

        private static func boolValue(_ value: String?) -> Bool {
            guard let value else {
                return false
            }

            return ["true", "1", "yes", "on"].contains(value.trimmingCharacters(in: .whitespacesAndNewlines).lowercased())
        }

        private static func intValue(_ value: String?) -> Int? {
            guard let value else {
                return nil
            }

            return Int(value.trimmingCharacters(in: .whitespacesAndNewlines))
        }

        private static func doubleValue(_ value: String?) -> Double? {
            guard let value else {
                return nil
            }

            return Double(value.trimmingCharacters(in: .whitespacesAndNewlines))
        }
    }

    let sourceId: String
    let startedAtUtc: Date
    let captureDirectory: URL

    private let reader: RaceStartCaptureReader
    private let startFrameIndex: Int
    private let endFrameIndex: Int
    private let playbackSpeed: Double
    private let sessionStartMonotonic = Date()

    init(configuration: Configuration) throws {
        reader = try RaceStartCaptureReader(captureDirectory: configuration.captureDirectory)
        captureDirectory = configuration.captureDirectory
        let maximumFrameIndex = max(0, reader.frameCount - 1)
        let clampedStart = min(max(0, configuration.startFrameIndex), maximumFrameIndex)
        let clampedEnd = min(max(clampedStart, configuration.endFrameIndex), maximumFrameIndex)
        startFrameIndex = clampedStart
        endFrameIndex = clampedEnd
        playbackSpeed = configuration.playbackSpeed
        startedAtUtc = Date()
        sourceId = "\(reader.captureId)-race-start-\(startFrameIndex)-\(endFrameIndex)"
    }

    func recordNextFrame() -> MockLiveTelemetryFrame? {
        let frameSpan = max(1, endFrameIndex - startFrameIndex + 1)
        let elapsed = max(0, Date().timeIntervalSince(sessionStartMonotonic)) * playbackSpeed
        let frameOffset = Int((elapsed * Double(reader.tickRate)).rounded(.down)) % frameSpan
        return try? reader.frame(at: startFrameIndex + frameOffset, capturedAtUtc: Date())
    }
}

private final class RaceStartCaptureReader {
    let captureId: String
    let tickRate: Int
    let frameCount: Int

    private static let fileHeaderLength = 32
    private static let frameHeaderLength = 32

    private let manifest: ReplayCaptureManifest
    private let variablesByName: [String: TelemetryVariableSchema]
    private let sessionInfo: ReplaySessionInfo
    private let telemetryFileHandle: FileHandle
    private let recordLength: Int

    init(captureDirectory: URL) throws {
        let manifestURL = captureDirectory.appendingPathComponent("capture-manifest.json")
        let schemaURL = captureDirectory.appendingPathComponent("telemetry-schema.json")
        let sessionURL = captureDirectory.appendingPathComponent("latest-session.yaml")
        let telemetryURL = captureDirectory.appendingPathComponent("telemetry.bin")

        let decoder = JSONDecoder()
        manifest = try decoder.decode(ReplayCaptureManifest.self, from: Data(contentsOf: manifestURL))
        let schema = try decoder.decode([TelemetryVariableSchema].self, from: Data(contentsOf: schemaURL))
        variablesByName = Dictionary(uniqueKeysWithValues: schema.map { ($0.name, $0) })
        sessionInfo = ReplaySessionInfo.parse(url: sessionURL)
        telemetryFileHandle = try FileHandle(forReadingFrom: telemetryURL)
        recordLength = Self.frameHeaderLength + manifest.bufferLength
        captureId = manifest.captureId
        tickRate = manifest.tickRate
        frameCount = manifest.frameCount
    }

    deinit {
        try? telemetryFileHandle.close()
    }

    func frame(at frameIndex: Int, capturedAtUtc: Date) throws -> MockLiveTelemetryFrame {
        let payload = try payload(at: frameIndex)
        let playerCarIdx = intValue("PlayerCarIdx", payload: payload) ?? sessionInfo.driverCarIdx ?? FourHourRacePreview.teamCarIdx
        let capturedCars = capturedCars(payload: payload, playerCarIdx: playerCarIdx)
        let playerCar = capturedCars.first { $0.carIdx == playerCarIdx }
        let sessionTime = doubleValue("SessionTime", payload: payload) ?? 0
        let sessionTimeRemain = doubleValue("SessionTimeRemain", payload: payload) ?? -1
        let sessionTimeTotal = sessionTimeRemain > 0 ? max(sessionTime + sessionTimeRemain, FourHourRacePreview.sessionLengthSeconds) : FourHourRacePreview.sessionLengthSeconds
        let estimatedLapSeconds = playerCar?.carClass.flatMap { sessionInfo.driversByCarIdx[playerCarIdx]?.estimatedLapSeconds ?? sessionInfo.estimatedLapSeconds(forClassId: $0) }
            ?? FourHourRacePreview.medianLapSeconds
        let fuelLevel = doubleValue("FuelLevel", payload: payload) ?? FourHourRacePreview.fuelLevelLiters(sessionTime: sessionTime)
        let fuelPercent = doubleValue("FuelLevelPct", payload: payload) ?? (fuelLevel / FourHourRacePreview.fuelMaxLiters)
        let leader = capturedCars.sorted(by: overallSort).first
        let classLeader = capturedCars
            .filter { playerCar?.carClass == nil || $0.carClass == playerCar?.carClass }
            .sorted(by: classSort)
            .first
        let driver = playerCar.flatMap { sessionInfo.driversByCarIdx[$0.carIdx] }
        let lap = intValue("Lap", payload: payload) ?? playerCar?.lapCompleted ?? 0
        let lapDistPct = doubleValue("LapDistPct", payload: payload) ?? playerCar?.lapDistPct ?? 0
        let onPitRoad = playerCar?.onPitRoad ?? false
        let isInGarage = boolValue("IsInGarage", payload: payload) ?? false
        let isOnTrack = boolValue("IsOnTrack", payload: payload) ?? (playerCar?.trackSurface.map { $0 > 0 } ?? true)

        return MockLiveTelemetryFrame(
            capturedAtUtc: capturedAtUtc,
            sessionTime: sessionTime,
            sessionTimeRemain: sessionTimeRemain,
            sessionTimeTotal: sessionTimeTotal,
            sessionState: intValue("SessionState", payload: payload) ?? 0,
            fuelLevelLiters: fuelLevel,
            fuelMaxLiters: FourHourRacePreview.fuelMaxLiters,
            fuelLevelPercent: fuelPercent,
            fuelUsePerHourLiters: doubleValue("FuelUsePerHour", payload: payload) ?? FourHourRacePreview.fuelUsePerHourLiters,
            estimatedLapSeconds: estimatedLapSeconds,
            teamLapCompleted: lap,
            teamLapDistPct: lapDistPct,
            leaderLapCompleted: leader?.lapCompleted ?? lap,
            leaderLapDistPct: leader?.lapDistPct ?? lapDistPct,
            teamPosition: playerCar?.overallPosition,
            teamClassPosition: playerCar?.classPosition,
            teamF2TimeSeconds: playerCar?.f2TimeSeconds,
            leaderF2TimeSeconds: leader?.f2TimeSeconds,
            classLeaderF2TimeSeconds: classLeader?.f2TimeSeconds,
            carLeftRight: intValue("CarLeftRight", payload: payload),
            playerCarIdx: playerCarIdx,
            focusCarIdx: playerCarIdx,
            lastLapTimeSeconds: nil,
            bestLapTimeSeconds: nil,
            lapDeltaToSessionBestLapSeconds: nil,
            lapDeltaToSessionBestLapOk: nil,
            isOnTrack: isOnTrack,
            isInGarage: isInGarage,
            isGarageVisible: isInGarage,
            onPitRoad: onPitRoad,
            brakeAbsActive: boolValue("BrakeABSactive", payload: payload) ?? false,
            trackWetness: intValue("TrackWetness", payload: payload) ?? 0,
            weatherDeclaredWet: boolValue("WeatherDeclaredWet", payload: payload) ?? false,
            teamDriverKey: driver?.driverName.slug() ?? "car-\(playerCarIdx)",
            teamDriverName: driver?.driverName ?? playerCar?.displayDriverName ?? MockDriverNames.displayName(for: playerCarIdx),
            teamDriverInitials: Self.initials(driver?.driverName ?? playerCar?.displayDriverName ?? "TMR"),
            driversSoFar: 1,
            capturedCars: capturedCars
        )
    }

    private func payload(at frameIndex: Int) throws -> Data {
        let offset = UInt64(Self.fileHeaderLength + frameIndex * recordLength)
        try telemetryFileHandle.seek(toOffset: offset)
        let record = telemetryFileHandle.readData(ofLength: recordLength)
        guard record.count >= Self.frameHeaderLength + manifest.bufferLength else {
            throw ReplayCaptureError.shortFrame(frameIndex)
        }

        return record.subdata(in: Self.frameHeaderLength..<(Self.frameHeaderLength + manifest.bufferLength))
    }

    private func capturedCars(payload: Data, playerCarIdx: Int) -> [CapturedReplayCar] {
        let laps = intArray("CarIdxLap", payload: payload)
        let lapDistances = doubleArray("CarIdxLapDistPct", payload: payload)
        let trackSurfaces = intArray("CarIdxTrackSurface", payload: payload)
        let pitRoad = boolArray("CarIdxOnPitRoad", payload: payload)
        let positions = intArray("CarIdxPosition", payload: payload)
        let classPositions = intArray("CarIdxClassPosition", payload: payload)
        let classes = intArray("CarIdxClass", payload: payload)
        let f2Times = doubleArray("CarIdxF2Time", payload: payload)
        let estTimes = doubleArray("CarIdxEstTime", payload: payload)
        let maximumCount = [
            laps.count,
            lapDistances.count,
            trackSurfaces.count,
            pitRoad.count,
            positions.count,
            classPositions.count,
            classes.count,
            f2Times.count,
            estTimes.count,
            sessionInfo.driversByCarIdx.keys.max().map { $0 + 1 } ?? 0
        ].max() ?? 0

        var cars: [CapturedReplayCar] = []
        for carIdx in 0..<maximumCount {
            guard let driver = sessionInfo.driversByCarIdx[carIdx],
                  !driver.isPaceCar else {
                continue
            }

            let lapDistPct = finiteNonNegative(lapDistances[safe: carIdx])
            let overallPosition = positivePosition(positions[safe: carIdx])
            let classPosition = positivePosition(classPositions[safe: carIdx])
            let trackSurface = trackSurfaces[safe: carIdx]
            let isActive = carIdx == playerCarIdx
                || overallPosition != nil
                || classPosition != nil
                || lapDistPct != nil
                || (trackSurface ?? 0) > 0
            guard isActive else {
                continue
            }

            cars.append(CapturedReplayCar(
                carIdx: carIdx,
                carNumber: driver.carNumber,
                driverName: driver.driverName,
                teamName: driver.teamName,
                carClass: positivePosition(classes[safe: carIdx]) ?? driver.carClassId,
                carClassName: driver.carClassName,
                carClassColorHex: driver.carClassColorHex,
                overallPosition: overallPosition,
                classPosition: classPosition,
                lapCompleted: laps[safe: carIdx],
                lapDistPct: lapDistPct,
                f2TimeSeconds: finiteNonNegative(f2Times[safe: carIdx]),
                estTimeSeconds: finiteNonNegative(estTimes[safe: carIdx]),
                onPitRoad: pitRoad[safe: carIdx] ?? false,
                trackSurface: trackSurface
            ))
        }

        return cars
    }

    private func intValue(_ name: String, payload: Data) -> Int? {
        guard let variable = variablesByName[name] else {
            return nil
        }

        switch variable.byteSize {
        case 4:
            return payload.int32LittleEndian(at: variable.offset)
        case 1:
            return payload.boolValue(at: variable.offset).map { $0 ? 1 : 0 }
        default:
            return nil
        }
    }

    private func doubleValue(_ name: String, payload: Data) -> Double? {
        guard let variable = variablesByName[name] else {
            return nil
        }

        switch variable.byteSize {
        case 8:
            return payload.doubleLittleEndian(at: variable.offset)
        case 4:
            return payload.floatLittleEndian(at: variable.offset).map(Double.init)
        case 1:
            return payload.boolValue(at: variable.offset).map { $0 ? 1 : 0 }
        default:
            return nil
        }
    }

    private func boolValue(_ name: String, payload: Data) -> Bool? {
        guard let variable = variablesByName[name] else {
            return nil
        }

        return payload.boolValue(at: variable.offset)
    }

    private func intArray(_ name: String, payload: Data) -> [Int] {
        guard let variable = variablesByName[name] else {
            return []
        }

        return (0..<variable.count).compactMap {
            payload.int32LittleEndian(at: variable.offset + $0 * variable.byteSize)
        }
    }

    private func doubleArray(_ name: String, payload: Data) -> [Double] {
        guard let variable = variablesByName[name] else {
            return []
        }

        return (0..<variable.count).compactMap { index in
            let offset = variable.offset + index * variable.byteSize
            switch variable.byteSize {
            case 8:
                return payload.doubleLittleEndian(at: offset)
            case 4:
                return payload.floatLittleEndian(at: offset).map(Double.init)
            default:
                return nil
            }
        }
    }

    private func boolArray(_ name: String, payload: Data) -> [Bool] {
        guard let variable = variablesByName[name] else {
            return []
        }

        return (0..<variable.count).compactMap {
            payload.boolValue(at: variable.offset + $0 * variable.byteSize)
        }
    }

    private static func initials(_ name: String) -> String {
        let letters = name
            .split(separator: " ")
            .compactMap { $0.first }
            .prefix(3)
        let initials = String(letters).uppercased()
        return initials.isEmpty ? "TMR" : initials
    }

    private func positivePosition(_ value: Int?) -> Int? {
        guard let value, value > 0 else {
            return nil
        }

        return value
    }

    private func finiteNonNegative(_ value: Double?) -> Double? {
        guard let value, value.isFinite, value >= 0 else {
            return nil
        }

        return value
    }

    private func classSort(_ left: CapturedReplayCar, _ right: CapturedReplayCar) -> Bool {
        let leftPosition = left.classPosition ?? Int.max
        let rightPosition = right.classPosition ?? Int.max
        if leftPosition != rightPosition {
            return leftPosition < rightPosition
        }

        return overallSort(left, right)
    }

    private func overallSort(_ left: CapturedReplayCar, _ right: CapturedReplayCar) -> Bool {
        let leftPosition = left.overallPosition ?? Int.max
        let rightPosition = right.overallPosition ?? Int.max
        if leftPosition != rightPosition {
            return leftPosition < rightPosition
        }

        return (left.trackProgress ?? -Double.greatestFiniteMagnitude) > (right.trackProgress ?? -Double.greatestFiniteMagnitude)
    }
}

private struct ReplayCaptureManifest: Decodable {
    var captureId: String
    var telemetryFile: String
    var schemaFile: String
    var latestSessionInfoFile: String
    var tickRate: Int
    var bufferLength: Int
    var frameCount: Int
}

private enum ReplayCaptureError: Error {
    case shortFrame(Int)
}

private struct ReplaySessionInfo {
    var driverCarIdx: Int?
    var driversByCarIdx: [Int: ReplayDriverInfo]

    static func parse(url: URL) -> ReplaySessionInfo {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else {
            return ReplaySessionInfo(driverCarIdx: nil, driversByCarIdx: [:])
        }

        var driverCarIdx: Int?
        var inDrivers = false
        var current: [String: String]?
        var drivers: [Int: ReplayDriverInfo] = [:]

        func flushCurrent() {
            guard let current,
                  let carIdx = intValue(current["CarIdx"]) else {
                return
            }

            drivers[carIdx] = ReplayDriverInfo(fields: current)
        }

        for rawLine in text.components(separatedBy: .newlines) {
            let trimmed = rawLine.trimmingCharacters(in: .whitespaces)
            if trimmed.hasPrefix("DriverCarIdx:") {
                driverCarIdx = intValue(valueAfterColon(trimmed))
            }
            if trimmed == "Drivers:" {
                inDrivers = true
                continue
            }
            guard inDrivers else {
                continue
            }

            if trimmed.hasPrefix("- CarIdx:") {
                flushCurrent()
                current = ["CarIdx": cleanValue(String(trimmed.dropFirst("- CarIdx:".count)))]
                continue
            }

            guard current != nil,
                  let colon = trimmed.firstIndex(of: ":") else {
                continue
            }

            let key = String(trimmed[..<colon]).trimmingCharacters(in: .whitespaces)
            let value = String(trimmed[trimmed.index(after: colon)...])
            current?[key] = cleanValue(value)
        }
        flushCurrent()

        return ReplaySessionInfo(driverCarIdx: driverCarIdx, driversByCarIdx: drivers)
    }

    func estimatedLapSeconds(forClassId carClassId: Int) -> Double? {
        driversByCarIdx.values.first { $0.carClassId == carClassId }?.estimatedLapSeconds
    }

    private static func valueAfterColon(_ text: String) -> String? {
        guard let colon = text.firstIndex(of: ":") else {
            return nil
        }

        return String(text[text.index(after: colon)...])
    }

    private static func cleanValue(_ raw: String?) -> String {
        var value = (raw ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if value.count >= 2, value.first == "\"", value.last == "\"" {
            value.removeFirst()
            value.removeLast()
        }
        return value
    }

    private static func intValue(_ raw: String?) -> Int? {
        guard let raw else {
            return nil
        }

        return Int(cleanValue(raw).components(separatedBy: .whitespaces).first ?? "")
    }
}

private struct ReplayDriverInfo {
    var carIdx: Int
    var driverName: String
    var teamName: String?
    var carNumber: String?
    var carClassId: Int?
    var carClassName: String?
    var carClassColorHex: String?
    var estimatedLapSeconds: Double?
    var isPaceCar: Bool

    init(fields: [String: String]) {
        carIdx = Self.intValue(fields["CarIdx"]) ?? -1
        driverName = fields["UserName"].flatMap(Self.nonEmpty) ?? fields["TeamName"].flatMap(Self.nonEmpty) ?? "Car \(carIdx)"
        teamName = fields["TeamName"].flatMap(Self.nonEmpty)
        carNumber = fields["CarNumber"].flatMap(Self.nonEmpty)
        carClassId = Self.intValue(fields["CarClassID"])
        carClassName = fields["CarClassShortName"].flatMap(Self.nonEmpty) ?? fields["CarClassName"].flatMap(Self.nonEmpty)
        carClassColorHex = fields["CarClassColor"].flatMap(Self.colorHex)
        estimatedLapSeconds = Self.doubleValue(fields["CarClassEstLapTime"])
        isPaceCar = Self.intValue(fields["CarIsPaceCar"]) == 1
    }

    private static func nonEmpty(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    private static func intValue(_ value: String?) -> Int? {
        guard let value else {
            return nil
        }

        return Int(value.trimmingCharacters(in: .whitespacesAndNewlines).components(separatedBy: .whitespaces).first ?? "")
    }

    private static func doubleValue(_ value: String?) -> Double? {
        guard let value else {
            return nil
        }

        return Double(value.trimmingCharacters(in: .whitespacesAndNewlines).components(separatedBy: .whitespaces).first ?? "")
    }

    private static func colorHex(_ value: String) -> String? {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            return nil
        }

        if trimmed.hasPrefix("#") {
            return trimmed.uppercased()
        }

        if trimmed.lowercased().hasPrefix("0x") {
            let hex = String(trimmed.dropFirst(2)).uppercased()
            return "#\(String(repeating: "0", count: max(0, 6 - hex.count)))\(hex)"
        }

        return nil
    }
}

private extension Array {
    subscript(safe index: Int) -> Element? {
        guard index >= 0, index < count else {
            return nil
        }

        return self[index]
    }
}

private extension Data {
    func int32LittleEndian(at offset: Int) -> Int? {
        guard offset >= 0, offset + 4 <= count else {
            return nil
        }

        let raw = UInt32(self[offset])
            | (UInt32(self[offset + 1]) << 8)
            | (UInt32(self[offset + 2]) << 16)
            | (UInt32(self[offset + 3]) << 24)
        return Int(Int32(bitPattern: raw))
    }

    func floatLittleEndian(at offset: Int) -> Float? {
        guard offset >= 0, offset + 4 <= count else {
            return nil
        }

        let raw = UInt32(self[offset])
            | (UInt32(self[offset + 1]) << 8)
            | (UInt32(self[offset + 2]) << 16)
            | (UInt32(self[offset + 3]) << 24)
        return Float(bitPattern: raw)
    }

    func doubleLittleEndian(at offset: Int) -> Double? {
        guard offset >= 0, offset + 8 <= count else {
            return nil
        }

        var raw: UInt64 = 0
        for index in 0..<8 {
            raw |= UInt64(self[offset + index]) << UInt64(index * 8)
        }
        return Double(bitPattern: raw)
    }

    func boolValue(at offset: Int) -> Bool? {
        guard offset >= 0, offset < count else {
            return nil
        }

        return self[offset] != 0
    }
}
