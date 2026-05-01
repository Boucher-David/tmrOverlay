using System.Text.Json;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using Xunit;

namespace TmrOverlay.App.Tests.Events;

public sealed class AppEventRecorderTests
{
    [Fact]
    public void Record_AddsEventVersionSeverityAndAppRunId()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-events-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var diagnosticContext = new TelemetryDiagnosticContext();
            var recorder = new AppEventRecorder(storage, diagnosticContext);

            recorder.Record("capture_test", new Dictionary<string, string?>
            {
                ["collectionId"] = "collection-test"
            }, severity: "warning");

            var eventFile = Assert.Single(Directory.EnumerateFiles(storage.EventsRoot, "events-*.jsonl"));
            using var document = JsonDocument.Parse(File.ReadAllText(eventFile));
            var rootElement = document.RootElement;
            Assert.Equal(2, rootElement.GetProperty("eventVersion").GetInt32());
            Assert.Equal("capture_test", rootElement.GetProperty("name").GetString());
            Assert.Equal("warning", rootElement.GetProperty("severity").GetString());
            var properties = rootElement.GetProperty("properties");
            Assert.Equal(diagnosticContext.AppRunId, properties.GetProperty("appRunId").GetString());
            Assert.Equal("collection-test", properties.GetProperty("collectionId").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static AppStorageOptions CreateStorage(string root)
    {
        return new AppStorageOptions
        {
            AppDataRoot = root,
            CaptureRoot = Path.Combine(root, "captures"),
            UserHistoryRoot = Path.Combine(root, "history", "user"),
            BaselineHistoryRoot = Path.Combine(root, "history", "baseline"),
            LogsRoot = Path.Combine(root, "logs"),
            SettingsRoot = Path.Combine(root, "settings"),
            DiagnosticsRoot = Path.Combine(root, "diagnostics"),
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }
}
