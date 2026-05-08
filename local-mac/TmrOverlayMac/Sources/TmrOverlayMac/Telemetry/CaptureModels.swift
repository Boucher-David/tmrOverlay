import Foundation

struct CaptureManifest: Codable {
    let formatVersion: Int
    let captureId: String
    let startedAtUtc: Date
    var finishedAtUtc: Date?
    let telemetryFile: String
    let schemaFile: String
    let latestSessionInfoFile: String
    let sessionInfoDirectory: String
    let sdkVersion: Int
    let tickRate: Int
    let bufferLength: Int
    let variableCount: Int
    var frameCount: Int
    var droppedFrameCount: Int
    var sessionInfoSnapshotCount: Int
}

struct TelemetryVariableSchema: Codable {
    let name: String
    let typeName: String
    let typeCode: Int
    let count: Int
    let offset: Int
    let byteSize: Int
    let length: Int
    let unit: String
    let description: String
}

struct MockHistorySample {
    let sessionTime: TimeInterval
    let fuelLevelLiters: Double
    let speedMetersPerSecond: Double
    let lapDistancePct: Double
}
