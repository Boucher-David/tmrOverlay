namespace TmrOverlay.App.Telemetry;

internal sealed record TelemetryCaptureStatusSnapshot(
    bool IsConnected,
    bool IsCapturing,
    bool RawCaptureEnabled,
    bool RawCaptureActive,
    string? CaptureRoot,
    string? CurrentCaptureDirectory,
    string? LastCaptureDirectory,
    int FrameCount,
    int WrittenFrameCount,
    int DroppedFrameCount,
    long? TelemetryFileBytes,
    DateTimeOffset? CaptureStartedAtUtc,
    DateTimeOffset? LastFrameCapturedAtUtc,
    DateTimeOffset? LastDiskWriteAtUtc,
    string? AppWarning,
    string? LastWarning,
    string? LastError,
    DateTimeOffset? LastIssueAtUtc);
