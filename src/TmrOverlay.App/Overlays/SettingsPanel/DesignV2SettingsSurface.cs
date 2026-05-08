using System.Drawing;
using System.Drawing.Drawing2D;
using TmrOverlay.App.Brand;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Overlays.BrowserSources;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Overlays.GapToLeader;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.App.Updates;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal sealed class DesignV2SettingsCallbacks
{
    public required Action SaveAndApply { get; init; }

    public required Action RequestApplicationExit { get; init; }

    public required Action<string?> SelectedOverlayChanged { get; init; }

    public required Func<bool, bool> SetRawCaptureEnabled { get; init; }

    public required Action CreateDiagnosticsBundle { get; init; }

    public required Action CopyLatestDiagnosticsBundlePath { get; init; }

    public required Action<string, string> OpenSupportDirectory { get; init; }

    public required Func<Task> CheckForUpdatesAsync { get; init; }

    public required Action OpenReleaseUpdatePage { get; init; }

    public required Action<string> CopyTextToClipboard { get; init; }

    public required Action<OverlaySettings> ImportGarageCoverImage { get; init; }

    public required Action<OverlaySettings> ClearGarageCoverImage { get; init; }

    public required Action<OverlaySettings> ShowGarageCoverPreview { get; init; }

    public required Func<string?> LatestDiagnosticsBundlePath { get; init; }

    public required Func<string> AdvancedDiagnosticsText { get; init; }
}

internal sealed class DesignV2SettingsSurface : Control
{
    private const string GeneralTabId = "general";
    private const string SupportTabId = "error-logging";
    private const int ShellX = 44;
    private const int ShellY = 36;
    private const int ShellWidth = 1152;
    private const int ShellHeight = 608;
    private const int SidebarX = 64;
    private const int SidebarY = 116;
    private const int SidebarWidth = 190;
    private const int SidebarHeight = 506;
    private const int ContentX = 278;
    private const int ContentY = 116;
    private const int ContentWidth = 890;
    private const int ContentHeight = 506;

    private static readonly string[] PreferredOverlayTabOrder =
    [
        "standings",
        "relative",
        "gap-to-leader",
        "track-map",
        "stream-chat",
        "garage-cover",
        "fuel-calculator",
        "input-state",
        "car-radar",
        "flags",
        "session-weather",
        "pit-service"
    ];

    private static readonly Color BgTop = Rgb(18, 5, 31);
    private static readonly Color BgMid = Rgb(12, 18, 42);
    private static readonly Color BgBottom = Rgb(3, 11, 24);
    private static readonly Color PanelRaised = Rgba(17, 28, 55, 250);
    private static readonly Color TitleBar = Rgba(11, 14, 33, 250);
    private static readonly Color Border = Rgba(40, 72, 108, 235);
    private static readonly Color BorderDim = Rgba(30, 52, 82, 200);
    private static readonly Color TextPrimary = Rgb(255, 247, 255);
    private static readonly Color TextSecondary = Rgb(208, 230, 255);
    private static readonly Color TextMuted = Rgb(140, 174, 212);
    private static readonly Color TextDim = Rgb(82, 112, 148);
    private static readonly Color Cyan = Rgb(0, 232, 255);
    private static readonly Color Magenta = Rgb(255, 42, 167);
    private static readonly Color Amber = Rgb(255, 209, 91);
    private static readonly Color Green = Rgb(98, 255, 159);
    private static readonly Color Orange = Rgb(255, 125, 73);
    private static readonly Color Purple = Rgb(126, 50, 255);

    private readonly ApplicationSettings _applicationSettings;
    private readonly Dictionary<string, OverlayDefinition> _overlayById;
    private readonly TelemetryCaptureState _captureState;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly AppStorageOptions _storageOptions;
    private readonly LocalhostOverlayOptions _localhostOverlayOptions;
    private readonly ReleaseUpdateService _releaseUpdates;
    private readonly DesignV2SettingsCallbacks _callbacks;
    private readonly IReadOnlyList<SidebarTab> _sidebarTabs;
    private readonly List<Control> _dynamicControls = [];
    private readonly Image? _brandLogo;
    private string _selectedTabId = GeneralTabId;
    private SettingsRegion _selectedRegion = SettingsRegion.General;
    private string _supportStatusText = string.Empty;
    private bool _supportStatusIsError;
    private Point? _dragCursorOrigin;
    private Point? _dragFormOrigin;

    public DesignV2SettingsSurface(
        ApplicationSettings applicationSettings,
        IReadOnlyList<OverlayDefinition> managedOverlays,
        TelemetryCaptureState captureState,
        DiagnosticsBundleService diagnosticsBundleService,
        AppStorageOptions storageOptions,
        LocalhostOverlayOptions localhostOverlayOptions,
        ReleaseUpdateService releaseUpdates,
        DesignV2SettingsCallbacks callbacks)
    {
        _applicationSettings = applicationSettings;
        _captureState = captureState;
        _diagnosticsBundleService = diagnosticsBundleService;
        _storageOptions = storageOptions;
        _localhostOverlayOptions = localhostOverlayOptions;
        _releaseUpdates = releaseUpdates;
        _callbacks = callbacks;
        _overlayById = managedOverlays.ToDictionary(overlay => overlay.Id, StringComparer.OrdinalIgnoreCase);
        _sidebarTabs = BuildSidebarTabs(managedOverlays);
        _brandLogo = TmrBrandAssets.LoadLogoImage();

        BackColor = Color.Black;
        Size = new Size(SettingsOverlayDefinition.Definition.DefaultWidth, SettingsOverlayDefinition.Definition.DefaultHeight);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);

        RebuildDynamicControls();
    }

    public string SelectedTabId => _selectedTabId;

    public string? SelectedOverlayId => _overlayById.ContainsKey(_selectedTabId) ? _selectedTabId : null;

    public bool IsSupportSelected => string.Equals(_selectedTabId, SupportTabId, StringComparison.OrdinalIgnoreCase);

    public bool IsGarageCoverSelected => string.Equals(_selectedTabId, "garage-cover", StringComparison.OrdinalIgnoreCase);

    public void RefreshRuntimeState()
    {
        if (IsSupportSelected)
        {
            RebuildDynamicControls();
        }

        Invalidate();
    }

    public void RefreshSelectedPage()
    {
        RebuildDynamicControls();
        Invalidate();
    }

    public void SetSupportStatus(string message, bool isError)
    {
        _supportStatusText = message;
        _supportStatusIsError = isError;
        Invalidate(ContentBounds());
    }

    public void SelectTab(string tabId)
    {
        if (!IsKnownTab(tabId))
        {
            return;
        }

        if (string.Equals(_selectedTabId, tabId, StringComparison.OrdinalIgnoreCase))
        {
            RebuildDynamicControls();
            Invalidate();
            return;
        }

        _selectedTabId = tabId;
        if (!_overlayById.ContainsKey(tabId))
        {
            _selectedRegion = SettingsRegion.General;
        }
        else if (!AvailableRegions(tabId).Contains(_selectedRegion))
        {
            _selectedRegion = AvailableRegions(tabId).FirstOrDefault();
        }

        RebuildDynamicControls();
        Invalidate();
        _callbacks.SelectedOverlayChanged(SelectedOverlayId);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _brandLogo?.Dispose();
            foreach (var control in _dynamicControls)
            {
                control.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        DrawBackdrop(graphics, ClientRectangle);
        DrawWindowShell(graphics);
        DrawTitleBar(graphics);
        DrawSidebar(graphics);
        DrawContentContainer(graphics);

        if (string.Equals(_selectedTabId, GeneralTabId, StringComparison.OrdinalIgnoreCase))
        {
            DrawContentHeader(graphics, "General", "Shared units.");
            DrawApplicationGeneralPage(graphics);
            return;
        }

        if (string.Equals(_selectedTabId, SupportTabId, StringComparison.OrdinalIgnoreCase))
        {
            DrawContentHeader(graphics, "Support", "Diagnostics and teammate handoff controls stay task-oriented.", "READY");
            DrawSupportPage(graphics);
            return;
        }

        if (!_overlayById.TryGetValue(_selectedTabId, out var definition))
        {
            DrawContentHeader(graphics, "Settings", "Overlay settings and browser-source controls.");
            return;
        }

        DrawContentHeader(graphics, definition.DisplayName, SubtitleFor(definition.Id));
        DrawSegments(graphics, SegmentsFor(definition.Id), _selectedRegion);
        DrawOverlayPage(graphics, definition, OverlaySettingsFor(definition));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (CloseButtonBounds().Contains(e.Location))
        {
            _callbacks.RequestApplicationExit();
            return;
        }

        for (var index = 0; index < _sidebarTabs.Count; index++)
        {
            if (SidebarButtonBounds(index).Contains(e.Location))
            {
                SelectTab(_sidebarTabs[index].Id);
                return;
            }
        }

        if (_overlayById.ContainsKey(_selectedTabId))
        {
            foreach (var (region, bounds) in SegmentBounds(SegmentsFor(_selectedTabId)))
            {
                if (!bounds.Contains(e.Location))
                {
                    continue;
                }

                _selectedRegion = region;
                RebuildDynamicControls();
                Invalidate();
                return;
            }
        }

        if (TitleDragBounds().Contains(e.Location))
        {
            _dragCursorOrigin = Cursor.Position;
            _dragFormOrigin = FindForm()?.Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragCursorOrigin is not { } cursorOrigin || _dragFormOrigin is not { } formOrigin)
        {
            return;
        }

        var form = FindForm();
        if (form is null)
        {
            return;
        }

        var cursor = Cursor.Position;
        form.Location = new Point(
            formOrigin.X + cursor.X - cursorOrigin.X,
            formOrigin.Y + cursor.Y - cursorOrigin.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragCursorOrigin = null;
        _dragFormOrigin = null;
        Capture = false;
    }

    private void RebuildDynamicControls()
    {
        foreach (var control in _dynamicControls)
        {
            Controls.Remove(control);
            control.Dispose();
        }

        _dynamicControls.Clear();

        if (string.Equals(_selectedTabId, GeneralTabId, StringComparison.OrdinalIgnoreCase))
        {
            BuildApplicationGeneralControls();
            return;
        }

        if (string.Equals(_selectedTabId, SupportTabId, StringComparison.OrdinalIgnoreCase))
        {
            BuildSupportControls();
            return;
        }

        if (!_overlayById.TryGetValue(_selectedTabId, out var definition))
        {
            return;
        }

        var settings = OverlaySettingsFor(definition);
        switch (_selectedRegion)
        {
            case SettingsRegion.General:
                BuildOverlayGeneralControls(definition, settings);
                break;
            case SettingsRegion.Content:
                BuildOverlayContentControls(definition, settings);
                break;
            case SettingsRegion.Header:
                BuildChromeControls(settings, HeaderChromeKeys);
                break;
            case SettingsRegion.Footer:
                BuildChromeControls(settings, FooterChromeKeys);
                break;
        }
    }

    private void BuildApplicationGeneralControls()
    {
        AddDynamic(new V2ChoiceControl(
            new Rectangle(506, 270, 154, 30),
            ["Metric", "Imperial"],
            string.Equals(_applicationSettings.General.UnitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
                ? "Imperial"
                : "Metric",
            selected =>
            {
                _applicationSettings.General.UnitSystem = selected;
                _callbacks.SaveAndApply();
                Invalidate();
            }));
    }

    private void BuildSupportControls()
    {
        var snapshot = _captureState.Snapshot();
        var rawToggle = new V2ToggleControl(
            new Rectangle(328, 276, 56, 28),
            snapshot.RawCaptureEnabled || snapshot.RawCaptureActive,
            isOn =>
            {
                if (_captureState.Snapshot().RawCaptureActive)
                {
                    SetSupportStatus("Diagnostic telemetry is active for this session.", isError: false);
                    RebuildDynamicControls();
                    return;
                }

                var accepted = _callbacks.SetRawCaptureEnabled(isOn);
                SetSupportStatus(
                    accepted
                        ? (isOn ? "Diagnostic telemetry will start with live data." : "Diagnostic telemetry capture disabled.")
                        : "Diagnostic telemetry change was rejected while capture is active.",
                    !accepted);
                RebuildDynamicControls();
            })
        {
            Enabled = !snapshot.RawCaptureActive
        };
        AddDynamic(rawToggle);

        AddActionButton(new Rectangle(328, 510, 138, 34), "Create Bundle", _callbacks.CreateDiagnosticsBundle);
        AddActionButton(new Rectangle(482, 510, 120, 34), "Open Logs", () => _callbacks.OpenSupportDirectory(_storageOptions.LogsRoot, "logs"));
        AddActionButton(new Rectangle(618, 510, 116, 34), "Copy Path", _callbacks.CopyLatestDiagnosticsBundlePath);
        AddActionButton(new Rectangle(754, 510, 132, 34), "Diagnostics", () => _callbacks.OpenSupportDirectory(_storageOptions.DiagnosticsRoot, "diagnostics"));
        AddActionButton(new Rectangle(902, 510, 100, 34), "Captures", () => _callbacks.OpenSupportDirectory(_storageOptions.CaptureRoot, "captures"));
        AddActionButton(new Rectangle(1018, 510, 92, 34), "History", () => _callbacks.OpenSupportDirectory(_storageOptions.UserHistoryRoot, "history"));
        AddActionButton(new Rectangle(912, 548, 86, 30), "Check", () => _callbacks.CheckForUpdatesAsync());
        AddActionButton(new Rectangle(1012, 548, 104, 30), "Releases", _callbacks.OpenReleaseUpdatePage);
    }

    private void BuildOverlayGeneralControls(OverlayDefinition definition, OverlaySettings settings)
    {
        var isGarageCover = string.Equals(definition.Id, "garage-cover", StringComparison.OrdinalIgnoreCase);
        if (!isGarageCover)
        {
            AddDynamic(new V2ToggleControl(
                new Rectangle(600, 328, 56, 28),
                settings.Enabled,
                isOn =>
                {
                    settings.Enabled = isOn;
                    _callbacks.SaveAndApply();
                    Invalidate();
                }));
        }

        if (definition.ShowScaleControl)
        {
            AddDynamic(new V2PercentSliderControl(
                new Rectangle(454, 368, 180, 28),
                ClosestPercent(settings.Scale, [60, 75, 100, 125, 150, 175, 200]),
                [60, 75, 100, 125, 150, 175, 200],
                Cyan,
                percent =>
                {
                    settings.Scale = Math.Clamp(percent / 100d, 0.6d, 2d);
                    settings.Width = ScaleDimension(definition.DefaultWidth, settings.Scale);
                    settings.Height = ScaleDimension(definition.DefaultHeight, settings.Scale);
                    settings.ScreenId = null;
                    _callbacks.SaveAndApply();
                    Invalidate();
                }));
        }

        if (definition.ShowOpacityControl)
        {
            AddDynamic(new V2PercentSliderControl(
                new Rectangle(454, 408, 180, 28),
                ClosestPercent(settings.Opacity, [20, 30, 40, 50, 60, 70, 80, 88, 90, 100]),
                [20, 30, 40, 50, 60, 70, 80, 88, 90, 100],
                Magenta,
                percent =>
                {
                    settings.Opacity = Math.Clamp(percent / 100d, 0.2d, 1d);
                    _callbacks.SaveAndApply();
                    Invalidate();
                }));
        }

        if (definition.ShowSessionFilters && !isGarageCover)
        {
            AddSessionCheck(new Rectangle(454, 442, 72, 20), "Test", settings.ShowInTest, isOn => settings.ShowInTest = isOn);
            AddSessionCheck(new Rectangle(548, 442, 100, 20), "Practice", settings.ShowInPractice, isOn => settings.ShowInPractice = isOn);
            AddSessionCheck(new Rectangle(454, 468, 72, 20), "Qual", settings.ShowInQualifying, isOn => settings.ShowInQualifying = isOn);
            AddSessionCheck(new Rectangle(548, 468, 76, 20), "Race", settings.ShowInRace, isOn => settings.ShowInRace = isOn);
        }

        if (isGarageCover)
        {
            AddActionButton(new Rectangle(750, 428, 112, 30), "Import", () => _callbacks.ImportGarageCoverImage(settings));
            AddActionButton(new Rectangle(876, 428, 86, 30), "Clear", () => _callbacks.ClearGarageCoverImage(settings));
            AddActionButton(new Rectangle(976, 428, 118, 30), "Test Cover", () => _callbacks.ShowGarageCoverPreview(settings));
        }

        if (BrowserOverlayCatalog.TryGetRouteForOverlayId(definition.Id, out var route))
        {
            var url = $"{_localhostOverlayOptions.Prefix.TrimEnd('/')}{route}";
            AddActionButton(new Rectangle(950, 542, 70, 30), "Copy", () => _callbacks.CopyTextToClipboard(url));
        }

        void AddSessionCheck(Rectangle bounds, string text, bool current, Action<bool> setter)
        {
            AddDynamic(new V2CheckControl(bounds, text, current, isOn =>
            {
                setter(isOn);
                _callbacks.SaveAndApply();
                Invalidate();
            }));
        }
    }

    private void BuildOverlayContentControls(OverlayDefinition definition, OverlaySettings settings)
    {
        switch (definition.Id)
        {
            case "relative":
                AddColumnToggleControls(settings, OverlayContentColumnSettings.Relative, new Rectangle(306, 272, 834, 222));
                AddDynamic(new V2StepperControl(
                    new Rectangle(454, 562, 220, 38),
                    settings.GetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, defaultValue: 5, minimum: 0, maximum: 8),
                    0,
                    8,
                    value => $"{value} each side",
                    value =>
                    {
                        settings.SetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, value, 0, 8);
                        settings.SetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, value, 0, 8);
                        settings.SetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, value, 0, 8);
                        _callbacks.SaveAndApply();
                        Invalidate();
                    }));
                break;
            case "standings":
                AddColumnToggleControls(settings, OverlayContentColumnSettings.Standings, new Rectangle(306, 272, 834, 236), rowHeight: 22, rowGap: 3);
                if (OverlayContentColumnSettings.Standings.Blocks?.FirstOrDefault() is { } standingsBlock)
                {
                    AddDynamic(new V2CheckControl(
                        new Rectangle(328, 582, 190, 20),
                        standingsBlock.Label,
                        OverlayContentColumnSettings.BlockEnabled(settings, standingsBlock),
                        isOn =>
                        {
                            settings.SetBooleanOption(standingsBlock.EnabledOptionKey, isOn);
                            _callbacks.SaveAndApply();
                            Invalidate();
                        }));
                    if (standingsBlock.CountOptionKey is { } countKey)
                    {
                        AddDynamic(new V2StepperControl(
                            new Rectangle(552, 572, 220, 38),
                            OverlayContentColumnSettings.BlockCount(settings, standingsBlock),
                            standingsBlock.MinimumCount,
                            standingsBlock.MaximumCount,
                            value => $"{value} other-class rows",
                            value =>
                            {
                                settings.SetIntegerOption(countKey, value, standingsBlock.MinimumCount, standingsBlock.MaximumCount);
                                _callbacks.SaveAndApply();
                                Invalidate();
                            }));
                    }
                }
                break;
            case "gap-to-leader":
                AddDynamic(new V2StepperControl(
                    new Rectangle(454, 484, 220, 38),
                    settings.GetIntegerOption(OverlayOptionKeys.GapCarsAhead, defaultValue: 5, minimum: 0, maximum: 12),
                    0,
                    12,
                    value => $"{value} each side",
                    value =>
                    {
                        settings.SetIntegerOption(OverlayOptionKeys.GapCarsAhead, value, 0, 12);
                        settings.SetIntegerOption(OverlayOptionKeys.GapCarsBehind, value, 0, 12);
                        _callbacks.SaveAndApply();
                        Invalidate();
                    }));
                break;
            case "fuel-calculator":
                AddMatrixCheck(new Rectangle(306, 272, 834, 150), 0, settings.GetBooleanOption(OverlayOptionKeys.FuelAdvice, true), isOn => settings.SetBooleanOption(OverlayOptionKeys.FuelAdvice, isOn));
                AddMatrixCheck(new Rectangle(306, 272, 834, 150), 1, settings.GetBooleanOption(OverlayOptionKeys.FuelSource, true), isOn => settings.SetBooleanOption(OverlayOptionKeys.FuelSource, isOn));
                break;
            case "track-map":
                AddMatrixCheck(new Rectangle(306, 272, 834, 190), 1, settings.GetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, true), isOn => settings.SetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, isOn));
                AddMatrixCheck(new Rectangle(306, 272, 834, 190), 2, settings.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, true), isOn => settings.SetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, isOn));
                break;
            case "input-state":
                AddBlockToggleControls(settings, OverlayContentColumnSettings.InputState, new Rectangle(306, 272, 834, 236), rowHeight: 22, rowGap: 3);
                break;
            case "car-radar":
                AddMatrixCheck(new Rectangle(306, 272, 834, 150), 1, settings.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, true), isOn => settings.SetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, isOn));
                break;
            case "flags":
                var flagRows = new (string Key, bool Default)[]
                {
                    (OverlayOptionKeys.FlagsShowGreen, true),
                    (OverlayOptionKeys.FlagsShowBlue, true),
                    (OverlayOptionKeys.FlagsShowYellow, true),
                    (OverlayOptionKeys.FlagsShowCritical, true),
                    (OverlayOptionKeys.FlagsShowFinish, true)
                };
                for (var index = 0; index < flagRows.Length; index++)
                {
                    var row = flagRows[index];
                    AddMatrixCheck(new Rectangle(306, 272, 834, 240), index, settings.GetBooleanOption(row.Key, row.Default), isOn => settings.SetBooleanOption(row.Key, isOn));
                }
                break;
            case "stream-chat":
                AddDynamic(new V2ToggleControl(new Rectangle(454, 330, 56, 28), settings.Enabled, isOn =>
                {
                    settings.Enabled = isOn;
                    _callbacks.SaveAndApply();
                    Invalidate();
                }));
                BuildStreamChatControls(definition, settings);
                break;
        }
    }

    private void BuildStreamChatControls(OverlayDefinition definition, OverlaySettings settings)
    {
        var provider = StreamChatOverlaySettings.NormalizeProvider(settings.GetStringOption(OverlayOptionKeys.StreamChatProvider, StreamChatOverlaySettings.ProviderNone));
        AddDynamic(new V2ChoiceControl(
            new Rectangle(454, 366, 300, 30),
            ["Not configured", "Streamlabs", "Twitch"],
            ProviderLabel(provider),
            selected =>
            {
                settings.SetStringOption(OverlayOptionKeys.StreamChatProvider, ProviderFromLabel(selected));
                _callbacks.SaveAndApply();
                RebuildDynamicControls();
                Invalidate();
            }));

        var streamlabsBox = CreateTextBox(
            settings.GetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl),
            new Rectangle(454, 404, 420, 28),
            provider == StreamChatOverlaySettings.ProviderStreamlabs);
        AddDynamic(streamlabsBox);

        var twitchBox = CreateTextBox(
            settings.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel),
            new Rectangle(454, 442, 210, 28),
            provider == StreamChatOverlaySettings.ProviderTwitch);
        AddDynamic(twitchBox);

        AddActionButton(new Rectangle(682, 440, 92, 30), "Save", () =>
        {
            settings.SetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl, streamlabsBox.Text);
            settings.SetStringOption(OverlayOptionKeys.StreamChatTwitchChannel, twitchBox.Text);
            _callbacks.SaveAndApply();
            Invalidate();
        });

        if (BrowserOverlayCatalog.TryGetRouteForOverlayId(definition.Id, out var route))
        {
            var url = $"{_localhostOverlayOptions.Prefix.TrimEnd('/')}{route}";
            AddActionButton(new Rectangle(950, 552, 70, 30), "Copy", () => _callbacks.CopyTextToClipboard(url));
        }
    }

    private void BuildChromeControls(OverlaySettings settings, IReadOnlyList<string> keys)
    {
        if (!SupportsSharedChromeSettings(settings.Id))
        {
            return;
        }

        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            AddDynamic(new V2CheckControl(
                new Rectangle(454 + index * 116, 370, 38, 22),
                string.Empty,
                settings.GetBooleanOption(key, true),
                isOn =>
                {
                    settings.SetBooleanOption(key, isOn);
                    _callbacks.SaveAndApply();
                    Invalidate();
                }));
        }
    }

    private void AddColumnToggleControls(
        OverlaySettings settings,
        OverlayContentDefinition contentDefinition,
        Rectangle rect,
        int rowHeight = 24,
        int rowGap = 5)
    {
        var columns = OverlayContentColumnSettings.ColumnsFor(settings, contentDefinition);
        for (var index = 0; index < columns.Count; index++)
        {
            var state = columns[index];
            AddMatrixCheck(rect, index, state.Enabled, isOn =>
            {
                settings.SetBooleanOption(ContentColumnEnabledKey(settings.Id, state.Id), isOn);
            }, rowHeight, rowGap);
        }
    }

    private void AddBlockToggleControls(
        OverlaySettings settings,
        OverlayContentDefinition contentDefinition,
        Rectangle rect,
        int rowHeight = 24,
        int rowGap = 5)
    {
        if (contentDefinition.Blocks is null)
        {
            return;
        }

        for (var index = 0; index < contentDefinition.Blocks.Count; index++)
        {
            var block = contentDefinition.Blocks[index];
            AddMatrixCheck(rect, index, OverlayContentColumnSettings.BlockEnabled(settings, block), isOn =>
            {
                settings.SetBooleanOption(block.EnabledOptionKey, isOn);
            }, rowHeight, rowGap);
        }
    }

    private void AddMatrixCheck(
        Rectangle matrixRect,
        int rowIndex,
        bool isOn,
        Action<bool> onChange,
        int rowHeight = 24,
        int rowGap = 5)
    {
        AddDynamic(new V2CheckControl(MatrixCheckBounds(rowIndex, matrixRect, rowHeight, rowGap), string.Empty, isOn, changed =>
        {
            onChange(changed);
            _callbacks.SaveAndApply();
            Invalidate();
        }));
    }

    private void AddActionButton(Rectangle bounds, string text, Action onClick)
    {
        var button = new V2ActionButton(bounds, text);
        button.Click += (_, _) => onClick();
        AddDynamic(button);
    }

    private void AddActionButton(Rectangle bounds, string text, Func<Task> onClick)
    {
        var button = new V2ActionButton(bounds, text);
        button.Click += async (_, _) => await onClick().ConfigureAwait(true);
        AddDynamic(button);
    }

    private void AddDynamic(Control control)
    {
        control.Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, control.Font.Size, control.Font.Style);
        _dynamicControls.Add(control);
        Controls.Add(control);
        control.BringToFront();
    }

    private void DrawBackdrop(Graphics graphics, Rectangle bounds)
    {
        FillGradient(graphics, bounds, [BgTop, BgMid, BgBottom], -55f);

        var sun = new Rectangle(bounds.Width - 288, 48, 176, 176);
        using (var path = RoundPath(sun, 88))
        {
            graphics.SetClip(path);
            FillGradient(graphics, sun, [Amber, Orange, Magenta, Purple], 90f);
            for (var offset = 44; offset <= 142; offset += 22)
            {
                FillRounded(graphics, new Rectangle(bounds.Width - 300, 48 + offset, 200, offset > 100 ? 12 : 8), 0, Rgba(18, 5, 31, 235));
            }
            graphics.ResetClip();
        }

        var gridTop = (int)(bounds.Height * 0.58);
        FillGradient(graphics, new Rectangle(0, gridTop, bounds.Width, bounds.Height - gridTop), [Rgba(0, 232, 255, 5), Rgba(0, 232, 255, 32), Rgba(255, 42, 167, 102)], 90f);
        using var cyanPen = new Pen(Rgba(0, 232, 255, 82), 1f);
        for (var y = gridTop + 16; y <= bounds.Height - 14; y += 24)
        {
            graphics.DrawLine(cyanPen, 0, y, bounds.Width, y);
        }

        using var magentaPen = new Pen(Rgba(255, 42, 167, 108), 1f);
        for (var x = -180; x <= bounds.Width + 180; x += 150)
        {
            graphics.DrawLine(magentaPen, bounds.Width / 2, gridTop, x, bounds.Height);
        }
    }

    private void DrawWindowShell(Graphics graphics)
    {
        FillRounded(graphics, new Rectangle(ShellX, ShellY, ShellWidth, ShellHeight), 18, Rgba(0, 0, 0, 72));
        FillGradient(graphics, new Rectangle(ShellX, ShellY, ShellWidth, ShellHeight), [Rgb(8, 10, 23), Rgb(15, 9, 32), Rgb(5, 20, 37)], -25f, 18);
        StrokeRounded(graphics, new Rectangle(ShellX, ShellY, ShellWidth, ShellHeight), 18, Rgba(0, 232, 255, 200), 1.4f);
        FillRounded(graphics, new Rectangle(ShellX, ShellY, ShellWidth, 58), 18, TitleBar);
        using var magentaBrush = new SolidBrush(Magenta);
        graphics.FillRectangle(magentaBrush, ShellX, 92, ShellWidth, 2);
        using var cyanBrush = new SolidBrush(Cyan);
        graphics.FillRectangle(cyanBrush, ShellX, 94, ShellWidth, 1);
    }

    private void DrawTitleBar(Graphics graphics)
    {
        var logoRect = new Rectangle(66, 50, 50, 30);
        if (_brandLogo is not null)
        {
            DrawAspectFit(graphics, _brandLogo, logoRect);
        }
        else
        {
            FillRounded(graphics, new Rectangle(66, 54, 46, 24), 5, PanelRaised);
            StrokeRounded(graphics, new Rectangle(66, 54, 46, 24), 5, Magenta, 1.2f);
            DrawCentered(graphics, "TMR", new Rectangle(66, 53, 46, 24), 14f, FontStyle.Bold, TextPrimary);
        }

        DrawText(graphics, "Tech Mates Racing Overlay", new Rectangle(128, 52, 480, 28), 24f, FontStyle.Bold, TextPrimary);
        DrawText(graphics, "Settings control plane - Windows Design V2", new Rectangle(129, 75, 480, 16), 12f, FontStyle.Regular, TextMuted);
        DrawCentered(graphics, "X", CloseButtonBounds(), 13f, FontStyle.Bold, Rgb(255, 200, 239));
    }

    private void DrawSidebar(Graphics graphics)
    {
        FillRounded(graphics, new Rectangle(SidebarX, SidebarY, SidebarWidth, SidebarHeight), 14, Rgba(6, 13, 26, 235));
        StrokeRounded(graphics, new Rectangle(SidebarX, SidebarY, SidebarWidth, SidebarHeight), 14, BorderDim, 1f);
        DrawText(graphics, "SETTINGS", new Rectangle(84, 136, 110, 18), 12f, FontStyle.Bold, Cyan);

        for (var index = 0; index < _sidebarTabs.Count; index++)
        {
            var tab = _sidebarTabs[index];
            var bounds = SidebarButtonBounds(index);
            var active = string.Equals(tab.Id, _selectedTabId, StringComparison.OrdinalIgnoreCase);
            FillRounded(graphics, bounds, 8, active ? Rgb(48, 16, 68) : Rgb(17, 26, 50));
            if (active)
            {
                StrokeRounded(graphics, bounds, 8, Magenta, 1.3f);
                FillRounded(graphics, new Rectangle(bounds.Left, bounds.Top, 5, bounds.Height), 3, Cyan);
            }

            DrawText(
                graphics,
                tab.Label,
                new Rectangle(bounds.Left + 14, bounds.Top + 7, bounds.Width - 30, 16),
                11.5f,
                active ? FontStyle.Bold : FontStyle.Regular,
                active ? TextPrimary : Rgb(185, 217, 255));
        }
    }

    private void DrawContentContainer(Graphics graphics)
    {
        FillRounded(graphics, ContentBounds(), 16, Rgba(8, 17, 33, 240));
        StrokeRounded(graphics, ContentBounds(), 16, Border, 1.2f);
    }

    private void DrawContentHeader(Graphics graphics, string title, string subtitle, string? status = null)
    {
        FillRounded(graphics, new Rectangle(ContentX, ContentY, ContentWidth, 70), 16, Rgba(16, 22, 50, 230));
        using var magentaBrush = new SolidBrush(Magenta);
        graphics.FillRectangle(magentaBrush, ContentX, 184, ContentWidth, 2);
        using var cyanBrush = new SolidBrush(Cyan);
        graphics.FillRectangle(cyanBrush, ContentX, 186, ContentWidth, 1);
        DrawText(graphics, title, new Rectangle(306, 134, 520, 32), 26f, FontStyle.Bold, TextPrimary);
        DrawText(graphics, subtitle, new Rectangle(306, 164, 570, 18), 12f, FontStyle.Regular, TextMuted);
        if (!string.IsNullOrWhiteSpace(status))
        {
            DrawPill(graphics, status, new Rectangle(1012, 134, 112, 30), Rgb(10, 47, 63), Cyan);
        }
    }

    private void DrawSegments(Graphics graphics, IReadOnlyList<SegmentSpec> segments, SettingsRegion selected)
    {
        var shell = new Rectangle(306, 202, SegmentShellWidth(segments), 42);
        FillRounded(graphics, shell, 21, Rgb(8, 15, 31));
        StrokeRounded(graphics, shell, 21, BorderDim, 1f);

        foreach (var (region, bounds) in SegmentBounds(segments))
        {
            var active = region == selected;
            if (active)
            {
                FillRounded(graphics, bounds, 15, Magenta);
            }

            DrawCentered(graphics, RegionTitle(region), new Rectangle(bounds.Left, bounds.Top - 1, bounds.Width, bounds.Height), 12f, FontStyle.Bold, active ? TextPrimary : Cyan);
        }
    }

    private void DrawApplicationGeneralPage(Graphics graphics)
    {
        DrawPanel(graphics, new Rectangle(306, 214, 392, 132), "Units");
        DrawText(graphics, "Measurement system", new Rectangle(328, 281, 160, 18), 13f, FontStyle.Regular, TextSecondary);
    }

    private void DrawSupportPage(Graphics graphics)
    {
        var capture = _captureState.Snapshot();
        var appStatus = SupportStatusText.AppStatus(capture);
        var update = _releaseUpdates.Snapshot();
        var diagnostics = _diagnosticsBundleService.Snapshot();
        var latestPath = diagnostics.LastBundlePath ?? _callbacks.LatestDiagnosticsBundlePath();

        DrawPanel(graphics, new Rectangle(306, 214, 392, 170), "Diagnostic Capture");
        DrawText(
            graphics,
            capture.RawCaptureActive ? "Raw diagnostic telemetry active" : "Raw diagnostic telemetry",
            new Rectangle(400, 282, 250, 18),
            13f,
            FontStyle.Bold,
            TextPrimary);
        DrawBodyLines(graphics, ["Capture writes raw frames only when explicitly requested.", "Live overlay diagnostics remain lightweight by default."], 328, 320, 326);

        DrawPanel(graphics, new Rectangle(726, 214, 414, 170), "Current State");
        DrawStatusRow(graphics, "App", appStatus.Text, 280, ColorForSupportStatus(appStatus.Level));
        DrawStatusRow(graphics, "iRacing", capture.IsConnected ? "Connected" : "Waiting", 314, capture.IsConnected ? Green : TextSecondary);
        DrawStatusRow(graphics, "Session", SupportStatusText.SessionStateText(capture), 348, capture.IsCapturing ? Cyan : TextSecondary);

        DrawPanel(graphics, new Rectangle(306, 410, 834, 156), "Support Bundle");
        DrawText(graphics, "Latest bundle", new Rectangle(328, 478, 110, 18), 13f, FontStyle.Regular, TextMuted);
        DrawText(graphics, SupportStatusText.LatestBundleDisplayText(latestPath), new Rectangle(456, 477, 420, 18), 12f, FontStyle.Bold, TextPrimary, monospaced: true);
        DrawText(graphics, _callbacks.AdvancedDiagnosticsText().Replace(Environment.NewLine, " / "), new Rectangle(328, 548, 400, 18), 10f, FontStyle.Regular, TextDim);
        DrawText(graphics, "Updates", new Rectangle(754, 552, 70, 18), 11f, FontStyle.Regular, TextMuted);
        DrawText(graphics, ReleaseUpdateSupportText(update), new Rectangle(824, 552, 80, 18), 10f, FontStyle.Bold, ColorForReleaseUpdateStatus(update.Status));
        if (!string.IsNullOrWhiteSpace(_supportStatusText))
        {
            DrawText(graphics, _supportStatusText, new Rectangle(328, 578, 620, 18), 11f, FontStyle.Bold, _supportStatusIsError ? OverlayTheme.Colors.WarningText : Green);
        }
    }

    private void DrawOverlayPage(Graphics graphics, OverlayDefinition definition, OverlaySettings settings)
    {
        switch (_selectedRegion)
        {
            case SettingsRegion.General:
                DrawOverlayGeneralPage(graphics, definition, settings);
                break;
            case SettingsRegion.Content:
                DrawOverlayContentPage(graphics, definition, settings);
                break;
            case SettingsRegion.Header:
                DrawChromePage(graphics, definition, settings, "Header", "Status");
                break;
            case SettingsRegion.Footer:
                DrawChromePage(graphics, definition, settings, "Footer", "Source");
                break;
        }
    }

    private void DrawOverlayGeneralPage(Graphics graphics, OverlayDefinition definition, OverlaySettings settings)
    {
        var isGarageCover = string.Equals(definition.Id, "garage-cover", StringComparison.OrdinalIgnoreCase);
        DrawPanel(graphics, new Rectangle(306, 272, 392, 226), "Overlay Controls");
        if (isGarageCover)
        {
            DrawText(graphics, "Browser source", new Rectangle(328, 334, 120, 18), 13f, FontStyle.Regular, TextSecondary);
            DrawText(graphics, "Local privacy cover route", new Rectangle(454, 334, 180, 18), 12f, FontStyle.Bold, TextPrimary);
        }
        else
        {
            DrawText(graphics, "Visible", new Rectangle(328, 334, 100, 18), 13f, FontStyle.Regular, TextSecondary);
        }

        if (definition.ShowScaleControl)
        {
            DrawText(graphics, "Scale", new Rectangle(328, 374, 100, 18), 13f, FontStyle.Regular, TextSecondary);
            DrawText(graphics, $"{(int)Math.Round(settings.Scale * 100d)}%", new Rectangle(642, 371, 40, 18), 12f, FontStyle.Bold, TextPrimary, alignment: StringAlignment.Far);
        }

        if (definition.ShowOpacityControl)
        {
            DrawText(graphics, string.Equals(definition.Id, TrackMapOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase) ? "Map fill" : "Opacity", new Rectangle(328, 414, 100, 18), 13f, FontStyle.Regular, TextSecondary);
            DrawText(graphics, $"{(int)Math.Round(settings.Opacity * 100d)}%", new Rectangle(642, 411, 40, 18), 12f, FontStyle.Bold, TextPrimary, alignment: StringAlignment.Far);
        }

        DrawText(graphics, isGarageCover ? "Cover image" : "Sessions", new Rectangle(328, 454, 100, 18), 13f, FontStyle.Regular, TextSecondary);
        if (isGarageCover)
        {
            DrawText(graphics, GarageCoverImageLabel(settings), new Rectangle(454, 454, 180, 18), 12f, FontStyle.Bold, TextSecondary);
        }
        else if (!definition.ShowSessionFilters)
        {
            DrawText(graphics, "Managed by overlay logic", new Rectangle(454, 454, 180, 18), 12f, FontStyle.Regular, TextMuted);
        }

        var previewTitle = string.Equals(definition.Id, "garage-cover", StringComparison.OrdinalIgnoreCase)
            ? "Cover Image"
            : $"{definition.DisplayName} Preview";
        DrawPanel(graphics, new Rectangle(726, 272, 414, 226), previewTitle);
        var previewRect = string.Equals(definition.Id, "garage-cover", StringComparison.OrdinalIgnoreCase)
            ? new Rectangle(750, 324, 366, 96)
            : new Rectangle(750, 324, 366, 132);
        FillRounded(graphics, previewRect, 10, Rgb(3, 8, 18));
        StrokeRounded(graphics, previewRect, 10, Rgba(0, 232, 255, 165), 1f);

        if (string.Equals(definition.Id, "garage-cover", StringComparison.OrdinalIgnoreCase))
        {
            DrawGarageCoverPreview(graphics, previewRect, settings.GetStringOption(OverlayOptionKeys.GarageCoverImagePath));
            DrawText(graphics, "Image", new Rectangle(750, 468, 60, 18), 12f, FontStyle.Regular, TextMuted);
            DrawText(graphics, GarageCoverImageLabel(settings), new Rectangle(814, 468, 292, 18), 12f, FontStyle.Bold, TextSecondary);
        }
        else
        {
            DrawPreviewPlaceholder(graphics, previewRect, definition.DisplayName);
            DrawText(graphics, "Default size", new Rectangle(750, 468, 100, 18), 12f, FontStyle.Regular, TextMuted);
            DrawText(graphics, $"{definition.DefaultWidth} x {definition.DefaultHeight}", new Rectangle(852, 468, 120, 18), 12f, FontStyle.Bold, TextSecondary, monospaced: true);
        }

        DrawBrowserSourcePanel(graphics, definition, settings, new Rectangle(306, 518, 834, 70));
    }

    private void DrawOverlayContentPage(Graphics graphics, OverlayDefinition definition, OverlaySettings settings)
    {
        switch (definition.Id)
        {
            case "relative":
                DrawContentMatrix(graphics, "Content Display", ColumnContentRows(settings, OverlayContentColumnSettings.Relative), new Rectangle(306, 272, 834, 222));
                DrawPanel(graphics, new Rectangle(306, 512, 834, 104), "Relative Rows");
                DrawText(graphics, "Cars each side", new Rectangle(328, 574, 130, 18), 13f, FontStyle.Regular, TextSecondary);
                var eachSide = settings.GetIntegerOption(OverlayOptionKeys.RelativeCarsEachSide, defaultValue: 5, minimum: 0, maximum: 8);
                DrawText(graphics, $"{eachSide * 2 + 1} visible rows", new Rectangle(700, 574, 112, 18), 12f, FontStyle.Bold, TextPrimary, alignment: StringAlignment.Far);
                break;
            case "standings":
                DrawContentMatrix(graphics, "Content Display", ColumnContentRows(settings, OverlayContentColumnSettings.Standings), new Rectangle(306, 272, 834, 236), rowHeight: 22, rowGap: 3);
                DrawPanel(graphics, new Rectangle(306, 520, 834, 102), "Class Separators");
                break;
            case "gap-to-leader":
                DrawContentMatrix(graphics, "Content Display", [new ContentMatrixRow("Class gap window", true)], new Rectangle(306, 272, 834, 126));
                DrawPanel(graphics, new Rectangle(306, 426, 834, 126), "Class Gap Window");
                DrawText(graphics, "Cars each side", new Rectangle(328, 498, 130, 18), 13f, FontStyle.Regular, TextSecondary);
                DrawText(graphics, "Keeps the focused class gap trend bounded around the team car.", new Rectangle(328, 532, 560, 18), 12f, FontStyle.Regular, TextMuted);
                break;
            case "fuel-calculator":
                DrawContentMatrix(
                    graphics,
                    "Content Display",
                    [
                        new ContentMatrixRow("Advice column", settings.GetBooleanOption(OverlayOptionKeys.FuelAdvice, true)),
                        new ContentMatrixRow("Source row", settings.GetBooleanOption(OverlayOptionKeys.FuelSource, true))
                    ],
                    new Rectangle(306, 272, 834, 150));
                break;
            case "track-map":
                DrawContentMatrix(
                    graphics,
                    "Content Display",
                    [
                        new ContentMatrixRow("Map source", true),
                        new ContentMatrixRow("Sector boundaries", settings.GetBooleanOption(OverlayOptionKeys.TrackMapSectorBoundariesEnabled, true)),
                        new ContentMatrixRow("Local map building", settings.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, true))
                    ],
                    new Rectangle(306, 272, 834, 190));
                DrawPanel(graphics, new Rectangle(306, 484, 834, 110), "Map Sources");
                DrawText(graphics, "Source", new Rectangle(328, 542, 90, 18), 13f, FontStyle.Regular, TextSecondary);
                DrawText(graphics, "Best bundled or local map; circle fallback", new Rectangle(454, 542, 320, 18), 12f, FontStyle.Regular, TextPrimary);
                DrawText(graphics, "Local maps", new Rectangle(328, 574, 110, 18), 13f, FontStyle.Regular, TextSecondary);
                DrawText(graphics, "Reviewed app maps load automatically for matching tracks.", new Rectangle(454, 574, 390, 18), 12f, FontStyle.Regular, TextSecondary);
                break;
            case "stream-chat":
                DrawStreamChatContentPage(graphics, definition);
                break;
            case "input-state":
                DrawContentMatrix(graphics, "Content Display", BlockContentRows(settings, OverlayContentColumnSettings.InputState), new Rectangle(306, 272, 834, 236), rowHeight: 22, rowGap: 3);
                break;
            case "car-radar":
                DrawContentMatrix(
                    graphics,
                    "Content Display",
                    [
                        new ContentMatrixRow("Radar proximity", true),
                        new ContentMatrixRow("Multiclass warning", settings.GetBooleanOption(OverlayOptionKeys.RadarMulticlassWarning, true))
                    ],
                    new Rectangle(306, 272, 834, 150));
                break;
            case "flags":
                DrawContentMatrix(
                    graphics,
                    "Content Display",
                    [
                        new ContentMatrixRow("Green", settings.GetBooleanOption(OverlayOptionKeys.FlagsShowGreen, true)),
                        new ContentMatrixRow("Blue", settings.GetBooleanOption(OverlayOptionKeys.FlagsShowBlue, true)),
                        new ContentMatrixRow("Yellow", settings.GetBooleanOption(OverlayOptionKeys.FlagsShowYellow, true)),
                        new ContentMatrixRow("Red / black", settings.GetBooleanOption(OverlayOptionKeys.FlagsShowCritical, true)),
                        new ContentMatrixRow("White / checkered", settings.GetBooleanOption(OverlayOptionKeys.FlagsShowFinish, true))
                    ],
                    new Rectangle(306, 272, 834, 240));
                break;
            default:
                DrawContentMatrix(graphics, "Content Display", [new ContentMatrixRow("Content", true)], new Rectangle(306, 272, 834, 126));
                DrawText(graphics, "This matches the current production settings surface for this overlay.", new Rectangle(328, 410, 560, 18), 12f, FontStyle.Regular, TextMuted);
                break;
        }
    }

    private void DrawStreamChatContentPage(Graphics graphics, OverlayDefinition definition)
    {
        DrawPanel(graphics, new Rectangle(306, 272, 834, 204), "Chat Source");
        DrawText(graphics, "Visible", new Rectangle(328, 336, 90, 18), 13f, FontStyle.Regular, TextSecondary);
        DrawText(graphics, "Mode", new Rectangle(328, 374, 90, 18), 13f, FontStyle.Regular, TextSecondary);
        DrawText(graphics, "Streamlabs URL", new Rectangle(328, 412, 120, 18), 13f, FontStyle.Regular, TextSecondary);
        DrawText(graphics, "Twitch channel", new Rectangle(328, 450, 120, 18), 13f, FontStyle.Regular, TextSecondary);
        DrawPanel(graphics, new Rectangle(306, 500, 834, 92), "Localhost");
        DrawLocalhostBox(graphics, definition, new Rectangle(462, 552, 470, 30));
        DrawText(graphics, "OBS browser source", new Rectangle(328, 560, 120, 18), 13f, FontStyle.Regular, TextSecondary);
    }

    private void DrawChromePage(Graphics graphics, OverlayDefinition definition, OverlaySettings settings, string title, string itemLabel)
    {
        DrawPanel(graphics, new Rectangle(306, 272, 834, 188), title);
        if (!SupportsSharedChromeSettings(definition.Id))
        {
            DrawText(graphics, $"No {title.ToLowerInvariant()} controls yet.", new Rectangle(328, 334, 420, 18), 13f, FontStyle.Regular, TextSecondary);
            DrawText(graphics, "This matches the current production settings surface for this overlay.", new Rectangle(328, 372, 560, 18), 12f, FontStyle.Regular, TextMuted);
            return;
        }

        DrawText(graphics, "Item", new Rectangle(328, 330, 110, 16), 10f, FontStyle.Bold, TextMuted);
        for (var index = 0; index < SessionLabels.Length; index++)
        {
            DrawText(graphics, SessionLabels[index], new Rectangle(454 + index * 116, 330, 104, 16), 10f, FontStyle.Bold, TextMuted);
        }

        var rowBounds = new Rectangle(328, 360, 768, 44);
        FillRounded(graphics, rowBounds, 8, Rgba(17, 28, 55, 200));
        StrokeRounded(graphics, rowBounds, 8, BorderDim, 1f);
        DrawText(graphics, itemLabel, new Rectangle(346, 373, 110, 18), 13f, FontStyle.Regular, TextSecondary);
    }

    private void DrawContentMatrix(
        Graphics graphics,
        string title,
        IReadOnlyList<ContentMatrixRow> rows,
        Rectangle rect,
        int rowHeight = 24,
        int rowGap = 5)
    {
        DrawPanel(graphics, rect, title);
        DrawText(graphics, "Item", new Rectangle(328, rect.Top + 58, 110, 16), 10f, FontStyle.Bold, TextMuted);
        for (var index = 0; index < SessionLabels.Length; index++)
        {
            DrawText(graphics, SessionLabels[index], new Rectangle(548 + index * 116, rect.Top + 58, 104, 16), 10f, FontStyle.Bold, TextMuted);
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var rowY = rect.Top + 78 + index * (rowHeight + rowGap);
            if (rowY + rowHeight > rect.Bottom - 10)
            {
                break;
            }

            var rowBounds = new Rectangle(328, rowY, 768, rowHeight);
            FillRounded(graphics, rowBounds, 8, Rgba(17, 28, 55, 200));
            StrokeRounded(graphics, rowBounds, 8, BorderDim, 1f);
            DrawCheckBox(graphics, MatrixCheckBounds(index, rect, rowHeight, rowGap), row.Enabled);
            DrawText(graphics, row.Label, new Rectangle(376, rowY + 5, 150, 16), 12f, FontStyle.Regular, row.Enabled ? TextSecondary : TextDim);

            var situationStates = SituationStates(row.Enabled);
            for (var sessionIndex = 0; sessionIndex < situationStates.Length; sessionIndex++)
            {
                DrawCheckBox(graphics, new Rectangle(556 + sessionIndex * 116, rowY + 3, 22, 18), situationStates[sessionIndex]);
            }
        }
    }

    private void DrawBrowserSourcePanel(Graphics graphics, OverlayDefinition definition, OverlaySettings settings, Rectangle bounds)
    {
        DrawPanel(graphics, bounds, "Browser Source");
        DrawLocalhostBox(graphics, definition, new Rectangle(462, 542, 470, 30));
        var browserSize = BrowserOverlayRecommendedSize.For(definition, settings);
        DrawText(graphics, $"OBS browser size {browserSize.Width} x {browserSize.Height}; native visibility is controlled separately.", new Rectangle(328, 594, 620, 18), 11f, FontStyle.Regular, TextDim);
    }

    private void DrawLocalhostBox(Graphics graphics, OverlayDefinition definition, Rectangle bounds)
    {
        FillRounded(graphics, bounds, 8, Rgb(4, 9, 20));
        StrokeRounded(graphics, bounds, 8, BorderDim, 1f);
        var text = BrowserOverlayCatalog.TryGetRouteForOverlayId(definition.Id, out var route)
            ? $"{_localhostOverlayOptions.Prefix.TrimEnd('/')}{route}"
            : "No localhost route";
        DrawText(graphics, text, new Rectangle(bounds.Left + 16, bounds.Top + 8, bounds.Width - 40, 18), 12f, FontStyle.Regular, Rgb(159, 220, 255), monospaced: true);
    }

    private void DrawPanel(Graphics graphics, Rectangle rect, string title)
    {
        FillRounded(graphics, rect, 12, Rgba(9, 18, 34, 245));
        StrokeRounded(graphics, rect, 12, BorderDim, 1f);
        DrawText(graphics, title, new Rectangle(rect.Left + 22, rect.Top + 18, rect.Width - 44, 20), 15f, FontStyle.Bold, TextPrimary);
        using var pen = new Pen(BorderDim);
        graphics.DrawLine(pen, rect.Left + 22, rect.Top + 48, rect.Right - 22, rect.Top + 48);
    }

    private void DrawStatusRow(Graphics graphics, string label, string value, int y, Color color)
    {
        DrawText(graphics, label, new Rectangle(750, y, 110, 18), 13f, FontStyle.Regular, TextMuted);
        FillRounded(graphics, new Rectangle(884, y + 5, 8, 8), 4, color);
        DrawText(graphics, value, new Rectangle(904, y, 190, 18), 13f, FontStyle.Bold, color == TextSecondary ? TextSecondary : TextPrimary);
    }

    private void DrawBodyLines(Graphics graphics, IReadOnlyList<string> lines, int x, int y, int width, float size = 12f)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            DrawText(graphics, lines[index], new Rectangle(x, y + (int)(index * (size + 6)), width, (int)(size + 4)), size, FontStyle.Regular, TextMuted);
        }
    }

    private void DrawCheckBox(Graphics graphics, Rectangle rect, bool isChecked)
    {
        FillRounded(graphics, rect, 5, isChecked ? Rgb(6, 46, 55) : PanelRaised);
        StrokeRounded(graphics, rect, 5, isChecked ? Cyan : Border, 1f);
        if (!isChecked)
        {
            return;
        }

        using var pen = new Pen(Green, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.DrawLine(pen, rect.Left + 5, rect.Top + rect.Height / 2, rect.Left + 9, rect.Bottom - 4);
        graphics.DrawLine(pen, rect.Left + 9, rect.Bottom - 4, rect.Right - 4, rect.Top + 5);
    }

    private void DrawPreviewPlaceholder(Graphics graphics, Rectangle rect, string text)
    {
        using var pen = new Pen(Rgba(0, 232, 255, 80));
        for (var y = rect.Top + 12; y < rect.Bottom - 8; y += 16)
        {
            graphics.DrawLine(pen, rect.Left + 12, y, rect.Right - 12, y);
        }

        DrawCentered(graphics, text, rect, 16f, FontStyle.Bold, Rgba(208, 230, 255, 180));
    }

    private void DrawGarageCoverPreview(Graphics graphics, Rectangle rect, string? imagePath)
    {
        using var image = GarageCoverImageStore.TryLoadPreviewImage(imagePath);
        if (image is not null && image.Width > 0 && image.Height > 0)
        {
            DrawAspectFill(graphics, image, Rectangle.Inflate(rect, -12, -10));
            return;
        }

        DrawCentered(graphics, "TMR", rect, 24f, FontStyle.Bold, TextPrimary);
    }

    private static void DrawAspectFit(Graphics graphics, Image image, Rectangle rect)
    {
        var scale = Math.Min(rect.Width / (double)image.Width, rect.Height / (double)image.Height);
        var width = (int)Math.Round(image.Width * scale);
        var height = (int)Math.Round(image.Height * scale);
        var target = new Rectangle(rect.Left + (rect.Width - width) / 2, rect.Top + (rect.Height - height) / 2, width, height);
        graphics.DrawImage(image, target);
    }

    private static void DrawAspectFill(Graphics graphics, Image image, Rectangle rect)
    {
        var scale = Math.Max(rect.Width / (double)image.Width, rect.Height / (double)image.Height);
        var width = (int)Math.Round(image.Width * scale);
        var height = (int)Math.Round(image.Height * scale);
        var target = new Rectangle(rect.Left + (rect.Width - width) / 2, rect.Top + (rect.Height - height) / 2, width, height);
        graphics.DrawImage(image, target);
    }

    private void DrawPill(Graphics graphics, string text, Rectangle rect, Color fill, Color textColor)
    {
        FillRounded(graphics, rect, rect.Height / 2, fill);
        StrokeRounded(graphics, rect, rect.Height / 2, Rgba(255, 255, 255, 42), 1f);
        DrawCentered(graphics, text, Rectangle.Inflate(rect, -8, 0), 12f, FontStyle.Bold, textColor);
    }

    private static void DrawText(
        Graphics graphics,
        string text,
        Rectangle rect,
        float size,
        FontStyle style,
        Color color,
        StringAlignment alignment = StringAlignment.Near,
        bool monospaced = false)
    {
        using var font = monospaced
            ? OverlayTheme.MonospaceFont(size, style)
            : OverlayTheme.Font(OverlayTheme.DefaultFontFamily, size, style);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = alignment,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(text, font, brush, rect, format);
    }

    private static void DrawCentered(Graphics graphics, string text, Rectangle rect, float size, FontStyle style, Color color, bool monospaced = false)
    {
        using var font = monospaced
            ? OverlayTheme.MonospaceFont(size, style)
            : OverlayTheme.Font(OverlayTheme.DefaultFontFamily, size, style);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.DrawString(text, font, brush, rect, format);
    }

    private static void FillRounded(Graphics graphics, Rectangle rect, int radius, Color color)
    {
        using var brush = new SolidBrush(color);
        if (radius <= 0)
        {
            graphics.FillRectangle(brush, rect);
            return;
        }

        using var path = RoundPath(rect, radius);
        graphics.FillPath(brush, path);
    }

    private static void StrokeRounded(Graphics graphics, Rectangle rect, int radius, Color color, float width)
    {
        using var pen = new Pen(color, width);
        using var path = RoundPath(Rectangle.Inflate(rect, -(int)Math.Ceiling(width / 2), -(int)Math.Ceiling(width / 2)), radius);
        graphics.DrawPath(pen, path);
    }

    private static void FillGradient(Graphics graphics, Rectangle rect, IReadOnlyList<Color> colors, float angle, int radius = 0)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using var brush = new LinearGradientBrush(rect, colors[0], colors[^1], angle);
        if (colors.Count > 2)
        {
            var blend = new ColorBlend(colors.Count)
            {
                Colors = colors.ToArray(),
                Positions = Enumerable.Range(0, colors.Count)
                    .Select(index => colors.Count == 1 ? 0f : index / (float)(colors.Count - 1))
                    .ToArray()
            };
            brush.InterpolationColors = blend;
        }

        if (radius <= 0)
        {
            graphics.FillRectangle(brush, rect);
            return;
        }

        using var path = RoundPath(rect, radius);
        graphics.FillPath(brush, path);
    }

    private static GraphicsPath RoundPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            path.CloseFigure();
            return path;
        }

        var diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private OverlaySettings OverlaySettingsFor(OverlayDefinition definition)
    {
        return _applicationSettings.GetOrAddOverlay(
            definition.Id,
            definition.DefaultWidth,
            definition.DefaultHeight,
            defaultEnabled: false);
    }

    private IReadOnlyList<ContentMatrixRow> ColumnContentRows(OverlaySettings settings, OverlayContentDefinition contentDefinition)
    {
        return OverlayContentColumnSettings.ColumnsFor(settings, contentDefinition)
            .Select(column => new ContentMatrixRow(column.Label, column.Enabled))
            .ToArray();
    }

    private IReadOnlyList<ContentMatrixRow> BlockContentRows(OverlaySettings settings, OverlayContentDefinition contentDefinition)
    {
        return (contentDefinition.Blocks ?? [])
            .Select(block => new ContentMatrixRow(block.Label, OverlayContentColumnSettings.BlockEnabled(settings, block)))
            .ToArray();
    }

    private bool[] SituationStates(bool rowEnabled)
    {
        if (!rowEnabled)
        {
            return [false, false, false, false];
        }

        if (string.Equals(_selectedTabId, GapToLeaderOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            return [false, false, false, true];
        }

        if (!_overlayById.TryGetValue(_selectedTabId, out var definition) || !definition.ShowSessionFilters)
        {
            return [true, true, true, true];
        }

        var settings = OverlaySettingsFor(definition);
        return [settings.ShowInTest, settings.ShowInPractice, settings.ShowInQualifying, settings.ShowInRace];
    }

    private IReadOnlyList<SettingsRegion> AvailableRegions(string overlayId)
    {
        if (string.Equals(overlayId, "garage-cover", StringComparison.OrdinalIgnoreCase))
        {
            return [SettingsRegion.General];
        }

        return SupportsSharedChromeSettings(overlayId)
            ? [SettingsRegion.General, SettingsRegion.Content, SettingsRegion.Header, SettingsRegion.Footer]
            : [SettingsRegion.General, SettingsRegion.Content];
    }

    private IReadOnlyList<SegmentSpec> SegmentsFor(string overlayId)
    {
        return AvailableRegions(overlayId)
            .Select(region => new SegmentSpec(region, region == SettingsRegion.General ? 86 : 76))
            .ToArray();
    }

    private IEnumerable<(SettingsRegion Region, Rectangle Bounds)> SegmentBounds(IReadOnlyList<SegmentSpec> segments)
    {
        var x = 312;
        foreach (var segment in segments)
        {
            yield return (segment.Region, new Rectangle(x, 208, segment.Width, 30));
            x += segment.Width + 12;
        }
    }

    private static int SegmentShellWidth(IReadOnlyList<SegmentSpec> segments)
    {
        return segments.Count == 0
            ? 0
            : segments.Sum(segment => segment.Width) + 12 * (segments.Count - 1) + 12;
    }

    private bool IsKnownTab(string tabId)
    {
        return string.Equals(tabId, GeneralTabId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tabId, SupportTabId, StringComparison.OrdinalIgnoreCase)
            || _overlayById.ContainsKey(tabId);
    }

    private static IReadOnlyList<SidebarTab> BuildSidebarTabs(IReadOnlyList<OverlayDefinition> overlays)
    {
        var tabs = new List<SidebarTab>
        {
            new(GeneralTabId, "General")
        };
        var byId = overlays.ToDictionary(overlay => overlay.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var preferredId in PreferredOverlayTabOrder)
        {
            if (byId.TryGetValue(preferredId, out var overlay))
            {
                tabs.Add(new SidebarTab(overlay.Id, overlay.DisplayName));
            }
        }

        foreach (var overlay in overlays)
        {
            if (!PreferredOverlayTabOrder.Contains(overlay.Id, StringComparer.OrdinalIgnoreCase))
            {
                tabs.Add(new SidebarTab(overlay.Id, overlay.DisplayName));
            }
        }

        tabs.Add(new SidebarTab(SupportTabId, "Support"));
        return tabs;
    }

    private Rectangle SidebarButtonBounds(int index)
    {
        return new Rectangle(78, 164 + index * 32, 162, 27);
    }

    private Rectangle ContentBounds()
    {
        return new Rectangle(ContentX, ContentY, ContentWidth, ContentHeight);
    }

    private static Rectangle CloseButtonBounds()
    {
        return new Rectangle(1132, 54, 30, 24);
    }

    private static Rectangle TitleDragBounds()
    {
        return new Rectangle(ShellX, ShellY, ShellWidth - 80, 58);
    }

    private static Rectangle MatrixCheckBounds(int rowIndex, Rectangle rect, int rowHeight = 24, int rowGap = 5)
    {
        var rowY = rect.Top + 78 + rowIndex * (rowHeight + rowGap);
        return new Rectangle(344, rowY + Math.Max(2, (rowHeight - 19) / 2), 19, 19);
    }

    private static TextBox CreateTextBox(string text, Rectangle bounds, bool enabled)
    {
        return new TextBox
        {
            BackColor = Rgb(4, 9, 20),
            BorderStyle = BorderStyle.FixedSingle,
            Enabled = enabled,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9f),
            ForeColor = enabled ? TextPrimary : TextDim,
            Location = bounds.Location,
            Size = bounds.Size,
            TabStop = true,
            Text = text
        };
    }

    private static string GarageCoverImageLabel(OverlaySettings settings)
    {
        var imagePath = settings.GetStringOption(OverlayOptionKeys.GarageCoverImagePath);
        return string.IsNullOrWhiteSpace(imagePath)
            ? "No image imported"
            : Path.GetFileName(imagePath);
    }

    private static string ProviderLabel(string provider)
    {
        return provider switch
        {
            StreamChatOverlaySettings.ProviderStreamlabs => "Streamlabs",
            StreamChatOverlaySettings.ProviderTwitch => "Twitch",
            _ => "Not configured"
        };
    }

    private static string ProviderFromLabel(string label)
    {
        return label switch
        {
            "Streamlabs" => StreamChatOverlaySettings.ProviderStreamlabs,
            "Twitch" => StreamChatOverlaySettings.ProviderTwitch,
            _ => StreamChatOverlaySettings.ProviderNone
        };
    }

    private static string SubtitleFor(string overlayId)
    {
        return overlayId switch
        {
            "standings" => "Class and overall running order for the current session.",
            "relative" => "Nearby-car timing around the local in-car reference.",
            "gap-to-leader" => "Focused class gap trend and nearby leader context.",
            "fuel-calculator" => "Fuel strategy, stint targets, and source confidence.",
            "session-weather" => "Session timing, track state, and weather telemetry.",
            "pit-service" => "Pit request state, service plan, and release context.",
            "track-map" => "Live car location, sector context, and local map sources.",
            "stream-chat" => "Local browser-source chat setup for Streamlabs or Twitch.",
            "garage-cover" => "Local browser-source privacy cover for garage and setup scenes.",
            "input-state" => "Input rail visibility for pedal, steering, gear, and speed telemetry.",
            "car-radar" => "Local proximity radar and multiclass approach warning controls.",
            "flags" => "Compact session flag strip display and size controls.",
            _ => "Overlay settings and browser-source controls."
        };
    }

    private static string RegionTitle(SettingsRegion region)
    {
        return region switch
        {
            SettingsRegion.General => "General",
            SettingsRegion.Content => "Content",
            SettingsRegion.Header => "Header",
            SettingsRegion.Footer => "Footer",
            _ => "General"
        };
    }

    private static bool SupportsSharedChromeSettings(string overlayId)
    {
        return overlayId is "standings" or "relative" or "fuel-calculator" or "gap-to-leader";
    }

    private static int ScaleDimension(int defaultDimension, double scale)
    {
        return Math.Max(80, (int)Math.Round(defaultDimension * Math.Clamp(scale, 0.6d, 2d)));
    }

    private static string ContentColumnEnabledKey(string overlayId, string columnId)
    {
        return $"{overlayId}.content.{columnId}.enabled";
    }

    private static int ClosestPercent(double value, IReadOnlyList<int> allowedValues)
    {
        var percent = (int)Math.Round(value * 100d);
        return allowedValues.OrderBy(candidate => Math.Abs(candidate - percent)).FirstOrDefault();
    }

    private static string ReleaseUpdateSupportText(ReleaseUpdateSnapshot snapshot)
    {
        return snapshot.Status switch
        {
            ReleaseUpdateStatus.Disabled => "Disabled.",
            ReleaseUpdateStatus.NotInstalled => "Dev run.",
            ReleaseUpdateStatus.Idle => "Ready.",
            ReleaseUpdateStatus.Checking => "Checking...",
            ReleaseUpdateStatus.UpToDate => $"Current v{snapshot.CurrentVersion}.",
            ReleaseUpdateStatus.Available => $"v{snapshot.LatestVersion} available.",
            ReleaseUpdateStatus.PendingRestart => $"v{snapshot.LatestVersion} pending restart.",
            ReleaseUpdateStatus.Failed => string.IsNullOrWhiteSpace(snapshot.LastError) ? "Check failed." : snapshot.LastError,
            _ => "Unknown."
        };
    }

    private static Color ColorForReleaseUpdateStatus(ReleaseUpdateStatus status)
    {
        return status switch
        {
            ReleaseUpdateStatus.Available or ReleaseUpdateStatus.PendingRestart => OverlayTheme.Colors.WarningText,
            ReleaseUpdateStatus.UpToDate => OverlayTheme.Colors.SuccessText,
            ReleaseUpdateStatus.Failed => OverlayTheme.Colors.ErrorText,
            ReleaseUpdateStatus.Checking => OverlayTheme.Colors.InfoText,
            _ => TextMuted
        };
    }

    private static Color ColorForSupportStatus(SupportStatusLevel level)
    {
        return level switch
        {
            SupportStatusLevel.Error => OverlayTheme.Colors.ErrorText,
            SupportStatusLevel.Warning => OverlayTheme.Colors.WarningText,
            SupportStatusLevel.Success => OverlayTheme.Colors.SuccessText,
            SupportStatusLevel.Info => OverlayTheme.Colors.InfoText,
            _ => TextSecondary
        };
    }

    private static Color Rgb(int red, int green, int blue)
    {
        return Color.FromArgb(red, green, blue);
    }

    private static Color Rgba(int red, int green, int blue, int alpha)
    {
        return Color.FromArgb(alpha, red, green, blue);
    }

    private static readonly string[] SessionLabels = ["Test", "Practice", "Qualifying", "Race"];

    private static readonly string[] HeaderChromeKeys =
    [
        OverlayOptionKeys.ChromeHeaderStatusTest,
        OverlayOptionKeys.ChromeHeaderStatusPractice,
        OverlayOptionKeys.ChromeHeaderStatusQualifying,
        OverlayOptionKeys.ChromeHeaderStatusRace
    ];

    private static readonly string[] FooterChromeKeys =
    [
        OverlayOptionKeys.ChromeFooterSourceTest,
        OverlayOptionKeys.ChromeFooterSourcePractice,
        OverlayOptionKeys.ChromeFooterSourceQualifying,
        OverlayOptionKeys.ChromeFooterSourceRace
    ];

    private enum SettingsRegion
    {
        General,
        Content,
        Header,
        Footer
    }

    private sealed record SidebarTab(string Id, string Label);

    private sealed record SegmentSpec(SettingsRegion Region, int Width);

    private sealed record ContentMatrixRow(string Label, bool Enabled);

    private abstract class V2PaintedControl : Control
    {
        protected V2PaintedControl(Rectangle bounds)
        {
            Location = bounds.Location;
            Size = bounds.Size;
            BackColor = Color.Transparent;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor
                | ControlStyles.UserPaint,
                true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Invalidate();
        }
    }

    private sealed class V2ActionButton : V2PaintedControl
    {
        public V2ActionButton(Rectangle bounds, string text)
            : base(bounds)
        {
            Text = text;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            FillRounded(e.Graphics, ClientRectangle, 8, ClientRectangle.Contains(PointToClient(Cursor.Position)) ? Rgb(48, 20, 74) : Rgb(36, 17, 56));
            StrokeRounded(e.Graphics, new Rectangle(0, 0, Width, Height), 8, Rgba(255, 255, 255, 40), 1f);
            DrawCentered(e.Graphics, Text, ClientRectangle, 12f, FontStyle.Bold, TextPrimary);
        }
    }

    private sealed class V2ToggleControl : V2PaintedControl
    {
        private readonly Action<bool> _onChange;

        public V2ToggleControl(Rectangle bounds, bool isOn, Action<bool> onChange)
            : base(bounds)
        {
            IsOn = isOn;
            _onChange = onChange;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool IsOn { get; private set; }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled)
            {
                return;
            }

            IsOn = !IsOn;
            _onChange(IsOn);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = !Enabled
                ? PanelRaised
                : IsOn ? Rgb(6, 46, 55) : Rgb(22, 27, 48);
            FillRounded(e.Graphics, ClientRectangle, Height / 2, fill);
            StrokeRounded(e.Graphics, new Rectangle(0, 0, Width, Height), Height / 2, IsOn ? Cyan : Border, 1f);
            var knobSize = Height - 8;
            var knobX = IsOn ? Width - knobSize - 4 : 4;
            FillRounded(e.Graphics, new Rectangle(knobX, 4, knobSize, knobSize), knobSize / 2, Enabled ? (IsOn ? Green : TextMuted) : TextDim);
        }
    }

    private sealed class V2CheckControl : V2PaintedControl
    {
        private readonly Action<bool> _onChange;

        public V2CheckControl(Rectangle bounds, string text, bool isChecked, Action<bool> onChange)
            : base(bounds)
        {
            Text = text;
            IsChecked = isChecked;
            _onChange = onChange;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool IsChecked { get; private set; }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled)
            {
                return;
            }

            IsChecked = !IsChecked;
            _onChange(IsChecked);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new Rectangle(0, Math.Max(0, (Height - 19) / 2), 19, 19);
            DrawCheckBox(e.Graphics, box, IsChecked);
            if (!string.IsNullOrWhiteSpace(Text))
            {
                DrawText(e.Graphics, Text, new Rectangle(28, 1, Width - 30, Height - 2), 12f, FontStyle.Regular, IsChecked ? TextSecondary : TextDim);
            }
        }
    }

    private sealed class V2ChoiceControl : V2PaintedControl
    {
        private readonly IReadOnlyList<string> _options;
        private readonly Action<string> _onChange;

        public V2ChoiceControl(Rectangle bounds, IReadOnlyList<string> options, string selected, Action<string> onChange)
            : base(bounds)
        {
            _options = options;
            Selected = options.FirstOrDefault(option => string.Equals(option, selected, StringComparison.OrdinalIgnoreCase)) ?? options[0];
            _onChange = onChange;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        private string Selected { get; set; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || _options.Count == 0)
            {
                return;
            }

            var index = Math.Clamp(e.X * _options.Count / Math.Max(1, Width), 0, _options.Count - 1);
            var selected = _options[index];
            if (string.Equals(selected, Selected, StringComparison.Ordinal))
            {
                return;
            }

            Selected = selected;
            _onChange(Selected);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            FillRounded(e.Graphics, ClientRectangle, 15, Rgb(8, 15, 31));
            StrokeRounded(e.Graphics, new Rectangle(0, 0, Width, Height), 15, BorderDim, 1f);
            var segmentWidth = Width / Math.Max(1, _options.Count);
            for (var index = 0; index < _options.Count; index++)
            {
                var bounds = new Rectangle(index * segmentWidth + 3, 3, index == _options.Count - 1 ? Width - index * segmentWidth - 6 : segmentWidth - 6, Height - 6);
                var active = string.Equals(_options[index], Selected, StringComparison.Ordinal);
                if (active)
                {
                    FillRounded(e.Graphics, bounds, 12, Magenta);
                }

                DrawCentered(e.Graphics, _options[index], bounds, 10.5f, FontStyle.Bold, active ? TextPrimary : Cyan);
            }
        }
    }

    private sealed class V2StepperControl : V2PaintedControl
    {
        private readonly int _minimum;
        private readonly int _maximum;
        private readonly Func<int, string> _valueLabel;
        private readonly Action<int> _onChange;

        public V2StepperControl(
            Rectangle bounds,
            int value,
            int minimum,
            int maximum,
            Func<int, string> valueLabel,
            Action<int> onChange)
            : base(bounds)
        {
            Value = Math.Clamp(value, minimum, maximum);
            _minimum = minimum;
            _maximum = maximum;
            _valueLabel = valueLabel;
            _onChange = onChange;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        private int Value { get; set; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var next = e.X < Width / 2
                ? Math.Max(_minimum, Value - 1)
                : Math.Min(_maximum, Value + 1);
            if (next == Value)
            {
                return;
            }

            Value = next;
            _onChange(Value);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            FillRounded(e.Graphics, ClientRectangle, 10, Rgb(17, 30, 60));
            StrokeRounded(e.Graphics, new Rectangle(0, 0, Width, Height), 10, BorderDim, 1f);
            DrawStepButton(e.Graphics, new Rectangle(4, 4, 34, Height - 8), "-", Value > _minimum);
            DrawStepButton(e.Graphics, new Rectangle(Width - 38, 4, 34, Height - 8), "+", Value < _maximum);
            DrawCentered(e.Graphics, _valueLabel(Value), new Rectangle(44, 0, Width - 88, Height), 12f, FontStyle.Bold, TextPrimary);
        }

        private static void DrawStepButton(Graphics graphics, Rectangle rect, string label, bool enabled)
        {
            FillRounded(graphics, rect, 8, enabled ? Rgb(6, 46, 55) : PanelRaised);
            StrokeRounded(graphics, rect, 8, enabled ? Cyan : Border, 1f);
            DrawCentered(graphics, label, rect, 13f, FontStyle.Bold, enabled ? Green : TextDim);
        }
    }

    private sealed class V2PercentSliderControl : V2PaintedControl
    {
        private readonly IReadOnlyList<int> _allowedValues;
        private readonly Color _activeColor;
        private readonly Action<int> _onChange;

        public V2PercentSliderControl(Rectangle bounds, int value, IReadOnlyList<int> allowedValues, Color activeColor, Action<int> onChange)
            : base(bounds)
        {
            _allowedValues = allowedValues;
            Value = allowedValues.Contains(value) ? value : allowedValues.OrderBy(candidate => Math.Abs(candidate - value)).First();
            _activeColor = activeColor;
            _onChange = onChange;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        private int Value { get; set; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_allowedValues.Count == 0)
            {
                return;
            }

            var percent = e.X / (double)Math.Max(1, Width);
            var index = Math.Clamp((int)Math.Round(percent * (_allowedValues.Count - 1)), 0, _allowedValues.Count - 1);
            var next = _allowedValues[index];
            if (next == Value)
            {
                return;
            }

            Value = next;
            _onChange(Value);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new Rectangle(4, Height / 2 - 4, Width - 8, 8);
            FillRounded(e.Graphics, track, 4, Rgb(17, 30, 60));
            var index = 0;
            for (var candidateIndex = 0; candidateIndex < _allowedValues.Count; candidateIndex++)
            {
                if (_allowedValues[candidateIndex] == Value)
                {
                    index = candidateIndex;
                    break;
                }
            }
            var activeWidth = _allowedValues.Count <= 1 ? track.Width : (int)Math.Round(index / (double)(_allowedValues.Count - 1) * track.Width);
            FillRounded(e.Graphics, new Rectangle(track.Left, track.Top, Math.Max(8, activeWidth), track.Height), 4, _activeColor);
            var knobX = track.Left + activeWidth - 7;
            FillRounded(e.Graphics, new Rectangle(Math.Clamp(knobX, track.Left, track.Right - 14), Height / 2 - 7, 14, 14), 7, Green);
        }
    }
}
