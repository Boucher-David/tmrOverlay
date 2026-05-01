namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureState
{
    private readonly object _sync = new();
    private bool _isConnected;
    private bool _isCollecting;
    private bool _rawCaptureEnabled;
    private bool _rawCaptureStopRequested;
    private DateTimeOffset? _rawCaptureStopRequestedAtUtc;
    private DateTimeOffset? _lastRawCaptureStoppedAtUtc;
    private string? _appRunId;
    private string? _currentCollectionId;
    private string? _lastCollectionId;
    private string? _currentSourceId;
    private string? _lastSourceId;
    private string? _currentCaptureId;
    private string? _lastCaptureId;
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
    private int _captureWriteStatusCount;
    private long? _lastCaptureWriteBytes;
    private long? _lastCaptureWriteElapsedMilliseconds;
    private string? _lastCaptureWriteKind;
    private double? _averageCaptureWriteElapsedMilliseconds;
    private long? _maxCaptureWriteElapsedMilliseconds;
    private bool _isCaptureSynthesisPending;
    private DateTimeOffset? _captureSynthesisPendingSinceUtc;
    private string? _captureSynthesisPendingReason;
    private bool _isCaptureSynthesisRunning;
    private DateTimeOffset? _captureSynthesisStartedAtUtc;
    private int _captureSynthesisSaveCount;
    private DateTimeOffset? _lastCaptureSynthesisSavedAtUtc;
    private string? _lastCaptureSynthesisPath;
    private long? _lastCaptureSynthesisBytes;
    private long? _lastCaptureSynthesisTelemetryBytes;
    private long? _lastCaptureSynthesisElapsedMilliseconds;
    private long? _lastCaptureSynthesisProcessCpuMilliseconds;
    private double? _lastCaptureSynthesisProcessCpuPercentOfOneCore;
    private int? _lastCaptureSynthesisTotalFrameRecords;
    private int? _lastCaptureSynthesisSampledFrameCount;
    private int? _lastCaptureSynthesisSampleStride;
    private int? _lastCaptureSynthesisFieldCount;
    private bool _isHistoryFinalizing;
    private DateTimeOffset? _historyFinalizationStartedAtUtc;
    private int _historySummarySaveCount;
    private DateTimeOffset? _lastHistorySavedAtUtc;
    private string? _lastHistorySummaryLabel;
    private long? _lastHistoryFinalizationElapsedMilliseconds;
    private long? _lastHistorySaveElapsedMilliseconds;
    private long? _lastAnalysisSaveElapsedMilliseconds;
    private string? _appWarning;
    private string? _lastWarning;
    private string? _lastError;
    private DateTimeOffset? _lastIssueAtUtc;

    public void SetAppRunId(string appRunId)
    {
        lock (_sync)
        {
            _appRunId = appRunId;
        }
    }

    public void SetCaptureRoot(string captureRoot)
    {
        lock (_sync)
        {
            _captureRoot = captureRoot;
        }
    }

    public bool SetRawCaptureEnabled(bool enabled)
    {
        lock (_sync)
        {
            if (enabled)
            {
                _rawCaptureEnabled = true;
                _rawCaptureStopRequested = false;
                _rawCaptureStopRequestedAtUtc = null;
                _lastWarning = null;
                return true;
            }

            _rawCaptureEnabled = false;
            if (!string.IsNullOrWhiteSpace(_currentCaptureDirectory))
            {
                var now = DateTimeOffset.UtcNow;
                _rawCaptureStopRequested = true;
                _rawCaptureStopRequestedAtUtc ??= now;
                _lastWarning = "Raw capture stop requested; live telemetry analysis will continue.";
                _lastIssueAtUtc = now;
                return true;
            }

            _rawCaptureStopRequested = false;
            _rawCaptureStopRequestedAtUtc = null;
            return true;
        }
    }

    public bool IsRawCaptureEnabled()
    {
        lock (_sync)
        {
            return _rawCaptureEnabled;
        }
    }

    public bool IsRawCaptureStopRequested()
    {
        lock (_sync)
        {
            return _rawCaptureStopRequested;
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
            _rawCaptureStopRequested = false;
            _rawCaptureStopRequestedAtUtc = null;
            _currentCollectionId = null;
            _currentSourceId = null;
            _currentCaptureId = null;
            _currentCaptureDirectory = null;
            _captureStartedAtUtc = null;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _telemetryFileBytes = null;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
            ResetCaptureWritePerformance();
            ResetCaptureSynthesisPending();
            _isCaptureSynthesisRunning = false;
            _captureSynthesisStartedAtUtc = null;
            _isHistoryFinalizing = false;
            _historyFinalizationStartedAtUtc = null;
        }
    }

    public void MarkCaptureStarted(
        string captureDirectory,
        DateTimeOffset startedAtUtc,
        string? captureId = null,
        string? collectionId = null,
        string? sourceId = null)
    {
        lock (_sync)
        {
            _isCollecting = true;
            _currentCollectionId = collectionId ?? _currentCollectionId;
            _lastCollectionId = _currentCollectionId ?? _lastCollectionId;
            _currentSourceId = sourceId ?? _currentSourceId;
            _lastSourceId = _currentSourceId ?? _lastSourceId;
            _currentCaptureId = captureId;
            _lastCaptureId = captureId ?? _lastCaptureId;
            _currentCaptureDirectory = captureDirectory;
            _lastCaptureDirectory = captureDirectory;
            _rawCaptureStopRequested = false;
            _rawCaptureStopRequestedAtUtc = null;
            _captureStartedAtUtc = startedAtUtc;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _telemetryFileBytes = null;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
            ResetCaptureWritePerformance();
            ResetCaptureSynthesisPending();
            _lastWarning = null;
            _lastError = null;
            _lastIssueAtUtc = null;
            _isCaptureSynthesisRunning = false;
            _captureSynthesisStartedAtUtc = null;
            _isHistoryFinalizing = false;
            _historyFinalizationStartedAtUtc = null;
        }
    }

    public void MarkCollectionStarted(
        string collectionId,
        string sourceId,
        DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _isCollecting = true;
            _currentCollectionId = collectionId;
            _lastCollectionId = collectionId;
            _currentSourceId = sourceId;
            _lastSourceId = sourceId;
            _captureStartedAtUtc = startedAtUtc;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _telemetryFileBytes = null;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
            ResetCaptureWritePerformance();
            ResetCaptureSynthesisPending();
            _lastWarning = null;
            _lastError = null;
            _lastIssueAtUtc = null;
            _isCaptureSynthesisRunning = false;
            _captureSynthesisStartedAtUtc = null;
            _isHistoryFinalizing = false;
            _historyFinalizationStartedAtUtc = null;
        }
    }

    public void MarkCaptureStopped()
    {
        lock (_sync)
        {
            _isCollecting = false;
            _currentCollectionId = null;
            _currentSourceId = null;
            _currentCaptureId = null;
            _currentCaptureDirectory = null;
            _rawCaptureStopRequested = false;
            _rawCaptureStopRequestedAtUtc = null;
            _captureStartedAtUtc = null;
            _frameCount = 0;
            _writtenFrameCount = 0;
            _droppedFrameCount = 0;
            _lastFrameCapturedAtUtc = null;
            _lastDiskWriteAtUtc = null;
        }
    }

    public void MarkRawCaptureStopped(DateTimeOffset stoppedAtUtc)
    {
        lock (_sync)
        {
            _currentCaptureId = null;
            _currentCaptureDirectory = null;
            _rawCaptureStopRequested = false;
            _rawCaptureStopRequestedAtUtc = null;
            _lastRawCaptureStoppedAtUtc = stoppedAtUtc;
            _captureStartedAtUtc = null;
            if (string.Equals(
                    _lastWarning,
                    "Raw capture stop requested; live telemetry analysis will continue.",
                    StringComparison.Ordinal))
            {
                _lastWarning = null;
                _lastIssueAtUtc = null;
            }
        }
    }

    public void MarkCaptureSynthesisStarted(DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            ResetCaptureSynthesisPending();
            _isCaptureSynthesisRunning = true;
            _captureSynthesisStartedAtUtc = startedAtUtc;
        }
    }

    public void MarkCaptureSynthesisPending(DateTimeOffset pendingSinceUtc, string reason)
    {
        lock (_sync)
        {
            if (!_isCaptureSynthesisPending)
            {
                _captureSynthesisPendingSinceUtc = pendingSinceUtc;
            }

            _isCaptureSynthesisPending = true;
            _captureSynthesisPendingReason = reason;
        }
    }

    public void MarkCaptureSynthesisPendingCleared()
    {
        lock (_sync)
        {
            ResetCaptureSynthesisPending();
        }
    }

    public void MarkCaptureSynthesisSaved(CaptureSynthesisResult result)
    {
        lock (_sync)
        {
            _captureSynthesisSaveCount++;
            _lastCaptureSynthesisSavedAtUtc = result.FinishedAtUtc;
            _lastCaptureSynthesisPath = result.Path;
            _lastCaptureSynthesisBytes = result.Bytes;
            _lastCaptureSynthesisTelemetryBytes = result.TelemetryBytes;
            _lastCaptureSynthesisElapsedMilliseconds = result.ElapsedMilliseconds;
            _lastCaptureSynthesisProcessCpuMilliseconds = result.ProcessCpuMilliseconds;
            _lastCaptureSynthesisProcessCpuPercentOfOneCore = result.ProcessCpuPercentOfOneCore;
            _lastCaptureSynthesisTotalFrameRecords = result.TotalFrameRecords;
            _lastCaptureSynthesisSampledFrameCount = result.SampledFrameCount;
            _lastCaptureSynthesisSampleStride = result.SampleStride;
            _lastCaptureSynthesisFieldCount = result.FieldCount;
        }
    }

    public void MarkCaptureSynthesisStopped()
    {
        lock (_sync)
        {
            _isCaptureSynthesisRunning = false;
            _captureSynthesisStartedAtUtc = null;
        }
    }

    public void MarkHistoryFinalizationStarted(DateTimeOffset startedAtUtc)
    {
        lock (_sync)
        {
            _isHistoryFinalizing = true;
            _historyFinalizationStartedAtUtc = startedAtUtc;
        }
    }

    public void MarkHistorySummarySaved(
        string summaryLabel,
        DateTimeOffset savedAtUtc,
        long? historySaveElapsedMilliseconds = null,
        long? analysisSaveElapsedMilliseconds = null)
    {
        lock (_sync)
        {
            _historySummarySaveCount++;
            _lastHistorySavedAtUtc = savedAtUtc;
            _lastHistorySummaryLabel = summaryLabel;
            _lastHistorySaveElapsedMilliseconds = historySaveElapsedMilliseconds;
            _lastAnalysisSaveElapsedMilliseconds = analysisSaveElapsedMilliseconds;
        }
    }

    public void MarkHistoryFinalizationStopped(long? elapsedMilliseconds = null)
    {
        lock (_sync)
        {
            _isHistoryFinalizing = false;
            _historyFinalizationStartedAtUtc = null;
            _lastHistoryFinalizationElapsedMilliseconds = elapsedMilliseconds ?? _lastHistoryFinalizationElapsedMilliseconds;
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
            if (writeStatus.LastWriteElapsedMilliseconds is not null)
            {
                _captureWriteStatusCount++;
                _lastCaptureWriteBytes = writeStatus.LastWriteBytes;
                _lastCaptureWriteElapsedMilliseconds = writeStatus.LastWriteElapsedMilliseconds;
                _lastCaptureWriteKind = writeStatus.LastWriteKind;
                _averageCaptureWriteElapsedMilliseconds = writeStatus.AverageWriteElapsedMilliseconds ?? _averageCaptureWriteElapsedMilliseconds;
                _maxCaptureWriteElapsedMilliseconds = writeStatus.MaxWriteElapsedMilliseconds ?? _maxCaptureWriteElapsedMilliseconds;
            }

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
                RawCaptureStopRequested: _rawCaptureStopRequested,
                RawCaptureStopRequestedAtUtc: _rawCaptureStopRequestedAtUtc,
                LastRawCaptureStoppedAtUtc: _lastRawCaptureStoppedAtUtc,
                AppRunId: _appRunId,
                CurrentCollectionId: _currentCollectionId,
                LastCollectionId: _lastCollectionId,
                CurrentSourceId: _currentSourceId,
                LastSourceId: _lastSourceId,
                CurrentCaptureId: _currentCaptureId,
                LastCaptureId: _lastCaptureId,
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
                CaptureWriteStatusCount: _captureWriteStatusCount,
                LastCaptureWriteBytes: _lastCaptureWriteBytes,
                LastCaptureWriteElapsedMilliseconds: _lastCaptureWriteElapsedMilliseconds,
                LastCaptureWriteKind: _lastCaptureWriteKind,
                AverageCaptureWriteElapsedMilliseconds: _averageCaptureWriteElapsedMilliseconds,
                MaxCaptureWriteElapsedMilliseconds: _maxCaptureWriteElapsedMilliseconds,
                IsCaptureSynthesisPending: _isCaptureSynthesisPending,
                CaptureSynthesisPendingSinceUtc: _captureSynthesisPendingSinceUtc,
                CaptureSynthesisPendingReason: _captureSynthesisPendingReason,
                IsCaptureSynthesisRunning: _isCaptureSynthesisRunning,
                CaptureSynthesisStartedAtUtc: _captureSynthesisStartedAtUtc,
                CaptureSynthesisSaveCount: _captureSynthesisSaveCount,
                LastCaptureSynthesisSavedAtUtc: _lastCaptureSynthesisSavedAtUtc,
                LastCaptureSynthesisPath: _lastCaptureSynthesisPath,
                LastCaptureSynthesisBytes: _lastCaptureSynthesisBytes,
                LastCaptureSynthesisTelemetryBytes: _lastCaptureSynthesisTelemetryBytes,
                LastCaptureSynthesisElapsedMilliseconds: _lastCaptureSynthesisElapsedMilliseconds,
                LastCaptureSynthesisProcessCpuMilliseconds: _lastCaptureSynthesisProcessCpuMilliseconds,
                LastCaptureSynthesisProcessCpuPercentOfOneCore: _lastCaptureSynthesisProcessCpuPercentOfOneCore,
                LastCaptureSynthesisTotalFrameRecords: _lastCaptureSynthesisTotalFrameRecords,
                LastCaptureSynthesisSampledFrameCount: _lastCaptureSynthesisSampledFrameCount,
                LastCaptureSynthesisSampleStride: _lastCaptureSynthesisSampleStride,
                LastCaptureSynthesisFieldCount: _lastCaptureSynthesisFieldCount,
                IsHistoryFinalizing: _isHistoryFinalizing,
                HistoryFinalizationStartedAtUtc: _historyFinalizationStartedAtUtc,
                HistorySummarySaveCount: _historySummarySaveCount,
                LastHistorySavedAtUtc: _lastHistorySavedAtUtc,
                LastHistorySummaryLabel: _lastHistorySummaryLabel,
                LastHistoryFinalizationElapsedMilliseconds: _lastHistoryFinalizationElapsedMilliseconds,
                LastHistorySaveElapsedMilliseconds: _lastHistorySaveElapsedMilliseconds,
                LastAnalysisSaveElapsedMilliseconds: _lastAnalysisSaveElapsedMilliseconds,
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

    private void ResetCaptureWritePerformance()
    {
        _captureWriteStatusCount = 0;
        _lastCaptureWriteBytes = null;
        _lastCaptureWriteElapsedMilliseconds = null;
        _lastCaptureWriteKind = null;
        _averageCaptureWriteElapsedMilliseconds = null;
        _maxCaptureWriteElapsedMilliseconds = null;
    }

    private void ResetCaptureSynthesisPending()
    {
        _isCaptureSynthesisPending = false;
        _captureSynthesisPendingSinceUtc = null;
        _captureSynthesisPendingReason = null;
    }
}
