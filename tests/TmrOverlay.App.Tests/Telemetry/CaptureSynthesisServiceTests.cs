using System.Text.Json;
using TmrOverlay.App.Telemetry;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class CaptureSynthesisServiceTests
{
    [Fact]
    public void FindPendingSynthesisCaptures_ReturnsRawCapturesWithoutStableSynthesis()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-synthesis-scan-test", Guid.NewGuid().ToString("N"));
        try
        {
            var pendingDirectory = CreateCaptureDirectory(
                root,
                "capture-20260501-120000-000",
                collectionId: "collection-pending",
                hasStableSynthesis: false);
            CreateCaptureDirectory(
                root,
                "capture-20260501-130000-000",
                collectionId: "collection-complete",
                hasStableSynthesis: true);

            var pending = CaptureSynthesisService.FindPendingSynthesisCaptures(root);

            var item = Assert.Single(pending);
            Assert.Equal(pendingDirectory, item.DirectoryPath);
            Assert.Equal("capture-20260501-120000-000", item.CaptureId);
            Assert.Equal("collection-pending", item.CollectionId);
            Assert.Equal("missing_synthesis", item.Reason);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string CreateCaptureDirectory(
        string root,
        string captureId,
        string collectionId,
        bool hasStableSynthesis)
    {
        var directory = Path.Combine(root, captureId);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "telemetry.bin"), new byte[32]);
        File.WriteAllText(Path.Combine(directory, "telemetry-schema.json"), "[]");
        File.WriteAllText(
            Path.Combine(directory, "capture-manifest.json"),
            JsonSerializer.Serialize(new CaptureManifest
            {
                CaptureId = captureId,
                CollectionId = collectionId,
                StartedAtUtc = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
                FinishedAtUtc = DateTimeOffset.Parse("2026-05-01T12:30:00Z"),
                TelemetryFile = "telemetry.bin",
                SchemaFile = "telemetry-schema.json",
                LatestSessionInfoFile = "latest-session.yaml",
                SessionInfoDirectory = "session-info",
                SdkVersion = 1,
                TickRate = 60,
                BufferLength = 16,
                VariableCount = 0
            }));

        if (hasStableSynthesis)
        {
            File.WriteAllText(Path.Combine(directory, "capture-synthesis.json"), "{}");
        }

        return directory;
    }
}
