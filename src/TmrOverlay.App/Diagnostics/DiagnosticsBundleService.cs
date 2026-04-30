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
    private const int MaxRecentAnalysisFiles = 12;
    private const int MaxRecentHistorySummaryFiles = 50;
    private const int MaxRecentHistoryAggregateFiles = 50;
    private const int MaxRecentEdgeCaseFiles = 20;

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
    private readonly object _sync = new();
    private string? _lastBundlePath;
    private DateTimeOffset? _lastBundleCreatedAtUtc;
    private string? _lastBundleSource;
    private string? _lastError;
    private DateTimeOffset? _lastErrorAtUtc;
    private string? _lastErrorSource;

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

    public DiagnosticsBundleStatus Snapshot()
    {
        lock (_sync)
        {
            return new DiagnosticsBundleStatus(
                _lastBundlePath,
                _lastBundleCreatedAtUtc,
                _lastBundleSource,
                _lastError,
                _lastErrorAtUtc,
                _lastErrorSource);
        }
    }

    public string CreateBundle(string source = DiagnosticsBundleSources.Manual)
    {
        var bundleStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var bundleSucceeded = false;
        try
        {
            Directory.CreateDirectory(_storageOptions.DiagnosticsRoot);
            var createdAtUtc = DateTimeOffset.UtcNow;
            var bundlePath = Path.Combine(
                _storageOptions.DiagnosticsRoot,
                $"tmroverlay-diagnostics-{createdAtUtc:yyyyMMdd-HHmmss-fff}.zip");

            using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);

            var metadataStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var metadataSucceeded = false;
            try
            {
                AddTextEntry(archive, "metadata/app-version.json", JsonSerializer.Serialize(AppVersionInfo.Current, JsonOptions));
                AddTextEntry(archive, "metadata/diagnostics-bundle.json", JsonSerializer.Serialize(new
                {
                    CreatedAtUtc = createdAtUtc,
                    Source = source
                }, JsonOptions));
                AddTextEntry(archive, "metadata/storage.json", JsonSerializer.Serialize(_storageOptions, JsonOptions));
                AddTextEntry(archive, "metadata/telemetry-state.json", JsonSerializer.Serialize(_captureState.Snapshot(), JsonOptions));
                metadataSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleMetadata,
                    metadataStarted,
                    metadataSucceeded);
            }

            var runtimeSettingsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var runtimeSettingsSucceeded = false;
            try
            {
                AddFileIfExists(archive, _storageOptions.RuntimeStatePath, "runtime/runtime-state.json");
                AddFileIfExists(archive, Path.Combine(_storageOptions.SettingsRoot, "settings.json"), "settings/settings.json");
                runtimeSettingsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleRuntimeSettings,
                    runtimeSettingsStarted,
                    runtimeSettingsSucceeded);
            }

            var logsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var logsSucceeded = false;
            try
            {
                AddRecentFiles(archive, _storageOptions.LogsRoot, "*.log", "logs", maxFiles: 10);
                logsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleLogs,
                    logsStarted,
                    logsSucceeded);
            }

            var performanceStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var performanceSucceeded = false;
            try
            {
                AddRecentFiles(archive, _performanceRecorder.PerformanceLogsRoot, "*.jsonl", "performance", maxFiles: 10);
                performanceSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundlePerformanceFiles,
                    performanceStarted,
                    performanceSucceeded);
            }

            var eventsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var eventsSucceeded = false;
            try
            {
                AddRecentFiles(archive, _storageOptions.EventsRoot, "*.jsonl", "events", maxFiles: 10);
                eventsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleEvents,
                    eventsStarted,
                    eventsSucceeded);
            }

            var latestCaptureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var latestCaptureSucceeded = false;
            try
            {
                AddLatestCaptureMetadata(archive);
                latestCaptureSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleLatestCapture,
                    latestCaptureStarted,
                    latestCaptureSucceeded);
            }

            var edgeCasesStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var edgeCasesSucceeded = false;
            try
            {
                AddRecentFiles(
                    archive,
                    Path.Combine(_storageOptions.LogsRoot, "edge-cases"),
                    "*-edge-cases.json",
                    "edge-cases",
                    MaxRecentEdgeCaseFiles);
                edgeCasesSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleEdgeCases,
                    edgeCasesStarted,
                    edgeCasesSucceeded);
            }

            var historyStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var historySucceeded = false;
            try
            {
                AddUserHistoryMetadata(archive);
                historySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleHistory,
                    historyStarted,
                    historySucceeded);
            }

            AddTextEntry(archive, "metadata/performance.json", JsonSerializer.Serialize(_performanceState.Snapshot(), JsonOptions));

            _logger.LogInformation("Created diagnostics bundle {DiagnosticsBundlePath}.", bundlePath);
            RecordSuccess(bundlePath, createdAtUtc, source);
            bundleSucceeded = true;
            return bundlePath;
        }
        catch (Exception exception)
        {
            RecordFailure(exception, source);
            throw;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.DiagnosticsBundleCreate,
                bundleStarted,
                bundleSucceeded);
        }
    }

    private void RecordSuccess(string bundlePath, DateTimeOffset createdAtUtc, string source)
    {
        lock (_sync)
        {
            _lastBundlePath = bundlePath;
            _lastBundleCreatedAtUtc = createdAtUtc;
            _lastBundleSource = source;
            _lastError = null;
            _lastErrorAtUtc = null;
            _lastErrorSource = null;
        }
    }

    private void RecordFailure(Exception exception, string source)
    {
        lock (_sync)
        {
            _lastError = exception.Message;
            _lastErrorAtUtc = DateTimeOffset.UtcNow;
            _lastErrorSource = source;
        }
    }

    private void AddUserHistoryMetadata(ZipArchive archive)
    {
        if (!Directory.Exists(_storageOptions.UserHistoryRoot))
        {
            return;
        }

        AddRecentFiles(
            archive,
            Path.Combine(_storageOptions.UserHistoryRoot, "analysis"),
            "*.json",
            "analysis",
            MaxRecentAnalysisFiles);
        AddFileIfExists(
            archive,
            Path.Combine(_storageOptions.UserHistoryRoot, ".maintenance", "manifest.json"),
            "history/user/.maintenance/manifest.json");

        var carsRoot = Path.Combine(_storageOptions.UserHistoryRoot, "cars");
        AddRecentRecursiveFiles(
            archive,
            carsRoot,
            file => string.Equals(file.Name, "aggregate.json", StringComparison.OrdinalIgnoreCase),
            "history/user/cars",
            MaxRecentHistoryAggregateFiles);
        AddRecentRecursiveFiles(
            archive,
            carsRoot,
            file => string.Equals(file.Directory?.Name, "summaries", StringComparison.OrdinalIgnoreCase),
            "history/user/cars",
            MaxRecentHistorySummaryFiles);
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

    private static void AddRecentRecursiveFiles(
        ZipArchive archive,
        string rootDirectory,
        Func<FileInfo, bool> includeFile,
        string entryDirectory,
        int maxFiles)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        var root = Path.GetFullPath(rootDirectory);
        var files = Directory
            .EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(includeFile)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(maxFiles);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(root, file.FullName);
            AddFileIfExists(
                archive,
                file.FullName,
                $"{entryDirectory}/{ToZipEntryPath(relativePath)}");
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

    private static string ToZipEntryPath(string relativePath)
    {
        return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}

internal static class DiagnosticsBundleSources
{
    public const string Manual = "manual";
    public const string SessionFinalization = "session_finalization";
}

internal sealed record DiagnosticsBundleStatus(
    string? LastBundlePath,
    DateTimeOffset? LastBundleCreatedAtUtc,
    string? LastBundleSource,
    string? LastError,
    DateTimeOffset? LastErrorAtUtc,
    string? LastErrorSource);
