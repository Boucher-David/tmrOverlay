namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureState
{
    private readonly object _sync = new();
    private bool _isConnected;
    private string? _currentCaptureDirectory;
    private string? _lastCaptureDirectory;
    private int _frameCount;
    private int _droppedFrameCount;
    private DateTimeOffset? _captureStartedAtUtc;
    private DateTimeOffset? _lastFrameCapturedAtUtc;

    public void MarkConnected()
    {
        lock (_sync)
        {
            _isConnected = true;
        }
    }

    public void MarkDisconnected()
    {
        lock (_sync)
        {
            _isConnected = false;
            _currentCaptureDirectory = null;
            _captureStartedAtUtc = null;
            _frameCount = 0;
            _droppedFrameCount = 0;
            _lastFrameCapturedAtUtc = null;
        }
    }

    public void MarkCaptureStarted(string captureDirectory, DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _currentCaptureDirectory = captureDirectory;
            _lastCaptureDirectory = captureDirectory;
            _captureStartedAtUtc = startedAtUtc;
            _frameCount = 0;
            _droppedFrameCount = 0;
            _lastFrameCapturedAtUtc = null;
        }
    }

    public void MarkCaptureStopped()
    {
        lock (_sync)
        {
            _currentCaptureDirectory = null;
            _captureStartedAtUtc = null;
            _frameCount = 0;
            _droppedFrameCount = 0;
            _lastFrameCapturedAtUtc = null;
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

    public TelemetryCaptureStatusSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new TelemetryCaptureStatusSnapshot(
                IsConnected: _isConnected,
                IsCapturing: !string.IsNullOrWhiteSpace(_currentCaptureDirectory),
                CurrentCaptureDirectory: _currentCaptureDirectory,
                LastCaptureDirectory: _lastCaptureDirectory,
                FrameCount: _frameCount,
                DroppedFrameCount: _droppedFrameCount,
                CaptureStartedAtUtc: _captureStartedAtUtc,
                LastFrameCapturedAtUtc: _lastFrameCapturedAtUtc);
        }
    }
}

