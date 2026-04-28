using TmrOverlay.Core.Overlays;
using TmrOverlay.App.Overlays.CarRadar;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.SettingsPanel;
using TmrOverlay.App.Overlays.Status;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;
using TmrOverlay.App.History;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Performance;

namespace TmrOverlay.App.Overlays;

internal sealed class OverlayManager : IDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private readonly AppStorageOptions _storageOptions;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly TelemetryCaptureState _telemetryCaptureState;
    private readonly AppPerformanceState _performanceState;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly PostRaceAnalysisStore _postRaceAnalysisStore;
    private readonly AppEventRecorder _events;
    private readonly ILogger<CarRadarForm> _carRadarLogger;
    private readonly ILogger<GapToLeaderForm> _gapToLeaderLogger;
    private readonly Dictionary<string, Form> _forms = [];
    private readonly Dictionary<string, double> _appliedScales = [];
    private readonly System.Windows.Forms.Timer _sessionVisibilityTimer;
    private ApplicationSettings? _settings;
    private string? _appliedFontFamily;
    private string? _appliedUnitSystem;
    private bool _startupShown;

    public event EventHandler? ApplicationExitRequested;

    public OverlayManager(
        AppSettingsStore settingsStore,
        AppStorageOptions storageOptions,
        DiagnosticsBundleService diagnosticsBundleService,
        TelemetryCaptureState telemetryCaptureState,
        AppPerformanceState performanceState,
        ILiveTelemetrySource liveTelemetrySource,
        SessionHistoryQueryService historyQueryService,
        PostRaceAnalysisStore postRaceAnalysisStore,
        AppEventRecorder events,
        ILogger<CarRadarForm> carRadarLogger,
        ILogger<GapToLeaderForm> gapToLeaderLogger)
    {
        _settingsStore = settingsStore;
        _storageOptions = storageOptions;
        _diagnosticsBundleService = diagnosticsBundleService;
        _telemetryCaptureState = telemetryCaptureState;
        _performanceState = performanceState;
        _liveTelemetrySource = liveTelemetrySource;
        _historyQueryService = historyQueryService;
        _postRaceAnalysisStore = postRaceAnalysisStore;
        _events = events;
        _carRadarLogger = carRadarLogger;
        _gapToLeaderLogger = gapToLeaderLogger;

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

        var form = EnsureForm(
            SettingsOverlayDefinition.Definition.Id,
            () => new SettingsOverlayForm(
                _settings,
                ManagedOverlayDefinitions,
                _postRaceAnalysisStore,
                _telemetryCaptureState,
                _performanceState,
                _storageOptions,
                _diagnosticsBundleService,
                _events,
                settings,
                SaveSettings,
                ApplyOverlaySettings,
                RequestApplicationExit));
        if (!form.Visible)
        {
            form.Show();
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
                registration.DefaultY);
            overlay.Scale = Math.Clamp(overlay.Scale, 0.6d, 2d);
            if (overlay.Width <= 0 || overlay.Height <= 0)
            {
                overlay.Width = ScaleDimension(registration.Definition.DefaultWidth, overlay.Scale);
                overlay.Height = ScaleDimension(registration.Definition.DefaultHeight, overlay.Scale);
            }
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
                registration.DefaultY);
            var shouldShow = settings.Enabled && IsAllowedForSession(settings, currentSession);

            if (!shouldShow)
            {
                if (_forms.TryGetValue(registration.Definition.Id, out var hiddenForm))
                {
                    hiddenForm.Hide();
                }

                continue;
            }

            var form = EnsureForm(
                registration.Definition.Id,
                () => registration.Create(settings));
            ApplyScaleIfChanged(registration.Definition, settings, form);
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

    private void ApplyScaleIfChanged(OverlayDefinition definition, OverlaySettings settings, Form form)
    {
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

    private static bool IsAllowedForSession(OverlaySettings settings, OverlaySessionKind? sessionKind)
    {
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
