namespace TmrOverlay.App.Telemetry;

internal sealed record TelemetryCaptureStatusSnapshot(
    bool IsConnected,
    bool IsCapturing,
    string? CurrentCaptureDirectory,
    string? LastCaptureDirectory,
    int FrameCount,
    int DroppedFrameCount,
    DateTimeOffset? CaptureStartedAtUtc,
    DateTimeOffset? LastFrameCapturedAtUtc);

