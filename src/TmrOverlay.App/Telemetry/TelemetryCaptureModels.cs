namespace TmrOverlay.App.Telemetry;

internal sealed class CaptureManifest
{
    public int FormatVersion { get; init; } = 1;

    public required string CaptureId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

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

