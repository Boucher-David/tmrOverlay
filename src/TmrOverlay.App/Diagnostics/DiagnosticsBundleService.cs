using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.App.Updates;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Diagnostics;

internal sealed class DiagnosticsBundleService
{
    private const int MaxRecentAnalysisFiles = 12;
    private const int MaxRecentHistorySummaryFiles = 50;
    private const int MaxRecentHistoryAggregateFiles = 50;
    private const int MaxRecentEdgeCaseFiles = 20;
    private const int MaxLatestCaptureIbtAnalysisFiles = 12;
    private const int MaxRecentModelParityFiles = 10;
    private const int MaxRecentOverlayDiagnosticsFiles = 10;
    private const int MaxRecentTrackMapReports = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly LiveModelParityOptions _liveModelParityOptions;
    private readonly LiveOverlayDiagnosticsOptions _liveOverlayDiagnosticsOptions;
    private readonly TelemetryCaptureState _captureState;
    private readonly LocalhostOverlayState _localhostOverlayState;
    private readonly TrackMapStore _trackMapStore;
    private readonly AppSettingsStore _settingsStore;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly AppPerformanceState _performanceState;
    private readonly AppPerformanceSnapshotRecorder _performanceRecorder;
    private readonly ReleaseUpdateService _releaseUpdates;
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
        LiveModelParityOptions liveModelParityOptions,
        LiveOverlayDiagnosticsOptions liveOverlayDiagnosticsOptions,
        TelemetryCaptureState captureState,
        LocalhostOverlayState localhostOverlayState,
        TrackMapStore trackMapStore,
        AppSettingsStore settingsStore,
        ILiveTelemetrySource liveTelemetrySource,
        AppPerformanceState performanceState,
        AppPerformanceSnapshotRecorder performanceRecorder,
        ReleaseUpdateService releaseUpdates,
        ILogger<DiagnosticsBundleService> logger)
    {
        _storageOptions = storageOptions;
        _liveModelParityOptions = liveModelParityOptions;
        _liveOverlayDiagnosticsOptions = liveOverlayDiagnosticsOptions;
        _captureState = captureState;
        _localhostOverlayState = localhostOverlayState;
        _trackMapStore = trackMapStore;
        _settingsStore = settingsStore;
        _liveTelemetrySource = liveTelemetrySource;
        _performanceState = performanceState;
        _performanceRecorder = performanceRecorder;
        _releaseUpdates = releaseUpdates;
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
                AddTextEntry(archive, "metadata/localhost-overlays.json", JsonSerializer.Serialize(_localhostOverlayState.Snapshot(), JsonOptions));
                AddTextEntry(archive, "metadata/release-updates.json", JsonSerializer.Serialize(_releaseUpdates.Snapshot(), JsonOptions));
                AddTextEntry(archive, "metadata/track-maps.json", JsonSerializer.Serialize(_trackMapStore.DiagnosticsSnapshot(), JsonOptions));
                AddTextEntry(archive, "metadata/garage-cover.json", JsonSerializer.Serialize(GarageCoverDiagnostics(), JsonOptions));
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
                AddSanitizedSettingsIfExists(archive, Path.Combine(_storageOptions.SettingsRoot, "settings.json"));
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

            var modelParityStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var modelParitySucceeded = false;
            try
            {
                AddRecentFiles(
                    archive,
                    Path.Combine(_storageOptions.LogsRoot, _liveModelParityOptions.LogDirectoryName),
                    $"*{_liveModelParityOptions.OutputFileName}",
                    "model-parity",
                    MaxRecentModelParityFiles);
                modelParitySucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    "diagnostics.bundle.model-parity",
                    modelParityStarted,
                    modelParitySucceeded);
            }

            var overlayDiagnosticsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var overlayDiagnosticsSucceeded = false;
            try
            {
                AddRecentFiles(
                    archive,
                    Path.Combine(_storageOptions.LogsRoot, _liveOverlayDiagnosticsOptions.LogDirectoryName),
                    $"*{_liveOverlayDiagnosticsOptions.OutputFileName}",
                    "overlay-diagnostics",
                    MaxRecentOverlayDiagnosticsFiles);
                overlayDiagnosticsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.DiagnosticsBundleOverlayDiagnostics,
                    overlayDiagnosticsStarted,
                    overlayDiagnosticsSucceeded);
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

            var trackMapsStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var trackMapsSucceeded = false;
            try
            {
                AddRecentFiles(
                    archive,
                    Path.Combine(_storageOptions.LogsRoot, "track-maps"),
                    "*.json",
                    "track-maps",
                    MaxRecentTrackMapReports);
                trackMapsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    "diagnostics.bundle.track-maps",
                    trackMapsStarted,
                    trackMapsSucceeded);
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
        AddFileIfExists(archive, Path.Combine(captureDirectory, "capture-synthesis.json"), "latest-capture/capture-synthesis.json");
        AddFileIfExists(
            archive,
            Path.Combine(captureDirectory, _liveModelParityOptions.OutputFileName),
            $"latest-capture/{_liveModelParityOptions.OutputFileName}");
        AddFileIfExists(
            archive,
            Path.Combine(captureDirectory, _liveOverlayDiagnosticsOptions.OutputFileName),
            $"latest-capture/{_liveOverlayDiagnosticsOptions.OutputFileName}");
        AddRecentFiles(
            archive,
            Path.Combine(captureDirectory, "ibt-analysis"),
            "*.json",
            "latest-capture/ibt-analysis",
            MaxLatestCaptureIbtAnalysisFiles);
    }

    private object GarageCoverDiagnostics()
    {
        try
        {
            return GarageCoverBrowserSettings.Diagnostics(
                _settingsStore.Load(),
                _localhostOverlayState.Snapshot(),
                _liveTelemetrySource.Snapshot());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to collect Garage Cover diagnostics metadata.");
            return new
            {
                Route = "/overlays/garage-cover",
                Error = exception.Message
            };
        }
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

    private static void AddSanitizedSettingsIfExists(ZipArchive archive, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path));
            if (node is not null)
            {
                RedactStreamChatSecrets(node);
                AddTextEntry(archive, "settings/settings.json", node.ToJsonString(JsonOptions));
                return;
            }
        }
        catch
        {
            AddTextEntry(
                archive,
                "settings/settings-redacted.txt",
                "Settings could not be parsed; omitted to avoid copying private stream chat widget URLs.");
            return;
        }

        AddTextEntry(
            archive,
            "settings/settings-redacted.txt",
            "Settings were empty or invalid; omitted to avoid copying private stream chat widget URLs.");
    }

    private static void RedactStreamChatSecrets(JsonNode node)
    {
        if (node["overlays"] is not JsonArray overlays)
        {
            return;
        }

        foreach (var overlay in overlays.OfType<JsonObject>())
        {
            if (!string.Equals((string?)overlay["id"], "stream-chat", StringComparison.OrdinalIgnoreCase)
                || overlay["options"] is not JsonObject options
                || !options.ContainsKey(OverlayOptionKeys.StreamChatStreamlabsUrl))
            {
                continue;
            }

            options[OverlayOptionKeys.StreamChatStreamlabsUrl] = "<redacted>";
        }
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
