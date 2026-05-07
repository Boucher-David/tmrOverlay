using TmrOverlay.Core.Overlays;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.CarRadar;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.SettingsPanel;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;
using TmrOverlay.App.History;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.TrackMaps;
using TmrOverlay.App.Updates;

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
    private readonly ReleaseUpdateService _releaseUpdates;
    private readonly LocalhostOverlayOptions _localhostOverlayOptions;
    private readonly LocalhostOverlayState _localhostOverlayState;
    private readonly TrackMapStore _trackMapStore;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly AppEventRecorder _events;
    private readonly ILogger<CarRadarForm> _carRadarLogger;
    private readonly ILogger<GapToLeaderForm> _gapToLeaderLogger;
    private readonly ILogger<RelativeForm> _relativeLogger;
    private readonly ILogger<StandingsForm> _standingsLogger;
    private readonly ILogger<TrackMapForm> _trackMapLogger;
    private readonly ILogger<StreamChatForm> _streamChatLogger;
    private readonly ILogger<SimpleTelemetryOverlayForm> _simpleTelemetryLogger;
    private readonly Dictionary<string, Form> _forms = [];
    private readonly Dictionary<string, double> _appliedScales = [];
    private readonly Dictionary<string, double> _appliedOpacities = [];
    private readonly System.Windows.Forms.Timer _sessionVisibilityTimer;
    private ApplicationSettings? _settings;
    private string? _appliedFontFamily;
    private string? _appliedUnitSystem;
    private Form? _settingsZOrderForm;
    private bool _radarSettingsPreviewVisible;
    private bool _settingsOverlayActive;
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
        ReleaseUpdateService releaseUpdates,
        LocalhostOverlayOptions localhostOverlayOptions,
        LocalhostOverlayState localhostOverlayState,
        TrackMapStore trackMapStore,
        ILiveTelemetrySource liveTelemetrySource,
        SessionHistoryQueryService historyQueryService,
        AppEventRecorder events,
        ILogger<CarRadarForm> carRadarLogger,
        ILogger<GapToLeaderForm> gapToLeaderLogger,
        ILogger<RelativeForm> relativeLogger,
        ILogger<StandingsForm> standingsLogger,
        ILogger<TrackMapForm> trackMapLogger,
        ILogger<StreamChatForm> streamChatLogger,
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
        _releaseUpdates = releaseUpdates;
        _localhostOverlayOptions = localhostOverlayOptions;
        _localhostOverlayState = localhostOverlayState;
        _trackMapStore = trackMapStore;
        _liveTelemetrySource = liveTelemetrySource;
        _historyQueryService = historyQueryService;
        _events = events;
        _carRadarLogger = carRadarLogger;
        _gapToLeaderLogger = gapToLeaderLogger;
        _relativeLogger = relativeLogger;
        _standingsLogger = standingsLogger;
        _trackMapLogger = trackMapLogger;
        _streamChatLogger = streamChatLogger;
        _simpleTelemetryLogger = simpleTelemetryLogger;

        _sessionVisibilityTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _sessionVisibilityTimer.Tick += (_, _) =>
        {
            _performanceState.RecordOverlayTimerTick("overlay-manager", 1000, visible: true, pauseEligible: false);
            ApplyOverlaySettings();
        };
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
                _releaseUpdates,
                _storageOptions,
                _localhostOverlayOptions,
                _localhostOverlayState,
                _liveTelemetrySource,
                _diagnosticsBundleService,
                _events,
                settings,
                SaveSettings,
                ApplyOverlaySettings,
                RequestApplicationExit,
                SelectSettingsOverlayTab));
        WireSettingsEmergencyZOrder(form);
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

        _settingsOverlayActive = true;
        ApplyEmergencyOverlayZOrder();
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
        _appliedOpacities.Clear();
    }

    private IReadOnlyList<OverlayDefinition> ManagedOverlayDefinitions =>
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

    private IReadOnlyList<OverlayRegistration> ManagedOverlayRegistrations =>
    [
        new OverlayRegistration(
            StandingsOverlayDefinition.Definition,
            settings => new StandingsForm(
                _liveTelemetrySource,
                _standingsLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            24,
            190),
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
            550),
        new OverlayRegistration(
            RelativeOverlayDefinition.Definition,
            settings => new RelativeForm(
                _liveTelemetrySource,
                _relativeLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            650,
            24),
        new OverlayRegistration(
            TrackMapOverlayDefinition.Definition,
            settings => new TrackMapForm(
                _liveTelemetrySource,
                _trackMapStore,
                _trackMapLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            650,
            400),
        new OverlayRegistration(
            StreamChatOverlayDefinition.Definition,
            settings => new StreamChatForm(
                _settingsStore,
                _streamChatLogger,
                _performanceState,
                settings,
                SelectedFontFamily,
                SaveSettings),
            1490,
            24),
        new OverlayRegistration(
            FlagsOverlayDefinition.Definition,
            settings => new FlagsOverlayForm(
                _liveTelemetrySource,
                _simpleTelemetryLogger,
                _performanceState,
                settings,
                SaveSettings),
            DefaultOverlayLocation(FlagsOverlayDefinition.Definition).X,
            DefaultOverlayLocation(FlagsOverlayDefinition.Definition).Y),
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
                SessionWeatherOverlayViewModel.CreateBuilder(),
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
                PitServiceOverlayViewModel.CreateBuilder(),
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
        foreach (var definition in ManagedOverlayDefinitions)
        {
            var defaultLocation = DefaultOverlayLocation(definition);
            var overlay = _settings!.GetOrAddOverlay(
                definition.Id,
                definition.DefaultWidth,
                definition.DefaultHeight,
                defaultLocation.X,
                defaultLocation.Y,
                defaultEnabled: false);
            overlay.Scale = Math.Clamp(overlay.Scale, 0.6d, 2d);
            ApplyGapToLeaderRaceOnlyPolicy(definition, overlay);
            ApplyFlagsCompactPolicy(definition, overlay);

            if (UsesScaleDerivedSize(definition))
            {
                var size = ScaledOverlaySize(definition, overlay.Scale);
                overlay.Width = size.Width;
                overlay.Height = size.Height;
            }
            else if (overlay.Width <= 0 || overlay.Height <= 0)
            {
                overlay.Width = ScaleDimension(definition.DefaultWidth, overlay.Scale);
                overlay.Height = ScaleDimension(definition.DefaultHeight, overlay.Scale);
            }
        }
    }

    private static Point DefaultOverlayLocation(OverlayDefinition definition)
    {
        if (string.Equals(definition.Id, StandingsOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(24, 190);
        }

        if (string.Equals(definition.Id, FuelCalculatorOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(24, 550);
        }

        if (string.Equals(definition.Id, RelativeOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(650, 24);
        }

        if (string.Equals(definition.Id, TrackMapOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(650, 400);
        }

        if (string.Equals(definition.Id, StreamChatOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(1490, 24);
        }

        if (string.Equals(definition.Id, SessionWeatherOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(1070, 24);
        }

        if (string.Equals(definition.Id, PitServiceOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(1070, 320);
        }

        if (string.Equals(definition.Id, InputStateOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(1070, 590);
        }

        if (string.Equals(definition.Id, CarRadarOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(650, 24);
        }

        if (string.Equals(definition.Id, GapToLeaderOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return new Point(650, 260);
        }

        if (string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            return new Point(
                area.Left + Math.Max(0, (area.Width - definition.DefaultWidth) / 2),
                area.Top + 96);
        }

        return new Point(24, 24);
    }

    private void ApplyOverlaySettings()
    {
        if (_settings is null)
        {
            return;
        }

        RecreateManagedFormsIfFontChanged();
        RecreateManagedFormsIfUnitsChanged();
        var liveSnapshot = _liveTelemetrySource.Snapshot();
        var liveTelemetryAvailable = IsLiveTelemetryAvailable(liveSnapshot);
        var currentSession = CurrentSessionKind(liveSnapshot);
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
            var sessionAllowed = IsAllowedForSession(registration.Definition, settings, currentSession);
            var shouldShow = settingsPreview || (settings.Enabled && sessionAllowed);
            var overlayLiveTelemetryAvailable = liveTelemetryAvailable || settingsPreview;

            if (string.Equals(registration.Definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal))
            {
                ApplyFlagsRegistration(
                    registration,
                    settings,
                    managedEnabled: shouldShow,
                    sessionAllowed,
                    settingsPreview,
                    liveTelemetryAvailable: overlayLiveTelemetryAvailable);
                continue;
            }

            if (!shouldShow)
            {
                if (_forms.TryGetValue(registration.Definition.Id, out var hiddenForm))
                {
                    ApplyRadarSettingsPreview(hiddenForm, previewVisible: false);
                    hiddenForm.Hide();
                }

                RecordOverlayLifecycleState(
                    registration.Definition,
                    settings,
                    sessionAllowed,
                    settingsPreview,
                    desiredVisible: false,
                    liveTelemetryAvailable: overlayLiveTelemetryAvailable);
                continue;
            }

            var form = EnsureForm(
                registration.Definition.Id,
                () => registration.Create(settings));
            var wasVisible = form.Visible;
            ApplyScaleIfChanged(registration.Definition, settings, form);
            ApplyOpacityIfChanged(registration.Definition, settings, form);
            ApplyRadarSettingsPreview(form, settingsPreview);
            ApplyLiveTelemetryFade(
                registration.Definition,
                form,
                overlayLiveTelemetryAvailable,
                immediate: !wasVisible);
            if (!form.Visible)
            {
                form.Show();
            }

            RecordOverlayLifecycleState(
                registration.Definition,
                settings,
                sessionAllowed,
                settingsPreview,
                desiredVisible: true,
                liveTelemetryAvailable: overlayLiveTelemetryAvailable);
        }

        ApplyEmergencyOverlayZOrder();
    }

    private static string SelectedFontFamily => OverlayTheme.DefaultFontFamily;

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
        settings.Scale = Math.Clamp(settings.Scale, 0.6d, 2d);
        ApplyFlagsCompactPolicy(definition, settings);
        var size = ScaledOverlaySize(definition, settings.Scale);
        settings.Width = size.Width;
        settings.Height = size.Height;
        if (_appliedScales.TryGetValue(definition.Id, out var appliedScale)
            && Math.Abs(appliedScale - settings.Scale) < 0.001d
            && form.ClientSize == size)
        {
            return;
        }

        form.ClientSize = size;
        _appliedScales[definition.Id] = settings.Scale;
    }

    private void ApplyOpacityIfChanged(OverlayDefinition definition, OverlaySettings settings, Form form)
    {
        if (!definition.ShowOpacityControl)
        {
            if (!_appliedOpacities.TryGetValue(definition.Id, out var appliedOpacity)
                || Math.Abs(appliedOpacity - 1d) > 0.001d)
            {
                if (form is PersistentOverlayForm persistent)
                {
                    persistent.SetBaseOverlayOpacity(1d);
                }
                else if (Math.Abs(form.Opacity - 1d) > 0.001d)
                {
                    form.Opacity = 1d;
                }

                _appliedOpacities[definition.Id] = 1d;
            }

            return;
        }

        settings.Opacity = Math.Clamp(settings.Opacity, 0.2d, 1d);
        var opacityChanged = !_appliedOpacities.TryGetValue(definition.Id, out var previousOpacity)
            || Math.Abs(previousOpacity - settings.Opacity) > 0.001d;
        if (string.Equals(definition.Id, TrackMapOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            if (opacityChanged || !_appliedOpacities.ContainsKey(definition.Id))
            {
                if (form is PersistentOverlayForm persistent)
                {
                    persistent.SetBaseOverlayOpacity(1d);
                }
                else if (Math.Abs(form.Opacity - 1d) > 0.001d)
                {
                    form.Opacity = 1d;
                }

                _appliedOpacities[definition.Id] = settings.Opacity;
                form.Invalidate();
            }

            return;
        }

        if (!opacityChanged)
        {
            return;
        }

        if (form is PersistentOverlayForm persistentForm)
        {
            persistentForm.SetBaseOverlayOpacity(settings.Opacity);
        }
        else if (Math.Abs(form.Opacity - settings.Opacity) > 0.001d)
        {
            form.Opacity = settings.Opacity;
        }

        _appliedOpacities[definition.Id] = settings.Opacity;
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
            _appliedOpacities.Remove(definition.Id);
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
            _appliedOpacities.Remove(definition.Id);
        }

        _appliedUnitSystem = unitSystem;
    }

    private OverlaySessionKind? CurrentSessionKind(LiveTelemetrySnapshot snapshot)
    {
        return OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot);
    }

    private static bool IsAllowedForSession(OverlayDefinition definition, OverlaySettings settings, OverlaySessionKind? sessionKind)
    {
        if (string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal)
            && sessionKind is null)
        {
            return false;
        }

        return OverlayAvailabilityEvaluator.IsAllowedForSession(settings, sessionKind);
    }

    private static bool IsLiveTelemetryAvailable(LiveTelemetrySnapshot snapshot)
    {
        return OverlayAvailabilityEvaluator.FromSnapshot(snapshot, DateTimeOffset.UtcNow).IsAvailable;
    }

    private void ApplyLiveTelemetryFade(
        OverlayDefinition definition,
        Form form,
        bool liveTelemetryAvailable,
        bool immediate)
    {
        if (form is not PersistentOverlayForm persistent)
        {
            return;
        }

        var shouldBeVisible = !definition.FadeWhenLiveTelemetryUnavailable || liveTelemetryAvailable;
        persistent.SetLiveTelemetryAvailable(shouldBeVisible, immediate);
        _performanceState.RecordOverlayLiveTelemetryState(
            definition.Id,
            DateTimeOffset.UtcNow,
            liveTelemetryAvailable,
            persistent.LiveTelemetryFadeAlpha);
    }

    private void RecordOverlayLifecycleState(
        OverlayDefinition definition,
        OverlaySettings settings,
        bool sessionAllowed,
        bool settingsPreview,
        bool desiredVisible,
        bool liveTelemetryAvailable)
    {
        var hasForm = _forms.TryGetValue(definition.Id, out var form) && !form.IsDisposed;
        var actualVisible = hasForm && form!.Visible;
        var fadeAlpha = form is PersistentOverlayForm persistent
            ? persistent.LiveTelemetryFadeAlpha
            : 1d;
        var fadedUnavailable = definition.FadeWhenLiveTelemetryUnavailable
            && !liveTelemetryAvailable
            && fadeAlpha <= 0.01d;
        var pauseEligible = hasForm && (!desiredVisible || !actualVisible || fadedUnavailable);
        _performanceState.RecordOverlayLifecycleState(
            definition.Id,
            DateTimeOffset.UtcNow,
            settings.Enabled,
            sessionAllowed,
            settingsPreview,
            desiredVisible,
            actualVisible,
            hasForm,
            liveTelemetryAvailable,
            fadeAlpha,
            definition.FadeWhenLiveTelemetryUnavailable,
            pauseEligible);
    }

    private static int ScaleDimension(int defaultDimension, double scale)
    {
        return Math.Max(80, (int)Math.Round(defaultDimension * Math.Clamp(scale, 0.6d, 2d)));
    }

    private static Size ScaledOverlaySize(OverlayDefinition definition, double scale)
    {
        return new Size(
            ScaleDimension(definition.DefaultWidth, scale),
            ScaleDimension(definition.DefaultHeight, scale));
    }

    private static bool UsesScaleDerivedSize(OverlayDefinition definition)
    {
        return definition.ShowScaleControl;
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

    private static void ApplyFlagsCompactPolicy(OverlayDefinition definition, OverlaySettings settings)
    {
        if (!string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.Ordinal))
        {
            return;
        }

        var hadPrimaryScreenDefault = string.Equals(settings.ScreenId, FlagsOverlayDefinition.PrimaryScreenDefaultId, StringComparison.Ordinal);
        var hadFullScreenSize = settings.Width > FlagsOverlayDefinition.MaximumWidth
            || settings.Height > FlagsOverlayDefinition.MaximumHeight
            || (settings.Width >= 900 && settings.Height >= 500);

        if (!hadPrimaryScreenDefault && !hadFullScreenSize)
        {
            if (settings.Width > 0
                && settings.Height > 0
                && Math.Abs(settings.Scale - 1d) < 0.001d)
            {
                var widthScale = settings.Width / (double)definition.DefaultWidth;
                var heightScale = settings.Height / (double)definition.DefaultHeight;
                settings.Scale = Math.Clamp((widthScale + heightScale) / 2d, 0.6d, 2d);
            }

            settings.ScreenId = null;
            return;
        }

        settings.Scale = 1d;
        settings.Width = definition.DefaultWidth;
        settings.Height = definition.DefaultHeight;
        var location = DefaultOverlayLocation(definition);
        settings.X = location.X;
        settings.Y = location.Y;
        settings.ScreenId = null;
    }

    private void ApplyFlagsRegistration(
        OverlayRegistration registration,
        OverlaySettings settings,
        bool managedEnabled,
        bool sessionAllowed,
        bool settingsPreview,
        bool liveTelemetryAvailable)
    {
        if (!managedEnabled)
        {
            if (_forms.TryGetValue(registration.Definition.Id, out var hiddenForm))
            {
                if (hiddenForm is FlagsOverlayForm hiddenFlags)
                {
                    hiddenFlags.SetManagedEnabled(false);
                }

                hiddenForm.Hide();
            }

            RecordOverlayLifecycleState(
                registration.Definition,
                settings,
                sessionAllowed,
                settingsPreview,
                desiredVisible: false,
                liveTelemetryAvailable: liveTelemetryAvailable);
            return;
        }

        var form = EnsureForm(
            registration.Definition.Id,
            () => registration.Create(settings));
        var wasVisible = form.Visible;
        ApplyScaleIfChanged(registration.Definition, settings, form);
        ApplyOpacityIfChanged(registration.Definition, settings, form);
        ApplyLiveTelemetryFade(
            registration.Definition,
            form,
            liveTelemetryAvailable,
            immediate: !wasVisible);
        if (form is FlagsOverlayForm visibleFlags)
        {
            visibleFlags.TopMost = !_settingsOverlayActive && settings.AlwaysOnTop;
            visibleFlags.SetManagedEnabled(true);
        }

        RecordOverlayLifecycleState(
            registration.Definition,
            settings,
            sessionAllowed,
            settingsPreview,
            desiredVisible: true,
            liveTelemetryAvailable: liveTelemetryAvailable);
    }

    private void ApplyEmergencyOverlayZOrder()
    {
        ApplyLargeOverlayTopMost(FlagsOverlayDefinition.Definition.Id);
        if (_settingsOverlayActive
            && _forms.TryGetValue(SettingsOverlayDefinition.Definition.Id, out var settingsForm)
            && settingsForm.Visible)
        {
            settingsForm.BringToFront();
        }
    }

    private void WireSettingsEmergencyZOrder(Form form)
    {
        if (ReferenceEquals(_settingsZOrderForm, form))
        {
            return;
        }

        _settingsZOrderForm = form;
        form.Activated += (_, _) =>
        {
            _settingsOverlayActive = true;
            ApplyEmergencyOverlayZOrder();
        };
        form.Deactivate += (_, _) =>
        {
            _settingsOverlayActive = false;
            ApplyEmergencyOverlayZOrder();
        };
    }

    private void ApplyLargeOverlayTopMost(string overlayId)
    {
        if (_settings is null || !_forms.TryGetValue(overlayId, out var form) || form.IsDisposed)
        {
            return;
        }

        var settings = _settings.GetOrAddOverlay(
            overlayId,
            FlagsOverlayDefinition.Definition.DefaultWidth,
            FlagsOverlayDefinition.Definition.DefaultHeight,
            defaultEnabled: false);
        var shouldBeTopMost = !_settingsOverlayActive && settings.AlwaysOnTop;
        if (form.TopMost != shouldBeTopMost)
        {
            form.TopMost = shouldBeTopMost;
        }

        if (_settingsOverlayActive)
        {
            form.SendToBack();
        }
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

}
