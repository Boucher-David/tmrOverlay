using TmrOverlay.Core.Overlays;
using TmrOverlay.App.Overlays.CarRadar;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SettingsPanel;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Status;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;
using TmrOverlay.App.History;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Analysis;

namespace TmrOverlay.App.Overlays;

internal sealed class OverlayManager : IDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private readonly AppStorageOptions _storageOptions;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly TelemetryCaptureState _telemetryCaptureState;
    private readonly TelemetryEdgeCaseOptions _telemetryEdgeCaseOptions;
    private readonly LiveModelParityOptions _liveModelParityOptions;
    private readonly LiveOverlayDiagnosticsOptions _liveOverlayDiagnosticsOptions;
    private readonly PostRaceAnalysisOptions _postRaceAnalysisOptions;
    private readonly AppPerformanceState _performanceState;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly AppEventRecorder _events;
    private readonly ILogger<CarRadarForm> _carRadarLogger;
    private readonly ILogger<GapToLeaderForm> _gapToLeaderLogger;
    private readonly ILogger<RelativeForm> _relativeLogger;
    private readonly ILogger<SimpleTelemetryOverlayForm> _simpleTelemetryLogger;
    private readonly Dictionary<string, Form> _forms = [];
    private readonly Dictionary<string, double> _appliedScales = [];
    private readonly System.Windows.Forms.Timer _sessionVisibilityTimer;
    private ApplicationSettings? _settings;
    private string? _appliedFontFamily;
    private string? _appliedUnitSystem;
    private bool _radarSettingsPreviewVisible;
    private bool _startupShown;

    public event EventHandler? ApplicationExitRequested;

    public OverlayManager(
        AppSettingsStore settingsStore,
        AppStorageOptions storageOptions,
        DiagnosticsBundleService diagnosticsBundleService,
        TelemetryCaptureState telemetryCaptureState,
        TelemetryEdgeCaseOptions telemetryEdgeCaseOptions,
        LiveModelParityOptions liveModelParityOptions,
        LiveOverlayDiagnosticsOptions liveOverlayDiagnosticsOptions,
        PostRaceAnalysisOptions postRaceAnalysisOptions,
        AppPerformanceState performanceState,
        ILiveTelemetrySource liveTelemetrySource,
        SessionHistoryQueryService historyQueryService,
        AppEventRecorder events,
        ILogger<CarRadarForm> carRadarLogger,
        ILogger<GapToLeaderForm> gapToLeaderLogger,
        ILogger<RelativeForm> relativeLogger,
        ILogger<SimpleTelemetryOverlayForm> simpleTelemetryLogger)
    {
        _settingsStore = settingsStore;
        _storageOptions = storageOptions;
        _diagnosticsBundleService = diagnosticsBundleService;
        _telemetryCaptureState = telemetryCaptureState;
        _telemetryEdgeCaseOptions = telemetryEdgeCaseOptions;
        _liveModelParityOptions = liveModelParityOptions;
        _liveOverlayDiagnosticsOptions = liveOverlayDiagnosticsOptions;
        _postRaceAnalysisOptions = postRaceAnalysisOptions;
        _performanceState = performanceState;
        _liveTelemetrySource = liveTelemetrySource;
        _historyQueryService = historyQueryService;
        _events = events;
        _carRadarLogger = carRadarLogger;
        _gapToLeaderLogger = gapToLeaderLogger;
        _relativeLogger = relativeLogger;
        _simpleTelemetryLogger = simpleTelemetryLogger;

        _sessionVisibilityTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _sessionVisibilityTimer.Tick += (_, _) => ApplyOverlaySettings();
    }

    public void ShowStartupOverlays()
    {
        if (_startupShown)
        {
            return;
        }

        _settings ??= _settingsStore.Load();
        EnsureManagedOverlaySettings();
        OpenSettingsOverlay();
        ApplyOverlaySettings();
        SaveSettings();
        _sessionVisibilityTimer.Start();
        _startupShown = true;
    }

    public void OpenSettingsOverlay()
    {
        _settings ??= _settingsStore.Load();
        EnsureManagedOverlaySettings();
        var defaultLocation = CenteredDefaultLocation(SettingsOverlayDefinition.Definition);
        var settings = _settings.GetOrAddOverlay(
            SettingsOverlayDefinition.Definition.Id,
            SettingsOverlayDefinition.Definition.DefaultWidth,
            SettingsOverlayDefinition.Definition.DefaultHeight,
            defaultLocation.X,
            defaultLocation.Y);
        var settingsSizeChanged = EnsureSettingsOverlayFixedSize(settings);
        CenterSettingsOverlay(settings);

        var form = EnsureForm(
            SettingsOverlayDefinition.Definition.Id,
            () => new SettingsOverlayForm(
                _settings,
                ManagedOverlayDefinitions,
                _telemetryCaptureState,
                _telemetryEdgeCaseOptions,
                _liveModelParityOptions,
                _liveOverlayDiagnosticsOptions,
                _postRaceAnalysisOptions,
                _performanceState,
                _storageOptions,
                _diagnosticsBundleService,
                _events,
                settings,
                SaveSettings,
                ApplyOverlaySettings,
                RequestApplicationExit,
                SelectSettingsOverlayTab));
        form.Location = new Point(settings.X, settings.Y);
        form.ClientSize = new Size(
            SettingsOverlayDefinition.Definition.DefaultWidth,
            SettingsOverlayDefinition.Definition.DefaultHeight);
        form.Opacity = 1d;
        form.TopMost = false;
        if (!form.Visible)
        {
            form.Show();
        }

        if (settingsSizeChanged)
        {
            SaveSettings();
        }

        form.Activate();
    }

    public void Dispose()
    {
        _sessionVisibilityTimer.Stop();
        _sessionVisibilityTimer.Dispose();

        foreach (var form in _forms.Values.ToArray())
        {
            form.Close();
            form.Dispose();
        }

        _forms.Clear();
        _appliedScales.Clear();
    }

    private IReadOnlyList<OverlayDefinition> ManagedOverlayDefinitions =>
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

    private IReadOnlyList<OverlayRegistration> ManagedOverlayRegistrations =>
    [
        new OverlayRegistration(
            StatusOverlayDefinition.Definition,
            settings => new StatusOverlayForm(
                _telemetryCaptureState,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            24,
            24),
        new OverlayRegistration(
            FuelCalculatorOverlayDefinition.Definition,
            settings => new FuelCalculatorForm(
                _liveTelemetrySource,
                _historyQueryService,
                _performanceState,
                settings,
                SelectedFontFamily,
                SelectedUnitSystem,
                SaveSettings),
            24,
            190),
        new OverlayRegistration(
            RelativeOverlayDefinition.Definition,
            settings => new RelativeForm(
                _liveTelemetrySource,
                _relativeLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            24,
            530),
        new OverlayRegistration(
            FlagsOverlayDefinition.Definition,
            settings => new FlagsOverlayForm(
                _liveTelemetrySource,
                _simpleTelemetryLogger,
                _performanceState,
                settings,
                SaveSettings),
            0,
            0),
        new OverlayRegistration(
            SessionWeatherOverlayDefinition.Definition,
            settings => new SimpleTelemetryOverlayForm(
                SessionWeatherOverlayDefinition.Definition,
                _liveTelemetrySource,
                _simpleTelemetryLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SelectedUnitSystem,
                new SimpleTelemetryOverlayMetrics(
                    AppPerformanceMetricIds.OverlaySessionWeatherRefresh,
                    AppPerformanceMetricIds.OverlaySessionWeatherSnapshot,
                    AppPerformanceMetricIds.OverlaySessionWeatherViewModel,
                    AppPerformanceMetricIds.OverlaySessionWeatherApplyUi,
                    AppPerformanceMetricIds.OverlaySessionWeatherRows,
                    AppPerformanceMetricIds.OverlaySessionWeatherPaint),
                SessionWeatherOverlayViewModel.From,
                SaveSettings),
            1070,
            24),
        new OverlayRegistration(
            PitServiceOverlayDefinition.Definition,
            settings => new SimpleTelemetryOverlayForm(
                PitServiceOverlayDefinition.Definition,
                _liveTelemetrySource,
                _simpleTelemetryLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SelectedUnitSystem,
                new SimpleTelemetryOverlayMetrics(
                    AppPerformanceMetricIds.OverlayPitServiceRefresh,
                    AppPerformanceMetricIds.OverlayPitServiceSnapshot,
                    AppPerformanceMetricIds.OverlayPitServiceViewModel,
                    AppPerformanceMetricIds.OverlayPitServiceApplyUi,
                    AppPerformanceMetricIds.OverlayPitServiceRows,
                    AppPerformanceMetricIds.OverlayPitServicePaint),
                PitServiceOverlayViewModel.From,
                SaveSettings),
            1070,
            320),
        new OverlayRegistration(
            InputStateOverlayDefinition.Definition,
            settings => new InputStateOverlayForm(
                _liveTelemetrySource,
                _simpleTelemetryLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SelectedUnitSystem,
                SaveSettings),
            1070,
            590),
        new OverlayRegistration(
            CarRadarOverlayDefinition.Definition,
            settings => new CarRadarForm(
                _liveTelemetrySource,
                _carRadarLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            650,
            24),
        new OverlayRegistration(
            GapToLeaderOverlayDefinition.Definition,
            settings => new GapToLeaderForm(
                _liveTelemetrySource,
                _gapToLeaderLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            650,
            260)
    ];

    private void EnsureManagedOverlaySettings()
    {
        foreach (var registration in ManagedOverlayRegistrations)
        {
            var overlay = _settings!.GetOrAddOverlay(
                registration.Definition.Id,
                registration.Definition.DefaultWidth,
                registration.Definition.DefaultHeight,
                registration.DefaultX,
                registration.DefaultY,
                defaultEnabled: false);
            overlay.Scale = Math.Clamp(overlay.Scale, 0.6d, 2d);
            if (overlay.Width <= 0 || overlay.Height <= 0)
            {
                overlay.Width = ScaleDimension(registration.Definition.DefaultWidth, overlay.Scale);
                overlay.Height = ScaleDimension(registration.Definition.DefaultHeight, overlay.Scale);
            }

            ApplyGapToLeaderRaceOnlyPolicy(registration.Definition, overlay);
            ApplyFlagsBorderPolicy(registration.Definition, overlay);
        }
    }

    private void ApplyOverlaySettings()
    {
        if (_settings is null)
        {
            return;
        }

        RecreateManagedFormsIfFontChanged();
        RecreateManagedFormsIfUnitsChanged();
        var currentSession = CurrentSessionKind();
        foreach (var registration in ManagedOverlayRegistrations)
        {
            var settings = _settings.GetOrAddOverlay(
                registration.Definition.Id,
                registration.Definition.DefaultWidth,
                registration.Definition.DefaultHeight,
                registration.DefaultX,
                registration.DefaultY,
                defaultEnabled: false);
            var settingsPreview = _radarSettingsPreviewVisible
                && string.Equals(registration.Definition.Id, CarRadarOverlayDefinition.Definition.Id, StringComparison.Ordinal);
            var shouldShow = settingsPreview || (settings.Enabled && IsAllowedForSession(registration.Definition, settings, currentSession));

            if (!shouldShow)
            {
                if (_forms.TryGetValue(registration.Definition.Id, out var hiddenForm))
                {
                    ApplyRadarSettingsPreview(hiddenForm, previewVisible: false);
                    hiddenForm.Hide();
                }

                continue;
            }

            var form = EnsureForm(
                registration.Definition.Id,
                () => registration.Create(settings));
            ApplyScaleIfChanged(registration.Definition, settings, form);
            ApplyOpacityIfChanged(registration.Definition, settings, form);
            ApplyRadarSettingsPreview(form, settingsPreview);
            if (!form.Visible)
            {
                form.Show();
            }
        }
    }

    private string SelectedFontFamily =>
        string.IsNullOrWhiteSpace(_settings?.General.FontFamily)
            ? "Segoe UI"
            : _settings.General.FontFamily;

    private string SelectedUnitSystem =>
        string.Equals(_settings?.General.UnitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
            ? "Imperial"
            : "Metric";

    private Form EnsureForm(string overlayId, Func<Form> createForm)
    {
        if (_forms.TryGetValue(overlayId, out var existing) && !existing.IsDisposed)
        {
            return existing;
        }

        var form = createForm();
        _forms[overlayId] = form;
        return form;
    }

    private void SaveSettings()
    {
        if (_settings is not null)
        {
            _settingsStore.Save(_settings);
        }
    }

    private void RequestApplicationExit()
    {
        ApplicationExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SelectSettingsOverlayTab(string? overlayId)
    {
        var radarPreviewVisible = false;
        if (string.Equals(overlayId, CarRadarOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            var radarSettings = _settings?.GetOrAddOverlay(
                CarRadarOverlayDefinition.Definition.Id,
                CarRadarOverlayDefinition.Definition.DefaultWidth,
                CarRadarOverlayDefinition.Definition.DefaultHeight,
                defaultEnabled: false);
            radarPreviewVisible = radarSettings?.Enabled == true;
        }
        if (_radarSettingsPreviewVisible == radarPreviewVisible)
        {
            return;
        }

        _radarSettingsPreviewVisible = radarPreviewVisible;
        ApplyOverlaySettings();
    }

    private static void ApplyRadarSettingsPreview(Form form, bool previewVisible)
    {
        if (form is CarRadarForm radar)
        {
            radar.SetSettingsPreviewVisible(previewVisible);
        }
    }

    private void ApplyScaleIfChanged(OverlayDefinition definition, OverlaySettings settings, Form form)
    {
        if (string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            ApplyFlagsBorderPolicy(definition, settings);
            var size = FlagsOverlayDefinition.ResolveSize(settings);
            settings.Width = size.Width;
            settings.Height = size.Height;
            if (form.ClientSize != size)
            {
                form.ClientSize = size;
            }
            var location = new Point(settings.X, settings.Y);
            if (form.Location != location)
            {
                form.Location = location;
            }

            _appliedScales[definition.Id] = settings.Scale;
            return;
        }

        settings.Scale = Math.Clamp(settings.Scale, 0.6d, 2d);
        if (_appliedScales.TryGetValue(definition.Id, out var appliedScale)
            && Math.Abs(appliedScale - settings.Scale) < 0.001d)
        {
            return;
        }

        form.ClientSize = new Size(
            ScaleDimension(definition.DefaultWidth, settings.Scale),
            ScaleDimension(definition.DefaultHeight, settings.Scale));
        _appliedScales[definition.Id] = settings.Scale;
    }

    private static void ApplyOpacityIfChanged(OverlayDefinition definition, OverlaySettings settings, Form form)
    {
        if (!definition.ShowOpacityControl)
        {
            return;
        }

        settings.Opacity = Math.Clamp(settings.Opacity, 0.2d, 1d);
        if (Math.Abs(form.Opacity - settings.Opacity) > 0.001d)
        {
            form.Opacity = settings.Opacity;
        }
    }

    private void RecreateManagedFormsIfFontChanged()
    {
        var fontFamily = SelectedFontFamily;
        if (_appliedFontFamily is null)
        {
            _appliedFontFamily = fontFamily;
            return;
        }

        if (string.Equals(_appliedFontFamily, fontFamily, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var definition in ManagedOverlayDefinitions)
        {
            if (!_forms.Remove(definition.Id, out var form))
            {
                continue;
            }

            form.Close();
            form.Dispose();
            _appliedScales.Remove(definition.Id);
        }

        _appliedFontFamily = fontFamily;
    }

    private void RecreateManagedFormsIfUnitsChanged()
    {
        var unitSystem = SelectedUnitSystem;
        if (_appliedUnitSystem is null)
        {
            _appliedUnitSystem = unitSystem;
            return;
        }

        if (string.Equals(_appliedUnitSystem, unitSystem, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var definition in ManagedOverlayDefinitions)
        {
            if (!_forms.Remove(definition.Id, out var form))
            {
                continue;
            }

            form.Close();
            form.Dispose();
            _appliedScales.Remove(definition.Id);
        }

        _appliedUnitSystem = unitSystem;
    }

    private OverlaySessionKind? CurrentSessionKind()
    {
        var context = _liveTelemetrySource.Snapshot().Context;
        return ClassifySession(
            context.Session.SessionType
            ?? context.Session.SessionName
            ?? context.Session.EventType);
    }

    private static OverlaySessionKind? ClassifySession(string? sessionType)
    {
        if (string.IsNullOrWhiteSpace(sessionType))
        {
            return null;
        }

        var normalized = sessionType.ToLowerInvariant();
        if (normalized.Contains("test", StringComparison.Ordinal))
        {
            return OverlaySessionKind.Test;
        }

        if (normalized.Contains("practice", StringComparison.Ordinal))
        {
            return OverlaySessionKind.Practice;
        }

        if (normalized.Contains("qual", StringComparison.Ordinal))
        {
            return OverlaySessionKind.Qualifying;
        }

        if (normalized.Contains("race", StringComparison.Ordinal))
        {
            return OverlaySessionKind.Race;
        }

        return null;
    }

    private static bool IsAllowedForSession(OverlayDefinition definition, OverlaySettings settings, OverlaySessionKind? sessionKind)
    {
        if (string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal)
            && sessionKind is null)
        {
            return false;
        }

        return sessionKind switch
        {
            OverlaySessionKind.Test => settings.ShowInTest,
            OverlaySessionKind.Practice => settings.ShowInPractice,
            OverlaySessionKind.Qualifying => settings.ShowInQualifying,
            OverlaySessionKind.Race => settings.ShowInRace,
            _ => true
        };
    }

    private static int ScaleDimension(int defaultDimension, double scale)
    {
        return Math.Max(80, (int)Math.Round(defaultDimension * Math.Clamp(scale, 0.6d, 2d)));
    }

    private static bool EnsureSettingsOverlayFixedSize(OverlaySettings settings)
    {
        var changed = false;
        var width = SettingsOverlayDefinition.Definition.DefaultWidth;
        var height = SettingsOverlayDefinition.Definition.DefaultHeight;
        if (settings.Width != width || settings.Height != height)
        {
            settings.Width = width;
            settings.Height = height;
            changed = true;
        }

        if (Math.Abs(settings.Opacity - 1d) > 0.001d)
        {
            settings.Opacity = 1d;
            changed = true;
        }

        if (settings.AlwaysOnTop)
        {
            settings.AlwaysOnTop = false;
            changed = true;
        }

        return changed;
    }

    private static void CenterSettingsOverlay(OverlaySettings settings)
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        var width = SettingsOverlayDefinition.Definition.DefaultWidth;
        var height = SettingsOverlayDefinition.Definition.DefaultHeight;
        settings.X = area.Left + Math.Max(0, (area.Width - width) / 2);
        settings.Y = area.Top + Math.Max(0, (area.Height - height) / 2);
    }

    private static void ApplyGapToLeaderRaceOnlyPolicy(OverlayDefinition definition, OverlaySettings settings)
    {
        if (!string.Equals(definition.Id, GapToLeaderOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return;
        }

        settings.ShowInTest = false;
        settings.ShowInPractice = false;
        settings.ShowInQualifying = false;
        settings.ShowInRace = true;
        settings.SetBooleanOption(OverlayOptionKeys.GapRaceOnlyDefaultApplied, true);
    }

    private static void ApplyFlagsBorderPolicy(OverlayDefinition definition, OverlaySettings settings)
    {
        if (!string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return;
        }

        var shouldApplyScreenDefault =
            string.Equals(settings.ScreenId, FlagsOverlayDefinition.PrimaryScreenDefaultId, StringComparison.Ordinal)
            || settings.Width <= 0
            || settings.Height <= 0
            || settings.Width < 900
            || settings.Height < 500
            || (settings.Width == definition.DefaultWidth
                && settings.Height == definition.DefaultHeight
                && string.IsNullOrWhiteSpace(settings.ScreenId));
        if (!shouldApplyScreenDefault)
        {
            return;
        }

        var area = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, definition.DefaultWidth, definition.DefaultHeight);
        var targetFrame = FlagsOverlayDefinition.DefaultFrameForScreen(area);
        settings.Scale = Math.Clamp(targetFrame.Width / (double)definition.DefaultWidth, 0.6d, 2d);
        settings.Width = targetFrame.Width;
        settings.Height = targetFrame.Height;
        settings.X = targetFrame.Left;
        settings.Y = targetFrame.Top;
        settings.ScreenId = FlagsOverlayDefinition.PrimaryScreenDefaultId;
    }

    private static Point CenteredDefaultLocation(OverlayDefinition definition)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        return new Point(
            area.Left + Math.Max(0, (area.Width - definition.DefaultWidth) / 2),
            area.Top + Math.Max(0, (area.Height - definition.DefaultHeight) / 2));
    }

    private sealed record OverlayRegistration(
        OverlayDefinition Definition,
        Func<OverlaySettings, Form> Create,
        int DefaultX,
        int DefaultY);

    private enum OverlaySessionKind
    {
        Test,
        Practice,
        Qualifying,
        Race
    }
}
