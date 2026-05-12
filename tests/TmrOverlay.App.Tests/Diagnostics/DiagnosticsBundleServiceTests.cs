using System.IO.Compression;
using System.Text.Json.Nodes;
using TmrOverlay.App.Events;
using TmrOverlay.App.Installation;
using TmrOverlay.App.Localhost;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.App.Updates;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Settings;
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
            File.WriteAllText(
                Path.Combine(captureDirectory, "latest-session.yaml"),
                """
                WeekendInfo:
                 TrackName: nurburgring combinedshortb
                 TrackDisplayName: Gesamtstrecke VLN
                SessionInfo:
                 CurrentSessionNum: 0
                 Sessions:
                 - SessionNum: 0
                   SessionType: Race
                   SessionName: RACE
                DriverInfo:
                 DriverCarIdx: 0
                 Drivers:
                 - CarIdx: 0
                   CarPath: bmwm4gt3
                   CarScreenName: BMW M4 GT3 EVO
                   CarScreenNameShort: BMW M4 GT3 EVO
                """);
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
            var liveOverlayWindowStore = new LiveOverlayWindowCaptureStore(storage);
            var streamChatOverlay = new OverlaySettings
            {
                Id = StreamChatOverlayDefinition.Definition.Id,
                Enabled = true,
                AlwaysOnTop = true,
                X = 1440,
                Y = 80,
                Width = StreamChatOverlayDefinition.Definition.DefaultWidth,
                Height = StreamChatOverlayDefinition.Definition.DefaultHeight
            };
            liveOverlayWindowStore.RecordOverlayWindow(
                StreamChatOverlayDefinition.Definition,
                streamChatOverlay,
                form: null,
                enabled: true,
                sessionAllowed: true,
                settingsPreview: false,
                desiredVisible: true,
                actualVisible: true,
                topMost: true,
                liveTelemetryAvailable: true,
                contextRequirement: StreamChatOverlayDefinition.Definition.ContextRequirement.ToString(),
                contextAvailable: true,
                contextReason: "not_required",
                settingsOverlayActive: false,
                settingsWindowVisible: false,
                settingsWindowIntersects: false,
                settingsWindowInputProtected: false,
                inputTransparent: true,
                noActivate: true,
                implementation: "native-v2",
                nativeFormType: "DesignV2LiveOverlayForm",
                nativeRenderer: "DesignV2LiveOverlayForm",
                nativeBodyKind: "stream-chat");
            var trackMapStore = new TrackMapStore(storage);
            var settingsStore = new AppSettingsStore(storage);
            var releaseUpdates = new ReleaseUpdateService(
                new ReleaseUpdateOptions { Enabled = false },
                new AppEventRecorder(storage),
                NullLogger<ReleaseUpdateService>.Instance);
            var sessionPreview = new SessionPreviewState(new AppEventRecorder(storage));
            sessionPreview.SetMode(TmrOverlay.Core.Overlays.OverlaySessionKind.Qualifying);
            var liveTelemetry = new TestLiveTelemetrySource(LiveTelemetrySnapshot.Empty with
            {
                IsConnected = true,
                IsCollecting = true,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                LatestSample = new HistoricalTelemetrySample(
                    CapturedAtUtc: DateTimeOffset.UtcNow,
                    SessionTime: 22.5d,
                    SessionTick: 12,
                    SessionInfoUpdate: 3,
                    IsOnTrack: false,
                    IsInGarage: false,
                    OnPitRoad: true,
                    PitstopActive: false,
                    PlayerCarInPitStall: false,
                    FuelLevelLiters: 42d,
                    FuelLevelPercent: 0.5d,
                    FuelUsePerHourKg: 0d,
                    SpeedMetersPerSecond: 0d,
                    Lap: 0,
                    LapCompleted: 0,
                    LapDistPct: 0.05d,
                    LapLastLapTimeSeconds: null,
                    LapBestLapTimeSeconds: null,
                    AirTempC: 20d,
                    TrackTempCrewC: 30d,
                    TrackWetness: 0,
                    WeatherDeclaredWet: false,
                    PlayerTireCompound: 0,
                    IsGarageVisible: true,
                    SessionState: 3,
                    PlayerCarIdx: 10,
                    RawCamCarIdx: 42,
                    FocusCarIdx: 42,
                    IsReplayPlaying: false,
                    AllCars:
                    [
                        new HistoricalCarProximity(
                            CarIdx: 10,
                            LapCompleted: 0,
                            LapDistPct: 0.04d,
                            F2TimeSeconds: 2.1d,
                            EstimatedTimeSeconds: 22.1d,
                            Position: 3,
                            ClassPosition: 1,
                            CarClass: 12,
                            TrackSurface: 3,
                            OnPitRoad: true),
                        new HistoricalCarProximity(
                            CarIdx: 42,
                            LapCompleted: 0,
                            LapDistPct: 0.05d,
                            F2TimeSeconds: -1d,
                            EstimatedTimeSeconds: 23.4d,
                            Position: 0,
                            ClassPosition: 0,
                            CarClass: 12,
                            TrackSurface: 3,
                            OnPitRoad: false)
                    ]),
                Models = LiveRaceModels.Empty with
                {
                    DriverDirectory = LiveDriverDirectoryModel.Empty with
                    {
                        HasData = true,
                        Quality = LiveModelQuality.Reliable,
                        PlayerCarIdx = 10,
                        FocusCarIdx = 42
                    },
                    RaceEvents = LiveRaceEventModel.Empty with
                    {
                        HasData = true,
                        Quality = LiveModelQuality.Reliable,
                        IsGarageVisible = true,
                        OnPitRoad = true
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
                sessionPreview,
                performance,
                performanceRecorder,
                liveOverlayWindowStore,
                new ForegroundWindowTracker(),
                releaseUpdates,
                NullLogger<DiagnosticsBundleService>.Instance);

            var bundlePath = service.CreateBundle();

            var bundleFileName = Path.GetFileName(bundlePath);
            Assert.StartsWith("bmw-m4-gt3-evo-gesamtstrecke-vln-", bundleFileName);
            Assert.EndsWith(".zip", bundleFileName);

            using var archive = ZipFile.OpenRead(bundlePath);
            var entryNames = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("metadata/app-version.json", entryNames);
            Assert.Contains("metadata/storage.json", entryNames);
            Assert.Contains("metadata/telemetry-state.json", entryNames);
            Assert.Contains("metadata/localhost-overlays.json", entryNames);
            Assert.Contains("metadata/browser-overlays.json", entryNames);
            Assert.Contains("metadata/session-preview.json", entryNames);
            Assert.Contains("metadata/shared-settings-contract.json", entryNames);
            Assert.Contains("metadata/release-updates.json", entryNames);
            Assert.Contains("metadata/installer-cleanup.json", entryNames);
            Assert.Contains("metadata/track-maps.json", entryNames);
            Assert.Contains("metadata/garage-cover.json", entryNames);
            Assert.Contains("metadata/live-telemetry-synthesis.json", entryNames);
            Assert.Contains("metadata/window-z-order.json", entryNames);
            Assert.Contains("metadata/performance.json", entryNames);
            Assert.Contains("metadata/ui-freeze-watch.json", entryNames);
            Assert.Contains("live-overlays/manifest.json", entryNames);
            Assert.Contains("runtime/runtime-state.json", entryNames);
            Assert.Contains("shared/tmr-overlay-contract.json", entryNames);
            Assert.Contains("shared/tmr-overlay-contract.schema.json", entryNames);
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

            var diagnosticsBundleEntry = archive.GetEntry("metadata/diagnostics-bundle.json");
            Assert.NotNull(diagnosticsBundleEntry);
            using (var diagnosticsBundleReader = new StreamReader(diagnosticsBundleEntry.Open()))
            {
                var diagnosticsBundleJson = JsonNode.Parse(diagnosticsBundleReader.ReadToEnd());
                Assert.Equal(bundleFileName, (string?)diagnosticsBundleJson?["fileName"]);
                Assert.Equal("BMW M4 GT3 EVO", (string?)diagnosticsBundleJson?["naming"]?["carName"]);
                Assert.Equal("Gesamtstrecke VLN", (string?)diagnosticsBundleJson?["naming"]?["trackName"]);
                Assert.Equal("latest-capture", (string?)diagnosticsBundleJson?["naming"]?["source"]);
            }

            var liveOverlaysEntry = archive.GetEntry("live-overlays/manifest.json");
            Assert.NotNull(liveOverlaysEntry);
            using (var liveOverlaysReader = new StreamReader(liveOverlaysEntry.Open()))
            {
                var liveOverlaysJson = JsonNode.Parse(liveOverlaysReader.ReadToEnd());
                Assert.Equal("live-window-screen-crops", (string?)liveOverlaysJson?["captureKind"]);
                var overlay = Assert.Single(Assert.IsType<JsonArray>(liveOverlaysJson?["overlays"]));
                Assert.Equal("stream-chat", (string?)overlay?["overlayId"]);
                Assert.True(((bool?)overlay?["topMost"]) == true);
                Assert.True(((bool?)overlay?["alwaysOnTopSetting"]) == true);
                Assert.True(((bool?)overlay?["inputTransparent"]) == true);
                Assert.True(((bool?)overlay?["noActivate"]) == true);
                Assert.False(((bool?)overlay?["inputInterceptRisk"]) ?? true);
                Assert.Equal("stream-chat", (string?)overlay?["nativeBodyKind"]);
            }

            var windowZOrderEntry = archive.GetEntry("metadata/window-z-order.json");
            Assert.NotNull(windowZOrderEntry);
            using (var windowZOrderReader = new StreamReader(windowZOrderEntry.Open()))
            {
                var windowZOrderJson = JsonNode.Parse(windowZOrderReader.ReadToEnd());
                Assert.NotNull(windowZOrderJson?["capturedAtUtc"]);
                Assert.NotNull(windowZOrderJson?["available"]);
                Assert.NotNull(windowZOrderJson?["foregroundHistory"]);
                Assert.IsType<JsonArray>(windowZOrderJson?["foregroundHistory"]);
                Assert.NotNull(windowZOrderJson?["windowCount"]);
                Assert.IsType<JsonArray>(windowZOrderJson?["windows"]);
            }

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

            var browserOverlaysEntry = archive.GetEntry("metadata/browser-overlays.json");
            Assert.NotNull(browserOverlaysEntry);
            using (var browserOverlaysReader = new StreamReader(browserOverlaysEntry.Open()))
            {
                var browserOverlaysJson = JsonNode.Parse(browserOverlaysReader.ReadToEnd());
                var pages = Assert.IsType<JsonArray>(browserOverlaysJson?["pages"]);
                Assert.Contains(pages, page =>
                    string.Equals((string?)page?["id"], "standings", StringComparison.Ordinal)
                    && string.Equals((string?)page?["canonicalRoute"], "/overlays/standings", StringComparison.Ordinal)
                    && ((int?)page?["refreshIntervalMilliseconds"]) == 250);
                Assert.Contains(pages, page =>
                    string.Equals((string?)page?["id"], "garage-cover", StringComparison.Ordinal)
                    && string.Equals((string?)page?["canonicalRoute"], "/overlays/garage-cover", StringComparison.Ordinal)
                    && ((bool?)page?["renderWhenTelemetryUnavailable"]) == true);
            }

            var sessionPreviewEntry = archive.GetEntry("metadata/session-preview.json");
            Assert.NotNull(sessionPreviewEntry);
            using (var sessionPreviewReader = new StreamReader(sessionPreviewEntry.Open()))
            {
                var sessionPreviewJson = JsonNode.Parse(sessionPreviewReader.ReadToEnd());
                Assert.True(((bool?)sessionPreviewJson?["active"]) == true);
                Assert.Equal("Qualifying", (string?)sessionPreviewJson?["mode"]);
                Assert.True(((bool?)sessionPreviewJson?["usesNormalOverlayVisibility"]) == true);
                Assert.False(((bool?)sessionPreviewJson?["overridesOverlayEnabledState"]) ?? true);
                Assert.False(((bool?)sessionPreviewJson?["overridesOverlaySessionFilters"]) ?? true);
                Assert.Equal("settings-general-preview", (string?)sessionPreviewJson?["source"]);
            }

            var sharedContractEntry = archive.GetEntry("metadata/shared-settings-contract.json");
            Assert.NotNull(sharedContractEntry);
            using (var sharedContractReader = new StreamReader(sharedContractEntry.Open()))
            {
                var sharedContractJson = JsonNode.Parse(sharedContractReader.ReadToEnd());
                Assert.Equal(9, ((int?)sharedContractJson?["settingsVersion"]) ?? -1);
                Assert.Equal("twitch", (string?)sharedContractJson?["streamChatDefaultProvider"]);
                Assert.Equal("techmatesracing", (string?)sharedContractJson?["streamChatDefaultTwitchChannel"]);
                Assert.Equal("#00E8FF", (string?)sharedContractJson?["designV2Colors"]?["cyan"]);
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

            var liveTelemetrySynthesisEntry = archive.GetEntry("metadata/live-telemetry-synthesis.json");
            Assert.NotNull(liveTelemetrySynthesisEntry);
            using (var liveTelemetrySynthesisReader = new StreamReader(liveTelemetrySynthesisEntry.Open()))
            {
                var liveTelemetrySynthesisJson = JsonNode.Parse(liveTelemetrySynthesisReader.ReadToEnd());
                Assert.Equal(42, ((int?)liveTelemetrySynthesisJson?["focus"]?["rawCamCarIdx"]) ?? -1);
                Assert.Equal(42, ((int?)liveTelemetrySynthesisJson?["focus"]?["focusCarIdx"]) ?? -1);
                Assert.True(((bool?)liveTelemetrySynthesisJson?["focus"]?["focusDiffersFromPlayer"]) == true);
                Assert.Equal("parade-laps", (string?)liveTelemetrySynthesisJson?["sessionPhase"]?["label"]);
                Assert.Equal(2, ((int?)liveTelemetrySynthesisJson?["carFieldCoverage"]?["rowCount"]) ?? -1);
                Assert.Equal(1, ((int?)liveTelemetrySynthesisJson?["carFieldCoverage"]?["officialPositionValidCount"]) ?? -1);
                Assert.Contains(
                    "gridding/startup/replay",
                    (string?)liveTelemetrySynthesisJson?["fieldSemantics"]?["sentinelNote"]);
                var overlayDecisions = Assert.IsType<JsonArray>(liveTelemetrySynthesisJson?["overlays"]);
                Assert.Contains(overlayDecisions, overlay =>
                    string.Equals((string?)overlay?["id"], "pit-service", StringComparison.Ordinal)
                    && string.Equals((string?)overlay?["contextRequirement"], "LocalPlayerInCarOrPit", StringComparison.Ordinal)
                    && ((bool?)overlay?["contextAvailable"]) == false
                    && string.Equals((string?)overlay?["contextReason"], "focus_on_another_car", StringComparison.Ordinal));
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

    [Fact]
    public void CreateBundle_UsesRecentHistoryAnalysisForNameWhenCaptureAndLiveContextUnavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-overlay-diagnostics-test", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = CreateStorage(root);
            var analysisDirectory = Path.Combine(storage.UserHistoryRoot, "analysis");
            var historySessionDirectory = Path.Combine(
                storage.UserHistoryRoot,
                "cars",
                "car-132-bmwm4gt3",
                "tracks",
                "track-262-nurburgring-combinedshortb",
                "sessions",
                "race");
            Directory.CreateDirectory(analysisDirectory);
            Directory.CreateDirectory(historySessionDirectory);

            var analysisPath = Path.Combine(analysisDirectory, "20260510-215638-session-20260510-214918-567.json");
            File.WriteAllText(
                analysisPath,
                """
                {
                  "analysisVersion": 1,
                  "id": "20260510-215638-session-20260510-214918-567",
                  "createdAtUtc": "2026-05-10T21:56:38.5083337Z",
                  "finishedAtUtc": "2026-05-10T21:56:38.4742549Z",
                  "sourceId": "session-20260510-214918-567",
                  "title": "Gesamtstrecke VLN - Race",
                  "subtitle": "BMW M4 GT3 EVO | none confidence",
                  "combo": {
                    "carKey": "car-132-bmwm4gt3",
                    "trackKey": "track-262-nurburgring-combinedshortb",
                    "sessionKey": "race"
                  },
                  "lines": []
                }
                """);
            File.WriteAllText(
                Path.Combine(historySessionDirectory, "aggregate.json"),
                """
                {
                  "aggregateVersion": 1,
                  "combo": {
                    "carKey": "car-132-bmwm4gt3",
                    "trackKey": "track-262-nurburgring-combinedshortb",
                    "sessionKey": "race"
                  },
                  "car": {
                    "carId": 132,
                    "carPath": "bmwm4gt3",
                    "carScreenName": "BMW M4 GT3 EVO",
                    "carScreenNameShort": "BMW M4 GT3 EVO"
                  },
                  "track": {
                    "trackId": 262,
                    "trackName": "nurburgring combinedshortb",
                    "trackDisplayName": "Gesamtstrecke VLN"
                  },
                  "session": {
                    "sessionType": "Race"
                  },
                  "updatedAtUtc": "2026-05-10T21:56:38.5015157Z",
                  "sessionCount": 1
                }
                """);

            var state = new TelemetryCaptureState();
            var localhostState = new LocalhostOverlayState(new LocalhostOverlayOptions());
            var performance = new AppPerformanceState();
            var performanceRecorder = new AppPerformanceSnapshotRecorder(storage);
            var trackMapStore = new TrackMapStore(storage);
            var settingsStore = new AppSettingsStore(storage);
            var releaseUpdates = new ReleaseUpdateService(
                new ReleaseUpdateOptions { Enabled = false },
                new AppEventRecorder(storage),
                NullLogger<ReleaseUpdateService>.Instance);
            var sessionPreview = new SessionPreviewState(new AppEventRecorder(storage));
            var liveTelemetry = new TestLiveTelemetrySource(LiveTelemetrySnapshot.Empty);
            var service = new DiagnosticsBundleService(
                storage,
                new LiveModelParityOptions(),
                new LiveOverlayDiagnosticsOptions(),
                state,
                localhostState,
                trackMapStore,
                settingsStore,
                liveTelemetry,
                sessionPreview,
                performance,
                performanceRecorder,
                new LiveOverlayWindowCaptureStore(storage),
                new ForegroundWindowTracker(),
                releaseUpdates,
                NullLogger<DiagnosticsBundleService>.Instance);

            var bundlePath = service.CreateBundle(DiagnosticsBundleSources.SessionFinalization);

            var bundleFileName = Path.GetFileName(bundlePath);
            Assert.StartsWith("bmw-m4-gt3-evo-gesamtstrecke-vln-", bundleFileName);
            Assert.EndsWith(".zip", bundleFileName);
            Assert.DoesNotContain("unknown-car-unknown-track", bundleFileName);

            using var archive = ZipFile.OpenRead(bundlePath);
            var diagnosticsBundleEntry = archive.GetEntry("metadata/diagnostics-bundle.json");
            Assert.NotNull(diagnosticsBundleEntry);
            using var diagnosticsBundleReader = new StreamReader(diagnosticsBundleEntry.Open());
            var diagnosticsBundleJson = JsonNode.Parse(diagnosticsBundleReader.ReadToEnd());
            Assert.Equal(bundleFileName, (string?)diagnosticsBundleJson?["fileName"]);
            Assert.Equal("BMW M4 GT3 EVO", (string?)diagnosticsBundleJson?["naming"]?["carName"]);
            Assert.Equal("Gesamtstrecke VLN", (string?)diagnosticsBundleJson?["naming"]?["trackName"]);
            Assert.Equal("history-analysis", (string?)diagnosticsBundleJson?["naming"]?["source"]);
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
