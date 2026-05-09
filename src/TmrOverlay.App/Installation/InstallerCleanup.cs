using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Storage;

namespace TmrOverlay.App.Installation;

internal static class InstallerCleanup
{
    private const string AppDataDirectoryName = "TmrOverlay";
    private const string LegacyPackageDirectoryName = "TechMatesRacing.TmrOverlay";
    private static readonly object LegacyCleanupSync = new();
    private static InstallerCleanupSnapshot? _lastLegacyCleanupSnapshot;

    public static InstallerCleanupResult RemoveUserDataForUninstall()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "TMR_")
                .Build();

            return RemoveUserDataForUninstall(
                AppStorageOptions.FromConfiguration(configuration),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }
        catch (Exception exception) when (IsCleanupException(exception))
        {
            return new InstallerCleanupResult(
                [],
                [new InstallerCleanupSkippedPath("storage_configuration", exception.GetType().Name)]);
        }
    }

    public static InstallerCleanupResult RemoveLegacyInstallerArtifacts()
    {
        var result = RemoveLegacyInstallerArtifacts(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs));
        RecordLegacyInstallerCleanup(result, DateTimeOffset.UtcNow);
        return result;
    }

    public static InstallerCleanupSnapshot LegacyInstallerCleanupSnapshot()
    {
        lock (LegacyCleanupSync)
        {
            return _lastLegacyCleanupSnapshot ?? new InstallerCleanupSnapshot(
                HasRun: false,
                LastRunAtUtc: null,
                LegacyPackageDirectoryName: LegacyPackageDirectoryName,
                DeletedPaths: [],
                SkippedPaths: []);
        }
    }

    internal static InstallerCleanupResult RemoveUserDataForUninstall(
        AppStorageOptions storageOptions,
        string localApplicationDataRoot)
    {
        var deleted = new List<string>();
        var skipped = new List<InstallerCleanupSkippedPath>();

        foreach (var path in CleanupCandidates(storageOptions, localApplicationDataRoot))
        {
            try
            {
                if (!IsSafeAppDataCleanupRoot(path, localApplicationDataRoot))
                {
                    skipped.Add(new InstallerCleanupSkippedPath(path, "outside_local_app_data_or_too_broad"));
                    continue;
                }

                if (IsCurrentAppBaseDirectoryInside(path))
                {
                    skipped.Add(new InstallerCleanupSkippedPath(path, "current_app_base_directory"));
                    continue;
                }

                if (!Directory.Exists(path))
                {
                    continue;
                }

                Directory.Delete(path, recursive: true);
                deleted.Add(Path.GetFullPath(path));
            }
            catch (Exception exception) when (IsCleanupException(exception))
            {
                skipped.Add(new InstallerCleanupSkippedPath(path, exception.GetType().Name));
            }
        }

        return new InstallerCleanupResult(deleted, skipped);
    }

    internal static InstallerCleanupResult RemoveLegacyInstallerArtifacts(
        string localApplicationDataRoot,
        string desktopDirectory,
        string programsDirectory)
    {
        var deleted = new List<string>();
        var skipped = new List<InstallerCleanupSkippedPath>();

        DeleteLegacyPackageDirectories(localApplicationDataRoot, deleted, skipped);
        DeleteLegacyShortcuts([desktopDirectory, programsDirectory], deleted, skipped);

        return new InstallerCleanupResult(deleted, skipped);
    }

    internal static void RecordLegacyInstallerCleanupForTests(
        InstallerCleanupResult result,
        DateTimeOffset lastRunAtUtc)
    {
        RecordLegacyInstallerCleanup(result, lastRunAtUtc);
    }

    internal static void ResetLegacyInstallerCleanupForTests()
    {
        lock (LegacyCleanupSync)
        {
            _lastLegacyCleanupSnapshot = null;
        }
    }

    private static void RecordLegacyInstallerCleanup(
        InstallerCleanupResult result,
        DateTimeOffset lastRunAtUtc)
    {
        lock (LegacyCleanupSync)
        {
            _lastLegacyCleanupSnapshot = new InstallerCleanupSnapshot(
                HasRun: true,
                LastRunAtUtc: lastRunAtUtc,
                LegacyPackageDirectoryName: LegacyPackageDirectoryName,
                DeletedPaths: result.DeletedPaths.ToArray(),
                SkippedPaths: result.SkippedPaths.ToArray());
        }
    }

    private static IEnumerable<string> CleanupCandidates(AppStorageOptions storageOptions, string localApplicationDataRoot)
    {
        yield return storageOptions.AppDataRoot;

        if (!string.IsNullOrWhiteSpace(localApplicationDataRoot))
        {
            yield return Path.Combine(localApplicationDataRoot, AppDataDirectoryName);
        }
    }

    private static bool IsSafeAppDataCleanupRoot(string path, string localApplicationDataRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(localApplicationDataRoot))
        {
            return false;
        }

        var fullPath = NormalizeDirectoryPath(path);
        var fullLocalAppDataRoot = NormalizeDirectoryPath(localApplicationDataRoot);
        if (string.Equals(fullPath, fullLocalAppDataRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fullPath.StartsWith(
            fullLocalAppDataRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteLegacyPackageDirectories(
        string localApplicationDataRoot,
        ICollection<string> deleted,
        ICollection<InstallerCleanupSkippedPath> skipped)
    {
        if (string.IsNullOrWhiteSpace(localApplicationDataRoot))
        {
            return;
        }

        foreach (var path in LegacyPackageDirectoryCandidates(localApplicationDataRoot))
        {
            try
            {
                if (!IsSafeAppDataCleanupRoot(path, localApplicationDataRoot))
                {
                    skipped.Add(new InstallerCleanupSkippedPath(path, "outside_local_app_data_or_too_broad"));
                    continue;
                }

                if (!Directory.Exists(path))
                {
                    continue;
                }

                Directory.Delete(path, recursive: true);
                deleted.Add(Path.GetFullPath(path));
            }
            catch (Exception exception) when (IsCleanupException(exception))
            {
                skipped.Add(new InstallerCleanupSkippedPath(path, exception.GetType().Name));
            }
        }
    }

    private static IEnumerable<string> LegacyPackageDirectoryCandidates(string localApplicationDataRoot)
    {
        yield return Path.Combine(localApplicationDataRoot, LegacyPackageDirectoryName);
        yield return Path.Combine(localApplicationDataRoot, "Programs", LegacyPackageDirectoryName);
    }

    private static void DeleteLegacyShortcuts(
        IEnumerable<string> shortcutRoots,
        ICollection<string> deleted,
        ICollection<InstallerCleanupSkippedPath> skipped)
    {
        foreach (var root in shortcutRoots.Where(root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> shortcutPaths;
            try
            {
                shortcutPaths = Directory
                    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(path => string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch (Exception exception) when (IsCleanupException(exception))
            {
                skipped.Add(new InstallerCleanupSkippedPath(root, exception.GetType().Name));
                continue;
            }

            foreach (var shortcutPath in shortcutPaths)
            {
                try
                {
                    if (!IsSafeShortcutCleanupPath(shortcutPath, root))
                    {
                        skipped.Add(new InstallerCleanupSkippedPath(shortcutPath, "outside_shortcut_root_or_not_lnk"));
                        continue;
                    }

                    if (!FileContainsLegacyIdentity(shortcutPath))
                    {
                        continue;
                    }

                    File.Delete(shortcutPath);
                    deleted.Add(Path.GetFullPath(shortcutPath));
                    DeleteEmptyParentDirectories(Path.GetDirectoryName(shortcutPath), root);
                }
                catch (Exception exception) when (IsCleanupException(exception))
                {
                    skipped.Add(new InstallerCleanupSkippedPath(shortcutPath, exception.GetType().Name));
                }
            }
        }
    }

    private static bool IsSafeShortcutCleanupPath(string path, string shortcutRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(shortcutRoot))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var fullShortcutRoot = NormalizeDirectoryPath(shortcutRoot);
        if (!string.Equals(Path.GetExtension(fullPath), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fullPath.StartsWith(
            fullShortcutRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool FileContainsLegacyIdentity(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return ContainsBytes(bytes, System.Text.Encoding.UTF8.GetBytes(LegacyPackageDirectoryName))
            || ContainsBytes(bytes, System.Text.Encoding.Unicode.GetBytes(LegacyPackageDirectoryName))
            || DecodedFileContainsLegacyIdentity(bytes, System.Text.Encoding.UTF8)
            || DecodedFileContainsLegacyIdentity(bytes, System.Text.Encoding.Unicode)
            || DecodedFileContainsLegacyIdentity(bytes, System.Text.Encoding.Latin1);
    }

    private static bool IsCurrentAppBaseDirectoryInside(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            return false;
        }

        var fullDirectory = NormalizeDirectoryPath(directory);
        var appBaseDirectory = NormalizeDirectoryPath(AppContext.BaseDirectory);
        return string.Equals(appBaseDirectory, fullDirectory, StringComparison.OrdinalIgnoreCase)
            || appBaseDirectory.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsBytes(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        return value.Length > 0 && source.IndexOf(value) >= 0;
    }

    private static bool DecodedFileContainsLegacyIdentity(byte[] bytes, System.Text.Encoding encoding)
    {
        return encoding
            .GetString(bytes)
            .Contains(LegacyPackageDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteEmptyParentDirectories(string? directory, string stopAtRoot)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var stop = NormalizeDirectoryPath(stopAtRoot);
        var current = NormalizeDirectoryPath(directory);
        while (!string.Equals(current, stop, StringComparison.OrdinalIgnoreCase)
            && current.StartsWith(stop + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(current)
            && !Directory.EnumerateFileSystemEntries(current).Any())
        {
            Directory.Delete(current);
            current = NormalizeDirectoryPath(Path.GetDirectoryName(current) ?? stop);
        }
    }

    private static string NormalizeDirectoryPath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsCleanupException(Exception exception)
    {
        return exception is ArgumentException
            or DirectoryNotFoundException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;
    }
}

internal sealed record InstallerCleanupResult(
    IReadOnlyList<string> DeletedPaths,
    IReadOnlyList<InstallerCleanupSkippedPath> SkippedPaths);

internal sealed record InstallerCleanupSkippedPath(string Path, string Reason);

internal sealed record InstallerCleanupSnapshot(
    bool HasRun,
    DateTimeOffset? LastRunAtUtc,
    string LegacyPackageDirectoryName,
    IReadOnlyList<string> DeletedPaths,
    IReadOnlyList<InstallerCleanupSkippedPath> SkippedPaths);
