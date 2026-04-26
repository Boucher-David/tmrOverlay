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
        Directory.CreateDirectory(_options.ResolvedCaptureRoot);

        _sdk = new IRacingSDK();
        _sdk.OnConnected += HandleConnected;
        _sdk.OnDisconnected += HandleDisconnected;
        _sdk.OnDataChanged += HandleDataChanged;

        _logger.LogInformation("Telemetry capture service started. Captures will be written to {CaptureRoot}.", _options.ResolvedCaptureRoot);
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
        lock (_sync)
        {
            captureToFinalize = _activeCapture;
            historyToFinalize = _activeHistory;
            _activeCapture = null;
            _activeHistory = null;
            _lastSessionInfoUpdate = -1;
        }

        if (captureToFinalize is not null)
        {
            await FinalizeCaptureAsync(captureToFinalize, historyToFinalize).ConfigureAwait(false);
        }

        await _finalizerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        lock (_sync)
        {
            captureToFinalize = _activeCapture;
            historyToFinalize = _activeHistory;
            _activeCapture = null;
            _activeHistory = null;
            _lastSessionInfoUpdate = -1;
            _frameIndex = 0;
        }

        if (captureToFinalize is not null)
        {
            _finalizerTask = FinalizeCaptureAsync(captureToFinalize, historyToFinalize);
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
            var capture = GetOrCreateCapture(sdk);
            var capturedAtUtc = DateTimeOffset.UtcNow;
            var sessionInfoUpdate = sdk.Header.SessionInfoUpdate;

            var latestSessionInfoUpdate = UpdateSessionInfoVersion(sessionInfoUpdate);
            if (latestSessionInfoUpdate)
            {
                QueueSessionInfoSnapshot(capture, sdk, capturedAtUtc, sessionInfoUpdate);
            }

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
                _events.Record("capture_dropped_frame");
                _logger.LogWarning("Dropped telemetry frame because the capture queue is full.");
                return;
            }

            RecordHistoricalFrame(sdk, capturedAtUtc, sessionInfoUpdate);
            _state.RecordFrame(capturedAtUtc);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An error occurred while reading telemetry from iRacing.");
        }
    }

    private TelemetryCaptureSession GetOrCreateCapture(IRacingSDK sdk)
    {
        lock (_sync)
        {
            if (_activeCapture is not null)
            {
                return _activeCapture;
            }

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

            _activeCapture = TelemetryCaptureSession.Create(
                _options.ResolvedCaptureRoot,
                _options.QueueCapacity,
                _options.StoreSessionInfoSnapshots,
                sdk.Header.Version,
                sdk.Header.TickRate,
                sdk.Header.BufferLength,
                schema);
            _activeHistory = new HistoricalSessionAccumulator();

            _frameIndex = 0;
            _lastSessionInfoUpdate = -1;
            _state.MarkCaptureStarted(_activeCapture.DirectoryPath, _activeCapture.StartedAtUtc);
            _events.Record("capture_started", new Dictionary<string, string?>
            {
                ["captureId"] = _activeCapture.CaptureId,
                ["captureDirectory"] = _activeCapture.DirectoryPath
            });
            _logger.LogInformation("Started capture {CaptureDirectory}.", _activeCapture.DirectoryPath);
            return _activeCapture;
        }
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

    private void QueueSessionInfoSnapshot(
        TelemetryCaptureSession capture,
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        int sessionInfoUpdate)
    {
        var sessionInfoYaml = sdk.GetSessionInfo();
        if (string.IsNullOrWhiteSpace(sessionInfoYaml))
        {
            return;
        }

        var sessionSnapshot = new SessionInfoSnapshot(
            CapturedAtUtc: capturedAtUtc,
            SessionInfoUpdate: sessionInfoUpdate,
            Yaml: sessionInfoYaml);

        if (!capture.TryQueueSessionInfo(sessionSnapshot))
        {
            _logger.LogWarning("Dropped session info update {SessionInfoUpdate} because the capture queue is full.", sessionInfoUpdate);
        }

        HistoricalSessionAccumulator? history;
        lock (_sync)
        {
            history = _activeHistory;
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

    private async Task FinalizeCaptureAsync(TelemetryCaptureSession capture, HistoricalSessionAccumulator? history)
    {
        try
        {
            _state.MarkCaptureStopped();
            await capture.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Finalized capture {CaptureDirectory}.", capture.DirectoryPath);
            _events.Record("capture_finalized", new Dictionary<string, string?>
            {
                ["captureId"] = capture.CaptureId,
                ["captureDirectory"] = capture.DirectoryPath,
                ["frameCount"] = capture.FrameCount.ToString(),
                ["droppedFrameCount"] = capture.DroppedFrameCount.ToString()
            });

            if (history is not null)
            {
                var summary = history.BuildSummary(
                    capture.CaptureId,
                    capture.StartedAtUtc,
                    capture.FinishedAtUtc ?? DateTimeOffset.UtcNow,
                    capture.DroppedFrameCount,
                    capture.SessionInfoSnapshotCount);

                await _sessionHistoryStore.SaveAsync(summary, CancellationToken.None).ConfigureAwait(false);
                _events.Record("history_summary_saved", new Dictionary<string, string?>
                {
                    ["captureId"] = capture.CaptureId,
                    ["carKey"] = summary.Combo.CarKey,
                    ["trackKey"] = summary.Combo.TrackKey,
                    ["sessionKey"] = summary.Combo.SessionKey,
                    ["confidence"] = summary.Quality.Confidence
                });
                _logger.LogInformation("Saved session history summary for capture {CaptureId}.", capture.CaptureId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to finalize capture {CaptureDirectory}.", capture.DirectoryPath);
        }
    }
}
