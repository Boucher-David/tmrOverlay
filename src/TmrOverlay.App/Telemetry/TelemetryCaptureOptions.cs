using Microsoft.Extensions.Configuration;

namespace TmrOverlay.App.Telemetry;

internal sealed class TelemetryCaptureOptions
{
    private const string SolutionFileName = "tmrOverlay.sln";

    public required string ResolvedCaptureRoot { get; init; }

    public bool StoreSessionInfoSnapshots { get; init; } = true;

    public int QueueCapacity { get; init; } = 2048;

    public static TelemetryCaptureOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("TelemetryCapture");
        var configuredCaptureRoot = section["CaptureRoot"];

        return new TelemetryCaptureOptions
        {
            ResolvedCaptureRoot = ResolveCaptureRoot(configuredCaptureRoot),
            StoreSessionInfoSnapshots = ParseBoolean(section["StoreSessionInfoSnapshots"], defaultValue: true),
            QueueCapacity = ParseInt32(section["QueueCapacity"], defaultValue: 2048, minimumValue: 128)
        };
    }

    private static string ResolveCaptureRoot(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return ResolveDefaultCaptureRoot();
        }

        var expandedValue = Environment.ExpandEnvironmentVariables(configuredValue);
        if (Path.IsPathRooted(expandedValue))
        {
            return Path.GetFullPath(expandedValue);
        }

        var baseDirectory = FindRepositoryRoot(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDirectory, expandedValue));
    }

    private static string ResolveDefaultCaptureRoot()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        if (repositoryRoot is not null)
        {
            return Path.Combine(repositoryRoot, "captures");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TmrOverlay",
            "captures");
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

    private static bool ParseBoolean(string? configuredValue, bool defaultValue)
    {
        return bool.TryParse(configuredValue, out var parsedValue) ? parsedValue : defaultValue;
    }

    private static int ParseInt32(string? configuredValue, int defaultValue, int minimumValue)
    {
        if (!int.TryParse(configuredValue, out var parsedValue))
        {
            return defaultValue;
        }

        return Math.Max(parsedValue, minimumValue);
    }
}
