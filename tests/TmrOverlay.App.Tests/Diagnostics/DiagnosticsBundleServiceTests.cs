using System.IO.Compression;
using System.Text.Json.Nodes;
using TmrOverlay.App.Events;
using TmrOverlay.App.Installation;
using TmrOverlay.App.Localhost;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.App.Updates;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Diagnostics;

public sealed class DiagnosticsBundleServiceTests
{
    [Fact]
    public void CreateBundle_IncludesTriageFilesAndExcludesRawTelemetry()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-diagnostics-test", Guid.NewGuid().ToString("N"));
        try
        {
            InstallerCleanup.ResetLegacyInstallerCleanupForTests();
            InstallerCleanup.RecordLegacyInstallerCleanupForTests(
                new InstallerCleanupResult(
                    [@"C:\Users\David\AppData\Local\TechMatesRacing.TmrOverlay"],
                    [new InstallerCleanupSkippedPath(@"C:\Users\David\Desktop\TmrOverlay.lnk", "IOException")]),
                DateTimeOffset.Parse("2026-05-09T12:00:00Z"));

            var storage = CreateStorage(root);
            Directory.CreateDirectory(storage.LogsRoot);
            Directory.CreateDirectory(storage.EventsRoot);
            Directory.CreateDirectory(storage.SettingsRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(storage.RuntimeStatePath)!);
            var garageCoverDirectory = Path.Combine(storage.SettingsRoot, "garage-cover");
            Directory.CreateDirectory(garageCoverDirectory);
            var garageCoverPath = Path.Combine(garageCoverDirectory, "cover.png");
            File.WriteAllText(garageCoverPath, "fake image");
            var escapedGarageCoverPath = garageCoverPath.Replace("\\", "\\\\", StringComparison.Ordinal);
            var edgeCaseDirectory = Path.Combine(storage.LogsRoot, "edge-cases");
            var modelParityDirectory = Path.Combine(storage.LogsRoot, "model-parity");
            var overlayDiagnosticsDirectory = Path.Combine(storage.LogsRoot, "overlay-diagnostics");
            Directory.CreateDirectory(edgeCaseDirectory);
            Directory.CreateDirectory(modelParityDirectory);
            Directory.CreateDirectory(overlayDiagnosticsDirectory);
            File.WriteAllText(Path.Combine(storage.LogsRoot, "tmroverlay-20260426.log"), "log line");
            File.WriteAllText(Path.Combine(edgeCaseDirectory, "session-20260426-edge-cases.json"), """{"clipCount":1}""");
            File.WriteAllText(Path.Combine(modelParityDirectory, "session-20260426-live-model-parity.json"), """{"frameCount":1}""");
            File.WriteAllText(Path.Combine(overlayDiagnosticsDirectory, "session-20260426-live-overlay-diagnostics.json"), """{"frameCount":1}""");
            File.WriteAllText(Path.Combine(storage.EventsRoot, "events-20260426.jsonl"), "{}");
            File.WriteAllText(
                Path.Combine(storage.SettingsRoot, "settings.json"),
                $$"""
                {
                  "overlays": [
                    {
                      "id": "stream-chat",
                      "options": {
                        "stream-chat.provider": "streamlabs",
                        "stream-chat.streamlabs-url": "https://streamlabs.com/widgets/chat-box/private-token"
                      }
                    },
                    {
                      "id": "garage-cover",
                      "options": {
                        "garage-cover.image-path": "{{escapedGarageCoverPath}}"
                      }
                    }
                  ]
                }
                """);
            File.WriteAllText(storage.RuntimeStatePath, "{}");

            var analysisDirectory = Path.Combine(storage.UserHistoryRoot, "analysis");
            var historySessionDirectory = Path.Combine(
                storage.UserHistoryRoot,
                "cars",
                "car-156-mercedesamgevogt3",
                "tracks",
                "track-262-nurburgring-combinedshortb",
                "sessions",
                "race");
            var summariesDirectory = Path.Combine(historySessionDirectory, "summaries");
            Directory.CreateDirectory(analysisDirectory);
            Directory.CreateDirectory(summariesDirectory);
            Directory.CreateDirectory(Path.Combine(storage.UserHistoryRoot, ".maintenance"));
            File.WriteAllText(Path.Combine(analysisDirectory, "20260426-race.json"), """{"title":"race analysis"}""");
            File.WriteAllText(Path.Combine(storage.UserHistoryRoot, ".maintenance", "manifest.json"), """{"summaryFilesScanned":1}""");
            File.WriteAllText(Path.Combine(historySessionDirectory, "aggregate.json"), """{"sessionCount":1}""");
            File.WriteAllText(Path.Combine(summariesDirectory, "capture-20260426-120000-000.json"), """{"sourceCaptureId":"capture-20260426-120000-000"}""");

            var captureDirectory = Path.Combine(storage.CaptureRoot, "capture-20260426-120000-000");
            Directory.CreateDirectory(captureDirectory);
            File.WriteAllText(Path.Combine(captureDirectory, "capture-manifest.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "telemetry-schema.json"), "[]");
            File.WriteAllText(Path.Combine(captureDirectory, "latest-session.yaml"), "WeekendInfo: {}");
            File.WriteAllText(Path.Combine(captureDirectory, "capture-synthesis.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "live-model-parity.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "live-overlay-diagnostics.json"), "{}");
            File.WriteAllText(Path.Combine(captureDirectory, "telemetry.bin"), "raw");
            var ibtAnalysisDirectory = Path.Combine(captureDirectory, "ibt-analysis");
            Directory.CreateDirectory(ibtAnalysisDirectory);
            File.WriteAllText(Path.Combine(ibtAnalysisDirectory, "status.json"), """{"status":"skipped"}""");
            File.WriteAllText(Path.Combine(ibtAnalysisDirectory, "ibt-schema-summary.json"), "{}");
            File.WriteAllText(Path.Combine(ibtAnalysisDirectory, "source.ibt"), "raw ibt");

            var state = new TelemetryCaptureState();
            state.MarkCaptureStarted(captureDirectory, DateTimeOffset.UtcNow);
            var localhostState = new LocalhostOverlayState(new LocalhostOverlayOptions
            {
                Enabled = true,
                Port = 9123
            });
            localhostState.RecordStartAttempted();
            localhostState.RecordStarted();
            localhostState.RecordRequest(
                "track_map",
                "GET",
                "/api/track-map",
                200,
                TimeSpan.FromMilliseconds(4));
            var performance = new AppPerformanceState();
            performance.RecordOperation("test.operation", TimeSpan.FromMilliseconds(3));
            var performanceRecorder = new AppPerformanceSnapshotRecorder(storage);
            performanceRecorder.Record(performance.Snapshot());
            var trackMapStore = new TrackMapStore(storage);
            var settingsStore = new AppSettingsStore(storage);
            var releaseUpdates = new ReleaseUpdateService(
                new ReleaseUpdateOptions { Enabled = false },
                new AppEventRecorder(storage),
                NullLogger<ReleaseUpdateService>.Instance);
            var liveTelemetry = new TestLiveTelemetrySource(LiveTelemetrySnapshot.Empty with
            {
                IsConnected = true,
                IsCollecting = true,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                Models = LiveRaceModels.Empty with
                {
                    RaceEvents = LiveRaceEventModel.Empty with
                    {
                        HasData = true,
                        Quality = LiveModelQuality.Reliable,
                        IsGarageVisible = true
                    }
                }
            });
            var service = new DiagnosticsBundleService(
                storage,
                new LiveModelParityOptions(),
                new LiveOverlayDiagnosticsOptions(),
                state,
                localhostState,
                trackMapStore,
                settingsStore,
                liveTelemetry,
                performance,
                performanceRecorder,
                releaseUpdates,
                NullLogger<DiagnosticsBundleService>.Instance);

            var bundlePath = service.CreateBundle();

            using var archive = ZipFile.OpenRead(bundlePath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("metadata/app-version.json", entryNames);
            Assert.Contains("metadata/storage.json", entryNames);
            Assert.Contains("metadata/telemetry-state.json", entryNames);
            Assert.Contains("metadata/localhost-overlays.json", entryNames);
            Assert.Contains("metadata/release-updates.json", entryNames);
            Assert.Contains("metadata/installer-cleanup.json", entryNames);
            Assert.Contains("metadata/track-maps.json", entryNames);
            Assert.Contains("metadata/garage-cover.json", entryNames);
            Assert.Contains("metadata/performance.json", entryNames);
            Assert.Contains("metadata/ui-freeze-watch.json", entryNames);
            Assert.Contains("runtime/runtime-state.json", entryNames);
            Assert.Contains("settings/settings.json", entryNames);
            Assert.Contains("logs/tmroverlay-20260426.log", entryNames);
            Assert.Contains("edge-cases/session-20260426-edge-cases.json", entryNames);
            Assert.Contains("model-parity/session-20260426-live-model-parity.json", entryNames);
            Assert.Contains("overlay-diagnostics/session-20260426-live-overlay-diagnostics.json", entryNames);
            Assert.Contains(entryNames, entryName => entryName.StartsWith("performance/performance-", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("events/events-20260426.jsonl", entryNames);
            Assert.Contains("latest-capture/capture-manifest.json", entryNames);
            Assert.Contains("latest-capture/telemetry-schema.json", entryNames);
            Assert.Contains("latest-capture/latest-session.yaml", entryNames);
            Assert.Contains("latest-capture/capture-synthesis.json", entryNames);
            Assert.Contains("latest-capture/live-model-parity.json", entryNames);
            Assert.Contains("latest-capture/live-overlay-diagnostics.json", entryNames);
            Assert.Contains("latest-capture/ibt-analysis/status.json", entryNames);
            Assert.Contains("latest-capture/ibt-analysis/ibt-schema-summary.json", entryNames);
            Assert.Contains("analysis/20260426-race.json", entryNames);
            Assert.DoesNotContain("history/user/analysis/20260426-race.json", entryNames);
            Assert.Contains("history/user/.maintenance/manifest.json", entryNames);
            Assert.Contains("history/user/cars/car-156-mercedesamgevogt3/tracks/track-262-nurburgring-combinedshortb/sessions/race/aggregate.json", entryNames);
            Assert.Contains("history/user/cars/car-156-mercedesamgevogt3/tracks/track-262-nurburgring-combinedshortb/sessions/race/summaries/capture-20260426-120000-000.json", entryNames);
            Assert.DoesNotContain("latest-capture/telemetry.bin", entryNames);
            Assert.DoesNotContain("latest-capture/ibt-analysis/source.ibt", entryNames);
            Assert.DoesNotContain("settings/garage-cover/cover.png", entryNames);

            var localhostEntry = archive.GetEntry("metadata/localhost-overlays.json");
            Assert.NotNull(localhostEntry);
            using (var localhostReader = new StreamReader(localhostEntry.Open()))
            {
                var localhostJson = JsonNode.Parse(localhostReader.ReadToEnd());
                Assert.True(((bool?)localhostJson?["enabled"]) == true);
                Assert.Equal("listening", (string?)localhostJson?["status"]);
                Assert.Equal(1L, ((long?)localhostJson?["totalRequests"]) ?? -1L);
                Assert.Equal(1L, ((long?)localhostJson?["routeCounts"]?["track_map"]) ?? -1L);
            }

            var releaseUpdatesEntry = archive.GetEntry("metadata/release-updates.json");
            Assert.NotNull(releaseUpdatesEntry);
            using (var releaseUpdatesReader = new StreamReader(releaseUpdatesEntry.Open()))
            {
                var releaseUpdatesJson = JsonNode.Parse(releaseUpdatesReader.ReadToEnd());
                Assert.Equal("Disabled", (string?)releaseUpdatesJson?["statusText"]);
                Assert.False(((bool?)releaseUpdatesJson?["canCheck"]) ?? true);
                Assert.False(((bool?)releaseUpdatesJson?["canDownload"]) ?? true);
                Assert.False(((bool?)releaseUpdatesJson?["canRestartToApply"]) ?? true);
                Assert.Null(releaseUpdatesJson?["downloadProgressPercent"]);
                Assert.Null(releaseUpdatesJson?["lastApplyStartedAtUtc"]);
            }

            var installerCleanupEntry = archive.GetEntry("metadata/installer-cleanup.json");
            Assert.NotNull(installerCleanupEntry);
            using (var installerCleanupReader = new StreamReader(installerCleanupEntry.Open()))
            {
                var installerCleanupJson = JsonNode.Parse(installerCleanupReader.ReadToEnd());
                Assert.True(((bool?)installerCleanupJson?["hasRun"]) == true);
                Assert.Equal("TechMatesRacing.TmrOverlay", (string?)installerCleanupJson?["legacyPackageDirectoryName"]);
                Assert.Equal(
                    DateTimeOffset.Parse("2026-05-09T12:00:00Z"),
                    DateTimeOffset.Parse((string?)installerCleanupJson?["lastRunAtUtc"] ?? string.Empty));
                Assert.Equal(
                    @"C:\Users\David\AppData\Local\TechMatesRacing.TmrOverlay",
                    (string?)installerCleanupJson?["deletedPaths"]?[0]);
                Assert.Equal("IOException", (string?)installerCleanupJson?["skippedPaths"]?[0]?["reason"]);
            }

            var trackMapsEntry = archive.GetEntry("metadata/track-maps.json");
            Assert.NotNull(trackMapsEntry);
            using (var trackMapsReader = new StreamReader(trackMapsEntry.Open()))
            {
                var trackMapsJson = JsonNode.Parse(trackMapsReader.ReadToEnd());
                Assert.Equal(storage.TrackMapRoot, (string?)trackMapsJson?["userRoot"]);
            }

            var settingsEntry = archive.GetEntry("settings/settings.json");
            Assert.NotNull(settingsEntry);
            using var settingsReader = new StreamReader(settingsEntry.Open());
            var bundledSettings = settingsReader.ReadToEnd();
            var bundledSettingsJson = JsonNode.Parse(bundledSettings);
            var overlays = Assert.IsType<JsonArray>(bundledSettingsJson?["overlays"]);
            var bundledOverlay = overlays.OfType<JsonObject>().Single(overlay =>
                string.Equals((string?)overlay["id"], "stream-chat", StringComparison.OrdinalIgnoreCase));
            var bundledOptions = Assert.IsType<JsonObject>(bundledOverlay["options"]);
            Assert.Equal("<redacted>", (string?)bundledOptions["stream-chat.streamlabs-url"]);
            Assert.DoesNotContain("private-token", bundledSettings);

            var garageCoverEntry = archive.GetEntry("metadata/garage-cover.json");
            Assert.NotNull(garageCoverEntry);
            using var garageCoverReader = new StreamReader(garageCoverEntry.Open());
            var garageCoverJson = JsonNode.Parse(garageCoverReader.ReadToEnd());
            Assert.Equal("/overlays/garage-cover", (string?)garageCoverJson?["route"]);
            Assert.Equal("ready", (string?)garageCoverJson?["imageStatus"]);
            Assert.Equal("cover.png", (string?)garageCoverJson?["imageFileName"]);
            Assert.True((bool?)garageCoverJson?["lastGarageVisible"]);
        }
        finally
        {
            InstallerCleanup.ResetLegacyInstallerCleanupForTests();
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
            TrackMapRoot = Path.Combine(root, "track-maps", "user"),
            EventsRoot = Path.Combine(root, "logs", "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        };
    }

    private sealed class TestLiveTelemetrySource : ILiveTelemetrySource
    {
        private readonly LiveTelemetrySnapshot _snapshot;

        public TestLiveTelemetrySource(LiveTelemetrySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public LiveTelemetrySnapshot Snapshot()
        {
            return _snapshot;
        }
    }
}
