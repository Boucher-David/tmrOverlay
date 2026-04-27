using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.App.Overlays.Status;
using TmrOverlay.App.Settings;
using TmrOverlay.App.Telemetry;
using TmrOverlay.App.Telemetry.Live;
using TmrOverlay.App.History;
using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;

namespace TmrOverlay.App.Overlays;

internal sealed class OverlayManager : IDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private readonly TelemetryCaptureState _telemetryCaptureState;
    private readonly LiveTelemetryStore _liveTelemetryStore;
    private readonly SessionHistoryQueryService _historyQueryService;
    private readonly AppEventRecorder _events;
    private readonly ILogger<StatusOverlayForm> _statusOverlayLogger;
    private readonly List<Form> _forms = [];
    private ApplicationSettings? _settings;

    public OverlayManager(
        AppSettingsStore settingsStore,
        TelemetryCaptureState telemetryCaptureState,
        LiveTelemetryStore liveTelemetryStore,
        SessionHistoryQueryService historyQueryService,
        AppEventRecorder events,
        ILogger<StatusOverlayForm> statusOverlayLogger)
    {
        _settingsStore = settingsStore;
        _telemetryCaptureState = telemetryCaptureState;
        _liveTelemetryStore = liveTelemetryStore;
        _historyQueryService = historyQueryService;
        _events = events;
        _statusOverlayLogger = statusOverlayLogger;
    }

    public void ShowStartupOverlays(Action closeApplication)
    {
        if (_forms.Count > 0)
        {
            return;
        }

        _settings = _settingsStore.Load();
        ShowStatusOverlay(closeApplication);
        ShowFuelCalculatorOverlay();
        SaveSettings();
    }

    public void Dispose()
    {
        foreach (var form in _forms.ToArray())
        {
            form.Close();
            form.Dispose();
        }

        _forms.Clear();
    }

    private void ShowStatusOverlay(Action closeApplication)
    {
        ShowOverlay(
            StatusOverlayDefinition.Definition,
            settings => new StatusOverlayForm(
                _telemetryCaptureState,
                _events,
                _statusOverlayLogger,
                settings,
                SaveSettings,
                closeApplication));
    }

    private void ShowFuelCalculatorOverlay()
    {
        ShowOverlay(
            FuelCalculatorOverlayDefinition.Definition,
            settings => new FuelCalculatorForm(
                _liveTelemetryStore,
                _historyQueryService,
                settings,
                SaveSettings),
            defaultX: 24,
            defaultY: 190);
    }

    private void ShowOverlay(
        OverlayDefinition definition,
        Func<OverlaySettings, Form> createForm,
        int defaultX = 24,
        int defaultY = 24)
    {
        var settings = _settings!.GetOrAddOverlay(
            definition.Id,
            definition.DefaultWidth,
            definition.DefaultHeight,
            defaultX,
            defaultY);

        if (!settings.Enabled)
        {
            return;
        }

        var form = createForm(settings);
        _forms.Add(form);
        form.Show();
    }

    private void SaveSettings()
    {
        if (_settings is not null)
        {
            _settingsStore.Save(_settings);
        }
    }
}
