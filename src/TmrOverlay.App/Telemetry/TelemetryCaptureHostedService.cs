using System.Diagnostics;
using irsdkSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureHostedService : IHostedService
{
    private readonly ILogger<TelemetryCaptureHostedService> _logger;
    private readonly TelemetryCaptureOptions _options;
    private readonly AppEventRecorder _events;
    private readonly SessionHistoryStore _sessionHistoryStore;
    private readonly PostRaceAnalysisPipeline _postRaceAnalysisPipeline;
    private readonly ILiveTelemetrySink _liveTelemetrySink;
    private readonly TelemetryCaptureState _state;
    private readonly AppPerformanceState _performance;
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
        PostRaceAnalysisPipeline postRaceAnalysisPipeline,
        ILiveTelemetrySink liveTelemetrySink,
        TelemetryCaptureState state,
        AppPerformanceState performance)
    {
        _logger = logger;
        _options = options;
        _events = events;
        _sessionHistoryStore = sessionHistoryStore;
        _postRaceAnalysisPipeline = postRaceAnalysisPipeline;
        _liveTelemetrySink = liveTelemetrySink;
        _state = state;
        _performance = performance;
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
        _liveTelemetrySink.MarkConnected();
        _events.Record("iracing_connected");
        _logger.LogInformation("Connected to iRacing.");
    }

    private void HandleDisconnected()
    {
        _logger.LogInformation("Disconnected from iRacing.");
        _events.Record("iracing_disconnected");

        _state.MarkDisconnected();
        _liveTelemetrySink.MarkDisconnected();

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
        var dataChangedStarted = Stopwatch.GetTimestamp();
        var succeeded = false;
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
            _performance.RecordTelemetryFrame(capturedAtUtc);
            succeeded = true;
        }
        catch (Exception exception)
        {
            _state.RecordError($"Telemetry read failed: {exception.Message}");
            _logger.LogError(exception, "An error occurred while reading telemetry from iRacing.");
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetryDataChanged,
                Stopwatch.GetElapsedTime(dataChangedStarted),
                succeeded);
        }
    }

    private TelemetryCaptureSession? GetOrCreateCollection(IRacingSDK sdk, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            if (_activeHistory is not null)
            {
                if (_activeCapture is null && _state.IsRawCaptureEnabled())
                {
                    _activeCapture = TryStartRawCaptureLocked(sdk, startedAtUtc, queueCurrentSessionInfo: true);
                }

                return _activeCapture;
            }

            TelemetryCaptureSession? capture = null;
            if (_state.IsRawCaptureEnabled())
            {
                capture = TryStartRawCaptureLocked(sdk, startedAtUtc, queueCurrentSessionInfo: false);
            }

            _activeHistory = new HistoricalSessionAccumulator();
            var sourceId = capture?.CaptureId ?? $"session-{startedAtUtc:yyyyMMdd-HHmmss-fff}";
            _activeSourceId = sourceId;
            _activeStartedAtUtc = capture?.StartedAtUtc ?? startedAtUtc;
            _sessionInfoSnapshotCount = 0;
            _frameIndex = 0;
            _lastSessionInfoUpdate = -1;
            _state.MarkCollectionStarted(_activeStartedAtUtc.Value);
            _liveTelemetrySink.MarkCollectionStarted(sourceId, _activeStartedAtUtc.Value);

            if (capture is not null)
            {
                _activeCapture = capture;
            }

            _events.Record("telemetry_collection_started", new Dictionary<string, string?>
            {
                ["sourceId"] = _activeSourceId,
                ["rawCaptureEnabled"] = _state.IsRawCaptureEnabled().ToString()
            });
            _logger.LogInformation(
                "Started live telemetry collection {SourceId}. Raw capture enabled: {RawCaptureEnabled}.",
                _activeSourceId,
                _state.IsRawCaptureEnabled());

            return capture;
        }
    }

    private TelemetryCaptureSession? TryStartRawCaptureLocked(
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        bool queueCurrentSessionInfo)
    {
        try
        {
            return StartRawCaptureLocked(sdk, capturedAtUtc, queueCurrentSessionInfo);
        }
        catch (Exception exception)
        {
            _state.RecordError($"Failed to start raw capture: {exception.Message}");
            _state.SetRawCaptureEnabled(false);
            _events.Record("capture_start_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name
            });
            _logger.LogError(exception, "Failed to start raw telemetry capture.");
            return null;
        }
    }

    private TelemetryCaptureSession StartRawCaptureLocked(
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        bool queueCurrentSessionInfo)
    {
        Directory.CreateDirectory(_options.ResolvedCaptureRoot);
        var capture = CreateCaptureSession(sdk);
        _activeCapture = capture;
        _state.MarkCaptureStarted(capture.DirectoryPath, capture.StartedAtUtc);
        _events.Record("capture_started", new Dictionary<string, string?>
        {
            ["captureId"] = capture.CaptureId,
            ["captureDirectory"] = capture.DirectoryPath
        });
        _logger.LogInformation("Started raw capture {CaptureDirectory}.", capture.DirectoryPath);

        if (queueCurrentSessionInfo && sdk.Header is not null)
        {
            RecordCaptureSessionInfoSnapshot(capture, sdk, capturedAtUtc, sdk.Header.SessionInfoUpdate);
        }

        return capture;
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
            RecordCaptureWriteStatus);
    }

    private void RecordCaptureWriteStatus(TelemetryCaptureWriteStatus writeStatus)
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            _state.RecordCaptureWrite(writeStatus);
            _performance.RecordCaptureWrite(writeStatus);
            succeeded = true;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.CaptureWriteStatusCallback,
                Stopwatch.GetElapsedTime(started),
                succeeded);
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
            uint value => unchecked((int)value),
            _ => 0
        };
    }

    private static int? ReadNullableInt32(IRacingSDK sdk, string variableName)
    {
        return sdk.GetData(variableName) switch
        {
            int value => value,
            uint value => unchecked((int)value),
            _ => null
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

    private static int? ReadInt32ArrayElement(IRacingSDK sdk, string variableName, int index)
    {
        if (index < 0)
        {
            return null;
        }

        return sdk.GetData(variableName) switch
        {
            int[] values when index < values.Length => values[index],
            Array values when index < values.Length && values.GetValue(index) is not null => Convert.ToInt32(values.GetValue(index)),
            _ => null
        };
    }

    private static double? ReadDoubleArrayElement(IRacingSDK sdk, string variableName, int index)
    {
        if (index < 0)
        {
            return null;
        }

        return sdk.GetData(variableName) switch
        {
            double[] values when index < values.Length => values[index],
            float[] values when index < values.Length => values[index],
            int[] values when index < values.Length => values[index],
            Array values when index < values.Length && values.GetValue(index) is not null => Convert.ToDouble(values.GetValue(index)),
            _ => null
        };
    }

    private static double? ReadNullableDoubleArrayElement(IRacingSDK sdk, string variableName, int index)
    {
        var value = ReadDoubleArrayElement(sdk, variableName, index);
        return value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0d
            ? null
            : value;
    }

    private static bool? ReadBooleanArrayElement(IRacingSDK sdk, string variableName, int index)
    {
        if (index < 0)
        {
            return null;
        }

        return sdk.GetData(variableName) switch
        {
            bool[] values when index < values.Length => values[index],
            int[] values when index < values.Length => values[index] != 0,
            Array values when index < values.Length && values.GetValue(index) is not null => Convert.ToBoolean(values.GetValue(index)),
            _ => null
        };
    }

    private static CarProgress? ReadLeaderProgress(IRacingSDK sdk)
    {
        CarProgress? bestProgress = null;

        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            var progress = ReadCarProgress(sdk, carIdx, requireLapProgress: false);
            if (progress is null)
            {
                continue;
            }

            if (progress.Position == 1)
            {
                return progress;
            }

            if (!progress.HasLapProgress)
            {
                continue;
            }

            if (bestProgress is null || progress.TotalLaps > bestProgress.TotalLaps)
            {
                bestProgress = progress;
            }
        }

        return bestProgress;
    }

    private static CarProgress? ReadClassLeaderProgress(IRacingSDK sdk, int playerCarIdx)
    {
        var playerClass = ReadInt32ArrayElement(sdk, "CarIdxClass", playerCarIdx);
        if (playerClass is null)
        {
            return null;
        }

        CarProgress? bestClassProgress = null;
        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            var carClass = ReadInt32ArrayElement(sdk, "CarIdxClass", carIdx);
            if (carClass != playerClass)
            {
                continue;
            }

            var progress = ReadCarProgress(sdk, carIdx, requireLapProgress: false);
            if (progress is null)
            {
                continue;
            }

            if (progress.ClassPosition == 1)
            {
                return progress;
            }

            if (!progress.HasLapProgress)
            {
                continue;
            }

            if (bestClassProgress is null || progress.TotalLaps > bestClassProgress.TotalLaps)
            {
                bestClassProgress = progress;
            }
        }

        return bestClassProgress;
    }

    private static CarProgress? ReadCarProgress(IRacingSDK sdk, int carIdx, bool requireLapProgress = true)
    {
        var lapCompleted = ReadInt32ArrayElement(sdk, "CarIdxLapCompleted", carIdx);
        var lapDistPct = ReadDoubleArrayElement(sdk, "CarIdxLapDistPct", carIdx);
        var f2TimeSeconds = ReadNullableDoubleArrayElement(sdk, "CarIdxF2Time", carIdx);
        var estimatedTimeSeconds = ReadNullableDoubleArrayElement(sdk, "CarIdxEstTime", carIdx);
        var position = ReadInt32ArrayElement(sdk, "CarIdxPosition", carIdx);
        var classPosition = ReadInt32ArrayElement(sdk, "CarIdxClassPosition", carIdx);
        var hasLapProgress = HasLapProgress(lapCompleted, lapDistPct);
        if (!hasLapProgress && requireLapProgress)
        {
            return null;
        }

        if (!hasLapProgress && !HasStandingOrTiming(position, classPosition, f2TimeSeconds, estimatedTimeSeconds))
        {
            return null;
        }

        return new CarProgress(
            CarIdx: carIdx,
            LapCompleted: hasLapProgress ? lapCompleted!.Value : -1,
            LapDistPct: hasLapProgress ? Math.Clamp(lapDistPct!.Value, 0d, 1d) : -1d,
            F2TimeSeconds: f2TimeSeconds,
            EstimatedTimeSeconds: estimatedTimeSeconds,
            LastLapTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxLastLapTime", carIdx),
            BestLapTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxBestLapTime", carIdx),
            Position: position,
            ClassPosition: classPosition,
            CarClass: ReadInt32ArrayElement(sdk, "CarIdxClass", carIdx));
    }

    private static IReadOnlyList<HistoricalCarProximity> ReadNearbyCars(IRacingSDK sdk, int playerCarIdx)
    {
        if (playerCarIdx < 0)
        {
            return [];
        }

        var cars = new List<HistoricalCarProximity>();
        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            if (carIdx == playerCarIdx)
            {
                continue;
            }

            var lapCompleted = ReadInt32ArrayElement(sdk, "CarIdxLapCompleted", carIdx);
            var lapDistPct = ReadDoubleArrayElement(sdk, "CarIdxLapDistPct", carIdx);
            if (lapCompleted is null || lapDistPct is null || lapCompleted < 0 || lapDistPct < 0d)
            {
                continue;
            }

            cars.Add(new HistoricalCarProximity(
                CarIdx: carIdx,
                LapCompleted: lapCompleted.Value,
                LapDistPct: Math.Clamp(lapDistPct.Value, 0d, 1d),
                F2TimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxF2Time", carIdx),
                EstimatedTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxEstTime", carIdx),
                Position: ReadInt32ArrayElement(sdk, "CarIdxPosition", carIdx),
                ClassPosition: ReadInt32ArrayElement(sdk, "CarIdxClassPosition", carIdx),
                CarClass: ReadInt32ArrayElement(sdk, "CarIdxClass", carIdx),
                TrackSurface: ReadInt32ArrayElement(sdk, "CarIdxTrackSurface", carIdx),
                OnPitRoad: ReadBooleanArrayElement(sdk, "CarIdxOnPitRoad", carIdx)));
        }

        return cars;
    }

    private static IReadOnlyList<HistoricalCarProximity> ReadClassCars(IRacingSDK sdk, int playerCarIdx)
    {
        if (playerCarIdx < 0)
        {
            return [];
        }

        var playerClass = ReadInt32ArrayElement(sdk, "CarIdxClass", playerCarIdx);
        if (playerClass is null)
        {
            return [];
        }

        var cars = new List<HistoricalCarProximity>();
        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            var carClass = ReadInt32ArrayElement(sdk, "CarIdxClass", carIdx);
            if (carClass != playerClass)
            {
                continue;
            }

            var lapCompleted = ReadInt32ArrayElement(sdk, "CarIdxLapCompleted", carIdx);
            var lapDistPct = ReadDoubleArrayElement(sdk, "CarIdxLapDistPct", carIdx);
            var f2TimeSeconds = ReadNullableDoubleArrayElement(sdk, "CarIdxF2Time", carIdx);
            var estimatedTimeSeconds = ReadNullableDoubleArrayElement(sdk, "CarIdxEstTime", carIdx);
            var position = ReadInt32ArrayElement(sdk, "CarIdxPosition", carIdx);
            var classPosition = ReadInt32ArrayElement(sdk, "CarIdxClassPosition", carIdx);
            var hasLapProgress = HasLapProgress(lapCompleted, lapDistPct);
            if (!hasLapProgress && !HasStandingOrTiming(position, classPosition, f2TimeSeconds, estimatedTimeSeconds))
            {
                continue;
            }

            cars.Add(new HistoricalCarProximity(
                CarIdx: carIdx,
                LapCompleted: hasLapProgress ? lapCompleted!.Value : -1,
                LapDistPct: hasLapProgress ? Math.Clamp(lapDistPct!.Value, 0d, 1d) : -1d,
                F2TimeSeconds: f2TimeSeconds,
                EstimatedTimeSeconds: estimatedTimeSeconds,
                Position: position,
                ClassPosition: classPosition,
                CarClass: carClass,
                TrackSurface: ReadInt32ArrayElement(sdk, "CarIdxTrackSurface", carIdx),
                OnPitRoad: ReadBooleanArrayElement(sdk, "CarIdxOnPitRoad", carIdx)));
        }

        return cars;
    }

    private static bool HasLapProgress(int? lapCompleted, double? lapDistPct)
    {
        return lapCompleted is >= 0
            && lapDistPct is { } pct
            && !double.IsNaN(pct)
            && !double.IsInfinity(pct)
            && pct >= 0d;
    }

    private static bool HasStandingOrTiming(
        int? position,
        int? classPosition,
        double? f2TimeSeconds,
        double? estimatedTimeSeconds)
    {
        return position is > 0
            || classPosition is > 0
            || f2TimeSeconds is not null
            || estimatedTimeSeconds is not null;
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
            RecordCaptureSessionInfoSnapshot(capture, sdk, capturedAtUtc, sessionInfoUpdate);
        }

        HistoricalSessionAccumulator? history;
        lock (_sync)
        {
            history = _activeHistory;
            _sessionInfoSnapshotCount++;
        }

        _liveTelemetrySink.ApplySessionInfo(sessionInfoYaml);
        history?.ApplySessionInfo(sessionInfoYaml);
    }

    private void RecordCaptureSessionInfoSnapshot(
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

    private void RecordHistoricalFrame(IRacingSDK sdk, DateTimeOffset capturedAtUtc, int sessionInfoUpdate)
    {
        HistoricalSessionAccumulator? history;
        lock (_sync)
        {
            history = _activeHistory;
        }

        var playerCarIdx = ReadInt32(sdk, "PlayerCarIdx");
        var leaderProgress = ReadLeaderProgress(sdk);
        var classLeaderProgress = ReadClassLeaderProgress(sdk, playerCarIdx);
        var sample = new HistoricalTelemetrySample(
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
            PlayerTireCompound: ReadInt32(sdk, "PlayerTireCompound"),
            SessionTimeRemain: ReadNullableDouble(sdk, "SessionTimeRemain"),
            SessionTimeTotal: ReadNullableDouble(sdk, "SessionTimeTotal"),
            SessionLapsRemainEx: ReadInt32(sdk, "SessionLapsRemainEx"),
            SessionLapsTotal: ReadInt32(sdk, "SessionLapsTotal"),
            SessionState: ReadInt32(sdk, "SessionState"),
            RaceLaps: ReadInt32(sdk, "RaceLaps"),
            PlayerCarIdx: playerCarIdx,
            TeamLapCompleted: ReadInt32ArrayElement(sdk, "CarIdxLapCompleted", playerCarIdx),
            TeamLapDistPct: ReadDoubleArrayElement(sdk, "CarIdxLapDistPct", playerCarIdx),
            TeamF2TimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxF2Time", playerCarIdx),
            TeamEstimatedTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxEstTime", playerCarIdx),
            TeamLastLapTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxLastLapTime", playerCarIdx),
            TeamBestLapTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxBestLapTime", playerCarIdx),
            TeamPosition: ReadInt32ArrayElement(sdk, "CarIdxPosition", playerCarIdx),
            TeamClassPosition: ReadInt32ArrayElement(sdk, "CarIdxClassPosition", playerCarIdx),
            TeamCarClass: ReadInt32ArrayElement(sdk, "CarIdxClass", playerCarIdx),
            LeaderCarIdx: leaderProgress?.CarIdx,
            LeaderLapCompleted: leaderProgress?.LapCompleted,
            LeaderLapDistPct: leaderProgress?.LapDistPct,
            LeaderF2TimeSeconds: leaderProgress?.F2TimeSeconds,
            LeaderEstimatedTimeSeconds: leaderProgress?.EstimatedTimeSeconds,
            LeaderLastLapTimeSeconds: leaderProgress?.LastLapTimeSeconds,
            LeaderBestLapTimeSeconds: leaderProgress?.BestLapTimeSeconds,
            ClassLeaderCarIdx: classLeaderProgress?.CarIdx,
            ClassLeaderLapCompleted: classLeaderProgress?.LapCompleted,
            ClassLeaderLapDistPct: classLeaderProgress?.LapDistPct,
            ClassLeaderF2TimeSeconds: classLeaderProgress?.F2TimeSeconds,
            ClassLeaderEstimatedTimeSeconds: classLeaderProgress?.EstimatedTimeSeconds,
            ClassLeaderLastLapTimeSeconds: classLeaderProgress?.LastLapTimeSeconds,
            ClassLeaderBestLapTimeSeconds: classLeaderProgress?.BestLapTimeSeconds,
            PlayerTrackSurface: ReadNullableInt32(sdk, "PlayerTrackSurface"),
            CarLeftRight: ReadNullableInt32(sdk, "CarLeftRight"),
            NearbyCars: ReadNearbyCars(sdk, playerCarIdx),
            ClassCars: ReadClassCars(sdk, playerCarIdx),
            TeamOnPitRoad: ReadBooleanArrayElement(sdk, "CarIdxOnPitRoad", playerCarIdx),
            TeamFastRepairsUsed: ReadInt32ArrayElement(sdk, "CarIdxFastRepairsUsed", playerCarIdx),
            PitServiceFlags: ReadInt32(sdk, "PitSvFlags"),
            PitServiceFuelLiters: ReadNullableDouble(sdk, "PitSvFuel"),
            PitRepairLeftSeconds: ReadNullableDouble(sdk, "PitRepairLeft"),
            PitOptRepairLeftSeconds: ReadNullableDouble(sdk, "PitOptRepairLeft"),
            TireSetsUsed: ReadInt32(sdk, "TireSetsUsed"),
            FastRepairUsed: ReadInt32(sdk, "FastRepairUsed"),
            DriversSoFar: ReadInt32(sdk, "DCDriversSoFar"),
            DriverChangeLapStatus: ReadInt32(sdk, "DCLapStatus"));

        var liveSinkStarted = Stopwatch.GetTimestamp();
        var liveSinkSucceeded = false;
        try
        {
            _liveTelemetrySink.RecordFrame(sample);
            liveSinkSucceeded = true;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.LiveTelemetrySink,
                Stopwatch.GetElapsedTime(liveSinkStarted),
                liveSinkSucceeded);
        }

        if (history is null)
        {
            return;
        }

        var historyStarted = Stopwatch.GetTimestamp();
        var historySucceeded = false;
        try
        {
            history.RecordFrame(sample);
            historySucceeded = true;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.HistoryRecordFrame,
                Stopwatch.GetElapsedTime(historyStarted),
                historySucceeded);
        }
    }

    private CaptureFinalizationContext BuildFinalizationContext(TelemetryCaptureSession? capture)
    {
        return new CaptureFinalizationContext(
            SourceId: _activeSourceId ?? capture?.CaptureId,
            StartedAtUtc: _activeStartedAtUtc ?? capture?.StartedAtUtc,
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
                await _postRaceAnalysisPipeline.SaveFromSummaryAsync(summary, CancellationToken.None).ConfigureAwait(false);
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

    private sealed record CarProgress(
        int CarIdx,
        int LapCompleted,
        double LapDistPct,
        double? F2TimeSeconds,
        double? EstimatedTimeSeconds,
        double? LastLapTimeSeconds,
        double? BestLapTimeSeconds,
        int? Position,
        int? ClassPosition,
        int? CarClass)
    {
        public bool HasLapProgress => LapCompleted >= 0 && LapDistPct >= 0d;

        public double TotalLaps => LapCompleted + LapDistPct;
    }
}
