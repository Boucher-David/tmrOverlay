using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Overlays;
using TmrOverlay.App.Overlays.CarRadar;
using TmrOverlay.App.Overlays.DesignV2;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SettingsPanel;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.App.Updates;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.WindowsScreenshots;

internal static class Program
{
    private static string ScreenshotFontFamily => OverlayTheme.DefaultFontFamily;

    private const int ContactSheetColumns = 3;
    private const int ContactCellWidth = 660;
    private const int ContactCellHeight = 420;
    private const int ContactPadding = 28;
    private const int ContactHeaderHeight = 52;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var outputRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? Path.GetFullPath(args[0])
                : Path.GetFullPath(Path.Combine("artifacts", "windows-overlay-screenshots"));
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }

            Directory.CreateDirectory(Path.Combine(outputRoot, "states"));
            Directory.CreateDirectory(Path.Combine(outputRoot, "native-overlays"));
            Directory.CreateDirectory(Path.Combine(outputRoot, "components", "settings"));
            var screenshots = RenderAll(outputRoot);
            RenderContactSheet(outputRoot, screenshots);
            WriteManifest(outputRoot, screenshots);
            Console.WriteLine($"Wrote {screenshots.Count} Windows overlay screenshots to {outputRoot}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static IReadOnlyList<RenderedScreenshot> RenderAll(string outputRoot)
    {
        var screenshots = new List<RenderedScreenshot>();
        var fixture = new TelemetryFixture();

        screenshots.AddRange(RenderSettingsScreenshots(outputRoot));
        screenshots.AddRange(RenderSettingsComponentCrops(outputRoot));
        screenshots.Add(RenderForm(
            outputRoot,
            "fuel-calculator-live",
            "Fuel Calculator",
            () => new FuelCalculatorForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000004)),
                new SessionHistoryQueryService(new SessionHistoryOptions
                {
                    Enabled = false,
                    ResolvedUserHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-windows-screenshots", "history", "user"),
                    ResolvedBaselineHistoryRoot = Path.Combine(Path.GetTempPath(), "tmr-overlay-windows-screenshots", "history", "baseline")
                }),
                new AppPerformanceState(),
                OverlaySettingsFor(FuelCalculatorOverlayDefinition.Definition),
                ScreenshotFontFamily,
                "Metric",
                Noop)));
        screenshots.Add(RenderForm(
            outputRoot,
            "relative-live",
            "Relative",
            () => new RelativeForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000004)),
                NullLogger<RelativeForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(RelativeOverlayDefinition.Definition),
                ScreenshotFontFamily,
                Noop)));
        screenshots.Add(RenderForm(
            outputRoot,
            "standings-live",
            "Standings",
            () => new StandingsForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000004)),
                NullLogger<StandingsForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(StandingsOverlayDefinition.Definition),
                ScreenshotFontFamily,
                Noop)));
        screenshots.Add(RenderForm(
            outputRoot,
            "track-map-placeholder",
            "Track Map",
            () => new TrackMapForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000004)),
                new TrackMapStore(StorageOptionsFor(Path.Combine(outputRoot, "track-map-store"))),
                NullLogger<TrackMapForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(TrackMapOverlayDefinition.Definition),
                ScreenshotFontFamily,
                Noop),
            refreshPasses: 3));
        screenshots.Add(RenderForm(
            outputRoot,
            "flags-blue",
            "Flags",
            () => new FlagsOverlayForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000020)),
                NullLogger<SimpleTelemetryOverlayForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(FlagsOverlayDefinition.Definition),
                Noop),
            postProcess: bitmap => ReplaceColorWithReviewBackdrop(bitmap, Color.FromArgb(1, 2, 3))));
        screenshots.Add(RenderForm(
            outputRoot,
            "session-weather-live",
            "Session / Weather",
            () => new SimpleTelemetryOverlayForm(
                SessionWeatherOverlayDefinition.Definition,
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000004)),
                NullLogger<SimpleTelemetryOverlayForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(SessionWeatherOverlayDefinition.Definition),
                ScreenshotFontFamily,
                "Metric",
                new SimpleTelemetryOverlayMetrics(
                    AppPerformanceMetricIds.OverlaySessionWeatherRefresh,
                    AppPerformanceMetricIds.OverlaySessionWeatherSnapshot,
                    AppPerformanceMetricIds.OverlaySessionWeatherViewModel,
                    AppPerformanceMetricIds.OverlaySessionWeatherApplyUi,
                    AppPerformanceMetricIds.OverlaySessionWeatherRows,
                    AppPerformanceMetricIds.OverlaySessionWeatherPaint),
                SessionWeatherOverlayViewModel.From,
                Noop)));
        screenshots.Add(RenderForm(
            outputRoot,
            "pit-service-active",
            "Pit Service",
            () => new SimpleTelemetryOverlayForm(
                PitServiceOverlayDefinition.Definition,
                fixture.SourceFor(frame => fixture.CreateSnapshot(
                    frame,
                    sessionFlags: 0x00000004,
                    pitServiceActive: true)),
                NullLogger<SimpleTelemetryOverlayForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(PitServiceOverlayDefinition.Definition),
                ScreenshotFontFamily,
                "Metric",
                new SimpleTelemetryOverlayMetrics(
                    AppPerformanceMetricIds.OverlayPitServiceRefresh,
                    AppPerformanceMetricIds.OverlayPitServiceSnapshot,
                    AppPerformanceMetricIds.OverlayPitServiceViewModel,
                    AppPerformanceMetricIds.OverlayPitServiceApplyUi,
                    AppPerformanceMetricIds.OverlayPitServiceRows,
                    AppPerformanceMetricIds.OverlayPitServicePaint),
                PitServiceOverlayViewModel.CreateBuilder(OverlaySettingsFor(PitServiceOverlayDefinition.Definition)),
                Noop)));
        screenshots.Add(RenderForm(
            outputRoot,
            "input-state-trace",
            "Inputs",
            () => new InputStateOverlayForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(
                    frame,
                    sessionFlags: 0x00000004,
                    throttle: Math.Clamp(0.35d + Math.Sin(frame.Index / 3d) * 0.35d, 0d, 1d),
                    brake: frame.Index % 9 is >= 5 and <= 7 ? 0.72d : 0.05d,
                    clutch: frame.Index < 4 ? 0.25d : 0d,
                    steeringWheelAngle: Math.Sin(frame.Index / 4d) * 0.45d)),
                NullLogger<SimpleTelemetryOverlayForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(InputStateOverlayDefinition.Definition),
                ScreenshotFontFamily,
                "Metric",
                Noop),
            refreshPasses: 28));
        screenshots.Add(RenderForm(
            outputRoot,
            "car-radar-side-pressure",
            "Car Radar",
            () =>
            {
                var form = new CarRadarForm(
                    fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000004)),
                    NullLogger<CarRadarForm>.Instance,
                    new AppPerformanceState(),
                    OverlaySettingsFor(CarRadarOverlayDefinition.Definition),
                    ScreenshotFontFamily,
                    Noop);
                form.SetSettingsPreviewVisible(true);
                return form;
            },
            refreshPasses: 4));
        screenshots.Add(RenderForm(
            outputRoot,
            "gap-to-leader-trend",
            "Gap To Leader",
            () => new GapToLeaderForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(
                    frame,
                    sessionFlags: 0x00000004,
                    sessionTime: 3600d + frame.Index * 30d,
                    focusF2TimeSeconds: 34d + Math.Sin(frame.Index / 5d) * 2.2d + frame.Index * 0.08d,
                    capturedAtUtc: fixture.StartedAtUtc.AddSeconds(frame.Index * 30d))),
                NullLogger<GapToLeaderForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(GapToLeaderOverlayDefinition.Definition),
                ScreenshotFontFamily,
                Noop),
            refreshPasses: 42));

        screenshots.AddRange(RenderInstalledNativeOverlayScreenshots(outputRoot));

        return screenshots;
    }

    private static IReadOnlyList<RenderedScreenshot> RenderSettingsScreenshots(string outputRoot)
    {
        var screenshots = new List<RenderedScreenshot>
        {
            RenderForm(
                outputRoot,
                "settings-general",
                "Settings - General",
                () => CreateSettingsForm("General"),
                metadata: SettingsMetadata(null, "general", null)),
            RenderForm(
                outputRoot,
                "settings-support",
                "Settings - Support",
                () => CreateSettingsForm("Support"),
                metadata: SettingsMetadata(null, "support", null))
        };

        foreach (var (definition, tabText) in SettingsOverlayTabs())
        {
            var fileStem = SettingsFileStem(definition.Id);
            foreach (var region in SettingsRegionsFor(definition.Id))
            {
                var suffix = string.Equals(region.Id, "general", StringComparison.Ordinal)
                    ? string.Empty
                    : $"-{region.Id}";
                screenshots.Add(RenderForm(
                    outputRoot,
                    $"settings-{fileStem}{suffix}",
                    $"Settings - {definition.DisplayName} - {region.Label}",
                    () => CreateSettingsForm(tabText, region.Id),
                    metadata: SettingsMetadata(definition.Id, region.Id, null)));
            }
        }

        foreach (var previewMode in PreviewModes())
        {
            screenshots.Add(RenderForm(
                outputRoot,
                $"settings-general-preview-{previewMode.FileStem}",
                $"Settings - General - {previewMode.Label} Preview",
                () => CreateSettingsForm("General", previewMode: previewMode.Kind),
                metadata: SettingsMetadata(null, "general", previewMode.FileStem)));
        }

        return screenshots;
    }

    private static IReadOnlyList<RenderedScreenshot> RenderInstalledNativeOverlayScreenshots(string outputRoot)
    {
        var screenshots = new List<RenderedScreenshot>();
        foreach (var overlay in NativeOverlaySpecs())
        {
            foreach (var previewMode in PreviewModesForOverlay(overlay.Definition.Id))
            {
                screenshots.Add(RenderForm(
                    outputRoot,
                    $"{overlay.Definition.Id}-{previewMode.FileStem}",
                    $"Native {overlay.Definition.DisplayName} - {previewMode.Label}",
                    () => CreateDesignV2LiveOverlayForm(overlay, previewMode.Kind),
                    postProcess: overlay.UsesTransparentBackdrop
                        ? bitmap => ReplaceColorWithReviewBackdrop(bitmap, Color.FromArgb(1, 2, 3))
                        : null,
                    refreshPasses: NativeRefreshPassesFor(overlay.Kind),
                    relativeDirectory: "native-overlays",
                    metadata: NativeOverlayMetadata(overlay.Definition.Id, previewMode.FileStem)));
            }
        }

        screenshots.Add(RenderForm(
            outputRoot,
            "standings-preview-sizing-race",
            "Native Standings - Race Preview Sizing",
            () =>
            {
                var overlay = new NativeOverlaySpec(DesignV2LiveOverlayKind.Standings, StandingsOverlayDefinition.Definition);
                var settings = OverlaySettingsFor(
                    StandingsOverlayDefinition.Definition,
                    width: StandingsOverlayDefinition.Definition.DefaultWidth,
                    height: 2160);
                var form = CreateDesignV2LiveOverlayForm(overlay, OverlaySessionKind.Race, settings);
                form.ClientSize = OverlayManager.TargetOverlayClientSizeForApply(
                    StandingsOverlayDefinition.Definition,
                    settings,
                    form.ClientSize,
                    sessionPreviewActive: true);
                return form;
            },
            refreshPasses: NativeRefreshPassesFor(DesignV2LiveOverlayKind.Standings),
            relativeDirectory: "native-overlays",
            metadata: NativeOverlayMetadata("standings", "race") with
            {
                Fixture = "SessionPreviewTelemetryFixtures/preview-sizing-persisted-expanded-height"
            }));

        return screenshots;
    }

    private static IReadOnlyList<RenderedScreenshot> RenderSettingsComponentCrops(string outputRoot)
    {
        return
        [
            RenderSettingsCrop(
                outputRoot,
                "sidebar-tabs",
                "Settings Components - Sidebar Tabs",
                "General",
                null,
                new Rectangle(64, 116, 190, 506)),
            RenderSettingsCrop(
                outputRoot,
                "region-tabs",
                "Settings Components - Region Tabs",
                "Relative",
                null,
                new Rectangle(300, 198, 420, 52)),
            RenderSettingsCrop(
                outputRoot,
                "unit-choice",
                "Settings Components - Unit Choice",
                "General",
                null,
                new Rectangle(306, 214, 392, 132)),
            RenderSettingsCrop(
                outputRoot,
                "overlay-controls",
                "Settings Components - Overlay Controls",
                "Relative",
                null,
                new Rectangle(306, 272, 392, 226)),
            RenderSettingsCrop(
                outputRoot,
                "content-matrix",
                "Settings Components - Content Matrix",
                "Relative",
                "Content",
                new Rectangle(306, 272, 690, 222)),
            RenderSettingsCrop(
                outputRoot,
                "chat-inputs",
                "Settings Components - Chat Inputs",
                "Stream Chat",
                "Content",
                new Rectangle(306, 272, 650, 204)),
            RenderSettingsCrop(
                outputRoot,
                "support-buttons",
                "Settings Components - Support Buttons",
                "Support",
                null,
                new Rectangle(306, 410, 716, 202)),
            RenderSettingsCrop(
                outputRoot,
                "browser-source",
                "Settings Components - Browser Source",
                "Relative",
                null,
                new Rectangle(726, 272, 296, 132))
        ];
    }

    private static RenderedScreenshot RenderSettingsCrop(
        string outputRoot,
        string fileStem,
        string label,
        string selectedTabText,
        string? selectedRegionText,
        Rectangle cropBounds)
    {
        return RenderFormCrop(
            outputRoot,
            Path.Combine("components", "settings"),
            fileStem,
            label,
            () => CreateSettingsForm(selectedTabText, selectedRegionText),
            cropBounds,
            metadata: new ScreenshotMetadata(
                Surface: "windows-settings-component",
                Renderer: "SettingsOverlayForm/DesignV2SettingsSurface",
                OverlayId: OverlayIdForSettingsTab(selectedTabText),
                Tab: DesignV2TabId(selectedTabText),
                Region: selectedRegionText?.Trim().ToLowerInvariant() ?? "general",
                Fixture: "deterministic-settings-fixture",
                SourceContract: "src/TmrOverlay.App/Overlays/SettingsPanel/DesignV2SettingsSurface.cs"));
    }

    private static SettingsOverlayForm CreateSettingsForm(
        string selectedTabText,
        string? selectedRegionText = null,
        OverlaySessionKind? previewMode = null)
    {
        var storage = StorageOptionsFor(Path.Combine(Path.GetTempPath(), "tmr-overlay-windows-screenshots", Guid.NewGuid().ToString("N")));
        var captureState = new TelemetryCaptureState();
        captureState.SetCaptureRoot(storage.CaptureRoot);
        captureState.MarkConnected();
        captureState.MarkCollectionStarted(DateTimeOffset.UtcNow);
        captureState.RecordFrame(DateTimeOffset.UtcNow);
        var performanceState = new AppPerformanceState();
        var localhostOptions = new LocalhostOverlayOptions();
        var localhostState = new LocalhostOverlayState(localhostOptions);
        var settingsStore = new AppSettingsStore(storage);
        var releaseUpdates = new ReleaseUpdateService(
            new ReleaseUpdateOptions { Enabled = false },
            new AppEventRecorder(storage),
            NullLogger<ReleaseUpdateService>.Instance);
        var liveTelemetry = new SequenceTelemetrySource(_ => LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        });
        var sessionPreview = new SessionPreviewState(new AppEventRecorder(storage));
        sessionPreview.SetMode(previewMode);
        var diagnostics = new DiagnosticsBundleService(
            storage,
            new LiveModelParityOptions(),
            new LiveOverlayDiagnosticsOptions(),
            captureState,
            localhostState,
            new TrackMapStore(storage),
            settingsStore,
            liveTelemetry,
            sessionPreview,
            performanceState,
            new AppPerformanceSnapshotRecorder(storage),
            new LiveOverlayWindowCaptureStore(storage),
            new ForegroundWindowTracker(),
            releaseUpdates,
            new StreamChatOverlaySource(
                NullLogger<StreamChatOverlaySource>.Instance,
                performanceState),
            NullLogger<DiagnosticsBundleService>.Instance);
        var settings = CreateApplicationSettings();

        var form = new SettingsOverlayForm(
            settings,
            ManagedOverlayDefinitions(),
            captureState,
            new TelemetryEdgeCaseOptions(),
            new LiveModelParityOptions(),
            new LiveOverlayDiagnosticsOptions(),
            new PostRaceAnalysisOptions(),
            performanceState,
            releaseUpdates,
            sessionPreview,
            storage,
            localhostOptions,
            localhostState,
            liveTelemetry,
            diagnostics,
            new AppEventRecorder(storage),
            settings.GetOrAddOverlay(
                SettingsOverlayDefinition.Definition.Id,
                SettingsOverlayDefinition.Definition.DefaultWidth,
                SettingsOverlayDefinition.Definition.DefaultHeight,
                defaultEnabled: true),
            Noop,
            Noop,
            Noop,
            _ => { });
        SelectTab(form, selectedTabText);
        if (!string.IsNullOrWhiteSpace(selectedRegionText))
        {
            SelectRegion(form, selectedRegionText);
        }
        return form;
    }

    private static Form CreateDesignV2LiveOverlayForm(
        NativeOverlaySpec overlay,
        OverlaySessionKind previewMode,
        OverlaySettings? settingsOverride = null)
    {
        var storage = StorageOptionsFor(Path.Combine(
            Path.GetTempPath(),
            "tmr-overlay-windows-screenshots",
            "native",
            Guid.NewGuid().ToString("N")));
        var performanceState = new AppPerformanceState();
        var telemetry = new SequenceTelemetrySource(frame =>
            SessionPreviewTelemetryFixtures.Build(
                previewMode,
                DateTimeOffset.UtcNow,
                generation: frame.Index + 1));
        var settings = settingsOverride ?? OverlaySettingsFor(overlay.Definition);
        if (overlay.Kind == DesignV2LiveOverlayKind.StreamChat)
        {
            settings.SetStringOption(OverlayOptionKeys.StreamChatProvider, StreamChatOverlaySettings.ProviderNone);
        }

        var form = new DesignV2LiveOverlayForm(
            overlay.Kind,
            overlay.Definition,
            telemetry,
            new TrackMapStore(storage),
            new SessionHistoryQueryService(new SessionHistoryOptions
            {
                Enabled = false,
                ResolvedUserHistoryRoot = storage.UserHistoryRoot,
                ResolvedBaselineHistoryRoot = storage.BaselineHistoryRoot
            }),
            new StreamChatOverlaySource(NullLogger<StreamChatOverlaySource>.Instance, performanceState),
            performanceState,
            NullLogger<DesignV2LiveOverlayForm>.Instance,
            settings,
            ScreenshotFontFamily,
            "Metric",
            Noop);

        if (overlay.Kind == DesignV2LiveOverlayKind.CarRadar)
        {
            form.SetSettingsPreviewVisible(true);
        }

        return form;
    }

    private static IReadOnlyList<OverlayDefinition> ManagedOverlayDefinitions()
    {
        return
        [
            StandingsOverlayDefinition.Definition,
            FuelCalculatorOverlayDefinition.Definition,
            RelativeOverlayDefinition.Definition,
            TrackMapOverlayDefinition.Definition,
            StreamChatOverlayDefinition.Definition,
            GarageCoverOverlayDefinition.Definition,
            FlagsOverlayDefinition.Definition,
            SessionWeatherOverlayDefinition.Definition,
            PitServiceOverlayDefinition.Definition,
            InputStateOverlayDefinition.Definition,
            CarRadarOverlayDefinition.Definition,
            GapToLeaderOverlayDefinition.Definition
        ];
    }

    private static IReadOnlyList<(OverlayDefinition Definition, string TabText)> SettingsOverlayTabs()
    {
        return ManagedOverlayDefinitions()
            .Select(definition => (definition, definition.DisplayName))
            .ToArray();
    }

    private static IReadOnlyList<SettingsRegionSpec> SettingsRegionsFor(string overlayId)
    {
        if (string.Equals(overlayId, GarageCoverOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new SettingsRegionSpec("general", "General"),
                new SettingsRegionSpec("preview", "Preview")
            ];
        }

        if (string.Equals(overlayId, StreamChatOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new SettingsRegionSpec("general", "General"),
                new SettingsRegionSpec("content", "Content"),
                new SettingsRegionSpec("twitch", "Twitch"),
                new SettingsRegionSpec("streamlabs", "Streamlabs")
            ];
        }

        if (SupportsSharedChromeSettings(overlayId))
        {
            return
            [
                new SettingsRegionSpec("general", "General"),
                new SettingsRegionSpec("content", "Content"),
                new SettingsRegionSpec("header", "Header"),
                new SettingsRegionSpec("footer", "Footer")
            ];
        }

        return
        [
            new SettingsRegionSpec("general", "General"),
            new SettingsRegionSpec("content", "Content")
        ];
    }

    private static bool SupportsSharedChromeSettings(string overlayId)
    {
        return overlayId is
            "standings"
            or "relative"
            or "fuel-calculator"
            or "gap-to-leader"
            or "session-weather"
            or "pit-service";
    }

    private static string SettingsFileStem(string overlayId)
    {
        return string.Equals(overlayId, InputStateOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase)
            ? "inputs"
            : overlayId;
    }

    private static IReadOnlyList<NativeOverlaySpec> NativeOverlaySpecs()
    {
        return
        [
            new NativeOverlaySpec(DesignV2LiveOverlayKind.Standings, StandingsOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.FuelCalculator, FuelCalculatorOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.Relative, RelativeOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.TrackMap, TrackMapOverlayDefinition.Definition, UsesTransparentBackdrop: true),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.StreamChat, StreamChatOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.Flags, FlagsOverlayDefinition.Definition, UsesTransparentBackdrop: true),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.SessionWeather, SessionWeatherOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.PitService, PitServiceOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.InputState, InputStateOverlayDefinition.Definition),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.CarRadar, CarRadarOverlayDefinition.Definition, UsesTransparentBackdrop: true),
            new NativeOverlaySpec(DesignV2LiveOverlayKind.GapToLeader, GapToLeaderOverlayDefinition.Definition)
        ];
    }

    private static IReadOnlyList<PreviewModeSpec> PreviewModes()
    {
        return
        [
            new PreviewModeSpec(OverlaySessionKind.Practice, "practice", "Practice"),
            new PreviewModeSpec(OverlaySessionKind.Qualifying, "qualifying", "Qualifying"),
            new PreviewModeSpec(OverlaySessionKind.Race, "race", "Race")
        ];
    }

    private static IReadOnlyList<PreviewModeSpec> PreviewModesForOverlay(string overlayId)
    {
        return string.Equals(overlayId, GapToLeaderOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase)
            ? PreviewModes().Where(mode => mode.Kind == OverlaySessionKind.Race).ToArray()
            : PreviewModes();
    }

    private static int NativeRefreshPassesFor(DesignV2LiveOverlayKind kind)
    {
        return kind switch
        {
            DesignV2LiveOverlayKind.InputState => 28,
            DesignV2LiveOverlayKind.GapToLeader => 42,
            DesignV2LiveOverlayKind.TrackMap => 3,
            DesignV2LiveOverlayKind.CarRadar => 4,
            _ => 4
        };
    }

    private static ApplicationSettings CreateApplicationSettings()
    {
        var settings = new ApplicationSettings();
        settings.General.FontFamily = ScreenshotFontFamily;
        settings.General.UnitSystem = "Metric";
        foreach (var definition in ManagedOverlayDefinitions())
        {
            settings.GetOrAddOverlay(
                definition.Id,
                definition.DefaultWidth,
                definition.DefaultHeight,
                defaultEnabled: false);
        }

        return settings;
    }

    private static RenderedScreenshot RenderForm(
        string outputRoot,
        string fileStem,
        string label,
        Func<Form> createForm,
        Action<Bitmap>? postProcess = null,
        int refreshPasses = 1,
        string relativeDirectory = "states",
        ScreenshotMetadata? metadata = null)
    {
        using var form = createForm();
        PrepareForm(form, refreshPasses);

        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        postProcess?.Invoke(bitmap);

        var directory = Path.Combine(outputRoot, relativeDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{fileStem}.png");
        bitmap.Save(path, ImageFormat.Png);
        return new RenderedScreenshot(label, path, form.ClientSize.Width, form.ClientSize.Height, CompleteMetadata(metadata, form, relativeDirectory));
    }

    private static RenderedScreenshot RenderFormCrop(
        string outputRoot,
        string relativeDirectory,
        string fileStem,
        string label,
        Func<Form> createForm,
        Rectangle cropBounds,
        int refreshPasses = 1,
        ScreenshotMetadata? metadata = null)
    {
        using var form = createForm();
        PrepareForm(form, refreshPasses);
        using var full = new Bitmap(form.ClientSize.Width, form.ClientSize.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(full, new Rectangle(Point.Empty, form.ClientSize));

        var boundedCrop = Rectangle.Intersect(new Rectangle(Point.Empty, form.ClientSize), cropBounds);
        if (boundedCrop.Width <= 0 || boundedCrop.Height <= 0)
        {
            throw new InvalidOperationException($"{label} crop is outside the rendered form bounds.");
        }

        using var bitmap = new Bitmap(boundedCrop.Width, boundedCrop.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(full, new Rectangle(Point.Empty, boundedCrop.Size), boundedCrop, GraphicsUnit.Pixel);
        }

        var directory = Path.Combine(outputRoot, relativeDirectory);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{fileStem}.png");
        bitmap.Save(path, ImageFormat.Png);
        return new RenderedScreenshot(label, path, bitmap.Width, bitmap.Height, CompleteMetadata(metadata, form, relativeDirectory));
    }

    private static void PrepareForm(Form form, int refreshPasses)
    {
        var targetClientSize = form.ClientSize;
        form.Location = new Point(-20000, -20000);
        form.CreateControl();
        CreateControlHandles(form);
        if (form.ClientSize != targetClientSize)
        {
            form.ClientSize = targetClientSize;
        }

        form.PerformLayout();
        Application.DoEvents();

        for (var pass = 0; pass < refreshPasses; pass++)
        {
            InvokeRefreshOverlay(form);
            Application.DoEvents();
        }

        if (form.ClientSize != targetClientSize)
        {
            form.ClientSize = targetClientSize;
            Application.DoEvents();
        }

        form.PerformLayout();
    }

    private static void CreateControlHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateControlHandles(child);
        }
    }

    private static void InvokeRefreshOverlay(Form form)
    {
        var method = form.GetType().GetMethod("RefreshOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(form, null);
    }

    private static void SelectTab(Control root, string selectedTabText)
    {
        foreach (var surface in Descendants(root).OfType<DesignV2SettingsSurface>())
        {
            surface.SelectTab(DesignV2TabId(selectedTabText));
            return;
        }

        foreach (var control in Descendants(root))
        {
            if (control is not TabControl tabs)
            {
                continue;
            }

            foreach (TabPage page in tabs.TabPages)
            {
                if (!string.Equals(page.Text, selectedTabText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                tabs.SelectedTab = page;
                return;
            }
        }
    }

    private static void SelectRegion(Control root, string selectedRegionText)
    {
        foreach (var surface in Descendants(root).OfType<DesignV2SettingsSurface>())
        {
            surface.SelectRegion(selectedRegionText);
            return;
        }
    }

    private static string DesignV2TabId(string selectedTabText)
    {
        return selectedTabText.Trim().ToLowerInvariant() switch
        {
            "general" => "general",
            "standings" => "standings",
            "relative" => "relative",
            "gap to leader" => "gap-to-leader",
            "track map" => "track-map",
            "stream chat" => "stream-chat",
            "garage cover" => "garage-cover",
            "fuel calculator" => "fuel-calculator",
            "inputs" => "input-state",
            "car radar" => "car-radar",
            "flags" => "flags",
            "session / weather" => "session-weather",
            "pit service" => "pit-service",
            "support" => "error-logging",
            _ => selectedTabText
        };
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void ReplaceColorWithReviewBackdrop(Bitmap bitmap, Color targetColor)
    {
        var target = targetColor.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var backdrop = ((x / 16) + (y / 16)) % 2 == 0
                    ? Color.FromArgb(20, 25, 29)
                    : Color.FromArgb(32, 38, 43);
                if (pixel.ToArgb() == target || pixel.A == 0)
                {
                    bitmap.SetPixel(x, y, backdrop);
                    continue;
                }

                if (pixel.A < 255)
                {
                    bitmap.SetPixel(x, y, CompositeOver(pixel, backdrop));
                }
            }
        }
    }

    private static Color CompositeOver(Color foreground, Color background)
    {
        var alpha = foreground.A / 255d;
        var inverseAlpha = 1d - alpha;
        return Color.FromArgb(
            255,
            (int)Math.Round(foreground.R * alpha + background.R * inverseAlpha),
            (int)Math.Round(foreground.G * alpha + background.G * inverseAlpha),
            (int)Math.Round(foreground.B * alpha + background.B * inverseAlpha));
    }

    private static void RenderContactSheet(string outputRoot, IReadOnlyList<RenderedScreenshot> screenshots)
    {
        var rows = (int)Math.Ceiling(screenshots.Count / (double)ContactSheetColumns);
        var width = ContactPadding * 2 + ContactCellWidth * ContactSheetColumns;
        var height = ContactPadding * 2 + ContactHeaderHeight + ContactCellHeight * rows;
        using var sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(sheet);
        graphics.Clear(OverlayTheme.Colors.SettingsBackground);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var titleFont = OverlayTheme.Font(ScreenshotFontFamily, 16f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        graphics.DrawString(
            $"Tech Mates Racing Overlay Windows screenshot parity - {AppVersionInfo.Current.InformationalVersion}",
            titleFont,
            titleBrush,
            ContactPadding,
            ContactPadding - 4);

        using var labelFont = OverlayTheme.Font(ScreenshotFontFamily, 9.5f, FontStyle.Bold);
        using var labelBrush = new SolidBrush(OverlayTheme.Colors.TextSecondary);
        using var cellBrush = new SolidBrush(OverlayTheme.Colors.PanelBackground);
        using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);

        for (var index = 0; index < screenshots.Count; index++)
        {
            var row = index / ContactSheetColumns;
            var column = index % ContactSheetColumns;
            var cell = new Rectangle(
                ContactPadding + column * ContactCellWidth,
                ContactPadding + ContactHeaderHeight + row * ContactCellHeight,
                ContactCellWidth - 18,
                ContactCellHeight - 18);
            graphics.FillRectangle(cellBrush, cell);
            graphics.DrawRectangle(borderPen, cell);
            graphics.DrawString(screenshots[index].Label, labelFont, labelBrush, cell.Left + 12, cell.Top + 10);

            using var image = Image.FromFile(screenshots[index].Path);
            var imageBounds = FitImage(
                image.Width,
                image.Height,
                new Rectangle(cell.Left + 12, cell.Top + 38, cell.Width - 24, cell.Height - 50));
            DrawTransparencyBackdrop(graphics, imageBounds);
            graphics.DrawImage(image, imageBounds);
        }

        sheet.Save(Path.Combine(outputRoot, "contact-sheet.png"), ImageFormat.Png);
    }

    private static ScreenshotMetadata SettingsMetadata(string? overlayId, string region, string? previewMode)
    {
        var tab = overlayId ?? (string.Equals(region, "support", StringComparison.Ordinal) ? "support" : "general");
        return new ScreenshotMetadata(
            Surface: "windows-settings",
            Renderer: "SettingsOverlayForm/DesignV2SettingsSurface",
            OverlayId: overlayId,
            Tab: tab,
            Region: region,
            PreviewMode: previewMode,
            Fixture: "deterministic-settings-fixture",
            SourceContract: "src/TmrOverlay.App/Overlays/SettingsPanel/DesignV2SettingsSurface.cs");
    }

    private static ScreenshotMetadata NativeOverlayMetadata(string overlayId, string previewMode)
    {
        return new ScreenshotMetadata(
            Surface: "windows-native-overlay",
            Renderer: nameof(DesignV2LiveOverlayForm),
            OverlayId: overlayId,
            PreviewMode: previewMode,
            Fixture: "SessionPreviewTelemetryFixtures",
            SourceContract: OverlayDefinitionSourceFor(overlayId));
    }

    private static ScreenshotMetadata CompleteMetadata(ScreenshotMetadata? metadata, Form form, string relativeDirectory)
    {
        var completed = metadata ?? new ScreenshotMetadata(
            Surface: relativeDirectory.Replace('\\', '/'),
            Fixture: "deterministic-telemetry-fixture");
        if (form is DesignV2LiveOverlayForm designV2)
        {
            completed = completed with
            {
                Status = designV2.DiagnosticStatus,
                Evidence = designV2.DiagnosticEvidence,
                Body = designV2.DiagnosticBodyKind,
                RadarShouldRender = designV2.DiagnosticRadarShouldRender,
                RadarSurfaceAlpha = designV2.DiagnosticRadarSurfaceAlpha,
                RadarCarCount = designV2.DiagnosticRadarCarCount,
                Layout = designV2.DiagnosticLayout
            };
        }

        return completed with
        {
            Renderer = completed.Renderer ?? form.GetType().FullName ?? form.GetType().Name
        };
    }

    private static string? OverlayIdForSettingsTab(string selectedTabText)
    {
        var tabId = DesignV2TabId(selectedTabText);
        return ManagedOverlayDefinitions().Any(definition => string.Equals(definition.Id, tabId, StringComparison.OrdinalIgnoreCase))
            ? tabId
            : null;
    }

    private static string? OverlayDefinitionSourceFor(string overlayId)
    {
        return overlayId switch
        {
            "standings" => "src/TmrOverlay.App/Overlays/Standings/StandingsOverlayDefinition.cs",
            "fuel-calculator" => "src/TmrOverlay.App/Overlays/FuelCalculator/FuelCalculatorOverlayDefinition.cs",
            "relative" => "src/TmrOverlay.App/Overlays/Relative/RelativeOverlayDefinition.cs",
            "track-map" => "src/TmrOverlay.App/Overlays/TrackMap/TrackMapOverlayDefinition.cs",
            "stream-chat" => "src/TmrOverlay.App/Overlays/StreamChat/StreamChatOverlayDefinition.cs",
            "flags" => "src/TmrOverlay.App/Overlays/Flags/FlagsOverlayDefinition.cs",
            "session-weather" => "src/TmrOverlay.App/Overlays/SessionWeather/SessionWeatherOverlayDefinition.cs",
            "pit-service" => "src/TmrOverlay.App/Overlays/PitService/PitServiceOverlayDefinition.cs",
            "input-state" => "src/TmrOverlay.App/Overlays/InputState/InputStateOverlayDefinition.cs",
            "car-radar" => "src/TmrOverlay.App/Overlays/CarRadar/CarRadarOverlayDefinition.cs",
            "gap-to-leader" => "src/TmrOverlay.App/Overlays/GapToLeader/GapToLeaderOverlayDefinition.cs",
            _ => null
        };
    }

    private static Rectangle FitImage(int imageWidth, int imageHeight, Rectangle bounds)
    {
        var scale = Math.Min(bounds.Width / (double)imageWidth, bounds.Height / (double)imageHeight);
        var width = Math.Max(1, (int)Math.Round(imageWidth * scale));
        var height = Math.Max(1, (int)Math.Round(imageHeight * scale));
        return new Rectangle(
            bounds.Left + (bounds.Width - width) / 2,
            bounds.Top + (bounds.Height - height) / 2,
            width,
            height);
    }

    private static void DrawTransparencyBackdrop(Graphics graphics, Rectangle bounds)
    {
        using var dark = new SolidBrush(Color.FromArgb(20, 25, 29));
        using var light = new SolidBrush(Color.FromArgb(32, 38, 43));
        const int cell = 16;
        for (var y = bounds.Top; y < bounds.Bottom; y += cell)
        {
            for (var x = bounds.Left; x < bounds.Right; x += cell)
            {
                var brush = ((x / cell) + (y / cell)) % 2 == 0 ? dark : light;
                graphics.FillRectangle(brush, x, y, Math.Min(cell, bounds.Right - x), Math.Min(cell, bounds.Bottom - y));
            }
        }
    }

    private static void WriteManifest(string outputRoot, IReadOnlyList<RenderedScreenshot> screenshots)
    {
        var manifest = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            version = AppVersionInfo.Current.InformationalVersion,
            screenshots = screenshots.Select(screenshot => new
            {
                label = screenshot.Label,
                path = Path.GetRelativePath(outputRoot, screenshot.Path).Replace('\\', '/'),
                width = screenshot.Width,
                height = screenshot.Height,
                bytes = new FileInfo(screenshot.Path).Length,
                surface = screenshot.Metadata.Surface,
                renderer = screenshot.Metadata.Renderer,
                sourceContract = screenshot.Metadata.SourceContract,
                overlayId = screenshot.Metadata.OverlayId,
                tab = screenshot.Metadata.Tab,
                region = screenshot.Metadata.Region,
                previewMode = screenshot.Metadata.PreviewMode,
                fixture = screenshot.Metadata.Fixture,
                status = screenshot.Metadata.Status,
                source = NativeSourceEvidence(screenshot.Metadata),
                bodyKind = NormalizedBodyKind(screenshot.Metadata.Body),
                shouldRender = NativeShouldRender(screenshot.Metadata),
                rowCount = NativeRowCount(screenshot.Metadata),
                metricCount = NativeMetricCount(screenshot.Metadata),
                flagCount = NativeFlagCount(screenshot.Metadata),
                radarShouldRender = screenshot.Metadata.RadarShouldRender,
                radarSurfaceAlpha = screenshot.Metadata.RadarSurfaceAlpha,
                radarCarCount = screenshot.Metadata.RadarCarCount,
                trackMapMarkerCount = NativeTrackMapMarkerCount(screenshot.Metadata),
                textSample = NativeTextSample(screenshot.Metadata),
                contentBounds = NativeContentBounds(screenshot.Metadata.Layout),
                layout = NativeLayoutEvidence(screenshot.Metadata.Layout),
                modelEvidence = NativeModelEvidence(screenshot.Metadata),
                metadata = new
                {
                    surface = screenshot.Metadata.Surface,
                    renderer = screenshot.Metadata.Renderer,
                    overlayId = screenshot.Metadata.OverlayId,
                    tab = screenshot.Metadata.Tab,
                    region = screenshot.Metadata.Region,
                    previewMode = screenshot.Metadata.PreviewMode,
                    fixture = screenshot.Metadata.Fixture,
                    sourceContract = screenshot.Metadata.SourceContract,
                    status = screenshot.Metadata.Status,
                    evidence = screenshot.Metadata.Evidence,
                    body = screenshot.Metadata.Body,
                    radarShouldRender = screenshot.Metadata.RadarShouldRender,
                    radarSurfaceAlpha = screenshot.Metadata.RadarSurfaceAlpha,
                    radarCarCount = screenshot.Metadata.RadarCarCount,
                    layout = screenshot.Metadata.Layout
                }
            })
        };
        File.WriteAllText(
            Path.Combine(outputRoot, "manifest.json"),
            $"{JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })}{Environment.NewLine}");
    }

    private static string? NativeSourceEvidence(ScreenshotMetadata metadata)
    {
        if (!string.Equals(metadata.Surface, "windows-native-overlay", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = new List<string> { "source: windows native preview" };
        if (!string.IsNullOrWhiteSpace(metadata.Evidence))
        {
            parts.Add($"evidence {metadata.Evidence}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.Fixture))
        {
            parts.Add($"fixture {metadata.Fixture}");
        }

        return string.Join(" | ", parts);
    }

    private static string? NormalizedBodyKind(string? body)
    {
        return body switch
        {
            "metric-rows" => "metrics",
            "radar" => "car-radar",
            "chat" => "stream-chat",
            _ => body
        };
    }

    private static bool? NativeShouldRender(ScreenshotMetadata metadata)
    {
        var body = metadata.Layout?.BodyLayout;
        if (body?.Vector is { } vector)
        {
            return vector.ShouldRender;
        }

        if (body?.Inputs is { } inputs)
        {
            return inputs.HasContent;
        }

        return body is not null ? true : null;
    }

    private static int NativeRowCount(ScreenshotMetadata metadata)
    {
        return metadata.Layout?.BodyLayout?.Rows.Count ?? 0;
    }

    private static int NativeMetricCount(ScreenshotMetadata metadata)
    {
        var body = metadata.Layout?.BodyLayout;
        if (body is null || body.Kind != "metric-rows")
        {
            return 0;
        }

        return body.MetricRows.Count + body.MetricGrids.Sum(grid => grid.Rows.Count);
    }

    private static int NativeFlagCount(ScreenshotMetadata metadata)
    {
        return metadata.Layout?.BodyLayout?.FlagCells.Count ?? 0;
    }

    private static int NativeTrackMapMarkerCount(ScreenshotMetadata metadata)
    {
        var body = metadata.Layout?.BodyLayout;
        return body?.Kind == "track-map"
            ? body.Vector?.ItemCount ?? 0
            : 0;
    }

    private static object? NativeContentBounds(DesignV2LayoutDiagnostics? layout)
    {
        var bounds = layout?.BodyLayout?.Bounds ?? layout?.Body;
        return bounds is { } rect ? RectEvidence(rect, includeAspectRatio: true) : null;
    }

    private static object? NativeLayoutEvidence(DesignV2LayoutDiagnostics? layout)
    {
        if (layout is null)
        {
            return null;
        }

        var elements = new List<object>();
        AddLayoutElement(elements, "header", 0, null, layout.Header, null, null);
        AddLayoutElement(elements, "content", 0, NativeBodyText(layout.BodyLayout), layout.BodyLayout?.Bounds ?? layout.Body, null, null);
        AddLayoutElement(elements, "footer", 0, null, layout.Footer, null, null);

        if (layout.BodyLayout is { } body)
        {
            foreach (var column in body.Columns)
            {
                AddLayoutElement(elements, "column", column.Index, column.Label, column.Bounds, null, null);
            }

            foreach (var row in body.Rows.Take(80))
            {
                AddLayoutElement(elements, row.Kind, row.Index, RowText(row), row.Bounds, row.Foreground, row.Background);
                foreach (var cell in row.Cells)
                {
                    AddLayoutElement(
                        elements,
                        "cell",
                        cell.ColumnIndex,
                        cell.Text,
                        cell.Bounds,
                        cell.Foreground,
                        cell.Background);
                }
            }

            foreach (var row in body.MetricRows)
            {
                AddLayoutElement(elements, "metric", elements.Count, $"{row.Label} {row.Value}", row.Bounds, row.Foreground, row.Background);
                foreach (var segment in row.Segments)
                {
                    AddLayoutElement(
                        elements,
                        "metric-segment",
                        segment.Index,
                        $"{segment.Label} {segment.Value}",
                        segment.Bounds,
                        segment.Foreground,
                        segment.Background);
                }
            }

            foreach (var grid in body.MetricGrids)
            {
                AddLayoutElement(elements, "metric-grid", elements.Count, grid.Title, grid.Bounds, null, null);
                foreach (var row in grid.Rows)
                {
                    AddLayoutElement(elements, row.Kind, row.Index, RowText(row), row.Bounds, row.Foreground, row.Background);
                    foreach (var cell in row.Cells)
                    {
                        AddLayoutElement(elements, "metric-grid-cell", cell.ColumnIndex, cell.Text, cell.Bounds, cell.Foreground, cell.Background);
                    }
                }
            }

            if (body.Graph is { } graph)
            {
                AddLayoutElement(elements, "graph-frame", 0, graph.ComparisonLabel, graph.Frame, null, null);
                AddLayoutElement(elements, "graph-plot", 0, null, graph.Plot, null, null);
                AddLayoutElement(elements, "graph-label-lane", 0, null, graph.LabelLane, null, null);
                AddLayoutElement(elements, "graph-metrics-table", 0, null, graph.MetricsTable, null, null);
            }

            if (body.Inputs is { } inputs)
            {
                AddLayoutElement(elements, "input-graph", 0, null, inputs.Graph, null, null);
                AddLayoutElement(elements, "input-rail", 0, null, inputs.Rail, null, null);
                foreach (var item in inputs.Items)
                {
                    AddLayoutElement(elements, $"input-{item.Kind}", elements.Count, item.Kind, item.Bounds, null, null);
                }
            }

            if (body.Vector is { } vector)
            {
                AddLayoutElement(elements, $"{body.Kind}-vector", 0, null, vector.Target, null, null);
                foreach (var primitive in vector.Primitives)
                {
                    AddLayoutElement(elements, $"{body.Kind}-primitive-{primitive.Kind}", elements.Count, primitive.Kind, primitive.Bounds, primitive.Stroke, primitive.Fill);
                }

                foreach (var item in vector.Items)
                {
                    AddLayoutElement(elements, $"{body.Kind}-{item.Kind}", elements.Count, item.Id?.ToString() ?? item.Label, item.Bounds, item.Stroke, item.Fill);
                }

                foreach (var label in vector.Labels)
                {
                    AddLayoutElement(elements, $"{body.Kind}-label", elements.Count, label.Text, label.Bounds, label.Color, null);
                }
            }

            foreach (var cell in body.FlagCells)
            {
                AddLayoutElement(elements, "flag-cell", cell.Index, cell.Kind, cell.Bounds, null, null);
                AddLayoutElement(elements, "flag-cloth", cell.Index, cell.Kind, cell.ClothBounds, null, null);
            }
        }

        return new
        {
            contract = "windows-native-layout/v1",
            root = RectEvidence(layout.Client),
            contentBounds = NativeContentBounds(layout),
            elements
        };
    }

    private static void AddLayoutElement(
        List<object> elements,
        string role,
        int index,
        string? text,
        DesignV2LayoutRect? bounds,
        string? foreground,
        string? background)
    {
        if (bounds is not { } rect)
        {
            return;
        }

        elements.Add(new
        {
            role,
            index,
            tag = "native",
            className = role,
            text = string.IsNullOrWhiteSpace(text) ? null : text,
            bounds = RectEvidence(rect),
            styles = NativeStyleEvidence(foreground, background)
        });
    }

    private static object NativeStyleEvidence(string? foreground, string? background)
    {
        return new
        {
            color = foreground,
            backgroundColor = background,
            borderColor = (string?)null,
            fontFamily = ScreenshotFontFamily,
            fontSize = (string?)null,
            fontWeight = (string?)null,
            display = "native"
        };
    }

    private static object? NativeModelEvidence(ScreenshotMetadata metadata)
    {
        var body = metadata.Layout?.BodyLayout;
        if (body is null)
        {
            return null;
        }

        return new
        {
            contract = "overlay-model-layout-evidence/v1",
            bodyKind = NormalizedBodyKind(body.Kind),
            nativeBodyKind = body.Kind,
            state = body.State,
            columns = body.Columns.Select(ColumnEvidence).ToArray(),
            rows = body.Rows.Take(80).Select(RowEvidence).ToArray(),
            metrics = body.MetricRows.Select(MetricEvidence).ToArray(),
            metricSections = MetricSectionEvidence(body),
            gridSections = body.MetricGrids.Select(GridSectionEvidence).ToArray(),
            graph = GraphEvidence(body.Graph),
            inputs = InputsEvidence(body.Inputs),
            flags = body.FlagCells.Count > 0
                ? new
                {
                    count = body.FlagCells.Count,
                    kinds = body.FlagCells.Select(flag => flag.Kind).ToArray(),
                    gridColumns = body.GridColumns,
                    gridRows = body.GridRows,
                    cells = body.FlagCells.Select(FlagCellEvidence).ToArray()
                }
                : null,
            carRadar = body.Kind == "radar" && body.Vector is { } radar
                ? CarRadarEvidence(radar)
                : null,
            trackMap = body.Kind == "track-map" && body.Vector is { } trackMap
                ? TrackMapEvidence(trackMap)
                : null
        };
    }

    private static object ColumnEvidence(DesignV2LayoutColumn column)
    {
        return new
        {
            index = column.Index,
            label = column.Label,
            dataKey = (string?)null,
            configuredWidth = column.ConfiguredWidth,
            renderedWidth = column.RenderedWidth,
            alignment = column.Alignment,
            bounds = RectEvidence(column.Bounds)
        };
    }

    private static object RowEvidence(DesignV2LayoutRow row)
    {
        return new
        {
            index = row.Index,
            sourceIndex = row.SourceIndex,
            kind = row.Kind,
            isReference = string.Equals(row.Kind, "reference", StringComparison.Ordinal),
            isPartial = string.Equals(row.Evidence, "Partial", StringComparison.OrdinalIgnoreCase),
            classColorHex = row.ClassColorHex,
            relativeLapDelta = row.RelativeLapDelta,
            evidence = row.Evidence,
            text = row.Text,
            detail = row.Detail,
            foreground = row.Foreground,
            background = row.Background,
            bounds = RectEvidence(row.Bounds),
            cells = row.Cells.Select(cell => cell.Text).ToArray(),
            renderedCells = row.Cells.Select(CellEvidence).ToArray()
        };
    }

    private static object CellEvidence(DesignV2LayoutCell cell)
    {
        return new
        {
            columnIndex = cell.ColumnIndex,
            column = cell.ColumnLabel,
            text = cell.Text,
            value = cell.Text,
            alignment = cell.Alignment,
            tone = ToneFromEvidence(cell.Evidence),
            evidence = cell.Evidence,
            foreground = cell.Foreground,
            background = cell.Background,
            bounds = RectEvidence(cell.Bounds)
        };
    }

    private static object MetricEvidence(DesignV2LayoutMetricRow row)
    {
        return new
        {
            label = row.Label,
            value = row.Value,
            tone = ToneFromEvidence(row.Evidence),
            rowColorHex = row.Accent,
            section = row.Section,
            evidence = row.Evidence,
            foreground = row.Foreground,
            background = row.Background,
            accentHex = row.Accent,
            bounds = RectEvidence(row.Bounds),
            labelBounds = RectEvidence(row.LabelBounds),
            valueBounds = RectEvidence(row.ValueBounds),
            sectionTitleBounds = row.SectionTitleBounds is { } sectionTitleBounds
                ? RectEvidence(sectionTitleBounds)
                : null,
            segments = row.Segments.Select(SegmentEvidence).ToArray()
        };
    }

    private static object SegmentEvidence(DesignV2LayoutMetricSegment segment)
    {
        return new
        {
            index = segment.Index,
            label = segment.Label,
            value = segment.Value,
            tone = ToneFromEvidence(segment.Evidence),
            accentHex = segment.Accent,
            rotationDegrees = segment.RotationDegrees,
            evidence = segment.Evidence,
            foreground = segment.Foreground,
            background = segment.Background,
            bounds = RectEvidence(segment.Bounds),
            labelBounds = RectEvidence(segment.LabelBounds),
            valueBounds = RectEvidence(segment.ValueBounds)
        };
    }

    private static object[] MetricSectionEvidence(DesignV2LayoutBody body)
    {
        return body.MetricRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Section))
            .GroupBy(row => row.Section!)
            .Select(section => new
            {
                title = section.Key,
                rows = section.Select(MetricEvidence).ToArray()
            })
            .ToArray<object>();
    }

    private static object GridSectionEvidence(DesignV2LayoutMetricGrid grid)
    {
        return new
        {
            title = grid.Title,
            bounds = RectEvidence(grid.Bounds),
            headers = grid.Headers.Select(header => header.Text).ToArray(),
            renderedHeaders = grid.Headers.Select(CellEvidence).ToArray(),
            rows = grid.Rows.Select(row => new
            {
                index = row.Index,
                label = row.Text,
                tone = ToneFromEvidence(row.Evidence),
                evidence = row.Evidence,
                foreground = row.Foreground,
                background = row.Background,
                bounds = RectEvidence(row.Bounds),
                cells = row.Cells.Select(cell => new
                {
                    value = cell.Text,
                    tone = ToneFromEvidence(cell.Evidence),
                    evidence = cell.Evidence,
                    foreground = cell.Foreground,
                    background = cell.Background,
                    bounds = RectEvidence(cell.Bounds)
                }).ToArray()
            }).ToArray()
        };
    }

    private static object? GraphEvidence(DesignV2LayoutGraph? graph)
    {
        if (graph is null)
        {
            return null;
        }

        return new
        {
            startSeconds = graph.StartSeconds,
            endSeconds = graph.EndSeconds,
            maxGapSeconds = graph.MaxGapSeconds,
            lapReferenceSeconds = graph.LapReferenceSeconds,
            selectedSeriesCount = graph.SeriesCount,
            metricDeadbandSeconds = (double?)null,
            comparisonLabel = graph.ComparisonLabel,
            canvasBounds = RectEvidence(graph.Frame),
            series = graph.Series.Select((series, index) => new
            {
                index,
                carIdx = series.CarIdx,
                classPosition = series.ClassPosition,
                isReference = series.IsReference,
                isClassLeader = series.IsClassLeader,
                alpha = series.Alpha,
                isStickyExit = series.IsStickyExit,
                isStale = series.IsStale,
                pointCount = series.PointCount,
                baseColor = series.BaseColor,
                renderedColor = series.RenderedColor,
                effectiveAlpha = series.EffectiveAlpha,
                strokeWidth = series.StrokeWidth,
                isDashed = series.IsDashed,
                endpointLabel = series.EndpointLabel,
                latestPoint = series.LatestPoint is { } latestPoint ? PointEvidence(latestPoint) : null,
                points = series.Points.Select(GraphPointEvidence).ToArray()
            }).ToArray(),
            trendMetricCount = graph.TrendMetricCount,
            trendMetrics = graph.MetricRows.Select(GraphMetricRowEvidence).ToArray(),
            weatherCount = graph.WeatherBands.Count,
            markerCount = graph.Markers.Count,
            geometry = new
            {
                frame = RectEvidence(graph.Frame),
                plot = RectEvidence(graph.Plot),
                axis = RectEvidence(graph.Axis),
                labelLane = RectEvidence(graph.LabelLane),
                metricsTable = graph.MetricsTable is { } metricsTable ? RectEvidence(metricsTable) : null,
                scale = graph.Scale,
                aheadSeconds = graph.AheadSeconds,
                behindSeconds = graph.BehindSeconds,
                latestReferenceGapSeconds = graph.LatestReferenceGapSeconds,
                weatherBands = graph.WeatherBands.Select(BandEvidence).ToArray(),
                markers = graph.Markers.Select(MarkerEvidence).ToArray(),
                metricRows = graph.MetricRows.Select(GraphMetricRowEvidence).ToArray(),
                series = graph.Series.Select(GraphSeriesEvidence).ToArray()
            }
        };
    }

    private static object GraphSeriesEvidence(DesignV2LayoutGraphSeries series, int drawIndex)
    {
        return new
        {
            sourceIndex = series.Index,
            drawIndex,
            drawPriority = series.DrawPriority,
            carIdx = series.CarIdx,
            classPosition = series.ClassPosition,
            isReference = series.IsReference,
            isClassLeader = series.IsClassLeader,
            pointCount = series.PointCount,
            baseColor = series.BaseColor,
            renderedColor = series.RenderedColor,
            alpha = series.Alpha,
            effectiveAlpha = series.EffectiveAlpha,
            strokeWidth = series.StrokeWidth,
            isDashed = series.IsDashed,
            isStickyExit = series.IsStickyExit,
            isStale = series.IsStale,
            endpointLabel = series.EndpointLabel,
            latestPoint = series.LatestPoint is { } latestPoint ? PointEvidence(latestPoint) : null,
            points = series.Points.Select(GraphPointEvidence).ToArray()
        };
    }

    private static object GraphPointEvidence(DesignV2LayoutGraphPoint point)
    {
        return new
        {
            axisSeconds = point.AxisSeconds,
            gapSeconds = point.GapSeconds,
            startsSegment = point.StartsSegment,
            point = PointEvidence(point.Point)
        };
    }

    private static object BandEvidence(DesignV2LayoutGraphBand band)
    {
        return new
        {
            kind = band.Kind,
            startAxisSeconds = band.StartAxisSeconds,
            endAxisSeconds = band.EndAxisSeconds,
            bounds = RectEvidence(band.Bounds),
            color = band.Color
        };
    }

    private static object MarkerEvidence(DesignV2LayoutGraphMarker marker)
    {
        return new
        {
            kind = marker.Kind,
            label = marker.Label,
            axisSeconds = marker.AxisSeconds,
            gapSeconds = marker.GapSeconds,
            carIdx = marker.CarIdx,
            isReference = marker.IsReference,
            start = PointEvidence(marker.Start),
            end = PointEvidence(marker.End),
            color = marker.Color
        };
    }

    private static object GraphMetricRowEvidence(DesignV2LayoutRow row)
    {
        return new
        {
            index = row.Index,
            text = row.Text,
            state = row.Evidence,
            bounds = RectEvidence(row.Bounds),
            cells = row.Cells.Select(cell => new
            {
                column = cell.ColumnLabel,
                text = cell.Text,
                foreground = cell.Foreground,
                bounds = RectEvidence(cell.Bounds)
            }).ToArray()
        };
    }

    private static object? InputsEvidence(DesignV2LayoutInputs? inputs)
    {
        if (inputs is null)
        {
            return null;
        }

        return new
        {
            hasContent = inputs.HasContent,
            hasGraph = inputs.Graph is not null,
            hasRail = inputs.Rail is not null,
            isAvailable = inputs.HasContent,
            sampleIntervalMilliseconds = (int?)null,
            maximumTracePoints = (int?)null,
            tracePointCount = inputs.TracePointCount,
            graph = inputs.Graph is { } graph
                ? new
                {
                    bounds = RectEvidence(graph),
                    gridLines = inputs.GridLines.Select(LineEvidence).ToArray(),
                    series = inputs.TraceSeries.Select(TraceSeriesEvidence).ToArray()
                }
                : null,
            rail = inputs.Rail is { } rail
                ? new
                {
                    bounds = RectEvidence(rail),
                    railWidth = inputs.RailWidth,
                    items = inputs.Items.Select(item => new
                    {
                        kind = item.Kind,
                        bounds = RectEvidence(item.Bounds)
                    }).ToArray()
                }
                : null
        };
    }

    private static object LineEvidence(DesignV2LayoutLine line)
    {
        return new
        {
            kind = line.Kind,
            start = PointEvidence(line.Start),
            end = PointEvidence(line.End),
            color = line.Color,
            strokeWidth = line.StrokeWidth
        };
    }

    private static object TraceSeriesEvidence(DesignV2LayoutInputTraceSeries series)
    {
        return new
        {
            kind = series.Kind,
            color = series.Color,
            strokeWidth = series.StrokeWidth,
            pointCount = series.Points.Count,
            curveCount = series.Curves.Count,
            points = series.Points.Select(PointEvidence).ToArray(),
            curves = series.Curves.Select(curve => new
            {
                start = PointEvidence(curve.Start),
                control1 = PointEvidence(curve.Control1),
                control2 = PointEvidence(curve.Control2),
                end = PointEvidence(curve.End)
            }).ToArray()
        };
    }

    private static object FlagCellEvidence(DesignV2LayoutFlagCell cell)
    {
        return new
        {
            index = cell.Index,
            row = cell.Row,
            column = cell.Column,
            kind = cell.Kind,
            bounds = RectEvidence(cell.Bounds),
            clothBounds = RectEvidence(cell.ClothBounds)
        };
    }

    private static object CarRadarEvidence(DesignV2LayoutVector vector)
    {
        return new
        {
            shouldRender = vector.ShouldRender,
            width = vector.SourceWidth,
            height = vector.SourceHeight,
            targetBounds = RectEvidence(vector.Target),
            scaleX = vector.ScaleX,
            scaleY = vector.ScaleY,
            carCount = vector.ItemCount,
            itemCount = vector.ItemCount,
            primitiveCount = vector.PrimitiveCount,
            labelCount = vector.LabelCount,
            ringCount = vector.Primitives.Count(primitive => string.Equals(primitive.Kind, "ring", StringComparison.Ordinal)),
            surfaceAlpha = vector.SurfaceAlpha,
            items = vector.Items.Select(VectorItemEvidence).ToArray(),
            primitives = vector.Primitives.Select(VectorPrimitiveEvidence).ToArray(),
            labels = vector.Labels.Select(VectorLabelEvidence).ToArray()
        };
    }

    private static object TrackMapEvidence(DesignV2LayoutVector vector)
    {
        return new
        {
            markerCount = vector.ItemCount,
            primitiveCount = vector.PrimitiveCount,
            width = vector.SourceWidth,
            height = vector.SourceHeight,
            targetBounds = RectEvidence(vector.Target),
            scaleX = vector.ScaleX,
            scaleY = vector.ScaleY,
            shouldRender = vector.ShouldRender,
            items = vector.Items.Select(VectorItemEvidence).ToArray(),
            primitives = vector.Primitives.Select(VectorPrimitiveEvidence).ToArray(),
            labels = vector.Labels.Select(VectorLabelEvidence).ToArray()
        };
    }

    private static object VectorItemEvidence(DesignV2LayoutVectorItem item)
    {
        return new
        {
            kind = item.Kind,
            id = item.Id,
            bounds = RectEvidence(item.Bounds),
            fill = item.Fill,
            stroke = item.Stroke,
            strokeWidth = item.StrokeWidth,
            label = item.Label,
            labelColor = item.LabelColor,
            alertKind = item.AlertKind,
            alertRingBounds = item.AlertRingBounds is { } alertRingBounds
                ? RectEvidence(alertRingBounds)
                : null,
            alertRingStroke = item.AlertRingStroke,
            alertRingStrokeWidth = item.AlertRingStrokeWidth
        };
    }

    private static object VectorPrimitiveEvidence(DesignV2LayoutVectorPrimitive primitive)
    {
        return new
        {
            kind = primitive.Kind,
            bounds = primitive.Bounds is { } bounds ? RectEvidence(bounds) : null,
            points = primitive.Points.Select(PointEvidence).ToArray(),
            closed = primitive.Closed,
            startDegrees = primitive.StartDegrees,
            sweepDegrees = primitive.SweepDegrees,
            fill = primitive.Fill,
            stroke = primitive.Stroke,
            strokeWidth = primitive.StrokeWidth
        };
    }

    private static object VectorLabelEvidence(DesignV2LayoutVectorLabel label)
    {
        return new
        {
            text = label.Text,
            bounds = RectEvidence(label.Bounds),
            fontSize = label.FontSize,
            bold = label.Bold,
            alignment = label.Alignment,
            color = label.Color
        };
    }

    private static string? NativeTextSample(ScreenshotMetadata metadata)
    {
        if (!string.Equals(metadata.Surface, "windows-native-overlay", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = new List<string>();
        AddText(parts, metadata.OverlayId);
        AddText(parts, metadata.Status);
        AddText(parts, metadata.Evidence);
        AddText(parts, metadata.Body);
        AddText(parts, NativeBodyText(metadata.Layout?.BodyLayout));

        var sample = string.Join(" ", parts)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        while (sample.Contains("  ", StringComparison.Ordinal))
        {
            sample = sample.Replace("  ", " ", StringComparison.Ordinal);
        }

        return sample.Length == 0 ? null : sample[..Math.Min(sample.Length, 512)];
    }

    private static string? NativeBodyText(DesignV2LayoutBody? body)
    {
        if (body is null)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var row in body.Rows.Take(12))
        {
            AddText(parts, RowText(row));
        }

        foreach (var row in body.MetricRows.Take(12))
        {
            AddText(parts, row.Label);
            AddText(parts, row.Value);
            foreach (var segment in row.Segments)
            {
                AddText(parts, segment.Label);
                AddText(parts, segment.Value);
            }
        }

        foreach (var grid in body.MetricGrids.Take(4))
        {
            AddText(parts, grid.Title);
            foreach (var row in grid.Rows.Take(6))
            {
                AddText(parts, RowText(row));
            }
        }

        if (body.Graph is { } graph)
        {
            AddText(parts, graph.ComparisonLabel);
            foreach (var series in graph.Series.Take(8))
            {
                AddText(parts, series.EndpointLabel);
            }

            foreach (var row in graph.MetricRows.Take(8))
            {
                AddText(parts, RowText(row));
            }
        }

        if (body.Inputs is { } inputs)
        {
            foreach (var item in inputs.Items)
            {
                AddText(parts, item.Kind);
            }

            foreach (var series in inputs.TraceSeries)
            {
                AddText(parts, series.Kind);
            }
        }

        foreach (var flag in body.FlagCells)
        {
            AddText(parts, flag.Kind);
        }

        if (body.Vector is { } vector)
        {
            foreach (var primitive in vector.Primitives.Take(24))
            {
                AddText(parts, primitive.Kind);
            }

            foreach (var item in vector.Items.Take(16))
            {
                AddText(parts, item.Kind);
                AddText(parts, item.Id?.ToString());
                AddText(parts, item.Label);
            }

            foreach (var label in vector.Labels.Take(16))
            {
                AddText(parts, label.Text);
            }
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string? RowText(DesignV2LayoutRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Text) || !string.IsNullOrWhiteSpace(row.Detail))
        {
            return string.Join(" ", new[] { row.Text, row.Detail }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        return row.Cells.Count == 0
            ? null
            : string.Join(" ", row.Cells.Select(cell => cell.Text).Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AddText(List<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }

    private static string? ToneFromEvidence(string? evidence)
    {
        var token = evidence?.Trim().ToLowerInvariant();
        return token switch
        {
            null or "" => null,
            "live" => "info",
            "ok" or "good" => "success",
            "critical" => "error",
            _ => token
        };
    }

    private static object RectEvidence(DesignV2LayoutRect rect, bool includeAspectRatio = false)
    {
        if (!includeAspectRatio)
        {
            return new
            {
                x = rect.X,
                y = rect.Y,
                width = rect.Width,
                height = rect.Height
            };
        }

        return new
        {
            x = rect.X,
            y = rect.Y,
            width = rect.Width,
            height = rect.Height,
            aspectRatio = rect.Height > 0f
                ? Math.Round(rect.Width / rect.Height, 4)
                : (double?)null
        };
    }

    private static object PointEvidence(DesignV2LayoutPoint point)
    {
        return new
        {
            x = point.X,
            y = point.Y
        };
    }

    private static AppStorageOptions StorageOptionsFor(string root)
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

    private static OverlaySettings OverlaySettingsFor(OverlayDefinition definition, int? width = null, int? height = null)
    {
        var settings = new OverlaySettings
        {
            Id = definition.Id,
            Enabled = true,
            Width = width ?? definition.DefaultWidth,
            Height = height ?? definition.DefaultHeight,
            Opacity = 1d,
            AlwaysOnTop = false
        };
        foreach (var option in definition.SettingsOptions)
        {
            if (option.Kind == OverlaySettingsOptionKind.Boolean)
            {
                settings.SetBooleanOption(option.Key, option.BooleanDefault);
            }
            else if (option.Kind == OverlaySettingsOptionKind.Integer)
            {
                settings.SetIntegerOption(option.Key, option.IntegerDefault, option.Minimum, option.Maximum);
            }
        }

        settings.SetBooleanOption(OverlayOptionKeys.FlagsShowGreen, true);
        settings.SetBooleanOption(OverlayOptionKeys.FlagsShowBlue, true);
        settings.SetBooleanOption(OverlayOptionKeys.FlagsShowYellow, true);
        settings.SetBooleanOption(OverlayOptionKeys.FlagsShowCritical, true);
        settings.SetBooleanOption(OverlayOptionKeys.FlagsShowFinish, true);
        return settings;
    }

    private static void Noop()
    {
    }

    private sealed record RenderedScreenshot(
        string Label,
        string Path,
        int Width,
        int Height,
        ScreenshotMetadata Metadata);

    private sealed record ScreenshotMetadata(
        string Surface,
        string? Renderer = null,
        string? OverlayId = null,
        string? Tab = null,
        string? Region = null,
        string? PreviewMode = null,
        string? Fixture = null,
        string? SourceContract = null,
        string? Status = null,
        string? Evidence = null,
        string? Body = null,
        bool? RadarShouldRender = null,
        double? RadarSurfaceAlpha = null,
        int? RadarCarCount = null,
        DesignV2LayoutDiagnostics? Layout = null);

    private sealed record SettingsRegionSpec(string Id, string Label);

    private sealed record PreviewModeSpec(OverlaySessionKind Kind, string FileStem, string Label);

    private sealed record NativeOverlaySpec(
        DesignV2LiveOverlayKind Kind,
        OverlayDefinition Definition,
        bool UsesTransparentBackdrop = false);

    public sealed record FixtureFrame(int Index);

    private sealed class SequenceTelemetrySource : ILiveTelemetrySource
    {
        private readonly Func<FixtureFrame, LiveTelemetrySnapshot> _snapshotFactory;
        private int _index;

        public SequenceTelemetrySource(Func<FixtureFrame, LiveTelemetrySnapshot> snapshotFactory)
        {
            _snapshotFactory = snapshotFactory;
        }

        public LiveTelemetrySnapshot Snapshot()
        {
            return _snapshotFactory(new FixtureFrame(_index++));
        }
    }

    private sealed class TelemetryFixture
    {
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.Parse("2026-05-03T15:00:00Z");

        public ILiveTelemetrySource SourceFor(Func<FixtureFrame, LiveTelemetrySnapshot> snapshotFactory)
        {
            return new SequenceTelemetrySource(snapshotFactory);
        }

        public LiveTelemetrySnapshot CreateSnapshot(
            FixtureFrame frame,
            int sessionFlags,
            bool pitServiceActive = false,
            double? throttle = null,
            double? brake = null,
            double? clutch = null,
            double? steeringWheelAngle = null,
            double? sessionTime = null,
            double? focusF2TimeSeconds = null,
            DateTimeOffset? capturedAtUtc = null)
        {
            var now = DateTimeOffset.UtcNow;
            var context = CreateContext();
            var sessionSeconds = sessionTime ?? 5210d + frame.Index * 0.2d;
            var lapDist = 0.420d + frame.Index * 0.00002d;
            if (lapDist > 0.98d)
            {
                lapDist = 0.420d;
            }

            var focusGap = focusF2TimeSeconds ?? 36.4d + Math.Sin(frame.Index / 7d) * 0.4d;
            var sample = new HistoricalTelemetrySample(
                CapturedAtUtc: capturedAtUtc ?? now,
                SessionTime: sessionSeconds,
                SessionTick: 120000 + frame.Index,
                SessionInfoUpdate: 4,
                IsOnTrack: true,
                IsInGarage: false,
                OnPitRoad: pitServiceActive,
                PitstopActive: pitServiceActive,
                PlayerCarInPitStall: pitServiceActive,
                FuelLevelLiters: 64.2d,
                FuelLevelPercent: 0.58d,
                FuelUsePerHourKg: 92d,
                SpeedMetersPerSecond: 61.4d,
                Lap: 18,
                LapCompleted: 17,
                LapDistPct: lapDist,
                LapLastLapTimeSeconds: 129.8d,
                LapBestLapTimeSeconds: 127.6d,
                AirTempC: 21.3d,
                TrackTempCrewC: 30.8d,
                TrackWetness: 1,
                WeatherDeclaredWet: false,
                PlayerTireCompound: 0,
                Skies: 1,
                PrecipitationPercent: 12d,
                WindVelocityMetersPerSecond: 4.2d,
                WindDirectionRadians: 4.32d,
                RelativeHumidityPercent: 67d,
                FogLevelPercent: 0d,
                AirPressurePa: 94_600d,
                SessionTimeRemain: 9090d,
                SessionTimeTotal: 14400d,
                SessionState: 4,
                SessionFlags: sessionFlags,
                RaceLaps: 112,
                PlayerCarIdx: 5,
                FocusCarIdx: 5,
                FocusLapCompleted: 17,
                FocusLapDistPct: lapDist,
                FocusF2TimeSeconds: focusGap,
                FocusEstimatedTimeSeconds: 54.0d,
                FocusLastLapTimeSeconds: 129.8d,
                FocusBestLapTimeSeconds: 127.6d,
                FocusPosition: 12,
                FocusClassPosition: 6,
                FocusCarClass: 4098,
                FocusOnPitRoad: pitServiceActive,
                FocusTrackSurface: pitServiceActive ? 2 : 3,
                TeamLapCompleted: 17,
                TeamLapDistPct: lapDist,
                TeamF2TimeSeconds: focusGap,
                TeamEstimatedTimeSeconds: 54.0d,
                TeamLastLapTimeSeconds: 129.8d,
                TeamBestLapTimeSeconds: 127.6d,
                TeamPosition: 12,
                TeamClassPosition: 6,
                TeamCarClass: 4098,
                LeaderCarIdx: 1,
                LeaderLapCompleted: 18,
                LeaderLapDistPct: 0.63d,
                LeaderF2TimeSeconds: 0d,
                LeaderEstimatedTimeSeconds: 0d,
                LeaderLastLapTimeSeconds: 126.4d,
                LeaderBestLapTimeSeconds: 125.9d,
                ClassLeaderCarIdx: 2,
                ClassLeaderLapCompleted: 18,
                ClassLeaderLapDistPct: 0.49d,
                ClassLeaderF2TimeSeconds: 0d,
                ClassLeaderEstimatedTimeSeconds: 17.1d,
                ClassLeaderLastLapTimeSeconds: 127.1d,
                ClassLeaderBestLapTimeSeconds: 126.2d,
                FocusClassLeaderCarIdx: 2,
                FocusClassLeaderLapCompleted: 18,
                FocusClassLeaderLapDistPct: 0.49d,
                FocusClassLeaderF2TimeSeconds: 0d,
                FocusClassLeaderEstimatedTimeSeconds: 17.1d,
                FocusClassLeaderLastLapTimeSeconds: 127.1d,
                FocusClassLeaderBestLapTimeSeconds: 126.2d,
                PlayerTrackSurface: pitServiceActive ? 2 : 3,
                CarLeftRight: 4,
                NearbyCars: NearbyCars(lapDist),
                ClassCars: SameClassCars(lapDist, focusGap),
                FocusClassCars: SameClassCars(lapDist, focusGap),
                TeamOnPitRoad: pitServiceActive,
                TeamFastRepairsUsed: 0,
                PitServiceFlags: pitServiceActive ? 0x1f : 0x10,
                PitServiceFuelLiters: 48.5d,
                PitRepairLeftSeconds: pitServiceActive ? 7.2d : null,
                PitOptRepairLeftSeconds: pitServiceActive ? 18.0d : null,
                TireSetsUsed: 2,
                FastRepairUsed: 0,
                DriversSoFar: frame.Index > 16 ? 2 : 1,
                DriverChangeLapStatus: 0,
                LapCurrentLapTimeSeconds: 74.2d,
                LapDeltaToBestLapSeconds: -0.21d,
                LapDeltaToBestLapRate: -0.003d,
                LapDeltaToBestLapOk: true,
                Gear: 4,
                Rpm: 7250d + Math.Sin(frame.Index / 3d) * 380d,
                Throttle: throttle ?? 0.72d,
                Brake: brake ?? 0.08d,
                Clutch: clutch ?? 0d,
                SteeringWheelAngle: steeringWheelAngle ?? 0.18d,
                EngineWarnings: 0,
                Voltage: 13.8d,
                WaterTempC: 88d,
                FuelPressureBar: 4.1d,
                OilTempC: 96d,
                OilPressureBar: 5.4d);

            var fuel = LiveFuelSnapshot.From(context, sample);
            var proximity = LiveProximitySnapshot.From(context, sample);
            var leaderGap = LiveLeaderGapSnapshot.From(sample);
            var models = LiveRaceModelBuilder.From(context, sample, fuel, proximity, leaderGap);
            return LiveTelemetrySnapshot.Empty with
            {
                IsConnected = true,
                IsCollecting = true,
                SourceId = "windows-screenshot-fixture",
                StartedAtUtc = StartedAtUtc,
                LastUpdatedAtUtc = now,
                Sequence = 1000 + frame.Index,
                Context = context,
                Combo = HistoricalComboIdentity.From(context),
                LatestSample = sample,
                Fuel = fuel,
                Proximity = proximity,
                LeaderGap = leaderGap,
                CompletedStintCount = 2,
                Models = models
            };
        }

        private static HistoricalSessionContext CreateContext()
        {
            return new HistoricalSessionContext
            {
                Car = new HistoricalCarIdentity
                {
                    CarId = 156,
                    CarPath = "mercedesamgevogt3",
                    CarScreenName = "Mercedes-AMG GT3 2020",
                    CarScreenNameShort = "Mercedes AMG GT3",
                    CarClassId = 4098,
                    CarClassShortName = "GT3",
                    DriverCarFuelMaxLiters = 104d,
                    DriverCarFuelKgPerLiter = 0.75d,
                    DriverCarEstLapTimeSeconds = 129d
                },
                Track = new HistoricalTrackIdentity
                {
                    TrackId = 262,
                    TrackName = "nurburgring combined",
                    TrackDisplayName = "Nurburgring Combined",
                    TrackConfigName = "Gesamtstrecke 24h",
                    TrackLengthKm = 25.378d,
                    TrackNumTurns = 170,
                    TrackType = "road"
                },
                Session = new HistoricalSessionIdentity
                {
                    CurrentSessionNum = 2,
                    SessionNum = 2,
                    SessionType = "Race",
                    SessionName = "Endurance",
                    SessionTime = "14400 sec",
                    SessionLaps = "unlimited",
                    EventType = "Race",
                    TeamRacing = true,
                    Official = true,
                    SessionId = 90210,
                    SubSessionId = 90211
                },
                Conditions = new HistoricalSessionInfoConditions
                {
                    TrackWeatherType = "Constant",
                    TrackSkies = "Partly Cloudy",
                    TrackPrecipitationPercent = 0d,
                    SessionTrackRubberState = "Moderate Usage"
                },
                Drivers =
                [
                    Driver(1, "Olivia Grant", "001", 4099, "GT4", "#48A868"),
                    Driver(2, "Noah Park", "002", 4098, "GT3", "#2D7DFF"),
                    Driver(3, "Alex Novak", "003", 4098, "GT3", "#2D7DFF"),
                    Driver(4, "Maya Rossi", "004", 4098, "GT3", "#2D7DFF"),
                    Driver(5, "Taylor Morgan", "005", 4098, "GT3", "#2D7DFF"),
                    Driver(6, "Kai Meyer", "006", 4098, "GT3", "#2D7DFF"),
                    Driver(7, "Samira Patel", "007", 4098, "GT3", "#2D7DFF"),
                    Driver(21, "Priya Shah", "021", 4101, "P2", "#D84B4B")
                ],
                Sectors =
                [
                    new HistoricalTrackSector { SectorNum = 0, SectorStartPct = 0d },
                    new HistoricalTrackSector { SectorNum = 1, SectorStartPct = 0.33d },
                    new HistoricalTrackSector { SectorNum = 2, SectorStartPct = 0.66d }
                ]
            };
        }

        private static HistoricalSessionDriver Driver(
            int carIdx,
            string name,
            string carNumber,
            int classId,
            string className,
            string colorHex)
        {
            return new HistoricalSessionDriver
            {
                CarIdx = carIdx,
                UserName = name,
                AbbrevName = name,
                Initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => part[0])),
                UserId = 100000 + carIdx,
                TeamId = 200000 + carIdx,
                TeamName = carIdx == 5 ? "TMR" : $"Team {carNumber}",
                CarNumber = carNumber,
                CarClassId = classId,
                CarClassShortName = className,
                CarClassColorHex = colorHex,
                IsSpectator = false
            };
        }

        private static IReadOnlyList<HistoricalCarProximity> NearbyCars(double referenceLapDist)
        {
            return
            [
                ProximityCar(3, referenceLapDist + 0.0060d, 33.1d, 54.9d, 9, 4, 4098),
                ProximityCar(4, referenceLapDist + 0.0010d, 35.0d, 54.4d, 10, 5, 4098),
                ProximityCar(6, referenceLapDist - 0.0008d, 37.9d, 53.6d, 13, 7, 4098),
                ProximityCar(7, referenceLapDist - 0.0040d, 42.4d, 51.2d, 14, 8, 4098),
                ProximityCar(21, referenceLapDist - 0.0011d, 32.0d, 52.8d, 6, 2, 4101)
            ];
        }

        private static IReadOnlyList<HistoricalCarProximity> SameClassCars(double referenceLapDist, double focusGap)
        {
            return
            [
                ProximityCar(2, referenceLapDist + 0.0700d, 0d, 17.1d, 2, 1, 4098),
                ProximityCar(3, referenceLapDist + 0.0060d, Math.Max(0d, focusGap - 5.8d), 54.9d, 9, 4, 4098),
                ProximityCar(4, referenceLapDist + 0.0010d, Math.Max(0d, focusGap - 1.4d), 54.4d, 10, 5, 4098),
                ProximityCar(6, referenceLapDist - 0.0008d, focusGap + 2.6d, 53.6d, 13, 7, 4098),
                ProximityCar(7, referenceLapDist - 0.0040d, focusGap + 6.9d, 51.2d, 14, 8, 4098)
            ];
        }

        private static HistoricalCarProximity ProximityCar(
            int carIdx,
            double lapDistPct,
            double? f2TimeSeconds,
            double? estimatedTimeSeconds,
            int position,
            int classPosition,
            int carClass)
        {
            var normalizedLapPct = lapDistPct;
            while (normalizedLapPct < 0d)
            {
                normalizedLapPct += 1d;
            }

            while (normalizedLapPct > 1d)
            {
                normalizedLapPct -= 1d;
            }

            return new HistoricalCarProximity(
                CarIdx: carIdx,
                LapCompleted: 17,
                LapDistPct: normalizedLapPct,
                F2TimeSeconds: f2TimeSeconds,
                EstimatedTimeSeconds: estimatedTimeSeconds,
                Position: position,
                ClassPosition: classPosition,
                CarClass: carClass,
                TrackSurface: 3,
                OnPitRoad: false);
        }
    }
}
