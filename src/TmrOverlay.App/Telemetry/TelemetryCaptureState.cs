namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureState
{
    private readonly object _sync = new();
    private bool _isConnected;
    private bool _isCollecting;
    private bool _rawCaptureEnabled;
    private string? _captureRoot;
    private string? _currentCaptureDirectory;
    private string? _lastCaptureDirectory;
    private int _frameCount;
    private int _writtenFrameCount;
    private int _droppedFrameCount;
    private long? _telemetryFileBytes;
    private DateTimeOffset? _captureStartedAtUtc;
    private DateTimeOffset? _lastFrameCapturedAtUtc;
    private DateTimeOffset? _lastDiskWriteAtUtc;
    private string? _appWarning;
    private string? _lastWarning;
    private string? _lastError;
    private DateTimeOffset? _lastIssueAtUtc;

    public void SetCaptureRoot(string captureRoot)
    {
        lock (_sync)
        {
            _captureRoot = captureRoot;
        }
    }

    public void SetRawCaptureEnabled(bool enabled)
    {
        lock (_sync)
        {
            _rawCaptureEnabled = enabled;
        }
    }

    public void MarkConnected()
    {
        lock (_sync)
        {
            _isConnected = true;
            _lastWarning = null;
        }
    }

    public void MarkDisconnected()
    {
        lock (_sync)
        {
            _isConnected = false;
            _isCollecting = false;
            _currentCaptureDirectory = null;
            _captureStartedAtUtc = null;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _telemetryFileBytes = null;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
        }
    }

    public void MarkCaptureStarted(string captureDirectory, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _isCollecting = true;
            _currentCaptureDirectory = captureDirectory;
            _lastCaptureDirectory = captureDirectory;
            _captureStartedAtUtc = startedAtUtc;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _telemetryFileBytes = null;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
            _lastWarning = null;
            _lastError = null;
            _lastIssueAtUtc = null;
        }
    }

    public void MarkCollectionStarted(DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _isCollecting = true;
            _captureStartedAtUtc = startedAtUtc;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _telemetryFileBytes = null;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
            _lastWarning = null;
            _lastError = null;
            _lastIssueAtUtc = null;
        }
    }

    public void MarkCaptureStopped()
    {
        lock (_sync)
        {
            _isCollecting = false;
            _currentCaptureDirectory = null;
            _captureStartedAtUtc = null;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
        }
    }

    public void RecordFrame(DateTimeOffset capturedAtUtc)
    {
        lock (_sync)
        {
            _frameCount++;
            _lastFrameCapturedAtUtc = capturedAtUtc;
        }
    }

    public void RecordDroppedFrame()
    {
        lock (_sync)
        {
            _droppedFrameCount++;
        }
    }

    public void RecordCaptureWrite(TelemetryCaptureWriteStatus writeStatus)
    {
        lock (_sync)
        {
            _writtenFrameCount = writeStatus.FramesWritten;
            _telemetryFileBytes = writeStatus.TelemetryFileBytes ?? _telemetryFileBytes;
            _lastDiskWriteAtUtc = writeStatus.TimestampUtc;

            if (writeStatus.Exception is not null)
            {
                RecordErrorCore($"Capture writer failed: {writeStatus.Exception.Message}", writeStatus.TimestampUtc);
            }
        }
    }

    public void RecordWarning(string message)
    {
        lock (_sync)
        {
            _lastWarning = message;
            _lastIssueAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void RecordAppWarning(string message)
    {
        lock (_sync)
        {
            _appWarning = message;
        }
    }

    public void RecordError(string message)
    {
        lock (_sync)
        {
            RecordErrorCore(message, DateTimeOffset.UtcNow);
        }
    }

    public TelemetryCaptureStatusSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new TelemetryCaptureStatusSnapshot(
                IsConnected: _isConnected,
                IsCapturing: _isCollecting,
                RawCaptureEnabled: _rawCaptureEnabled,
                RawCaptureActive: !string.IsNullOrWhiteSpace(_currentCaptureDirectory),
                CaptureRoot: _captureRoot,
                CurrentCaptureDirectory: _currentCaptureDirectory,
                LastCaptureDirectory: _lastCaptureDirectory,
                FrameCount: _frameCount,
                WrittenFrameCount: _writtenFrameCount,
                DroppedFrameCount: _droppedFrameCount,
                TelemetryFileBytes: _telemetryFileBytes,
                CaptureStartedAtUtc: _captureStartedAtUtc,
                LastFrameCapturedAtUtc: _lastFrameCapturedAtUtc,
                LastDiskWriteAtUtc: _lastDiskWriteAtUtc,
                AppWarning: _appWarning,
                LastWarning: _lastWarning,
                LastError: _lastError,
                LastIssueAtUtc: _lastIssueAtUtc);
        }
    }

    private void RecordErrorCore(string message, DateTimeOffset timestampUtc)
    {
        _lastError = message;
        _lastIssueAtUtc = timestampUtc;
    }
}
