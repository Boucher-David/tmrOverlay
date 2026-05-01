using TmrOverlay.Core.AppInfo;

namespace TmrOverlay.App.Telemetry;

internal sealed class CaptureManifest
{
    public int FormatVersion { get; init; } = 1;

    public required string CaptureId { get; init; }

    public string? AppRunId { get; init; }

    public string? CollectionId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string? EndedReason { get; set; }

    public required string TelemetryFile { get; init; }

    public required string SchemaFile { get; init; }

    public required string LatestSessionInfoFile { get; init; }

    public required string SessionInfoDirectory { get; init; }

    public required int SdkVersion { get; init; }

    public required int TickRate { get; init; }

    public required int BufferLength { get; init; }

    public required int VariableCount { get; init; }

    public int FrameCount { get; set; }

    public int DroppedFrameCount { get; set; }

    public int SessionInfoSnapshotCount { get; set; }

    public long? RawCaptureElapsedMilliseconds { get; set; }

    public long? ProcessCpuMilliseconds { get; set; }

    public double? ProcessCpuPercentOfOneCore { get; set; }

    public int? WriteOperationCount { get; set; }

    public double? AverageWriteElapsedMilliseconds { get; set; }

    public long? MaxWriteElapsedMilliseconds { get; set; }

    public AppVersionInfo? AppVersion { get; init; }
}

internal sealed record TelemetryVariableSchema(
    string Name,
    string TypeName,
    int TypeCode,
    int Count,
    int Offset,
    int ByteSize,
    int Length,
    string Unit,
    string Description);

internal sealed record TelemetryFrameEnvelope(
    DateTimeOffset CapturedAtUtc,
    int FrameIndex,
    int SessionTick,
    int SessionInfoUpdate,
    double SessionTime,
    byte[] Payload);

internal sealed record SessionInfoSnapshot(
    DateTimeOffset CapturedAtUtc,
    int SessionInfoUpdate,
    string Yaml);

internal sealed record TelemetryCaptureWriteStatus(
    DateTimeOffset TimestampUtc,
    string CaptureId,
    string DirectoryPath,
    int FramesWritten,
    int SessionInfoSnapshotCount,
    long? TelemetryFileBytes,
    long? LastWriteBytes = null,
    long? LastWriteElapsedMilliseconds = null,
    string? LastWriteKind = null,
    double? AverageWriteElapsedMilliseconds = null,
    long? MaxWriteElapsedMilliseconds = null,
    Exception? Exception = null);
