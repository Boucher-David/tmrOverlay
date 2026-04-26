using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureSession : IAsyncDisposable
{
    private const string TelemetryFileName = "telemetry.bin";
    private const string SchemaFileName = "telemetry-schema.json";
    private const string ManifestFileName = "capture-manifest.json";
    private const string LatestSessionInfoFileName = "latest-session.yaml";
    private const string SessionInfoDirectoryName = "session-info";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Channel<CaptureMessage> _messages;
    private readonly Task _writerTask;
    private readonly bool _storeSessionInfoSnapshots;
    private readonly string _telemetryFilePath;
    private readonly string _manifestFilePath;
    private readonly string _latestSessionInfoPath;
    private readonly string _sessionInfoDirectoryPath;
    private readonly CaptureManifest _manifest;
    private int _droppedFrameCount;
    private int _disposed;

    private TelemetryCaptureSession(
        string directoryPath,
        bool storeSessionInfoSnapshots,
        CaptureManifest manifest,
        int queueCapacity)
    {
        DirectoryPath = directoryPath;
        StartedAtUtc = manifest.StartedAtUtc;
        _storeSessionInfoSnapshots = storeSessionInfoSnapshots;
        _manifest = manifest;
        _telemetryFilePath = Path.Combine(directoryPath, TelemetryFileName);
        _manifestFilePath = Path.Combine(directoryPath, ManifestFileName);
        _latestSessionInfoPath = Path.Combine(directoryPath, LatestSessionInfoFileName);
        _sessionInfoDirectoryPath = Path.Combine(directoryPath, SessionInfoDirectoryName);
        _messages = Channel.CreateBounded<CaptureMessage>(new BoundedChannelOptions(queueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
        _writerTask = Task.Run(RunWriterLoopAsync);
    }

    public string DirectoryPath { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public static TelemetryCaptureSession Create(
        string rootDirectory,
        int queueCapacity,
        bool storeSessionInfoSnapshots,
        int sdkVersion,
        int tickRate,
        int bufferLength,
        IReadOnlyCollection<TelemetryVariableSchema> schema)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var captureId = $"capture-{startedAtUtc:yyyyMMdd-HHmmss-fff}";
        var directoryPath = Path.Combine(rootDirectory, captureId);

        Directory.CreateDirectory(directoryPath);

        var manifest = new CaptureManifest
        {
            CaptureId = captureId,
            StartedAtUtc = startedAtUtc,
            TelemetryFile = TelemetryFileName,
            SchemaFile = SchemaFileName,
            LatestSessionInfoFile = LatestSessionInfoFileName,
            SessionInfoDirectory = SessionInfoDirectoryName,
            SdkVersion = sdkVersion,
            TickRate = tickRate,
            BufferLength = bufferLength,
            VariableCount = schema.Count
        };

        File.WriteAllText(
            Path.Combine(directoryPath, SchemaFileName),
            JsonSerializer.Serialize(schema, JsonOptions));

        File.WriteAllText(
            Path.Combine(directoryPath, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));

        return new TelemetryCaptureSession(
            directoryPath,
            storeSessionInfoSnapshots,
            manifest,
            queueCapacity);
    }

    public bool TryQueueFrame(TelemetryFrameEnvelope frame)
    {
        return _messages.Writer.TryWrite(new CaptureFrameMessage(frame));
    }

    public bool TryQueueSessionInfo(SessionInfoSnapshot sessionInfo)
    {
        return _messages.Writer.TryWrite(new CaptureSessionInfoMessage(sessionInfo));
    }

    public void RecordDroppedFrame()
    {
        Interlocked.Increment(ref _droppedFrameCount);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _messages.Writer.TryComplete();
        await _writerTask.ConfigureAwait(false);
    }

    private async Task RunWriterLoopAsync()
    {
        Directory.CreateDirectory(_sessionInfoDirectoryPath);

        await using var telemetryStream = new FileStream(
            _telemetryFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        await WriteFileHeaderAsync(telemetryStream).ConfigureAwait(false);

        await foreach (var message in _messages.Reader.ReadAllAsync())
        {
            switch (message)
            {
                case CaptureFrameMessage frameMessage:
                    await WriteFrameAsync(telemetryStream, frameMessage.Frame).ConfigureAwait(false);
                    _manifest.FrameCount++;
                    break;

                case CaptureSessionInfoMessage sessionInfoMessage:
                    await WriteSessionInfoAsync(sessionInfoMessage.SessionInfo).ConfigureAwait(false);
                    _manifest.SessionInfoSnapshotCount++;
                    break;
            }
        }

        await telemetryStream.FlushAsync().ConfigureAwait(false);

        _manifest.FinishedAtUtc = DateTimeOffset.UtcNow;
        _manifest.DroppedFrameCount = Volatile.Read(ref _droppedFrameCount);

        await File.WriteAllTextAsync(
                _manifestFilePath,
                JsonSerializer.Serialize(_manifest, JsonOptions))
            .ConfigureAwait(false);
    }

    private async Task WriteFileHeaderAsync(FileStream telemetryStream)
    {
        var header = new byte[32];
        Encoding.ASCII.GetBytes("TMRCAP01").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8, 4), _manifest.SdkVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), _manifest.TickRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), _manifest.BufferLength);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(20, 4), _manifest.VariableCount);
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(24, 8), _manifest.StartedAtUtc.ToUnixTimeMilliseconds());
        await telemetryStream.WriteAsync(header).ConfigureAwait(false);
    }

    private static async Task WriteFrameAsync(FileStream telemetryStream, TelemetryFrameEnvelope frame)
    {
        var frameHeader = new byte[32];
        BinaryPrimitives.WriteInt64LittleEndian(frameHeader.AsSpan(0, 8), frame.CapturedAtUtc.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteInt32LittleEndian(frameHeader.AsSpan(8, 4), frame.FrameIndex);
        BinaryPrimitives.WriteInt32LittleEndian(frameHeader.AsSpan(12, 4), frame.SessionTick);
        BinaryPrimitives.WriteInt32LittleEndian(frameHeader.AsSpan(16, 4), frame.SessionInfoUpdate);
        BinaryPrimitives.WriteInt64LittleEndian(frameHeader.AsSpan(20, 8), BitConverter.DoubleToInt64Bits(frame.SessionTime));
        BinaryPrimitives.WriteInt32LittleEndian(frameHeader.AsSpan(28, 4), frame.Payload.Length);

        await telemetryStream.WriteAsync(frameHeader).ConfigureAwait(false);
        await telemetryStream.WriteAsync(frame.Payload).ConfigureAwait(false);
    }

    private async Task WriteSessionInfoAsync(SessionInfoSnapshot sessionInfo)
    {
        await File.WriteAllTextAsync(_latestSessionInfoPath, sessionInfo.Yaml).ConfigureAwait(false);

        if (!_storeSessionInfoSnapshots)
        {
            return;
        }

        var snapshotPath = Path.Combine(
            _sessionInfoDirectoryPath,
            $"session-{sessionInfo.SessionInfoUpdate:D4}.yaml");

        await File.WriteAllTextAsync(snapshotPath, sessionInfo.Yaml).ConfigureAwait(false);
    }

    private abstract record CaptureMessage;

    private sealed record CaptureFrameMessage(TelemetryFrameEnvelope Frame) : CaptureMessage;

    private sealed record CaptureSessionInfoMessage(SessionInfoSnapshot SessionInfo) : CaptureMessage;
}
