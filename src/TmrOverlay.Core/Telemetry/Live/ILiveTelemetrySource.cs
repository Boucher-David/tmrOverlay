namespace TmrOverlay.Core.Telemetry.Live;

internal interface ILiveTelemetrySource
{
    LiveTelemetrySnapshot Snapshot();
}
