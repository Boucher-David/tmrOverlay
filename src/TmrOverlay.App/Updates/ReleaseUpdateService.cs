using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.Core.AppInfo;
using Velopack;
using Velopack.Sources;

namespace TmrOverlay.App.Updates;

internal sealed class ReleaseUpdateService : IHostedService, IDisposable
{
    private readonly ReleaseUpdateOptions _options;
    private readonly AppEventRecorder _events;
    private readonly ILogger<ReleaseUpdateService> _logger;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly object _sync = new();
    private CancellationTokenSource? _startupCheckCancellation;
    private UpdateManager? _updateManager;
    private bool _updateManagerCreated;
    private ReleaseUpdateSnapshot _snapshot;

    public ReleaseUpdateService(
        ReleaseUpdateOptions options,
        AppEventRecorder events,
        ILogger<ReleaseUpdateService> logger)
    {
        _options = options;
        _events = events;
        _logger = logger;
        _snapshot = options.Enabled
            ? IdleSnapshot()
            : ReleaseUpdateSnapshot.Disabled(options.RepositoryUrl);
    }

    public event EventHandler? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.CheckOnStartup)
        {
            return Task.CompletedTask;
        }

        _startupCheckCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = CheckOnStartupAsync(_startupCheckCancellation.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _startupCheckCancellation?.Cancel();
        return Task.CompletedTask;
    }

    public ReleaseUpdateSnapshot Snapshot()
    {
        lock (_sync)
        {
            return _snapshot;
        }
    }

    public async Task<ReleaseUpdateSnapshot> CheckForUpdatesAsync(
        ReleaseUpdateCheckSource source,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return SetSnapshot(ReleaseUpdateSnapshot.Disabled(_options.RepositoryUrl));
        }

        await _checkLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manager = CreateUpdateManager();
            if (manager is null)
            {
                return Snapshot();
            }

            if (!manager.IsInstalled)
            {
                return SetSnapshot(NotInstalledSnapshot(manager));
            }

            var pendingRestart = manager.UpdatePendingRestart;
            if (pendingRestart is not null)
            {
                return SetSnapshot(PendingRestartSnapshot(manager, pendingRestart));
            }

            var checking = InstalledSnapshot(
                ReleaseUpdateStatus.Checking,
                manager,
                checkInProgress: true,
                latestVersion: Snapshot().LatestVersion,
                latestFileName: Snapshot().LatestFileName,
                deltaCount: Snapshot().DeltaCount,
                lastCheckedAtUtc: Snapshot().LastCheckedAtUtc,
                lastFailedAtUtc: Snapshot().LastFailedAtUtc,
                lastError: Snapshot().LastError,
                releasePageUrl: Snapshot().ReleasePageUrl);
            SetSnapshot(checking);

            _events.Record("update_check_started", new Dictionary<string, string?>
            {
                ["source"] = source.ToString().ToLowerInvariant(),
                ["repositoryUrl"] = _options.RepositoryUrl
            });

            try
            {
                var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
                var checkedAtUtc = DateTimeOffset.UtcNow;
                if (update is null)
                {
                    _events.Record("update_check_succeeded", new Dictionary<string, string?>
                    {
                        ["source"] = source.ToString().ToLowerInvariant(),
                        ["result"] = "up_to_date"
                    });
                    return SetSnapshot(InstalledSnapshot(
                        ReleaseUpdateStatus.UpToDate,
                        manager,
                        checkInProgress: false,
                        latestVersion: manager.CurrentVersion?.ToString(),
                        latestFileName: null,
                        deltaCount: 0,
                        lastCheckedAtUtc: checkedAtUtc,
                        lastFailedAtUtc: null,
                        lastError: null,
                        releasePageUrl: ReleasePageUrl(null)));
                }

                var target = update.TargetFullRelease;
                var latestVersion = target.Version?.ToString();
                _events.Record("update_check_succeeded", new Dictionary<string, string?>
                {
                    ["source"] = source.ToString().ToLowerInvariant(),
                    ["result"] = "available",
                    ["latestVersion"] = latestVersion
                });
                return SetSnapshot(InstalledSnapshot(
                    ReleaseUpdateStatus.Available,
                    manager,
                    checkInProgress: false,
                    latestVersion: latestVersion,
                    latestFileName: target.FileName,
                    deltaCount: update.DeltasToTarget?.Length ?? 0,
                    lastCheckedAtUtc: checkedAtUtc,
                    lastFailedAtUtc: null,
                    lastError: null,
                    releasePageUrl: ReleasePageUrl(latestVersion)));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Failed to check for TmrOverlay updates.");
                _events.Record("update_check_failed", new Dictionary<string, string?>
                {
                    ["source"] = source.ToString().ToLowerInvariant(),
                    ["error"] = exception.GetType().Name
                });
                return SetSnapshot(InstalledSnapshot(
                    ReleaseUpdateStatus.Failed,
                    manager,
                    checkInProgress: false,
                    latestVersion: null,
                    latestFileName: null,
                    deltaCount: 0,
                    lastCheckedAtUtc: Snapshot().LastCheckedAtUtc,
                    lastFailedAtUtc: DateTimeOffset.UtcNow,
                    lastError: exception.Message,
                    releasePageUrl: ReleasePageUrl(null)));
            }
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public void Dispose()
    {
        _startupCheckCancellation?.Cancel();
        _startupCheckCancellation?.Dispose();
        _checkLock.Dispose();
    }

    private async Task CheckOnStartupAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_options.StartupDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), cancellationToken)
                    .ConfigureAwait(false);
            }

            await CheckForUpdatesAsync(ReleaseUpdateCheckSource.Startup, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private UpdateManager? CreateUpdateManager()
    {
        lock (_sync)
        {
            if (_updateManagerCreated)
            {
                return _updateManager;
            }

            _updateManagerCreated = true;
            try
            {
                var source = new GithubSource(
                    _options.RepositoryUrl,
                    accessToken: string.Empty,
                    prerelease: _options.IncludePrerelease,
                    downloader: new HttpClientFileDownloader());
                _updateManager = new UpdateManager(source, new UpdateOptions(), locator: null!);
                _snapshot = _updateManager.IsInstalled
                    ? InstalledSnapshot(
                        ReleaseUpdateStatus.Idle,
                        _updateManager,
                        checkInProgress: false,
                        latestVersion: null,
                        latestFileName: null,
                        deltaCount: 0,
                        lastCheckedAtUtc: null,
                        lastFailedAtUtc: null,
                        lastError: null,
                        releasePageUrl: ReleasePageUrl(null))
                    : NotInstalledSnapshot(_updateManager);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to initialize Velopack update manager.");
                _snapshot = FailedSnapshot(exception);
            }

            return _updateManager;
        }
    }

    private ReleaseUpdateSnapshot SetSnapshot(ReleaseUpdateSnapshot snapshot)
    {
        lock (_sync)
        {
            _snapshot = snapshot;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    private ReleaseUpdateSnapshot IdleSnapshot() => new(
        ReleaseUpdateStatus.Idle,
        Enabled: true,
        IsInstalled: false,
        IsPortable: false,
        CheckInProgress: false,
        SourceName: "GitHub Releases",
        RepositoryUrl: _options.RepositoryUrl,
        CurrentVersion: AppVersionInfo.Current.Version,
        LatestVersion: null,
        LatestFileName: null,
        DeltaCount: 0,
        LastCheckedAtUtc: null,
        LastFailedAtUtc: null,
        LastError: null,
        ReleasePageUrl: ReleasePageUrl(null));

    private ReleaseUpdateSnapshot NotInstalledSnapshot(UpdateManager manager) => new(
        ReleaseUpdateStatus.NotInstalled,
        Enabled: _options.Enabled,
        IsInstalled: false,
        IsPortable: manager.IsPortable,
        CheckInProgress: false,
        SourceName: "GitHub Releases",
        RepositoryUrl: _options.RepositoryUrl,
        CurrentVersion: AppVersionInfo.Current.Version,
        LatestVersion: null,
        LatestFileName: null,
        DeltaCount: 0,
        LastCheckedAtUtc: null,
        LastFailedAtUtc: null,
        LastError: null,
        ReleasePageUrl: ReleasePageUrl(null));

    private ReleaseUpdateSnapshot PendingRestartSnapshot(UpdateManager manager, VelopackAsset pendingRestart) => new(
        ReleaseUpdateStatus.PendingRestart,
        Enabled: _options.Enabled,
        IsInstalled: true,
        IsPortable: manager.IsPortable,
        CheckInProgress: false,
        SourceName: "GitHub Releases",
        RepositoryUrl: _options.RepositoryUrl,
        CurrentVersion: manager.CurrentVersion?.ToString() ?? AppVersionInfo.Current.Version,
        LatestVersion: pendingRestart.Version?.ToString(),
        LatestFileName: pendingRestart.FileName,
        DeltaCount: 0,
        LastCheckedAtUtc: Snapshot().LastCheckedAtUtc,
        LastFailedAtUtc: null,
        LastError: null,
        ReleasePageUrl: ReleasePageUrl(pendingRestart.Version?.ToString()));

    private ReleaseUpdateSnapshot InstalledSnapshot(
        ReleaseUpdateStatus status,
        UpdateManager manager,
        bool checkInProgress,
        string? latestVersion,
        string? latestFileName,
        int deltaCount,
        DateTimeOffset? lastCheckedAtUtc,
        DateTimeOffset? lastFailedAtUtc,
        string? lastError,
        string? releasePageUrl) => new(
        status,
        Enabled: _options.Enabled,
        IsInstalled: true,
        IsPortable: manager.IsPortable,
        CheckInProgress: checkInProgress,
        SourceName: "GitHub Releases",
        RepositoryUrl: _options.RepositoryUrl,
        CurrentVersion: manager.CurrentVersion?.ToString() ?? AppVersionInfo.Current.Version,
        LatestVersion: latestVersion,
        LatestFileName: latestFileName,
        DeltaCount: deltaCount,
        LastCheckedAtUtc: lastCheckedAtUtc,
        LastFailedAtUtc: lastFailedAtUtc,
        LastError: lastError,
        ReleasePageUrl: releasePageUrl);

    private ReleaseUpdateSnapshot FailedSnapshot(Exception exception) => new(
        ReleaseUpdateStatus.Failed,
        Enabled: _options.Enabled,
        IsInstalled: false,
        IsPortable: false,
        CheckInProgress: false,
        SourceName: "GitHub Releases",
        RepositoryUrl: _options.RepositoryUrl,
        CurrentVersion: AppVersionInfo.Current.Version,
        LatestVersion: null,
        LatestFileName: null,
        DeltaCount: 0,
        LastCheckedAtUtc: null,
        LastFailedAtUtc: DateTimeOffset.UtcNow,
        LastError: exception.Message,
        ReleasePageUrl: ReleasePageUrl(null));

    private string ReleasePageUrl(string? version)
    {
        var baseUrl = _options.RepositoryUrl.TrimEnd('/');
        return string.IsNullOrWhiteSpace(version)
            ? $"{baseUrl}/releases"
            : $"{baseUrl}/releases/tag/v{version}";
    }
}
