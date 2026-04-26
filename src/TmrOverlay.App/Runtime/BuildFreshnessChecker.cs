using System.Reflection;

namespace TmrOverlay.App.Runtime;

internal static class BuildFreshnessChecker
{
    private const string SolutionFileName = "tmrOverlay.sln";
    private static readonly string[] SourceSearchPatterns =
    [
        "*.cs",
        "*.csproj",
        "appsettings.json",
        "Package.swift",
        "*.swift",
        "*.md"
    ];

    public static BuildFreshnessResult Check()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        if (repositoryRoot is null || string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return BuildFreshnessResult.Current;
        }

        var buildTimeUtc = File.GetLastWriteTimeUtc(assemblyPath);
        var newestSource = FindNewestSource(repositoryRoot);

        if (newestSource is null || newestSource.LastWriteTimeUtc <= buildTimeUtc.AddSeconds(2))
        {
            return BuildFreshnessResult.Current;
        }

        var relativePath = Path.GetRelativePath(repositoryRoot, newestSource.FullName);
        return new BuildFreshnessResult(
            SourceNewerThanBuild: true,
            Message: $"Local source is newer than this build; rebuild recommended. Newest: {relativePath}");
    }

    private static FileInfo? FindNewestSource(string repositoryRoot)
    {
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src"),
            Path.Combine(repositoryRoot, "tests"),
            Path.Combine(repositoryRoot, "local-mac", "TmrOverlayMac"),
            Path.Combine(repositoryRoot, "README.md"),
            Path.Combine(repositoryRoot, "VERSION.md")
        };

        return roots
            .SelectMany(EnumerateSourceFiles)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static IEnumerable<FileInfo> EnumerateSourceFiles(string path)
    {
        if (File.Exists(path))
        {
            yield return new FileInfo(path);
            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield break;
        }

        foreach (var pattern in SourceSearchPatterns)
        {
            foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || file.Contains($"{Path.DirectorySeparatorChar}.build{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return new FileInfo(file);
            }
        }
    }

    private static string? FindRepositoryRoot(string startingDirectory)
    {
        var directory = new DirectoryInfo(startingDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, SolutionFileName);
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

internal sealed record BuildFreshnessResult(bool SourceNewerThanBuild, string? Message)
{
    public static BuildFreshnessResult Current { get; } = new(SourceNewerThanBuild: false, Message: null);
}
