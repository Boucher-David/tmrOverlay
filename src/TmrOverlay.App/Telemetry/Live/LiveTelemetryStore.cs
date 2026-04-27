using TmrOverlay.App.History;

namespace TmrOverlay.App.Telemetry.Live;

internal sealed class LiveTelemetryStore
{
    private readonly object _sync = new();
    private HistoricalSessionContext _context = HistoricalSessionContext.Empty;
    private LiveTelemetrySnapshot _snapshot = LiveTelemetrySnapshot.Empty;
    private long _sequence;

    public LiveTelemetrySnapshot Snapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    public void MarkConnected()
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                IsConnected = true,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Sequence = ++_sequence
            };
        }
    }

    public void MarkCollectionStarted(string sourceId, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _snapshot = _snapshot with
            {
                IsConnected = true,
                IsCollecting = true,
                SourceId = sourceId,
                StartedAtUtc = startedAtUtc,
                LastUpdatedAtUtc = startedAtUtc,
                Sequence = ++_sequence
            };
        }
    }

    public void MarkDisconnected()
    {
        lock (_sync)
        {
            _context = HistoricalSessionContext.Empty;
            _snapshot = LiveTelemetrySnapshot.Empty with
            {
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Sequence = ++_sequence
            };
        }
    }

    public void ApplySessionInfo(string sessionInfoYaml)
    {
        var context = SessionInfoSummaryParser.Parse(sessionInfoYaml);
        lock (_sync)
        {
            _context = context;
            _snapshot = _snapshot with
            {
                Context = context,
                Combo = HistoricalComboIdentity.From(context),
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Sequence = ++_sequence
            };
        }
    }

    public void RecordFrame(HistoricalTelemetrySample sample)
    {
        lock (_sync)
        {
            _snapshot = new LiveTelemetrySnapshot(
                IsConnected: true,
                IsCollecting: true,
                SourceId: _snapshot.SourceId,
                StartedAtUtc: _snapshot.StartedAtUtc ?? sample.CapturedAtUtc,
                LastUpdatedAtUtc: sample.CapturedAtUtc,
                Sequence: ++_sequence,
                Context: _context,
                Combo: HistoricalComboIdentity.From(_context),
                LatestSample: sample,
                Fuel: LiveFuelSnapshot.From(_context, sample));
        }
    }
}
