using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Diagnostics;

internal sealed class DiagnosticsBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly TelemetryCaptureState _captureState;
    private readonly AppPerformanceState _performanceState;
    private readonly AppPerformanceSnapshotRecorder _performanceRecorder;
    private readonly ILogger<DiagnosticsBundleService> _logger;

    public DiagnosticsBundleService(
        AppStorageOptions storageOptions,
        TelemetryCaptureState captureState,
        AppPerformanceState performanceState,
        AppPerformanceSnapshotRecorder performanceRecorder,
        ILogger<DiagnosticsBundleService> logger)
    {
        _storageOptions = storageOptions;
        _captureState = captureState;
        _performanceState = performanceState;
        _performanceRecorder = performanceRecorder;
        _logger = logger;
    }

    public string CreateBundle()
    {
        Directory.CreateDirectory(_storageOptions.DiagnosticsRoot);
        var bundlePath = Path.Combine(
            _storageOptions.DiagnosticsRoot,
            $"tmroverlay-diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");

        using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);
        AddTextEntry(archive, "metadata/app-version.json", JsonSerializer.Serialize(AppVersionInfo.Current, JsonOptions));
        AddTextEntry(archive, "metadata/storage.json", JsonSerializer.Serialize(_storageOptions, JsonOptions));
        AddTextEntry(archive, "metadata/telemetry-state.json", JsonSerializer.Serialize(_captureState.Snapshot(), JsonOptions));
        AddTextEntry(archive, "metadata/performance.json", JsonSerializer.Serialize(_performanceState.Snapshot(), JsonOptions));
        AddFileIfExists(archive, _storageOptions.RuntimeStatePath, "runtime/runtime-state.json");
        AddFileIfExists(archive, Path.Combine(_storageOptions.SettingsRoot, "settings.json"), "settings/settings.json");
        AddRecentFiles(archive, _storageOptions.LogsRoot, "*.log", "logs", maxFiles: 10);
        AddRecentFiles(archive, _performanceRecorder.PerformanceLogsRoot, "*.jsonl", "performance", maxFiles: 10);
        AddRecentFiles(archive, _storageOptions.EventsRoot, "*.jsonl", "events", maxFiles: 10);
        AddLatestCaptureMetadata(archive);

        _logger.LogInformation("Created diagnostics bundle {DiagnosticsBundlePath}.", bundlePath);
        return bundlePath;
    }

    private void AddLatestCaptureMetadata(ZipArchive archive)
    {
        var snapshot = _captureState.Snapshot();
        var captureDirectory = snapshot.CurrentCaptureDirectory ?? snapshot.LastCaptureDirectory;
        if (string.IsNullOrWhiteSpace(captureDirectory) || !Directory.Exists(captureDirectory))
        {
            return;
        }

        AddFileIfExists(archive, Path.Combine(captureDirectory, "capture-manifest.json"), "latest-capture/capture-manifest.json");
        AddFileIfExists(archive, Path.Combine(captureDirectory, "telemetry-schema.json"), "latest-capture/telemetry-schema.json");
        AddFileIfExists(archive, Path.Combine(captureDirectory, "latest-session.yaml"), "latest-capture/latest-session.yaml");
    }

    private static void AddRecentFiles(
        ZipArchive archive,
        string directory,
        string searchPattern,
        string entryDirectory,
        int maxFiles)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(directory, searchPattern)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(maxFiles);

        foreach (var file in files)
        {
            AddFileIfExists(archive, file.FullName, $"{entryDirectory}/{file.Name}");
        }
    }

    private static void AddFileIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Fastest);
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
