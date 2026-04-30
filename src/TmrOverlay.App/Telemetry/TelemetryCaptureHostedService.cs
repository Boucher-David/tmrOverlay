using System.Diagnostics;
using irsdkSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Performance;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.EdgeCases;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureHostedService : IHostedService
{
    private readonly ILogger<TelemetryCaptureHostedService> _logger;
    private readonly TelemetryCaptureOptions _options;
    private readonly AppEventRecorder _events;
    private readonly SessionHistoryStore _sessionHistoryStore;
    private readonly PostRaceAnalysisPipeline _postRaceAnalysisPipeline;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly ILiveTelemetrySink _liveTelemetrySink;
    private readonly TelemetryCaptureState _state;
    private readonly AppPerformanceState _performance;
    private readonly TelemetryEdgeCaseRecorder _edgeCaseRecorder;
    private readonly object _sync = new();
    private IRacingSDK? _sdk;
    private TelemetryCaptureSession? _activeCapture;
    private HistoricalSessionAccumulator? _activeHistory;
    private Task _finalizerTask = Task.CompletedTask;
    private string? _activeSourceId;
    private DateTimeOffset? _activeStartedAtUtc;
    private IReadOnlyList<string> _activeRawWatchVariableNames = [];
    private int _sessionInfoSnapshotCount;
    private int _frameIndex;
    private int _lastSessionInfoUpdate = -1;

    public TelemetryCaptureHostedService(
        ILogger<TelemetryCaptureHostedService> logger,
        TelemetryCaptureOptions options,
        AppEventRecorder events,
        SessionHistoryStore sessionHistoryStore,
        PostRaceAnalysisPipeline postRaceAnalysisPipeline,
        DiagnosticsBundleService diagnosticsBundleService,
        ILiveTelemetrySink liveTelemetrySink,
        TelemetryCaptureState state,
        AppPerformanceState performance,
        TelemetryEdgeCaseRecorder edgeCaseRecorder)
    {
        _logger = logger;
        _options = options;
        _events = events;
        _sessionHistoryStore = sessionHistoryStore;
        _postRaceAnalysisPipeline = postRaceAnalysisPipeline;
        _diagnosticsBundleService = diagnosticsBundleService;
        _liveTelemetrySink = liveTelemetrySink;
        _state = state;
        _performance = performance;
        _edgeCaseRecorder = edgeCaseRecorder;
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
            _activeRawWatchVariableNames = [];
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
            _activeRawWatchVariableNames = [];
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
            TelemetryCaptureSession? capture;
            var collectionStarted = Stopwatch.GetTimestamp();
            var collectionSucceeded = false;
            try
            {
                capture = GetOrCreateCollection(sdk, capturedAtUtc);
                collectionSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryDataChangedGetCollection,
                    collectionStarted,
                    collectionSucceeded);
            }

            var sessionInfoUpdate = sdk.Header.SessionInfoUpdate;

            bool latestSessionInfoUpdate;
            var sessionVersionStarted = Stopwatch.GetTimestamp();
            var sessionVersionSucceeded = false;
            try
            {
                latestSessionInfoUpdate = UpdateSessionInfoVersion(sessionInfoUpdate);
                sessionVersionSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryDataChangedSessionInfoVersion,
                    sessionVersionStarted,
                    sessionVersionSucceeded);
            }

            if (latestSessionInfoUpdate)
            {
                var sessionInfoStarted = Stopwatch.GetTimestamp();
                var sessionInfoSucceeded = false;
                try
                {
                    RecordSessionInfoSnapshot(capture, sdk, capturedAtUtc, sessionInfoUpdate);
                    sessionInfoSucceeded = true;
                }
                finally
                {
                    _performance.RecordOperation(
                        AppPerformanceMetricIds.TelemetryDataChangedSessionInfoSnapshot,
                        sessionInfoStarted,
                        sessionInfoSucceeded);
                }
            }

            if (capture is not null)
            {
                var rawCaptureStarted = Stopwatch.GetTimestamp();
                var rawCaptureSucceeded = false;
                try
                {
                    RecordRawCaptureFrame(capture, sdk, capturedAtUtc, sessionInfoUpdate);
                    rawCaptureSucceeded = true;
                }
                finally
                {
                    _performance.RecordOperation(
                        AppPerformanceMetricIds.TelemetryDataChangedRawCaptureFrame,
                        rawCaptureStarted,
                        rawCaptureSucceeded);
                }
            }

            var historicalFrameStarted = Stopwatch.GetTimestamp();
            var historicalFrameSucceeded = false;
            try
            {
                RecordHistoricalFrame(sdk, capturedAtUtc, sessionInfoUpdate);
                historicalFrameSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryDataChangedHistoricalFrame,
                    historicalFrameStarted,
                    historicalFrameSucceeded);
            }

            var stateFrameStarted = Stopwatch.GetTimestamp();
            var stateFrameSucceeded = false;
            try
            {
                _state.RecordFrame(capturedAtUtc);
                _performance.RecordTelemetryFrame(capturedAtUtc);
                stateFrameSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryDataChangedStateFrame,
                    stateFrameStarted,
                    stateFrameSucceeded);
            }

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
                dataChangedStarted,
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
            var edgeCaseSchema = ReadEdgeCaseSchema(sdk);
            _activeRawWatchVariableNames = edgeCaseSchema.WatchedVariables
                .Select(variable => variable.Name)
                .ToArray();
            _sessionInfoSnapshotCount = 0;
            _frameIndex = 0;
            _lastSessionInfoUpdate = -1;
            _state.MarkCollectionStarted(_activeStartedAtUtc.Value);
            _liveTelemetrySink.MarkCollectionStarted(sourceId, _activeStartedAtUtc.Value);
            _edgeCaseRecorder.StartCollection(sourceId, _activeStartedAtUtc.Value, edgeCaseSchema);

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
            RecordCurrentCaptureSessionInfoSnapshot(capture, sdk, capturedAtUtc, sdk.Header.SessionInfoUpdate);
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

    private static RawTelemetrySchemaSnapshot ReadEdgeCaseSchema(IRacingSDK sdk)
    {
        var headers = IRacingSDK.GetVarHeaders(sdk);
        if (headers is null)
        {
            return RawTelemetrySchemaSnapshot.Empty;
        }

        var watched = headers.Values
            .Where(header => RawTelemetryWatchVariables.Names.Contains(header.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .Select(header => new RawTelemetryWatchedVariable(
                Name: header.Name,
                Group: RawTelemetryWatchVariables.GroupFor(header.Name),
                TypeName: header.Type.ToString(),
                Count: header.Count,
                Unit: string.IsNullOrWhiteSpace(header.Unit) ? null : header.Unit,
                Description: string.IsNullOrWhiteSpace(header.Desc) ? null : header.Desc))
            .ToArray();
        var found = watched.Select(variable => variable.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RawTelemetryWatchVariables.Names
            .Where(name => !found.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new RawTelemetrySchemaSnapshot(watched, missing);
    }

    private static RawTelemetryWatchSnapshot ReadRawTelemetryWatchSnapshot(
        IRacingSDK sdk,
        IReadOnlyCollection<string> variableNames)
    {
        var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in variableNames)
        {
            if (TryReadRawWatchValue(sdk, name, out var value))
            {
                values[name] = value;
            }
        }

        return values.Count == 0
            ? RawTelemetryWatchSnapshot.Empty
            : new RawTelemetryWatchSnapshot(values);
    }

    private static bool TryReadRawWatchValue(IRacingSDK sdk, string variableName, out double value)
    {
        value = 0d;
        try
        {
            switch (sdk.GetData(variableName))
            {
                case bool boolValue:
                    value = boolValue ? 1d : 0d;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue:
                    value = sbyteValue;
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                case uint uintValue:
                    value = uintValue;
                    return true;
                case long longValue:
                    value = longValue;
                    return true;
                case ulong ulongValue when ulongValue <= long.MaxValue:
                    value = ulongValue;
                    return true;
                case float floatValue when !float.IsNaN(floatValue) && !float.IsInfinity(floatValue):
                    value = floatValue;
                    return true;
                case double doubleValue when !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue):
                    value = doubleValue;
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
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
                started,
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

    private static int ReadFocusCarIdx(IRacingSDK sdk, int playerCarIdx)
    {
        var camCarIdx = ReadNullableInt32(sdk, "CamCarIdx");
        if (camCarIdx is >= 0 and < 64 && ReadCarProgress(sdk, camCarIdx.Value, requireLapProgress: false) is not null)
        {
            return camCarIdx.Value;
        }

        return playerCarIdx;
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

    private static IReadOnlyList<HistoricalCarProximity> ReadNearbyCars(IRacingSDK sdk, int referenceCarIdx)
    {
        if (referenceCarIdx < 0)
        {
            return [];
        }

        var cars = new List<HistoricalCarProximity>();
        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            if (carIdx == referenceCarIdx)
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

    private static IReadOnlyList<HistoricalCarProximity> ReadClassCars(IRacingSDK sdk, int referenceCarIdx)
    {
        if (referenceCarIdx < 0)
        {
            return [];
        }

        var referenceClass = ReadInt32ArrayElement(sdk, "CarIdxClass", referenceCarIdx);
        if (referenceClass is null)
        {
            return [];
        }

        var cars = new List<HistoricalCarProximity>();
        for (var carIdx = 0; carIdx < 64; carIdx++)
        {
            var carClass = ReadInt32ArrayElement(sdk, "CarIdxClass", carIdx);
            if (carClass != referenceClass)
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

    private void RecordRawCaptureFrame(
        TelemetryCaptureSession capture,
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        int sessionInfoUpdate)
    {
        try
        {
            TelemetryFrameEnvelope frame;
            var rawReadStarted = Stopwatch.GetTimestamp();
            var rawReadSucceeded = false;
            try
            {
                var payload = ReadTelemetryBuffer(sdk);
                frame = new TelemetryFrameEnvelope(
                    CapturedAtUtc: capturedAtUtc,
                    FrameIndex: Interlocked.Increment(ref _frameIndex),
                    SessionTick: ReadInt32(sdk, "SessionTick"),
                    SessionInfoUpdate: sessionInfoUpdate,
                    SessionTime: ReadDouble(sdk, "SessionTime"),
                    Payload: payload);
                rawReadSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryRawFrameRead,
                    rawReadStarted,
                    rawReadSucceeded);
            }

            var queueStarted = Stopwatch.GetTimestamp();
            var queueSucceeded = false;
            try
            {
                if (capture.TryQueueFrame(frame))
                {
                    queueSucceeded = true;
                    return;
                }

                RecordRawCaptureDroppedFrame(capture);
                queueSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryRawFrameQueue,
                    queueStarted,
                    queueSucceeded);
            }
        }
        catch (Exception exception)
        {
            capture.RecordDroppedFrame();
            _state.RecordDroppedFrame();
            _state.RecordError($"Raw capture frame read failed: {exception.Message}");
            _events.Record("capture_frame_read_failed", new Dictionary<string, string?>
            {
                ["captureId"] = capture.CaptureId,
                ["error"] = exception.GetType().Name
            });
            _logger.LogError(
                exception,
                "Failed to read a raw telemetry frame. Live telemetry will continue from SDK variables.");
        }
    }

    private void RecordRawCaptureDroppedFrame(TelemetryCaptureSession capture)
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
            _logger.LogError(writerFault, "Dropped telemetry frame because the capture writer failed. Live telemetry will continue.");
            return;
        }

        _state.RecordWarning("Dropped telemetry frame because the capture queue is full.");
        _events.Record("capture_dropped_frame", new Dictionary<string, string?>
        {
            ["captureId"] = capture.CaptureId
        });
        _logger.LogWarning("Dropped telemetry frame because the capture queue is full. Live telemetry will continue.");
    }

    private void RecordSessionInfoSnapshot(
        TelemetryCaptureSession? capture,
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        int sessionInfoUpdate)
    {
        string sessionInfoYaml;
        var readStarted = Stopwatch.GetTimestamp();
        var readSucceeded = false;
        try
        {
            sessionInfoYaml = sdk.GetSessionInfo();
            readSucceeded = true;
        }
        catch (Exception exception)
        {
            _state.RecordWarning($"Session info read failed: {exception.Message}");
            _events.Record("session_info_read_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name
            });
            _logger.LogWarning(exception, "Failed to read session info. Live telemetry frames will continue with the previous session context.");
            return;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetrySessionInfoRead,
                readStarted,
                readSucceeded);
        }

        if (string.IsNullOrWhiteSpace(sessionInfoYaml))
        {
            return;
        }

        if (capture is not null)
        {
            var captureQueueStarted = Stopwatch.GetTimestamp();
            var captureQueueSucceeded = false;
            try
            {
                RecordCaptureSessionInfoSnapshot(capture, capturedAtUtc, sessionInfoUpdate, sessionInfoYaml);
                captureQueueSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetrySessionInfoCaptureQueue,
                    captureQueueStarted,
                    captureQueueSucceeded);
            }
        }

        var applyStarted = Stopwatch.GetTimestamp();
        var applySucceeded = false;
        try
        {
            HistoricalSessionAccumulator? history;
            lock (_sync)
            {
                history = _activeHistory;
                _sessionInfoSnapshotCount++;
            }

            _liveTelemetrySink.ApplySessionInfo(sessionInfoYaml);
            history?.ApplySessionInfo(sessionInfoYaml);
            applySucceeded = true;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetrySessionInfoApply,
                applyStarted,
                applySucceeded);
        }
    }

    private void RecordCurrentCaptureSessionInfoSnapshot(
        TelemetryCaptureSession capture,
        IRacingSDK sdk,
        DateTimeOffset capturedAtUtc,
        int sessionInfoUpdate)
    {
        string sessionInfoYaml;
        var readStarted = Stopwatch.GetTimestamp();
        var readSucceeded = false;
        try
        {
            sessionInfoYaml = sdk.GetSessionInfo();
            readSucceeded = true;
        }
        catch (Exception exception)
        {
            _state.RecordWarning($"Raw capture session info read failed: {exception.Message}");
            _events.Record("capture_session_info_read_failed", new Dictionary<string, string?>
            {
                ["captureId"] = capture.CaptureId,
                ["error"] = exception.GetType().Name
            });
            _logger.LogWarning(exception, "Failed to read current session info for raw capture. Raw frame capture and live telemetry will continue.");
            return;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetrySessionInfoRead,
                readStarted,
                readSucceeded);
        }

        var captureQueueStarted = Stopwatch.GetTimestamp();
        var captureQueueSucceeded = false;
        try
        {
            RecordCaptureSessionInfoSnapshot(capture, capturedAtUtc, sessionInfoUpdate, sessionInfoYaml);
            captureQueueSucceeded = true;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetrySessionInfoCaptureQueue,
                captureQueueStarted,
                captureQueueSucceeded);
        }
    }

    private void RecordCaptureSessionInfoSnapshot(
        TelemetryCaptureSession capture,
        DateTimeOffset capturedAtUtc,
        int sessionInfoUpdate,
        string sessionInfoYaml)
    {
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
        IReadOnlyList<string> rawWatchVariableNames;
        lock (_sync)
        {
            history = _activeHistory;
            rawWatchVariableNames = _activeRawWatchVariableNames;
        }

        HistoricalTelemetrySample sample;
        RawTelemetryWatchSnapshot rawWatch = RawTelemetryWatchSnapshot.Empty;
        var buildSampleStarted = Stopwatch.GetTimestamp();
        var buildSampleSucceeded = false;
        try
        {
            var playerCarIdx = ReadInt32(sdk, "PlayerCarIdx");
            var focusCarIdx = ReadFocusCarIdx(sdk, playerCarIdx);
            var focusProgress = ReadCarProgress(sdk, focusCarIdx, requireLapProgress: false);

            CarProgress? leaderProgress;
            var leaderStarted = Stopwatch.GetTimestamp();
            var leaderSucceeded = false;
            try
            {
                leaderProgress = ReadLeaderProgress(sdk);
                leaderSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryHistoryReadLeader,
                    leaderStarted,
                    leaderSucceeded);
            }

            CarProgress? classLeaderProgress;
            var classLeaderStarted = Stopwatch.GetTimestamp();
            var classLeaderSucceeded = false;
            try
            {
                classLeaderProgress = ReadClassLeaderProgress(sdk, playerCarIdx);
                classLeaderSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryHistoryReadClassLeader,
                    classLeaderStarted,
                    classLeaderSucceeded);
            }

            var focusClassLeaderProgress = focusCarIdx == playerCarIdx
                ? classLeaderProgress
                : ReadClassLeaderProgress(sdk, focusCarIdx);

            IReadOnlyList<HistoricalCarProximity> nearbyCars;
            var nearbyStarted = Stopwatch.GetTimestamp();
            var nearbySucceeded = false;
            try
            {
                nearbyCars = ReadNearbyCars(sdk, focusCarIdx);
                nearbySucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryHistoryReadNearbyCars,
                    nearbyStarted,
                    nearbySucceeded);
            }

            IReadOnlyList<HistoricalCarProximity> classCars;
            var classCarsStarted = Stopwatch.GetTimestamp();
            var classCarsSucceeded = false;
            try
            {
                classCars = ReadClassCars(sdk, playerCarIdx);
                classCarsSucceeded = true;
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryHistoryReadClassCars,
                    classCarsStarted,
                    classCarsSucceeded);
            }

            var focusClassCars = focusCarIdx == playerCarIdx
                ? classCars
                : ReadClassCars(sdk, focusCarIdx);

            sample = new HistoricalTelemetrySample(
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
                FocusCarIdx: focusCarIdx,
                FocusLapCompleted: focusProgress?.LapCompleted,
                FocusLapDistPct: focusProgress?.LapDistPct,
                FocusF2TimeSeconds: focusProgress?.F2TimeSeconds,
                FocusEstimatedTimeSeconds: focusProgress?.EstimatedTimeSeconds,
                FocusLastLapTimeSeconds: focusProgress?.LastLapTimeSeconds,
                FocusBestLapTimeSeconds: focusProgress?.BestLapTimeSeconds,
                FocusPosition: focusProgress?.Position,
                FocusClassPosition: focusProgress?.ClassPosition,
                FocusCarClass: focusProgress?.CarClass,
                FocusOnPitRoad: ReadBooleanArrayElement(sdk, "CarIdxOnPitRoad", focusCarIdx),
                FocusTrackSurface: ReadInt32ArrayElement(sdk, "CarIdxTrackSurface", focusCarIdx),
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
                FocusClassLeaderCarIdx: focusClassLeaderProgress?.CarIdx,
                FocusClassLeaderLapCompleted: focusClassLeaderProgress?.LapCompleted,
                FocusClassLeaderLapDistPct: focusClassLeaderProgress?.LapDistPct,
                FocusClassLeaderF2TimeSeconds: focusClassLeaderProgress?.F2TimeSeconds,
                FocusClassLeaderEstimatedTimeSeconds: focusClassLeaderProgress?.EstimatedTimeSeconds,
                FocusClassLeaderLastLapTimeSeconds: focusClassLeaderProgress?.LastLapTimeSeconds,
                FocusClassLeaderBestLapTimeSeconds: focusClassLeaderProgress?.BestLapTimeSeconds,
                PlayerTrackSurface: ReadNullableInt32(sdk, "PlayerTrackSurface"),
                CarLeftRight: ReadNullableInt32(sdk, "CarLeftRight"),
                NearbyCars: nearbyCars,
                ClassCars: classCars,
                FocusClassCars: focusClassCars,
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
            rawWatch = ReadRawTelemetryWatchSnapshot(sdk, rawWatchVariableNames);
            buildSampleSucceeded = true;
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetryHistoryBuildSample,
                buildSampleStarted,
                buildSampleSucceeded);
        }

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
                liveSinkStarted,
                liveSinkSucceeded);
        }

        var edgeCaseStarted = Stopwatch.GetTimestamp();
        var edgeCaseSucceeded = false;
        try
        {
            _edgeCaseRecorder.RecordFrame(sample, rawWatch);
            edgeCaseSucceeded = true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to record telemetry edge-case frame.");
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetryEdgeCaseRecordFrame,
                edgeCaseStarted,
                edgeCaseSucceeded);
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
                historyStarted,
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
        var finalizeStarted = Stopwatch.GetTimestamp();
        var finalizeSucceeded = false;
        try
        {
            _state.MarkCaptureStopped();
            if (capture is not null)
            {
                var captureFinalizeStarted = Stopwatch.GetTimestamp();
                var captureFinalizeSucceeded = false;
                try
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
                    captureFinalizeSucceeded = true;
                }
                finally
                {
                    _performance.RecordOperation(
                        AppPerformanceMetricIds.TelemetryFinalizeCapture,
                        captureFinalizeStarted,
                        captureFinalizeSucceeded);
                }
            }

            if (history is not null && finalization.SourceId is not null && finalization.StartedAtUtc is not null)
            {
                HistoricalSessionSummary summary;
                var buildSummaryStarted = Stopwatch.GetTimestamp();
                var buildSummarySucceeded = false;
                try
                {
                    summary = history.BuildSummary(
                        finalization.SourceId,
                        finalization.StartedAtUtc.Value,
                        capture?.FinishedAtUtc ?? DateTimeOffset.UtcNow,
                        capture?.DroppedFrameCount ?? finalization.DroppedFrameCount,
                        capture?.SessionInfoSnapshotCount ?? finalization.SessionInfoSnapshotCount);
                    buildSummarySucceeded = true;
                }
                finally
                {
                    _performance.RecordOperation(
                        AppPerformanceMetricIds.TelemetryFinalizeBuildSummary,
                        buildSummaryStarted,
                        buildSummarySucceeded);
                }

                var saveHistoryStarted = Stopwatch.GetTimestamp();
                var saveHistorySucceeded = false;
                try
                {
                    await _sessionHistoryStore.SaveAsync(summary, CancellationToken.None).ConfigureAwait(false);
                    saveHistorySucceeded = true;
                }
                finally
                {
                    _performance.RecordOperation(
                        AppPerformanceMetricIds.TelemetryFinalizeSaveHistory,
                        saveHistoryStarted,
                        saveHistorySucceeded);
                }

                var saveAnalysisStarted = Stopwatch.GetTimestamp();
                var saveAnalysisSucceeded = false;
                try
                {
                    await _postRaceAnalysisPipeline.SaveFromSummaryAsync(summary, CancellationToken.None).ConfigureAwait(false);
                    saveAnalysisSucceeded = true;
                }
                finally
                {
                    _performance.RecordOperation(
                        AppPerformanceMetricIds.TelemetryFinalizeSaveAnalysis,
                        saveAnalysisStarted,
                        saveAnalysisSucceeded);
                }

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

            var edgeCaseFinalizeStarted = Stopwatch.GetTimestamp();
            var edgeCaseFinalizeSucceeded = false;
            try
            {
                _edgeCaseRecorder.CompleteCollection(capture?.FinishedAtUtc ?? DateTimeOffset.UtcNow);
                edgeCaseFinalizeSucceeded = true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to save telemetry edge-case artifact.");
            }
            finally
            {
                _performance.RecordOperation(
                    AppPerformanceMetricIds.TelemetryFinalizeEdgeCases,
                    edgeCaseFinalizeStarted,
                    edgeCaseFinalizeSucceeded);
            }

            finalizeSucceeded = true;
        }
        catch (Exception exception)
        {
            _state.RecordError($"Telemetry collection finalization failed: {exception.Message}");
            _logger.LogError(exception, "Failed to finalize telemetry collection.");
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetryFinalizeCollection,
                finalizeStarted,
                finalizeSucceeded);
        }

        CreateEndOfSessionDiagnosticsBundle(finalization);
    }

    private void CreateEndOfSessionDiagnosticsBundle(CaptureFinalizationContext finalization)
    {
        var diagnosticsStarted = Stopwatch.GetTimestamp();
        var diagnosticsSucceeded = false;
        try
        {
            var bundlePath = _diagnosticsBundleService.CreateBundle(DiagnosticsBundleSources.SessionFinalization);
            _events.Record("diagnostics_bundle_created", new Dictionary<string, string?>
            {
                ["bundlePath"] = bundlePath,
                ["source"] = DiagnosticsBundleSources.SessionFinalization,
                ["sourceId"] = finalization.SourceId
            });
            _logger.LogInformation(
                "Created end-of-session diagnostics bundle {DiagnosticsBundlePath} for {SourceId}.",
                bundlePath,
                finalization.SourceId);
            diagnosticsSucceeded = true;
        }
        catch (Exception exception)
        {
            _events.Record("diagnostics_bundle_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name,
                ["source"] = DiagnosticsBundleSources.SessionFinalization,
                ["sourceId"] = finalization.SourceId
            });
            _logger.LogWarning(
                exception,
                "Failed to create end-of-session diagnostics bundle for {SourceId}.",
                finalization.SourceId);
        }
        finally
        {
            _performance.RecordOperation(
                AppPerformanceMetricIds.TelemetryFinalizeDiagnosticsBundle,
                diagnosticsStarted,
                diagnosticsSucceeded);
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
