using System.Diagnostics;
using irsdkSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Runtime;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureHostedService : IHostedService
{
    private readonly ILogger<TelemetryCaptureHostedService> _logger;
    private readonly TelemetryCaptureOptions _options;
    private readonly AppEventRecorder _events;
    private readonly TelemetryDiagnosticContext _diagnosticContext;
    private readonly SessionHistoryStore _sessionHistoryStore;
    private readonly PostRaceAnalysisPipeline _postRaceAnalysisPipeline;
    private readonly RuntimeStateService _runtimeState;
    private readonly ILiveTelemetrySink _liveTelemetrySink;
    private readonly TelemetryCaptureState _state;
    private readonly object _sync = new();
    private readonly object _rawFinalizerSync = new();
    private readonly SemaphoreSlim _synthesisSemaphore = new(1, 1);
    private readonly CancellationTokenSource _startupSynthesisCancellation = new();
    private IRacingSDK? _sdk;
    private TelemetryCaptureSession? _activeCapture;
    private HistoricalSessionAccumulator? _activeHistory;
    private Task _finalizerTask = Task.CompletedTask;
    private Task _rawFinalizerTask = Task.CompletedTask;
    private Task _startupSynthesisTask = Task.CompletedTask;
    private string? _activeSourceId;
    private string? _activeCollectionId;
    private DateTimeOffset? _activeStartedAtUtc;
    private int _sessionInfoSnapshotCount;
    private int _frameIndex;
    private int _lastSessionInfoUpdate = -1;

    public TelemetryCaptureHostedService(
        ILogger<TelemetryCaptureHostedService> logger,
        TelemetryCaptureOptions options,
        AppEventRecorder events,
        TelemetryDiagnosticContext diagnosticContext,
        SessionHistoryStore sessionHistoryStore,
        PostRaceAnalysisPipeline postRaceAnalysisPipeline,
        RuntimeStateService runtimeState,
        ILiveTelemetrySink liveTelemetrySink,
        TelemetryCaptureState state)
    {
        _logger = logger;
        _options = options;
        _events = events;
        _diagnosticContext = diagnosticContext;
        _sessionHistoryStore = sessionHistoryStore;
        _postRaceAnalysisPipeline = postRaceAnalysisPipeline;
        _runtimeState = runtimeState;
        _liveTelemetrySink = liveTelemetrySink;
        _state = state;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _state.SetCaptureRoot(_options.ResolvedCaptureRoot);
        _state.SetAppRunId(_diagnosticContext.AppRunId);
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
        _startupSynthesisTask = Task.Run(
            () => SynthesizePendingCapturesFromStartupAsync(_startupSynthesisCancellation.Token),
            CancellationToken.None);
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
            finalization = BuildFinalizationContext(captureToFinalize, "app_stopped");
            _activeCapture = null;
            _activeHistory = null;
            _activeSourceId = null;
            _activeCollectionId = null;
            _activeStartedAtUtc = null;
            _sessionInfoSnapshotCount = 0;
            _lastSessionInfoUpdate = -1;
        }

        _startupSynthesisCancellation.Cancel();

        if (captureToFinalize is not null || historyToFinalize is not null)
        {
            await FinalizeCollectionAsync(captureToFinalize, historyToFinalize, finalization).ConfigureAwait(false);
        }

        try
        {
            await _rawFinalizerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The raw writer finalizer is best-effort during app shutdown; startup recovery will pick up unfinished synthesis.
        }

        try
        {
            await _startupSynthesisTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _startupSynthesisCancellation.IsCancellationRequested)
        {
            // Startup synthesis recovery may be waiting for iRacing to close; do not block application shutdown.
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
            finalization = BuildFinalizationContext(captureToFinalize, "iracing_disconnected");
            _activeCapture = null;
            _activeHistory = null;
            _activeSourceId = null;
            _activeCollectionId = null;
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
                            ["collectionId"] = _activeCollectionId,
                            ["captureId"] = capture.CaptureId,
                            ["error"] = writerFault.Message
                        }, severity: "error");
                        _logger.LogError(writerFault, "Dropped telemetry frame because the capture writer failed.");
                        return;
                    }

                    _state.RecordWarning("Dropped telemetry frame because the capture queue is full.");
                    _events.Record("capture_dropped_frame", new Dictionary<string, string?>
                    {
                        ["collectionId"] = _activeCollectionId,
                        ["captureId"] = capture.CaptureId,
                        ["reason"] = "capture_queue_full"
                    }, severity: "warning");
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
        TelemetryCaptureSession? captureToStop = null;
        string? captureToStopCollectionId = null;
        string? captureToStopSourceId = null;
        TelemetryCaptureSession? activeCapture = null;

        lock (_sync)
        {
            if (_activeHistory is not null)
            {
                if (_activeCapture is not null && _state.IsRawCaptureStopRequested())
                {
                    captureToStop = _activeCapture;
                    captureToStopCollectionId = _activeCollectionId;
                    captureToStopSourceId = _activeSourceId;
                    _activeCapture = null;
                }
                else if (_activeCapture is null && _state.IsRawCaptureEnabled())
                {
                    _activeCollectionId ??= _diagnosticContext.NewCollectionId(startedAtUtc);
                    _activeCapture = TryStartRawCaptureLocked(
                        sdk,
                        startedAtUtc,
                        queueCurrentSessionInfo: true,
                        _activeCollectionId);
                }

                activeCapture = _activeCapture;
            }
            else
            {
                var collectionId = _diagnosticContext.NewCollectionId(startedAtUtc);
                TelemetryCaptureSession? capture = null;
                if (_state.IsRawCaptureEnabled())
                {
                    capture = TryStartRawCaptureLocked(
                        sdk,
                        startedAtUtc,
                        queueCurrentSessionInfo: false,
                        collectionId);
                }

                _activeHistory = new HistoricalSessionAccumulator();
                var sourceId = capture?.CaptureId ?? $"session-{startedAtUtc:yyyyMMdd-HHmmss-fff}";
                _activeCollectionId = collectionId;
                _activeSourceId = sourceId;
                _activeStartedAtUtc = capture?.StartedAtUtc ?? startedAtUtc;
                _sessionInfoSnapshotCount = 0;
                _frameIndex = 0;
                _lastSessionInfoUpdate = -1;
                _state.MarkCollectionStarted(collectionId, sourceId, _activeStartedAtUtc.Value);
                _liveTelemetrySink.MarkCollectionStarted(sourceId, _activeStartedAtUtc.Value);

                if (capture is not null)
                {
                    _activeCapture = capture;
                }

                _events.Record("telemetry_collection_started", new Dictionary<string, string?>
                {
                    ["collectionId"] = _activeCollectionId,
                    ["sourceId"] = _activeSourceId,
                    ["rawCaptureEnabled"] = _state.IsRawCaptureEnabled().ToString()
                });
                _logger.LogInformation(
                    "Started live telemetry collection {SourceId}. Raw capture enabled: {RawCaptureEnabled}.",
                    _activeSourceId,
                    _state.IsRawCaptureEnabled());

                activeCapture = capture;
            }
        }

        if (captureToStop is not null)
        {
            QueueRawCaptureWriterFinalization(
                captureToStop,
                captureToStopCollectionId,
                captureToStopSourceId,
                endedReason: "manual_stop",
                allowWaitingForIRacing: true);
        }

        return activeCapture;
    }

    private TelemetryCaptureSession? TryStartRawCaptureLocked(
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        bool queueCurrentSessionInfo,
        string collectionId)
    {
        try
        {
            return StartRawCaptureLocked(sdk, capturedAtUtc, queueCurrentSessionInfo, collectionId);
        }
        catch (Exception exception)
        {
            _state.RecordError($"Failed to start raw capture: {exception.Message}");
            _state.SetRawCaptureEnabled(false);
            _events.Record("capture_start_failed", new Dictionary<string, string?>
            {
                ["collectionId"] = collectionId,
                ["error"] = exception.GetType().Name
            }, severity: "error");
            _logger.LogError(exception, "Failed to start raw telemetry capture.");
            return null;
        }
    }

    private TelemetryCaptureSession StartRawCaptureLocked(
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        bool queueCurrentSessionInfo,
        string collectionId)
    {
        Directory.CreateDirectory(_options.ResolvedCaptureRoot);
        var capture = CreateCaptureSession(sdk, collectionId);
        _activeCapture = capture;
        _state.MarkCaptureStarted(
            capture.DirectoryPath,
            capture.StartedAtUtc,
            capture.CaptureId,
            collectionId,
            _activeSourceId ?? capture.CaptureId);
        _events.Record("capture_started", new Dictionary<string, string?>
        {
            ["collectionId"] = collectionId,
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

    private TelemetryCaptureSession CreateCaptureSession(IRacingSDK sdk, string collectionId)
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
            _state.RecordCaptureWrite,
            _diagnosticContext.AppRunId,
            collectionId);
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

    private static double? ReadFiniteDouble(IRacingSDK sdk, string variableName)
    {
        var value = ReadDouble(sdk, variableName);
        return double.IsNaN(value) || double.IsInfinity(value) ? null : value;
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

    private static int ResolveFocusCarIdx(IRacingSDK sdk, int playerCarIdx)
    {
        var cameraCarIdx = ReadNullableInt32(sdk, "CamCarIdx");
        if (cameraCarIdx is >= 0 and < 64 && ReadCarProgress(sdk, cameraCarIdx.Value, requireLapProgress: false) is not null)
        {
            return cameraCarIdx.Value;
        }

        return playerCarIdx;
    }

    private static CarProgress? ReadClassLeaderProgress(IRacingSDK sdk, int referenceCarIdx)
    {
        var referenceClass = ReadInt32ArrayElement(sdk, "CarIdxClass", referenceCarIdx);
        if (referenceClass is null)
        {
            return null;
        }

        CarProgress? bestClassProgress = null;
        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            var carClass = ReadInt32ArrayElement(sdk, "CarIdxClass", carIdx);
            if (carClass != referenceClass)
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
            if (!HasLapDistancePct(lapDistPct))
            {
                continue;
            }

            cars.Add(new HistoricalCarProximity(
                CarIdx: carIdx,
                LapCompleted: lapCompleted is >= 0 ? lapCompleted.Value : -1,
                LapDistPct: Math.Clamp(lapDistPct!.Value, 0d, 1d),
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
            && HasLapDistancePct(lapDistPct);
    }

    private static bool HasLapDistancePct(double? lapDistPct)
    {
        return lapDistPct is { } pct
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
        var focusCarIdx = ResolveFocusCarIdx(sdk, playerCarIdx);
        var leaderProgress = ReadLeaderProgress(sdk);
        var classLeaderProgress = ReadClassLeaderProgress(sdk, playerCarIdx);
        var focusClassLeaderProgress = focusCarIdx == playerCarIdx
            ? classLeaderProgress
            : ReadClassLeaderProgress(sdk, focusCarIdx);
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
            CarLeftRight: focusCarIdx == playerCarIdx ? ReadNullableInt32(sdk, "CarLeftRight") : null,
            NearbyCars: ReadNearbyCars(sdk, focusCarIdx),
            ClassCars: ReadClassCars(sdk, focusCarIdx),
            TeamOnPitRoad: ReadBooleanArrayElement(sdk, "CarIdxOnPitRoad", playerCarIdx),
            TeamFastRepairsUsed: ReadInt32ArrayElement(sdk, "CarIdxFastRepairsUsed", playerCarIdx),
            PitServiceFlags: ReadInt32(sdk, "PitSvFlags"),
            PitServiceFuelLiters: ReadNullableDouble(sdk, "PitSvFuel"),
            PitRepairLeftSeconds: ReadNullableDouble(sdk, "PitRepairLeft"),
            PitOptRepairLeftSeconds: ReadNullableDouble(sdk, "PitOptRepairLeft"),
            TireSetsUsed: ReadInt32(sdk, "TireSetsUsed"),
            FastRepairUsed: ReadInt32(sdk, "FastRepairUsed"),
            DriversSoFar: ReadInt32(sdk, "DCDriversSoFar"),
            DriverChangeLapStatus: ReadInt32(sdk, "DCLapStatus"),
            FocusCarIdx: focusCarIdx,
            FocusLapCompleted: ReadInt32ArrayElement(sdk, "CarIdxLapCompleted", focusCarIdx),
            FocusLapDistPct: ReadDoubleArrayElement(sdk, "CarIdxLapDistPct", focusCarIdx),
            FocusF2TimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxF2Time", focusCarIdx),
            FocusEstimatedTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxEstTime", focusCarIdx),
            FocusLastLapTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxLastLapTime", focusCarIdx),
            FocusBestLapTimeSeconds: ReadNullableDoubleArrayElement(sdk, "CarIdxBestLapTime", focusCarIdx),
            FocusPosition: ReadInt32ArrayElement(sdk, "CarIdxPosition", focusCarIdx),
            FocusClassPosition: ReadInt32ArrayElement(sdk, "CarIdxClassPosition", focusCarIdx),
            FocusCarClass: ReadInt32ArrayElement(sdk, "CarIdxClass", focusCarIdx),
            FocusOnPitRoad: ReadBooleanArrayElement(sdk, "CarIdxOnPitRoad", focusCarIdx),
            FocusClassLeaderCarIdx: focusClassLeaderProgress?.CarIdx,
            FocusClassLeaderLapCompleted: focusClassLeaderProgress?.LapCompleted,
            FocusClassLeaderLapDistPct: focusClassLeaderProgress?.LapDistPct,
            FocusClassLeaderF2TimeSeconds: focusClassLeaderProgress?.F2TimeSeconds,
            FocusClassLeaderEstimatedTimeSeconds: focusClassLeaderProgress?.EstimatedTimeSeconds,
            FocusClassLeaderLastLapTimeSeconds: focusClassLeaderProgress?.LastLapTimeSeconds,
            FocusClassLeaderBestLapTimeSeconds: focusClassLeaderProgress?.BestLapTimeSeconds,
            TrackTempC: ReadFiniteDouble(sdk, "TrackTemp"),
            Skies: ReadNullableInt32(sdk, "Skies"),
            WindVelMetersPerSecond: ReadFiniteDouble(sdk, "WindVel"),
            WindDirRadians: ReadFiniteDouble(sdk, "WindDir"),
            RelativeHumidityPercent: ReadFiniteDouble(sdk, "RelativeHumidity"),
            FogLevelPercent: ReadFiniteDouble(sdk, "FogLevel"),
            PrecipitationPercent: ReadFiniteDouble(sdk, "Precipitation"),
            AirDensityKgPerCubicMeter: ReadFiniteDouble(sdk, "AirDensity"),
            AirPressurePa: ReadFiniteDouble(sdk, "AirPressure"),
            SolarAltitudeRadians: ReadFiniteDouble(sdk, "SolarAltitude"),
            SolarAzimuthRadians: ReadFiniteDouble(sdk, "SolarAzimuth"));

        _liveTelemetrySink.RecordFrame(sample);
        history?.RecordFrame(sample);
    }

    private CaptureFinalizationContext BuildFinalizationContext(
        TelemetryCaptureSession? capture,
        string endedReason)
    {
        return new CaptureFinalizationContext(
            CollectionId: _activeCollectionId,
            SourceId: _activeSourceId ?? capture?.CaptureId,
            StartedAtUtc: _activeStartedAtUtc ?? capture?.StartedAtUtc,
            DroppedFrameCount: capture?.DroppedFrameCount ?? 0,
            SessionInfoSnapshotCount: capture?.SessionInfoSnapshotCount ?? _sessionInfoSnapshotCount,
            EndedReason: endedReason);
    }

    private async Task FinalizeCollectionAsync(
        TelemetryCaptureSession? capture,
        HistoricalSessionAccumulator? history,
        CaptureFinalizationContext finalization)
    {
        var historyFinalizationStarted = false;

        try
        {
            _state.MarkCaptureStopped();
            if (capture is not null)
            {
                capture.SetEndedReason(finalization.EndedReason);
                await capture.DisposeAsync().ConfigureAwait(false);
                _logger.LogInformation("Finalized raw capture {CaptureDirectory}.", capture.DirectoryPath);
                _events.Record("capture_finalized", new Dictionary<string, string?>
                {
                    ["collectionId"] = finalization.CollectionId,
                    ["captureId"] = capture.CaptureId,
                    ["captureDirectory"] = capture.DirectoryPath,
                    ["endedReason"] = finalization.EndedReason,
                    ["frameCount"] = capture.FrameCount.ToString(),
                    ["droppedFrameCount"] = capture.DroppedFrameCount.ToString(),
                    ["rawCaptureElapsedMilliseconds"] = capture.ManifestPerformance.RawCaptureElapsedMilliseconds?.ToString(),
                    ["processCpuMilliseconds"] = capture.ManifestPerformance.ProcessCpuMilliseconds?.ToString(),
                    ["processCpuPercentOfOneCore"] = capture.ManifestPerformance.ProcessCpuPercentOfOneCore?.ToString("0.0"),
                    ["writeOperationCount"] = capture.ManifestPerformance.WriteOperationCount?.ToString(),
                    ["averageWriteElapsedMilliseconds"] = capture.ManifestPerformance.AverageWriteElapsedMilliseconds?.ToString("0.###"),
                    ["maxWriteElapsedMilliseconds"] = capture.ManifestPerformance.MaxWriteElapsedMilliseconds?.ToString()
                });
            }

            if (history is not null && finalization.SourceId is not null && finalization.StartedAtUtc is not null)
            {
                historyFinalizationStarted = true;
                _state.MarkHistoryFinalizationStarted(DateTimeOffset.UtcNow);
                var summary = history.BuildSummary(
                    finalization.SourceId,
                    finalization.StartedAtUtc.Value,
                    capture?.FinishedAtUtc ?? DateTimeOffset.UtcNow,
                    capture?.DroppedFrameCount ?? finalization.DroppedFrameCount,
                    capture?.SessionInfoSnapshotCount ?? finalization.SessionInfoSnapshotCount);
                summary = WithCorrelation(summary, finalization);

                var segmentContext = BuildSegmentContext(finalization);
                var finalizationStartedTimestamp = Stopwatch.GetTimestamp();
                var historySaveStartedTimestamp = Stopwatch.GetTimestamp();
                var sessionGroup = await _sessionHistoryStore
                    .SaveAsync(summary, segmentContext, CancellationToken.None)
                    .ConfigureAwait(false);
                var historySaveElapsedMilliseconds = ElapsedMilliseconds(historySaveStartedTimestamp);
                var analysisSaveStartedTimestamp = Stopwatch.GetTimestamp();
                await _postRaceAnalysisPipeline
                    .SaveFromSummaryAsync(summary, sessionGroup, CancellationToken.None)
                    .ConfigureAwait(false);
                var analysisSaveElapsedMilliseconds = ElapsedMilliseconds(analysisSaveStartedTimestamp);
                var finalizationElapsedMilliseconds = ElapsedMilliseconds(finalizationStartedTimestamp);
                _state.MarkHistorySummarySaved(
                    FormatHistorySummaryLabel(summary),
                    DateTimeOffset.UtcNow,
                    historySaveElapsedMilliseconds,
                    analysisSaveElapsedMilliseconds);
                _events.Record("history_summary_saved", new Dictionary<string, string?>
                {
                    ["collectionId"] = finalization.CollectionId,
                    ["sourceId"] = finalization.SourceId,
                    ["sessionGroupId"] = sessionGroup?.GroupId,
                    ["sessionSegmentCount"] = sessionGroup?.Segments.Count.ToString(),
                    ["endedReason"] = segmentContext.EndedReason,
                    ["previousAppRunUnclean"] = segmentContext.PreviousAppRunUnclean.ToString(),
                    ["historySaveElapsedMilliseconds"] = historySaveElapsedMilliseconds.ToString(),
                    ["analysisSaveElapsedMilliseconds"] = analysisSaveElapsedMilliseconds.ToString(),
                    ["finalizationElapsedMilliseconds"] = finalizationElapsedMilliseconds.ToString(),
                    ["carKey"] = summary.Combo.CarKey,
                    ["trackKey"] = summary.Combo.TrackKey,
                    ["sessionKey"] = summary.Combo.SessionKey,
                    ["confidence"] = summary.Quality.Confidence
                });
                _logger.LogInformation(
                    "Saved session history summary for {SourceId} in session group {SessionGroupId}.",
                    finalization.SourceId,
                    sessionGroup?.GroupId ?? "none");
                _state.MarkHistoryFinalizationStopped(finalizationElapsedMilliseconds);
                historyFinalizationStarted = false;
            }

            if (capture is not null)
            {
                await WriteCaptureSynthesisAsync(
                        capture.DirectoryPath,
                        finalization.CollectionId,
                        allowWaitingForIRacing: !string.Equals(finalization.EndedReason, "app_stopped", StringComparison.OrdinalIgnoreCase),
                        cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            _state.RecordError($"Telemetry collection finalization failed: {exception.Message}");
            _events.Record("telemetry_collection_finalization_failed", new Dictionary<string, string?>
            {
                ["collectionId"] = finalization.CollectionId,
                ["sourceId"] = finalization.SourceId,
                ["error"] = exception.GetType().Name
            }, severity: "error");
            _logger.LogError(exception, "Failed to finalize telemetry collection.");
        }
        finally
        {
            if (historyFinalizationStarted)
            {
                _state.MarkHistoryFinalizationStopped();
            }
        }
    }

    private void QueueRawCaptureWriterFinalization(
        TelemetryCaptureSession capture,
        string? collectionId,
        string? sourceId,
        string endedReason,
        bool allowWaitingForIRacing)
    {
        lock (_rawFinalizerSync)
        {
            _rawFinalizerTask = _rawFinalizerTask
                .ContinueWith(
                    _ => FinalizeRawCaptureWriterAsync(
                        capture,
                        collectionId,
                        sourceId,
                        endedReason,
                        allowWaitingForIRacing),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private async Task FinalizeRawCaptureWriterAsync(
        TelemetryCaptureSession capture,
        string? collectionId,
        string? sourceId,
        string endedReason,
        bool allowWaitingForIRacing)
    {
        try
        {
            capture.SetEndedReason(endedReason);
            await capture.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation(
                "Finalized raw capture {CaptureDirectory} with reason {EndedReason}.",
                capture.DirectoryPath,
                endedReason);
            _events.Record("capture_finalized", new Dictionary<string, string?>
            {
                ["collectionId"] = collectionId,
                ["sourceId"] = sourceId,
                ["captureId"] = capture.CaptureId,
                ["captureDirectory"] = capture.DirectoryPath,
                ["endedReason"] = endedReason,
                ["frameCount"] = capture.FrameCount.ToString(),
                ["droppedFrameCount"] = capture.DroppedFrameCount.ToString(),
                ["rawCaptureElapsedMilliseconds"] = capture.ManifestPerformance.RawCaptureElapsedMilliseconds?.ToString(),
                ["processCpuMilliseconds"] = capture.ManifestPerformance.ProcessCpuMilliseconds?.ToString(),
                ["processCpuPercentOfOneCore"] = capture.ManifestPerformance.ProcessCpuPercentOfOneCore?.ToString("0.0"),
                ["writeOperationCount"] = capture.ManifestPerformance.WriteOperationCount?.ToString(),
                ["averageWriteElapsedMilliseconds"] = capture.ManifestPerformance.AverageWriteElapsedMilliseconds?.ToString("0.###"),
                ["maxWriteElapsedMilliseconds"] = capture.ManifestPerformance.MaxWriteElapsedMilliseconds?.ToString()
            });

            _ = Task.Run(
                () => WriteCaptureSynthesisAsync(
                    capture.DirectoryPath,
                    collectionId,
                    allowWaitingForIRacing,
                    CancellationToken.None),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _state.RecordError($"Raw capture stop failed: {exception.Message}");
            _events.Record("capture_manual_stop_failed", new Dictionary<string, string?>
            {
                ["collectionId"] = collectionId,
                ["sourceId"] = sourceId,
                ["captureId"] = capture.CaptureId,
                ["captureDirectory"] = capture.DirectoryPath,
                ["error"] = exception.GetType().Name
            }, severity: "error");
            _logger.LogError(exception, "Failed to finalize manually stopped raw capture {CaptureDirectory}.", capture.DirectoryPath);
        }
        finally
        {
            _state.MarkRawCaptureStopped(capture.FinishedAtUtc ?? DateTimeOffset.UtcNow);
        }
    }

    private async Task SynthesizePendingCapturesFromStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pendingCaptures = CaptureSynthesisService.FindPendingSynthesisCaptures(_options.ResolvedCaptureRoot);
            if (pendingCaptures.Count == 0)
            {
                return;
            }

            _events.Record("capture_synthesis_startup_scan_found_pending", new Dictionary<string, string?>
            {
                ["pendingCount"] = pendingCaptures.Count.ToString(),
                ["captureRoot"] = _options.ResolvedCaptureRoot
            });
            _logger.LogInformation(
                "Startup synthesis scan found {PendingCaptureCount} raw captures without capture-synthesis.json.",
                pendingCaptures.Count);

            foreach (var pending in pendingCaptures)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _events.Record("capture_synthesis_startup_recovery_started", new Dictionary<string, string?>
                {
                    ["collectionId"] = pending.CollectionId,
                    ["captureId"] = pending.CaptureId,
                    ["captureDirectory"] = pending.DirectoryPath,
                    ["reason"] = pending.Reason
                });
                await WriteCaptureSynthesisAsync(
                        pending.DirectoryPath,
                        pending.CollectionId,
                        allowWaitingForIRacing: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Startup capture synthesis recovery was cancelled.");
        }
        catch (Exception exception)
        {
            _state.RecordWarning($"Startup capture synthesis recovery failed: {exception.Message}");
            _events.Record("capture_synthesis_startup_recovery_failed", new Dictionary<string, string?>
            {
                ["captureRoot"] = _options.ResolvedCaptureRoot,
                ["error"] = exception.GetType().Name
            }, severity: "warning");
            _logger.LogWarning(exception, "Startup capture synthesis recovery failed.");
        }
    }

    private async Task WriteCaptureSynthesisAsync(
        string captureDirectory,
        string? collectionId,
        bool allowWaitingForIRacing,
        CancellationToken cancellationToken)
    {
        await _synthesisSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (CaptureSynthesisService.HasStableSynthesis(captureDirectory))
            {
                _state.MarkCaptureSynthesisPendingCleared();
                _events.Record("capture_synthesis_already_exists", new Dictionary<string, string?>
                {
                    ["collectionId"] = collectionId,
                    ["captureDirectory"] = captureDirectory
                });
                return;
            }

            if (allowWaitingForIRacing)
            {
                await WaitForIRacingToCloseBeforeSynthesisAsync(captureDirectory, collectionId, cancellationToken).ConfigureAwait(false);
            }
            else if (IsIRacingStillRunning(out var reason))
            {
                _state.MarkCaptureSynthesisPendingCleared();
                _events.Record("capture_synthesis_skipped_app_stopping_iracing_running", new Dictionary<string, string?>
                {
                    ["collectionId"] = collectionId,
                    ["captureDirectory"] = captureDirectory,
                    ["reason"] = reason
                }, severity: "warning");
                _logger.LogInformation(
                    "Skipped capture synthesis for {CaptureDirectory} during app shutdown because iRacing is still running. Current blocker: {IRacingBlocker}.",
                    captureDirectory,
                    reason);
                return;
            }

            _state.MarkCaptureSynthesisStarted(DateTimeOffset.UtcNow);
            try
            {
                var result = await CaptureSynthesisService.WriteAsync(captureDirectory, cancellationToken).ConfigureAwait(false);
                _state.MarkCaptureSynthesisSaved(result);
                _events.Record("capture_synthesis_saved", new Dictionary<string, string?>
                {
                    ["collectionId"] = collectionId,
                    ["captureDirectory"] = captureDirectory,
                    ["synthesisPath"] = result.Path,
                    ["stableSynthesisPath"] = result.StablePath,
                    ["synthesisBytes"] = result.Bytes.ToString(),
                    ["telemetryBytes"] = result.TelemetryBytes.ToString(),
                    ["elapsedMilliseconds"] = result.ElapsedMilliseconds.ToString(),
                    ["processCpuMilliseconds"] = result.ProcessCpuMilliseconds.ToString(),
                    ["processCpuPercentOfOneCore"] = result.ProcessCpuPercentOfOneCore?.ToString("0.0"),
                    ["totalFrameRecords"] = result.TotalFrameRecords.ToString(),
                    ["sampledFrameCount"] = result.SampledFrameCount.ToString(),
                    ["sampleStride"] = result.SampleStride.ToString(),
                    ["fieldCount"] = result.FieldCount.ToString()
                });
                _logger.LogInformation(
                    "Saved capture synthesis {CaptureSynthesisPath} ({CaptureSynthesisBytes} bytes) in {ElapsedMilliseconds} ms with {ProcessCpuMilliseconds} ms process CPU from {TelemetryBytes} telemetry bytes, {TotalFrameRecords} frames, {FieldCount} fields.",
                    result.Path,
                    result.Bytes,
                    result.ElapsedMilliseconds,
                    result.ProcessCpuMilliseconds,
                    result.TelemetryBytes,
                    result.TotalFrameRecords,
                    result.FieldCount);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _state.RecordWarning($"Capture synthesis failed: {exception.Message}");
                _events.Record("capture_synthesis_failed", new Dictionary<string, string?>
                {
                    ["collectionId"] = collectionId,
                    ["captureDirectory"] = captureDirectory,
                    ["error"] = exception.Message
                }, severity: "warning");
                _logger.LogWarning(exception, "Failed to write capture synthesis for {CaptureDirectory}.", captureDirectory);
            }
            finally
            {
                _state.MarkCaptureSynthesisStopped();
            }
        }
        finally
        {
            _synthesisSemaphore.Release();
        }
    }

    private async Task WaitForIRacingToCloseBeforeSynthesisAsync(
        string captureDirectory,
        string? collectionId,
        CancellationToken cancellationToken)
    {
        var deferred = false;
        var pendingSinceUtc = DateTimeOffset.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsIRacingStillRunning(out var reason))
            {
                if (deferred)
                {
                    _state.MarkCaptureSynthesisPendingCleared();
                    _events.Record("capture_synthesis_deferred_released", new Dictionary<string, string?>
                    {
                        ["collectionId"] = collectionId,
                        ["captureDirectory"] = captureDirectory
                    });
                    _logger.LogInformation(
                        "iRacing has closed; starting deferred capture synthesis for {CaptureDirectory}.",
                        captureDirectory);
                }

                return;
            }

            _state.MarkCaptureSynthesisPending(pendingSinceUtc, reason);
            if (!deferred)
            {
                _events.Record("capture_synthesis_deferred_iracing_running", new Dictionary<string, string?>
                {
                    ["collectionId"] = collectionId,
                    ["captureDirectory"] = captureDirectory,
                    ["reason"] = reason
                });
                _logger.LogInformation(
                    "Deferring capture synthesis for {CaptureDirectory} until iRacing closes. Current blocker: {IRacingBlocker}.",
                    captureDirectory,
                    reason);
                deferred = true;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsIRacingStillRunning(out string reason)
    {
        var runningSimProcesses = CaptureSynthesisService.FindRunningIRacingSimProcesses();
        if (runningSimProcesses.Count > 0)
        {
            reason = string.Join(", ", runningSimProcesses);
            return true;
        }

        if (IsSdkStillConnected())
        {
            reason = "SDK still connected";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool IsSdkStillConnected()
    {
        try
        {
            return _sdk?.IsConnected() == true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatHistorySummaryLabel(HistoricalSessionSummary summary)
    {
        var car = FirstNonEmpty(summary.Car.CarScreenNameShort, summary.Car.CarScreenName, summary.Combo.CarKey);
        var track = FirstNonEmpty(summary.Track.TrackDisplayName, summary.Track.TrackName, summary.Combo.TrackKey);
        var session = FirstNonEmpty(summary.Session.SessionType, summary.Session.EventType, summary.Combo.SessionKey);
        return $"{car} / {track} / {session}";
    }

    private HistoricalSessionSegmentContext BuildSegmentContext(CaptureFinalizationContext finalization)
    {
        var previousState = _runtimeState.PreviousState;
        return new HistoricalSessionSegmentContext
        {
            EndedReason = string.IsNullOrWhiteSpace(finalization.EndedReason)
                ? "unknown"
                : finalization.EndedReason,
            AppRunId = _diagnosticContext.AppRunId,
            CollectionId = finalization.CollectionId,
            PreviousAppRunUnclean = previousState?.StoppedCleanly == false,
            PreviousAppStartedAtUtc = previousState?.StartedAtUtc,
            PreviousAppLastHeartbeatAtUtc = previousState?.LastHeartbeatAtUtc,
            PreviousAppStoppedAtUtc = previousState?.StoppedAtUtc
        };
    }

    private HistoricalSessionSummary WithCorrelation(
        HistoricalSessionSummary summary,
        CaptureFinalizationContext finalization)
    {
        return new HistoricalSessionSummary
        {
            SummaryVersion = summary.SummaryVersion,
            SourceCaptureId = summary.SourceCaptureId,
            AppRunId = _diagnosticContext.AppRunId,
            CollectionId = finalization.CollectionId,
            StartedAtUtc = summary.StartedAtUtc,
            FinishedAtUtc = summary.FinishedAtUtc,
            Combo = summary.Combo,
            Car = summary.Car,
            Track = summary.Track,
            Session = summary.Session,
            Conditions = summary.Conditions,
            Metrics = summary.Metrics,
            Stints = summary.Stints,
            PitStops = summary.PitStops,
            Quality = summary.Quality,
            AppVersion = summary.AppVersion
        };
    }

    private static long ElapsedMilliseconds(long startedTimestamp)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startedTimestamp;
        return (long)Math.Round(elapsedTicks * 1000d / Stopwatch.Frequency);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "unknown";
    }

    private sealed record CaptureFinalizationContext(
        string? SourceId,
        string? CollectionId,
        DateTimeOffset? StartedAtUtc,
        int DroppedFrameCount,
        int SessionInfoSnapshotCount,
        string EndedReason)
    {
        public static CaptureFinalizationContext Empty { get; } = new(null, null, null, 0, 0, "unknown");
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
