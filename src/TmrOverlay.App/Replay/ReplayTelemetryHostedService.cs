using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Replay;

internal sealed class ReplayTelemetryHostedService : IHostedService
{
    private readonly ReplayOptions _options;
    private readonly TelemetryCaptureState _state;
    private readonly ILiveTelemetrySink _liveTelemetrySink;
    private readonly AppEventRecorder _events;
    private readonly ILogger<ReplayTelemetryHostedService> _logger;
    private CancellationTokenSource? _replayCancellation;
    private Task? _replayTask;

    public ReplayTelemetryHostedService(
        ReplayOptions options,
        TelemetryCaptureState state,
        ILiveTelemetrySink liveTelemetrySink,
        AppEventRecorder events,
        ILogger<ReplayTelemetryHostedService> logger)
    {
        _options = options;
        _state = state;
        _liveTelemetrySink = liveTelemetrySink;
        _events = events;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_options.CaptureDirectory))
        {
            _logger.LogWarning("Replay mode is enabled, but no Replay:CaptureDirectory was configured.");
            return Task.CompletedTask;
        }

        _replayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _replayTask = Task.Run(() => RunReplayAsync(_replayCancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _replayCancellation?.Cancel();

        if (_replayTask is not null)
        {
            try
            {
                await _replayTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_replayCancellation?.IsCancellationRequested == true)
            {
                // Expected when the host is stopping replay mode.
            }
        }

        _state.MarkDisconnected();
        _liveTelemetrySink.MarkDisconnected();
    }

    private async Task RunReplayAsync(CancellationToken cancellationToken)
    {
        var manifest = ReadManifest(_options.CaptureDirectory!);
        _logger.LogInformation(
            "Replay mode started from {CaptureDirectory} with {FrameCount} frames.",
            _options.CaptureDirectory,
            manifest?.FrameCount);
        _events.Record("replay_started", new Dictionary<string, string?>
        {
            ["captureDirectory"] = _options.CaptureDirectory,
            ["frameCount"] = manifest?.FrameCount.ToString()
        });

        try
        {
            _state.SetCaptureRoot(Path.GetDirectoryName(_options.CaptureDirectory!) ?? _options.CaptureDirectory!);
            _state.SetRawCaptureEnabled(true);
            _state.MarkConnected();
            _liveTelemetrySink.MarkConnected();
            var startedAtUtc = DateTimeOffset.UtcNow;
            var sourceId = Path.GetFileName(_options.CaptureDirectory!);
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                sourceId = "replay";
            }

            _state.MarkCaptureStarted(_options.CaptureDirectory!, startedAtUtc);
            _liveTelemetrySink.MarkCollectionStarted(sourceId, startedAtUtc);

            var frameCount = Math.Max(0, manifest?.FrameCount ?? 0);
            var intervalMs = Math.Max(1, (int)Math.Round(1000d / Math.Max(1, manifest?.TickRate ?? 60) / _options.SpeedMultiplier));
            var telemetryFileBytes = ReadTelemetryFileBytes(_options.CaptureDirectory!);

            for (var frame = 0; frame < frameCount && !cancellationToken.IsCancellationRequested; frame++)
            {
                var timestampUtc = DateTimeOffset.UtcNow;
                _state.RecordFrame(timestampUtc);
                _state.RecordCaptureWrite(new TelemetryCaptureWriteStatus(
                    TimestampUtc: timestampUtc,
                    CaptureId: Path.GetFileName(_options.CaptureDirectory!),
                    DirectoryPath: _options.CaptureDirectory!,
                    FramesWritten: frame + 1,
                    SessionInfoSnapshotCount: 0,
                    TelemetryFileBytes: telemetryFileBytes,
                    Exception: null));
                await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the host stops while replay is active.
        }
        finally
        {
            _state.MarkCaptureStopped();
            _state.MarkDisconnected();
            _liveTelemetrySink.MarkDisconnected();
            _events.Record("replay_stopped");
            _logger.LogInformation("Replay mode stopped.");
        }
    }

    private static ReplayCaptureManifest? ReadManifest(string captureDirectory)
    {
        var manifestPath = Path.Combine(captureDirectory, "capture-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        using var stream = File.OpenRead(manifestPath);
        return JsonSerializer.Deserialize<ReplayCaptureManifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static long? ReadTelemetryFileBytes(string captureDirectory)
    {
        var telemetryPath = Path.Combine(captureDirectory, "telemetry.bin");
        return File.Exists(telemetryPath)
            ? new FileInfo(telemetryPath).Length
            : null;
    }

    private sealed class ReplayCaptureManifest
    {
        public int TickRate { get; init; } = 60;

        public int FrameCount { get; init; }
    }
}
