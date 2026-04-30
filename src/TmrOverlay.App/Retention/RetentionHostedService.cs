using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Retention;

internal sealed class RetentionHostedService : IHostedService
{
    private readonly AppStorageOptions _storageOptions;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionHostedService> _logger;

    public RetentionHostedService(
        AppStorageOptions storageOptions,
        RetentionOptions options,
        ILogger<RetentionHostedService> logger)
    {
        _storageOptions = storageOptions;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        CleanupDirectories(_storageOptions.CaptureRoot, "capture-*", _options.CaptureRetentionDays, _options.MaxCaptureDirectories);
        CleanupFiles(_storageOptions.DiagnosticsRoot, "*.zip", _options.DiagnosticsRetentionDays, _options.MaxDiagnosticsBundles);
        CleanupFiles(
            Path.Combine(_storageOptions.LogsRoot, "performance"),
            "performance-*.jsonl",
            _options.PerformanceLogRetentionDays,
            _options.MaxPerformanceLogFiles);
        CleanupFiles(
            Path.Combine(_storageOptions.LogsRoot, "edge-cases"),
            "*-edge-cases.json",
            _options.EdgeCaseRetentionDays,
            _options.MaxEdgeCaseFiles);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void CleanupDirectories(string root, string searchPattern, int retentionDays, int maxCount)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
        var directories = Directory
            .EnumerateDirectories(root, searchPattern)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .ToArray();

        foreach (var directory in directories.Skip(maxCount).Concat(directories.Where(directory => directory.LastWriteTimeUtc < cutoffUtc)).DistinctBy(directory => directory.FullName))
        {
            TryDeleteDirectory(directory.FullName);
        }
    }

    private void CleanupFiles(string root, string searchPattern, int retentionDays, int maxCount)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
        var files = Directory
            .EnumerateFiles(root, searchPattern)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();

        foreach (var file in files.Skip(maxCount).Concat(files.Where(file => file.LastWriteTimeUtc < cutoffUtc)).DistinctBy(file => file.FullName))
        {
            TryDeleteFile(file.FullName);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
            _logger.LogInformation("Deleted old retained directory {DirectoryPath}.", path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete retained directory {DirectoryPath}.", path);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            _logger.LogInformation("Deleted old retained file {FilePath}.", path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete retained file {FilePath}.", path);
        }
    }
}
