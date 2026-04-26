using irsdkSharp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureHostedService : IHostedService
{
    private readonly ILogger<TelemetryCaptureHostedService> _logger;
    private readonly TelemetryCaptureOptions _options;
    private readonly TelemetryCaptureState _state;
    private readonly object _sync = new();
    private IRacingSDK? _sdk;
    private TelemetryCaptureSession? _activeCapture;
    private Task _finalizerTask = Task.CompletedTask;
    private int _frameIndex;
    private int _lastSessionInfoUpdate = -1;

    public TelemetryCaptureHostedService(
        ILogger<TelemetryCaptureHostedService> logger,
        TelemetryCaptureOptions options,
        TelemetryCaptureState state)
    {
        _logger = logger;
        _options = options;
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
        lock (_sync)
        {
            captureToFinalize = _activeCapture;
            _activeCapture = null;
            _lastSessionInfoUpdate = -1;
        }

        if (captureToFinalize is not null)
        {
            await FinalizeCaptureAsync(captureToFinalize).ConfigureAwait(false);
        }

        await _finalizerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void HandleConnected()
    {
        _state.MarkConnected();
        _logger.LogInformation("Connected to iRacing.");
    }

    private void HandleDisconnected()
    {
        _logger.LogInformation("Disconnected from iRacing.");

        _state.MarkDisconnected();

        TelemetryCaptureSession? captureToFinalize;
        lock (_sync)
        {
            captureToFinalize = _activeCapture;
            _activeCapture = null;
            _lastSessionInfoUpdate = -1;
            _frameIndex = 0;
        }

        if (captureToFinalize is not null)
        {
            _finalizerTask = FinalizeCaptureAsync(captureToFinalize);
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
                _logger.LogWarning("Dropped telemetry frame because the capture queue is full.");
                return;
            }

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

            _frameIndex = 0;
            _lastSessionInfoUpdate = -1;
            _state.MarkCaptureStarted(_activeCapture.DirectoryPath, _activeCapture.StartedAtUtc);
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
    }

    private async Task FinalizeCaptureAsync(TelemetryCaptureSession capture)
    {
        try
        {
            _state.MarkCaptureStopped();
            await capture.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Finalized capture {CaptureDirectory}.", capture.DirectoryPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to finalize capture {CaptureDirectory}.", capture.DirectoryPath);
        }
    }
}
