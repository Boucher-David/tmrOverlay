using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Telemetry.Live;

internal interface ILiveTelemetrySink
{
    void MarkConnected();

    void MarkCollectionStarted(string sourceId, DateTimeOffset startedAtUtc);

    void MarkDisconnected();

    void ApplySessionInfo(string sessionInfoYaml);

    void RecordFrame(HistoricalTelemetrySample sample);
}
