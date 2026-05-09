using TmrOverlay.App.Installation;
using TmrOverlay.App.Storage;
using Xunit;

namespace TmrOverlay.App.Tests.Installation;

public sealed class InstallerCleanupTests
{
    [Fact]
    public void RemoveUserDataForUninstall_DeletesDefaultAppDataRoot()
    {
        using var temp = TemporaryDirectory.Create();
        var localAppDataRoot = Path.Combine(temp.Path, "local-app-data");
        var appDataRoot = Path.Combine(localAppDataRoot, "TmrOverlay");
        Directory.CreateDirectory(Path.Combine(appDataRoot, "track-maps", "user"));
        File.WriteAllText(Path.Combine(appDataRoot, "track-maps", "user", "map.json"), "{}");
        File.WriteAllText(Path.Combine(appDataRoot, "runtime-state.json"), "{}");

        var result = InstallerCleanup.RemoveUserDataForUninstall(Storage(appDataRoot), localAppDataRoot);

        Assert.False(Directory.Exists(appDataRoot));
        Assert.Contains(Path.GetFullPath(appDataRoot), result.DeletedPaths);
        Assert.Empty(result.SkippedPaths);
    }

    [Fact]
    public void RemoveUserDataForUninstall_DeletesConfiguredLocalAppDataChildAndDefaultRoot()
    {
        using var temp = TemporaryDirectory.Create();
        var localAppDataRoot = Path.Combine(temp.Path, "local-app-data");
        var configuredRoot = Path.Combine(localAppDataRoot, "TmrOverlay-custom");
        var defaultRoot = Path.Combine(localAppDataRoot, "TmrOverlay");
        Directory.CreateDirectory(configuredRoot);
        Directory.CreateDirectory(defaultRoot);
        File.WriteAllText(Path.Combine(configuredRoot, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(defaultRoot, "runtime-state.json"), "{}");

        var result = InstallerCleanup.RemoveUserDataForUninstall(Storage(configuredRoot), localAppDataRoot);

        Assert.False(Directory.Exists(configuredRoot));
        Assert.False(Directory.Exists(defaultRoot));
        Assert.Contains(Path.GetFullPath(configuredRoot), result.DeletedPaths);
        Assert.Contains(Path.GetFullPath(defaultRoot), result.DeletedPaths);
        Assert.Empty(result.SkippedPaths);
    }

    [Fact]
    public void RemoveUserDataForUninstall_SkipsConfiguredRootOutsideLocalAppData()
    {
        using var temp = TemporaryDirectory.Create();
        var localAppDataRoot = Path.Combine(temp.Path, "local-app-data");
        var outsideRoot = Path.Combine(temp.Path, "external-storage");
        var defaultRoot = Path.Combine(localAppDataRoot, "TmrOverlay");
        Directory.CreateDirectory(outsideRoot);
        Directory.CreateDirectory(defaultRoot);

        var result = InstallerCleanup.RemoveUserDataForUninstall(Storage(outsideRoot), localAppDataRoot);

        Assert.True(Directory.Exists(outsideRoot));
        Assert.False(Directory.Exists(defaultRoot));
        Assert.Contains(result.SkippedPaths, skipped =>
            string.Equals(Path.GetFullPath(outsideRoot), skipped.Path, StringComparison.OrdinalIgnoreCase) &&
            skipped.Reason == "outside_local_app_data_or_too_broad");
    }

    [Fact]
    public void RemoveUserDataForUninstall_SkipsLocalAppDataRootItself()
    {
        using var temp = TemporaryDirectory.Create();
        var localAppDataRoot = Path.Combine(temp.Path, "local-app-data");
        Directory.CreateDirectory(localAppDataRoot);

        var result = InstallerCleanup.RemoveUserDataForUninstall(Storage(localAppDataRoot), localAppDataRoot);

        Assert.True(Directory.Exists(localAppDataRoot));
        Assert.Contains(result.SkippedPaths, skipped =>
            string.Equals(Path.GetFullPath(localAppDataRoot), skipped.Path, StringComparison.OrdinalIgnoreCase) &&
            skipped.Reason == "outside_local_app_data_or_too_broad");
    }

    [Fact]
    public void RemoveLegacyInstallerArtifacts_DeletesLegacyPackageDirectoriesWithoutDeletingUserData()
    {
        using var temp = TemporaryDirectory.Create();
        var localAppDataRoot = Path.Combine(temp.Path, "local-app-data");
        var userDataRoot = Path.Combine(localAppDataRoot, "TmrOverlay");
        var legacyPackageRoot = Path.Combine(localAppDataRoot, "TechMatesRacing.TmrOverlay");
        var legacyProgramsPackageRoot = Path.Combine(localAppDataRoot, "Programs", "TechMatesRacing.TmrOverlay");
        Directory.CreateDirectory(userDataRoot);
        Directory.CreateDirectory(legacyPackageRoot);
        Directory.CreateDirectory(legacyProgramsPackageRoot);
        File.WriteAllText(Path.Combine(userDataRoot, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(legacyPackageRoot, "old.exe"), "");
        File.WriteAllText(Path.Combine(legacyProgramsPackageRoot, "old.exe"), "");

        var result = InstallerCleanup.RemoveLegacyInstallerArtifacts(
            localAppDataRoot,
            Path.Combine(temp.Path, "desktop"),
            Path.Combine(temp.Path, "programs"));

        Assert.True(Directory.Exists(userDataRoot));
        Assert.False(Directory.Exists(legacyPackageRoot));
        Assert.False(Directory.Exists(legacyProgramsPackageRoot));
        Assert.Contains(Path.GetFullPath(legacyPackageRoot), result.DeletedPaths);
        Assert.Contains(Path.GetFullPath(legacyProgramsPackageRoot), result.DeletedPaths);
        Assert.Empty(result.SkippedPaths);
    }

    [Fact]
    public void RemoveLegacyInstallerArtifacts_DeletesOnlyShortcutsPointingAtLegacyIdentity()
    {
        using var temp = TemporaryDirectory.Create();
        var localAppDataRoot = Path.Combine(temp.Path, "local-app-data");
        var desktopRoot = Path.Combine(temp.Path, "desktop");
        var programsRoot = Path.Combine(temp.Path, "programs");
        var legacyShortcut = Path.Combine(programsRoot, "Tech Mates Racing", "TmrOverlay.lnk");
        var currentShortcut = Path.Combine(programsRoot, "Tech Mates Racing", "TMROverlay.lnk");
        var desktopShortcut = Path.Combine(desktopRoot, "TmrOverlay.lnk");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyShortcut)!);
        Directory.CreateDirectory(desktopRoot);
        File.WriteAllText(legacyShortcut, @"C:\Users\David\AppData\Local\TechMatesRacing.TmrOverlay\current\TMROverlay.exe");
        File.WriteAllText(currentShortcut, @"C:\Program Files\TMROverlay\current\TMROverlay.exe");
        File.WriteAllText(desktopShortcut, @"C:\Users\David\AppData\Local\TechMatesRacing.TmrOverlay\current\TMROverlay.exe");

        var result = InstallerCleanup.RemoveLegacyInstallerArtifacts(localAppDataRoot, desktopRoot, programsRoot);

        Assert.False(File.Exists(legacyShortcut));
        Assert.False(File.Exists(desktopShortcut));
        Assert.True(File.Exists(currentShortcut));
        Assert.Contains(Path.GetFullPath(legacyShortcut), result.DeletedPaths);
        Assert.Contains(Path.GetFullPath(desktopShortcut), result.DeletedPaths);
        Assert.Empty(result.SkippedPaths);
    }

    private static AppStorageOptions Storage(string appDataRoot)
    {
        return new AppStorageOptions
        {
            AppDataRoot = appDataRoot,
            CaptureRoot = Path.Combine(appDataRoot, "captures"),
            UserHistoryRoot = Path.Combine(appDataRoot, "history", "user"),
            BaselineHistoryRoot = Path.Combine(appDataRoot, "history", "baseline"),
            LogsRoot = Path.Combine(appDataRoot, "logs"),
            SettingsRoot = Path.Combine(appDataRoot, "settings"),
            DiagnosticsRoot = Path.Combine(appDataRoot, "diagnostics"),
            TrackMapRoot = Path.Combine(appDataRoot, "track-maps", "user"),
            EventsRoot = Path.Combine(appDataRoot, "logs", "events"),
            RuntimeStatePath = Path.Combine(appDataRoot, "runtime-state.json")
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "tmr-installer-cleanup-tests",
                Guid.NewGuid().ToString("N")));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
