using Microsoft.Extensions.Configuration;
using TmrOverlay.App.Storage;
using Xunit;

namespace TmrOverlay.App.Tests.Storage;

public sealed class AppStorageOptionsTests
{
    [Fact]
    public void FromConfiguration_UsesAppDataRootForDefaultWritableFolders()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-storage-test");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Storage:AppDataRoot"] = appDataRoot
        });

        var options = AppStorageOptions.FromConfiguration(configuration);

        Assert.Equal(Path.GetFullPath(appDataRoot), options.AppDataRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "captures")), options.CaptureRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "history", "user")), options.UserHistoryRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "logs")), options.LogsRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "settings")), options.SettingsRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "diagnostics")), options.DiagnosticsRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "logs", "events")), options.EventsRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "runtime-state.json")), options.RuntimeStatePath);
    }

    [Fact]
    public void FromConfiguration_HonorsExplicitWritableFolderOverrides()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-storage-test");
        var captureRoot = Path.Combine(Path.GetTempPath(), "tmr-captures");
        var userHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-history-user");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Storage:AppDataRoot"] = appDataRoot,
            ["Storage:CaptureRoot"] = captureRoot,
            ["Storage:UserHistoryRoot"] = userHistoryRoot
        });

        var options = AppStorageOptions.FromConfiguration(configuration);

        Assert.Equal(Path.GetFullPath(captureRoot), options.CaptureRoot);
        Assert.Equal(Path.GetFullPath(userHistoryRoot), options.UserHistoryRoot);
    }

    [Fact]
    public void FromConfiguration_ResolvesLegacyRelativeCaptureRootUnderAppDataRoot()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-storage-test");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Storage:AppDataRoot"] = appDataRoot,
            ["TelemetryCapture:CaptureRoot"] = "captures"
        });

        var options = AppStorageOptions.FromConfiguration(configuration);

        Assert.Equal(Path.GetFullPath(Path.Combine(appDataRoot, "captures")), options.CaptureRoot);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
