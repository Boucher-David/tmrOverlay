namespace TmrOverlay.App.Updates;

internal enum ReleaseUpdateStatus
{
    Disabled,
    NotInstalled,
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    PendingRestart,
    Applying,
    Failed
}

internal enum ReleaseUpdateCheckSource
{
    Startup,
    Manual
}

internal sealed record ReleaseUpdateSnapshot(
    ReleaseUpdateStatus Status,
    bool Enabled,
    bool IsInstalled,
    bool IsPortable,
    bool CheckInProgress,
    string SourceName,
    string RepositoryUrl,
    string? CurrentVersion,
    string? LatestVersion,
    string? LatestFileName,
    int DeltaCount,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastDownloadStartedAtUtc,
    DateTimeOffset? LastDownloadedAtUtc,
    int? DownloadProgressPercent,
    DateTimeOffset? LastApplyStartedAtUtc,
    DateTimeOffset? LastFailedAtUtc,
    string? LastError,
    string? ReleasePageUrl)
{
    public string StatusText => Status.ToString();

    public bool OperationInProgress => CheckInProgress
        || Status is ReleaseUpdateStatus.Downloading or ReleaseUpdateStatus.Applying;

    public bool CanCheck => Enabled && IsInstalled && !OperationInProgress;

    public bool CanDownload => Enabled && IsInstalled && Status == ReleaseUpdateStatus.Available && !OperationInProgress;

    public bool CanRestartToApply => Enabled && IsInstalled && Status == ReleaseUpdateStatus.PendingRestart && !OperationInProgress;

    public static ReleaseUpdateSnapshot Disabled(string repositoryUrl) => new(
        ReleaseUpdateStatus.Disabled,
        Enabled: false,
        IsInstalled: false,
        IsPortable: false,
        CheckInProgress: false,
        SourceName: "disabled",
        RepositoryUrl: repositoryUrl,
        CurrentVersion: null,
        LatestVersion: null,
        LatestFileName: null,
        DeltaCount: 0,
        LastCheckedAtUtc: null,
        LastDownloadStartedAtUtc: null,
        LastDownloadedAtUtc: null,
        DownloadProgressPercent: null,
        LastApplyStartedAtUtc: null,
        LastFailedAtUtc: null,
        LastError: null,
        ReleasePageUrl: null);
}
