namespace TmrOverlay.App.Updates;

internal enum ReleaseUpdateStatus
{
    Disabled,
    NotInstalled,
    Idle,
    Checking,
    UpToDate,
    Available,
    PendingRestart,
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
    DateTimeOffset? LastFailedAtUtc,
    string? LastError,
    string? ReleasePageUrl)
{
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
        LastFailedAtUtc: null,
        LastError: null,
        ReleasePageUrl: null);
}
