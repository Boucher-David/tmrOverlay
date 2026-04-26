using irsdkSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureHostedService : IHostedService
{
    private readonly ILogger<TelemetryCaptureHostedService> _logger;
    private readonly TelemetryCaptureOptions _options;
    private readonly AppEventRecorder _events;
    private readonly SessionHistoryStore _sessionHistoryStore;
    private readonly TelemetryCaptureState _state;
    private readonly object _sync = new();
    private IRacingSDK? _sdk;
    private TelemetryCaptureSession? _activeCapture;
    private HistoricalSessionAccumulator? _activeHistory;
    private Task _finalizerTask = Task.CompletedTask;
    private string? _activeSourceId;
    private DateTimeOffset? _activeStartedAtUtc;
    private int _sessionInfoSnapshotCount;
    private int _frameIndex;
    private int _lastSessionInfoUpdate = -1;

    public TelemetryCaptureHostedService(
        ILogger<TelemetryCaptureHostedService> logger,
        TelemetryCaptureOptions options,
        AppEventRecorder events,
        SessionHistoryStore sessionHistoryStore,
        TelemetryCaptureState state)
    {
        _logger = logger;
        _options = options;
        _events = events;
        _sessionHistoryStore = sessionHistoryStore;
        _state = state;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _state.SetCaptureRoot(_options.ResolvedCaptureRoot);
        _state.SetRawCaptureEnabled(_options.RawCaptureEnabled);

        if (_options.RawCaptureEnabled)
        {
            try
            {
                Directory.CreateDirectory(_options.ResolvedCaptureRoot);
            }
            catch (Exception exception)
            {
                _state.RecordError($"Cannot create capture root: {exception.Message}");
                _logger.LogError(exception, "Failed to create telemetry capture root {CaptureRoot}.", _options.ResolvedCaptureRoot);
                throw;
            }
        }

        _sdk = new IRacingSDK();
        _sdk.OnConnected += HandleConnected;
        _sdk.OnDisconnected += HandleDisconnected;
        _sdk.OnDataChanged += HandleDataChanged;

        _logger.LogInformation(
            "Telemetry collection service started. Raw capture enabled: {RawCaptureEnabled}. Capture root: {CaptureRoot}.",
            _options.RawCaptureEnabled,
            _options.ResolvedCaptureRoot);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_sdk is not null)
        {
            _sdk.OnConnected -= HandleConnected;
            _sdk.OnDisconnected -= HandleDisconnected;
            _sdk.OnDataChanged -= HandleDataChanged;
        }

        TelemetryCaptureSession? captureToFinalize;
        HistoricalSessionAccumulator? historyToFinalize;
        var finalization = CaptureFinalizationContext.Empty;
        lock (_sync)
        {
            captureToFinalize = _activeCapture;
            historyToFinalize = _activeHistory;
            finalization = BuildFinalizationContext(captureToFinalize);
            _activeCapture = null;
            _activeHistory = null;
            _activeSourceId = null;
            _activeStartedAtUtc = null;
            _sessionInfoSnapshotCount = 0;
            _lastSessionInfoUpdate = -1;
        }

        if (captureToFinalize is not null || historyToFinalize is not null)
        {
            await FinalizeCollectionAsync(captureToFinalize, historyToFinalize, finalization).ConfigureAwait(false);
        }

        try
        {
            await _finalizerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _state.RecordError($"Capture finalizer failed: {exception.Message}");
            throw;
        }
    }

    private void HandleConnected()
    {
        _state.MarkConnected();
        _events.Record("iracing_connected");
        _logger.LogInformation("Connected to iRacing.");
    }

    private void HandleDisconnected()
    {
        _logger.LogInformation("Disconnected from iRacing.");
        _events.Record("iracing_disconnected");

        _state.MarkDisconnected();

        TelemetryCaptureSession? captureToFinalize;
        HistoricalSessionAccumulator? historyToFinalize;
        var finalization = CaptureFinalizationContext.Empty;
        lock (_sync)
        {
            captureToFinalize = _activeCapture;
            historyToFinalize = _activeHistory;
            finalization = BuildFinalizationContext(captureToFinalize);
            _activeCapture = null;
            _activeHistory = null;
            _activeSourceId = null;
            _activeStartedAtUtc = null;
            _sessionInfoSnapshotCount = 0;
            _lastSessionInfoUpdate = -1;
            _frameIndex = 0;
        }

        if (captureToFinalize is not null || historyToFinalize is not null)
        {
            _finalizerTask = FinalizeCollectionAsync(captureToFinalize, historyToFinalize, finalization);
        }
    }

    private void HandleDataChanged()
    {
        var sdk = _sdk;

        if (sdk is null || !sdk.IsConnected() || sdk.Header is null)
        {
            return;
        }

        try
        {
            var capturedAtUtc = DateTimeOffset.UtcNow;
            var capture = GetOrCreateCollection(sdk, capturedAtUtc);
            var sessionInfoUpdate = sdk.Header.SessionInfoUpdate;

            var latestSessionInfoUpdate = UpdateSessionInfoVersion(sessionInfoUpdate);
            if (latestSessionInfoUpdate)
            {
                RecordSessionInfoSnapshot(capture, sdk, capturedAtUtc, sessionInfoUpdate);
            }

            if (capture is not null)
            {
                var payload = ReadTelemetryBuffer(sdk);
                var frame = new TelemetryFrameEnvelope(
                    CapturedAtUtc: capturedAtUtc,
                    FrameIndex: Interlocked.Increment(ref _frameIndex),
                    SessionTick: ReadInt32(sdk, "SessionTick"),
                    SessionInfoUpdate: sessionInfoUpdate,
                    SessionTime: ReadDouble(sdk, "SessionTime"),
                    Payload: payload);

                if (!capture.TryQueueFrame(frame))
                {
                    capture.RecordDroppedFrame();
                    _state.RecordDroppedFrame();
                    var writerFault = capture.WriterFault;
                    if (writerFault is not null)
                    {
                        _state.RecordError($"Capture writer failed: {writerFault.Message}");
                        _events.Record("capture_writer_failed", new Dictionary<string, string?>
                        {
                            ["captureId"] = capture.CaptureId,
                            ["error"] = writerFault.Message
                        });
                        _logger.LogError(writerFault, "Dropped telemetry frame because the capture writer failed.");
                        return;
                    }

                    _state.RecordWarning("Dropped telemetry frame because the capture queue is full.");
                    _events.Record("capture_dropped_frame");
                    _logger.LogWarning("Dropped telemetry frame because the capture queue is full.");
                    return;
                }
            }

            RecordHistoricalFrame(sdk, capturedAtUtc, sessionInfoUpdate);
            _state.RecordFrame(capturedAtUtc);
        }
        catch (Exception exception)
        {
            _state.RecordError($"Telemetry read failed: {exception.Message}");
            _logger.LogError(exception, "An error occurred while reading telemetry from iRacing.");
        }
    }

    private TelemetryCaptureSession? GetOrCreateCollection(IRacingSDK sdk, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            if (_activeHistory is not null)
            {
                return _activeCapture;
            }

            TelemetryCaptureSession? capture = null;
            if (_options.RawCaptureEnabled)
            {
                capture = CreateCaptureSession(sdk);
                _activeCapture = capture;
            }

            _activeHistory = new HistoricalSessionAccumulator();
            _activeSourceId = capture?.CaptureId ?? $"session-{startedAtUtc:yyyyMMdd-HHmmss-fff}";
            _activeStartedAtUtc = capture?.StartedAtUtc ?? startedAtUtc;
            _sessionInfoSnapshotCount = 0;
            _frameIndex = 0;
            _lastSessionInfoUpdate = -1;
            _state.MarkCollectionStarted(_activeStartedAtUtc.Value);

            if (capture is not null)
            {
                _state.MarkCaptureStarted(capture.DirectoryPath, capture.StartedAtUtc);
                _events.Record("capture_started", new Dictionary<string, string?>
                {
                    ["captureId"] = capture.CaptureId,
                    ["captureDirectory"] = capture.DirectoryPath
                });
                _logger.LogInformation("Started raw capture {CaptureDirectory}.", capture.DirectoryPath);
            }

            _events.Record("telemetry_collection_started", new Dictionary<string, string?>
            {
                ["sourceId"] = _activeSourceId,
                ["rawCaptureEnabled"] = _options.RawCaptureEnabled.ToString()
            });
            _logger.LogInformation(
                "Started live telemetry collection {SourceId}. Raw capture enabled: {RawCaptureEnabled}.",
                _activeSourceId,
                _options.RawCaptureEnabled);

            return capture;
        }
    }

    private TelemetryCaptureSession CreateCaptureSession(IRacingSDK sdk)
    {
        var headers = IRacingSDK.GetVarHeaders(sdk);
        if (headers is null || sdk.Header is null)
        {
            throw new InvalidOperationException("The SDK headers are not available yet.");
        }

        var schema = headers.Values
            .OrderBy(header => header.Offset)
            .Select(header => new TelemetryVariableSchema(
                Name: header.Name,
                TypeName: header.Type.ToString(),
                TypeCode: (int)header.Type,
                Count: header.Count,
                Offset: header.Offset,
                ByteSize: header.Bytes,
                Length: header.Length,
                Unit: header.Unit,
                Description: header.Desc))
            .ToArray();

        return TelemetryCaptureSession.Create(
            _options.ResolvedCaptureRoot,
            _options.QueueCapacity,
            _options.StoreSessionInfoSnapshots,
            sdk.Header.Version,
            sdk.Header.TickRate,
            sdk.Header.BufferLength,
            schema,
            _state.RecordCaptureWrite);
    }

    private bool UpdateSessionInfoVersion(int sessionInfoUpdate)
    {
        lock (_sync)
        {
            if (sessionInfoUpdate == _lastSessionInfoUpdate)
            {
                return false;
            }

            _lastSessionInfoUpdate = sessionInfoUpdate;
            return true;
        }
    }

    private static byte[] ReadTelemetryBuffer(IRacingSDK sdk)
    {
        var fileView = IRacingSDK.GetFileMapView(sdk);
        if (fileView is null || sdk.Header is null)
        {
            throw new InvalidOperationException("The SDK memory map is not available.");
        }

        var payload = GC.AllocateUninitializedArray<byte>(sdk.Header.BufferLength);
        fileView.ReadArray(sdk.Header.Offset, payload, 0, payload.Length);
        return payload;
    }

    private static int ReadInt32(IRacingSDK sdk, string variableName)
    {
        return sdk.GetData(variableName) switch
        {
            int value => value,
            _ => 0
        };
    }

    private static double ReadDouble(IRacingSDK sdk, string variableName)
    {
        return sdk.GetData(variableName) switch
        {
            double value => value,
            float value => value,
            int value => value,
            _ => double.NaN
        };
    }

    private static double? ReadNullableDouble(IRacingSDK sdk, string variableName)
    {
        var value = ReadDouble(sdk, variableName);
        return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? null : value;
    }

    private static bool ReadBoolean(IRacingSDK sdk, string variableName)
    {
        return sdk.GetData(variableName) switch
        {
            bool value => value,
            int value => value != 0,
            _ => false
        };
    }

    private void RecordSessionInfoSnapshot(
        TelemetryCaptureSession? capture,
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        int sessionInfoUpdate)
    {
        var sessionInfoYaml = sdk.GetSessionInfo();
        if (string.IsNullOrWhiteSpace(sessionInfoYaml))
        {
            return;
        }

        if (capture is not null)
        {
            var sessionSnapshot = new SessionInfoSnapshot(
                CapturedAtUtc: capturedAtUtc,
                SessionInfoUpdate: sessionInfoUpdate,
                Yaml: sessionInfoYaml);

            if (!capture.TryQueueSessionInfo(sessionSnapshot))
            {
                var writerFault = capture.WriterFault;
                if (writerFault is not null)
                {
                    _state.RecordError($"Capture writer failed while saving session info: {writerFault.Message}");
                    _logger.LogError(writerFault, "Dropped session info update because the capture writer failed.");
                    return;
                }

                _state.RecordWarning($"Dropped session info update {sessionInfoUpdate} because the capture queue is full.");
                _logger.LogWarning("Dropped session info update {SessionInfoUpdate} because the capture queue is full.", sessionInfoUpdate);
            }
        }

        HistoricalSessionAccumulator? history;
        lock (_sync)
        {
            history = _activeHistory;
            _sessionInfoSnapshotCount++;
        }

        history?.ApplySessionInfo(sessionInfoYaml);
    }

    private void RecordHistoricalFrame(IRacingSDK sdk, DateTimeOffset capturedAtUtc, int sessionInfoUpdate)
    {
        HistoricalSessionAccumulator? history;
        lock (_sync)
        {
            history = _activeHistory;
        }

        history?.RecordFrame(new HistoricalTelemetrySample(
            CapturedAtUtc: capturedAtUtc,
            SessionTime: ReadDouble(sdk, "SessionTime"),
            SessionTick: ReadInt32(sdk, "SessionTick"),
            SessionInfoUpdate: sessionInfoUpdate,
            IsOnTrack: ReadBoolean(sdk, "IsOnTrack"),
            IsInGarage: ReadBoolean(sdk, "IsInGarage"),
            OnPitRoad: ReadBoolean(sdk, "OnPitRoad"),
            PitstopActive: ReadBoolean(sdk, "PitstopActive"),
            PlayerCarInPitStall: ReadBoolean(sdk, "PlayerCarInPitStall"),
            FuelLevelLiters: ReadDouble(sdk, "FuelLevel"),
            FuelLevelPercent: ReadDouble(sdk, "FuelLevelPct"),
            FuelUsePerHourKg: ReadDouble(sdk, "FuelUsePerHour"),
            SpeedMetersPerSecond: ReadDouble(sdk, "Speed"),
            Lap: ReadInt32(sdk, "Lap"),
            LapCompleted: ReadInt32(sdk, "LapCompleted"),
            LapDistPct: ReadDouble(sdk, "LapDistPct"),
            LapLastLapTimeSeconds: ReadNullableDouble(sdk, "LapLastLapTime"),
            LapBestLapTimeSeconds: ReadNullableDouble(sdk, "LapBestLapTime"),
            AirTempC: ReadDouble(sdk, "AirTemp"),
            TrackTempCrewC: ReadDouble(sdk, "TrackTempCrew"),
            TrackWetness: ReadInt32(sdk, "TrackWetness"),
            WeatherDeclaredWet: ReadBoolean(sdk, "WeatherDeclaredWet"),
            PlayerTireCompound: ReadInt32(sdk, "PlayerTireCompound")));
    }

    private CaptureFinalizationContext BuildFinalizationContext(TelemetryCaptureSession? capture)
    {
        return new CaptureFinalizationContext(
            SourceId: capture?.CaptureId ?? _activeSourceId,
            StartedAtUtc: capture?.StartedAtUtc ?? _activeStartedAtUtc,
            DroppedFrameCount: capture?.DroppedFrameCount ?? 0,
            SessionInfoSnapshotCount: capture?.SessionInfoSnapshotCount ?? _sessionInfoSnapshotCount);
    }

    private async Task FinalizeCollectionAsync(
        TelemetryCaptureSession? capture,
        HistoricalSessionAccumulator? history,
        CaptureFinalizationContext finalization)
    {
        try
        {
            _state.MarkCaptureStopped();
            if (capture is not null)
            {
                await capture.DisposeAsync().ConfigureAwait(false);
                _logger.LogInformation("Finalized raw capture {CaptureDirectory}.", capture.DirectoryPath);
                _events.Record("capture_finalized", new Dictionary<string, string?>
                {
                    ["captureId"] = capture.CaptureId,
                    ["captureDirectory"] = capture.DirectoryPath,
                    ["frameCount"] = capture.FrameCount.ToString(),
                    ["droppedFrameCount"] = capture.DroppedFrameCount.ToString()
                });
            }

            if (history is not null && finalization.SourceId is not null && finalization.StartedAtUtc is not null)
            {
                var summary = history.BuildSummary(
                    finalization.SourceId,
                    finalization.StartedAtUtc.Value,
                    capture?.FinishedAtUtc ?? DateTimeOffset.UtcNow,
                    capture?.DroppedFrameCount ?? finalization.DroppedFrameCount,
                    capture?.SessionInfoSnapshotCount ?? finalization.SessionInfoSnapshotCount);

                await _sessionHistoryStore.SaveAsync(summary, CancellationToken.None).ConfigureAwait(false);
                _events.Record("history_summary_saved", new Dictionary<string, string?>
                {
                    ["sourceId"] = finalization.SourceId,
                    ["carKey"] = summary.Combo.CarKey,
                    ["trackKey"] = summary.Combo.TrackKey,
                    ["sessionKey"] = summary.Combo.SessionKey,
                    ["confidence"] = summary.Quality.Confidence
                });
                _logger.LogInformation("Saved session history summary for {SourceId}.", finalization.SourceId);
            }
        }
        catch (Exception exception)
        {
            _state.RecordError($"Telemetry collection finalization failed: {exception.Message}");
            _logger.LogError(exception, "Failed to finalize telemetry collection.");
        }
    }

    private sealed record CaptureFinalizationContext(
        string? SourceId,
        DateTimeOffset? StartedAtUtc,
        int DroppedFrameCount,
        int SessionInfoSnapshotCount)
    {
        public static CaptureFinalizationContext Empty { get; } = new(null, null, 0, 0);
    }
}
