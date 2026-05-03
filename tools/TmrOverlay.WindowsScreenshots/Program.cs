using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.History;
using TmrOverlay.App.Overlays.CarRadar;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SettingsPanel;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Status;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
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

        screenshots.Add(RenderForm(
            outputRoot,
            "settings-general",
            "Settings - General",
            () => CreateSettingsForm("General")));
        screenshots.Add(RenderForm(
            outputRoot,
            "settings-relative",
            "Settings - Relative",
            () => CreateSettingsForm("Relative")));
        screenshots.Add(RenderForm(
            outputRoot,
            "settings-support",
            "Settings - Support",
            () => CreateSettingsForm("Support")));
        screenshots.Add(RenderForm(
            outputRoot,
            "status-live-analysis",
            "Collector Status",
            CreateStatusForm));
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
            "flags-blue",
            "Flags",
            () => new FlagsOverlayForm(
                fixture.SourceFor(frame => fixture.CreateSnapshot(frame, sessionFlags: 0x00000020)),
                NullLogger<SimpleTelemetryOverlayForm>.Instance,
                new AppPerformanceState(),
                OverlaySettingsFor(FlagsOverlayDefinition.Definition, width: 960, height: 540),
                Noop),
            postProcess: bitmap => ReplaceColorWithTransparency(bitmap, Color.Magenta)));
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
                PitServiceOverlayViewModel.From,
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

        return screenshots;
    }

    private static SettingsOverlayForm CreateSettingsForm(string selectedTabText)
    {
        var storage = StorageOptionsFor(Path.Combine(Path.GetTempPath(), "tmr-overlay-windows-screenshots", Guid.NewGuid().ToString("N")));
        var captureState = new TelemetryCaptureState();
        captureState.SetCaptureRoot(storage.CaptureRoot);
        captureState.MarkConnected();
        captureState.MarkCollectionStarted(DateTimeOffset.UtcNow);
        captureState.RecordFrame(DateTimeOffset.UtcNow);
        var performanceState = new AppPerformanceState();
        var diagnostics = new DiagnosticsBundleService(
            storage,
            new LiveModelParityOptions(),
            new LiveOverlayDiagnosticsOptions(),
            captureState,
            performanceState,
            new AppPerformanceSnapshotRecorder(storage),
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
            storage,
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
        return form;
    }

    private static StatusOverlayForm CreateStatusForm()
    {
        var state = new TelemetryCaptureState();
        state.SetCaptureRoot(Path.Combine(Path.GetTempPath(), "tmr-overlay-windows-screenshots", "captures"));
        state.MarkConnected();
        state.MarkCollectionStarted(DateTimeOffset.UtcNow);
        for (var index = 0; index < 1842; index++)
        {
            state.RecordFrame(DateTimeOffset.UtcNow);
        }

        return new StatusOverlayForm(
            state,
            new AppPerformanceState(),
            OverlaySettingsFor(StatusOverlayDefinition.Definition),
            ScreenshotFontFamily,
            Noop);
    }

    private static IReadOnlyList<OverlayDefinition> ManagedOverlayDefinitions()
    {
        return
        [
            StatusOverlayDefinition.Definition,
            FuelCalculatorOverlayDefinition.Definition,
            RelativeOverlayDefinition.Definition,
            FlagsOverlayDefinition.Definition,
            SessionWeatherOverlayDefinition.Definition,
            PitServiceOverlayDefinition.Definition,
            InputStateOverlayDefinition.Definition,
            CarRadarOverlayDefinition.Definition,
            GapToLeaderOverlayDefinition.Definition
        ];
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
        int refreshPasses = 1)
    {
        using var form = createForm();
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
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height, PixelFormat.Format32bppArgb);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        postProcess?.Invoke(bitmap);

        var path = Path.Combine(outputRoot, "states", $"{fileStem}.png");
        bitmap.Save(path, ImageFormat.Png);
        return new RenderedScreenshot(label, path, form.ClientSize.Width, form.ClientSize.Height);
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

    private static void ReplaceColorWithTransparency(Bitmap bitmap, Color transparentColor)
    {
        var target = transparentColor.ToArgb();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == target)
                {
                    bitmap.SetPixel(x, y, Color.Transparent);
                }
            }
        }
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
        var lines = new List<string>
        {
            "{",
            $"  \"generatedAtUtc\": \"{DateTimeOffset.UtcNow:O}\",",
            $"  \"version\": \"{EscapeJson(AppVersionInfo.Current.InformationalVersion)}\",",
            "  \"screenshots\": ["
        };

        for (var index = 0; index < screenshots.Count; index++)
        {
            var screenshot = screenshots[index];
            var comma = index == screenshots.Count - 1 ? string.Empty : ",";
            lines.Add("    {");
            lines.Add($"      \"label\": \"{EscapeJson(screenshot.Label)}\",");
            lines.Add($"      \"path\": \"states/{EscapeJson(Path.GetFileName(screenshot.Path))}\",");
            lines.Add($"      \"width\": {screenshot.Width},");
            lines.Add($"      \"height\": {screenshot.Height}");
            lines.Add($"    }}{comma}");
        }

        lines.Add("  ]");
        lines.Add("}");
        File.WriteAllLines(Path.Combine(outputRoot, "manifest.json"), lines);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
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
            Opacity = definition.ShowOpacityControl ? 0.88d : 1d,
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

    private sealed record RenderedScreenshot(string Label, string Path, int Width, int Height);

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
                TrackWetness: 0,
                WeatherDeclaredWet: false,
                PlayerTireCompound: 0,
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
                    Driver(1, "Overall Leader", "001", 4099, "GT4", "#48A868"),
                    Driver(2, "Class Leader", "002", 4098, "GT3", "#2D7DFF"),
                    Driver(3, "A. Novak", "003", 4098, "GT3", "#2D7DFF"),
                    Driver(4, "M. Rossi", "004", 4098, "GT3", "#2D7DFF"),
                    Driver(5, "TMR Driver", "005", 4098, "GT3", "#2D7DFF"),
                    Driver(6, "K. Meyer", "006", 4098, "GT3", "#2D7DFF"),
                    Driver(7, "S. Patel", "007", 4098, "GT3", "#2D7DFF"),
                    Driver(21, "Prototype Traffic", "021", 4101, "P2", "#D84B4B")
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
