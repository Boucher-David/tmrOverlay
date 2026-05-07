using System.Diagnostics;
using System.Drawing;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Brand;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.BrowserSources;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.GarageCover;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal sealed class SettingsOverlayForm : PersistentOverlayForm
{
    private const int SideTabThickness = 38;
    private const int SideTabLength = 174;
    private static readonly TimeSpan SupportStatusRefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SupportHeavyRefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LatestDiagnosticsScanInterval = TimeSpan.FromSeconds(5);

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

    private readonly ApplicationSettings _applicationSettings;
    private readonly IReadOnlyList<OverlayDefinition> _managedOverlays;
    private readonly TelemetryCaptureState _captureState;
    private readonly TelemetryEdgeCaseOptions _telemetryEdgeCaseOptions;
    private readonly LiveModelParityOptions _liveModelParityOptions;
    private readonly LiveOverlayDiagnosticsOptions _liveOverlayDiagnosticsOptions;
    private readonly PostRaceAnalysisOptions _postRaceAnalysisOptions;
    private readonly AppPerformanceState _performanceState;
    private readonly AppStorageOptions _storageOptions;
    private readonly LocalhostOverlayOptions _localhostOverlayOptions;
    private readonly LocalhostOverlayState _localhostOverlayState;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly AppEventRecorder _events;
    private readonly Action _saveSettings;
    private readonly Action _applyOverlaySettings;
    private readonly Action _requestApplicationExit;
    private readonly Action<string?> _selectedOverlayChanged;
    private readonly Panel _titleBar;
    private readonly PictureBox _brandLogo;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Button _closeButton;
    private readonly TabControl _tabs;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private CheckBox? _rawCaptureCheckBox;
    private string? _lastDisplayedDiagnosticsBundlePath;
    private DateTimeOffset? _lastDisplayedDiagnosticsBundleErrorAtUtc;
    private bool _loading = true;
    private bool _syncingRawCaptureCheckBox;
    private bool _applicationExitRequested;
    private Label? _appVersionLabel;
    private Label? _appStatusLabel;
    private Label? _sessionStateLabel;
    private Label? _currentIssueLabel;
    private Label? _advancedDiagnosticsLabel;
    private Label? _latestDiagnosticsBundleLabel;
    private Label? _supportStatusLabel;
    private Label? _garageCoverStateLabel;
    private DateTimeOffset _nextSupportStatusRefreshAtUtc;
    private DateTimeOffset _nextSupportHeavyRefreshAtUtc;
    private DateTimeOffset _nextLatestDiagnosticsScanAtUtc;
    private string? _cachedLatestDiagnosticsBundlePath;

    public SettingsOverlayForm(
        ApplicationSettings applicationSettings,
        IReadOnlyList<OverlayDefinition> managedOverlays,
        TelemetryCaptureState captureState,
        TelemetryEdgeCaseOptions telemetryEdgeCaseOptions,
        LiveModelParityOptions liveModelParityOptions,
        LiveOverlayDiagnosticsOptions liveOverlayDiagnosticsOptions,
        PostRaceAnalysisOptions postRaceAnalysisOptions,
        AppPerformanceState performanceState,
        AppStorageOptions storageOptions,
        LocalhostOverlayOptions localhostOverlayOptions,
        LocalhostOverlayState localhostOverlayState,
        ILiveTelemetrySource liveTelemetrySource,
        DiagnosticsBundleService diagnosticsBundleService,
        AppEventRecorder events,
        OverlaySettings settings,
        Action saveSettings,
        Action applyOverlaySettings,
        Action requestApplicationExit,
        Action<string?> selectedOverlayChanged)
        : base(
            settings,
            saveSettings,
            SettingsOverlayDefinition.Definition.DefaultWidth,
            SettingsOverlayDefinition.Definition.DefaultHeight)
    {
        _applicationSettings = applicationSettings;
        _managedOverlays = managedOverlays;
        _captureState = captureState;
        _telemetryEdgeCaseOptions = telemetryEdgeCaseOptions;
        _liveModelParityOptions = liveModelParityOptions;
        _liveOverlayDiagnosticsOptions = liveOverlayDiagnosticsOptions;
        _postRaceAnalysisOptions = postRaceAnalysisOptions;
        _performanceState = performanceState;
        _storageOptions = storageOptions;
        _localhostOverlayOptions = localhostOverlayOptions;
        _localhostOverlayState = localhostOverlayState;
        _liveTelemetrySource = liveTelemetrySource;
        _diagnosticsBundleService = diagnosticsBundleService;
        _events = events;
        _saveSettings = saveSettings;
        _applyOverlaySettings = applyOverlaySettings;
        _requestApplicationExit = requestApplicationExit;
        _selectedOverlayChanged = selectedOverlayChanged;

        BackColor = OverlayTheme.Colors.SettingsBackground;
        Icon = TmrBrandAssets.LoadIcon();
        Padding = Padding.Empty;
        ShowIcon = true;
        ShowInTaskbar = true;
        Text = "Tech Mates Racing Overlay";
        TopMost = false;
        MinimumSize = SizeFromClientSize(new Size(
            SettingsOverlayDefinition.Definition.DefaultWidth,
            SettingsOverlayDefinition.Definition.DefaultHeight));
        MaximumSize = MinimumSize;

        _titleBar = new Panel
        {
            BackColor = OverlayTheme.Colors.TitleBarBackground,
            Location = Point.Empty,
            Size = new Size(ClientSize.Width, OverlayTheme.Layout.SettingsTitleBarHeight)
        };

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 11f, FontStyle.Bold),
            Location = new Point(72, 4),
            Size = new Size(ClientSize.Width - 132, 19),
            Text = "Tech Mates Racing Overlay",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _subtitleLabel = new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextMuted,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 8.5f),
            Location = new Point(72, 21),
            Size = new Size(ClientSize.Width - 132, 16),
            Text = "TMR Overlay",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _brandLogo = new PictureBox
        {
            BackColor = Color.Transparent,
            Image = TmrBrandAssets.LoadLogoImage(),
            Location = new Point(14, 8),
            Size = new Size(48, 27),
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false
        };

        _closeButton = new Button
        {
            BackColor = OverlayTheme.Colors.ButtonBackground,
            FlatStyle = FlatStyle.Flat,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9.5f, FontStyle.Bold),
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Location = new Point(ClientSize.Width - 36, 8),
            Size = new Size(26, 24),
            TabStop = false,
            Text = "X",
            UseVisualStyleBackColor = false
        };
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Cursor = Cursors.Hand;
        _closeButton.Click += (_, _) => RequestApplicationExit();

        _tabs = new TabControl
        {
            Alignment = TabAlignment.Left,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(SideTabThickness, SideTabLength),
            Location = new Point(OverlayTheme.Layout.SettingsTabInset, OverlayTheme.Layout.SettingsTabTop),
            Multiline = true,
            SizeMode = TabSizeMode.Fixed,
            Size = new Size(ClientSize.Width - 24, ClientSize.Height - 66),
            TabIndex = 0
        };
        _tabs.DrawItem += DrawSettingsTab;
        _tabs.SelectedIndexChanged += (_, _) => ReportSelectedOverlayTab();

        _titleBar.Controls.Add(_brandLogo);
        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_subtitleLabel);
        _titleBar.Controls.Add(_closeButton);
        Controls.Add(_titleBar);
        Controls.Add(_tabs);

        RegisterDragSurfaces(_titleBar, _brandLogo, _titleLabel, _subtitleLabel);

        BuildTabs();
        ReportSelectedOverlayTab();
        ApplyFontFamily(OverlayTheme.DefaultFontFamily);
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _refreshTimer.Tick += (_, _) => RefreshSettingsOverlayState();
        _refreshTimer.Start();
        SyncRawCaptureCheckBox();
        SyncErrorLoggingTab();
        _loading = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _tabs.Dispose();
            _closeButton.Dispose();
            _subtitleLabel.Dispose();
            _titleLabel.Dispose();
            _brandLogo.Image?.Dispose();
            _brandLogo.Dispose();
            _titleBar.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_titleBar is null || _brandLogo is null || _titleLabel is null || _subtitleLabel is null || _closeButton is null || _tabs is null)
        {
            return;
        }

        _titleBar.Size = new Size(ClientSize.Width, OverlayTheme.Layout.SettingsTitleBarHeight);
        _brandLogo.Location = new Point(14, 8);
        _titleLabel.Size = new Size(Math.Max(120, ClientSize.Width - 132), 19);
        _subtitleLabel.Size = new Size(Math.Max(120, ClientSize.Width - 132), 16);
        _closeButton.Location = new Point(ClientSize.Width - 36, 8);
        _tabs.Location = new Point(OverlayTheme.Layout.SettingsTabInset, OverlayTheme.Layout.SettingsTabTop);
        _tabs.Size = new Size(Math.Max(360, ClientSize.Width - 24), Math.Max(320, ClientSize.Height - 66));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        var shouldRequestApplicationExit = e.CloseReason == CloseReason.UserClosing && !_applicationExitRequested;
        if (shouldRequestApplicationExit)
        {
            _applicationExitRequested = true;
        }

        base.OnFormClosing(e);

        if (shouldRequestApplicationExit && !e.Cancel)
        {
            _requestApplicationExit();
        }
    }

    protected override bool UseToolWindowStyle => false;

    protected override void PersistOverlayFrame()
    {
        // The settings window is an access point, not a trackside overlay. Keep it
        // centered on each open instead of carrying monitor-specific placement.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borderPen = new Pen(OverlayTheme.Colors.WindowBorder);
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    private void BuildTabs()
    {
        _garageCoverStateLabel = null;
        _tabs.TabPages.Clear();
        _tabs.TabPages.Add(CreateGeneralTab());

        foreach (var overlay in OrderedSettingsOverlays())
        {
            _tabs.TabPages.Add(CreateOverlayTab(overlay));
        }

        _tabs.TabPages.Add(CreateSupportTab());
        _tabs.SelectedIndex = 0;
    }

    private IEnumerable<OverlayDefinition> OrderedSettingsOverlays()
    {
        var userFacingOverlays = _managedOverlays
            .Where(overlay => !string.Equals(overlay.Id, "status", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var preferredId in PreferredOverlayTabOrder)
        {
            var preferred = userFacingOverlays.FirstOrDefault(
                overlay => string.Equals(overlay.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                yield return preferred;
            }
        }

        foreach (var overlay in userFacingOverlays)
        {
            if (PreferredOverlayTabOrder.Contains(overlay.Id, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return overlay;
        }
    }

    private void ReportSelectedOverlayTab()
    {
        _selectedOverlayChanged(_tabs.SelectedTab?.Tag as string);
    }

    private void SelectOverlayTab(string overlayId)
    {
        foreach (TabPage page in _tabs.TabPages)
        {
            if (string.Equals(page.Tag as string, overlayId, StringComparison.OrdinalIgnoreCase))
            {
                _tabs.SelectedTab = page;
                ReportSelectedOverlayTab();
                return;
            }
        }
    }

    private void RequestApplicationExit()
    {
        if (_applicationExitRequested)
        {
            return;
        }

        _applicationExitRequested = true;
        _requestApplicationExit();
    }

    private void RefreshSettingsOverlayState()
    {
        var started = Stopwatch.GetTimestamp();
        var succeeded = false;
        try
        {
            var captureStarted = Stopwatch.GetTimestamp();
            var captureSucceeded = false;
            try
            {
                SyncRawCaptureCheckBox();
                SyncGarageCoverStateLabel();
                captureSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlaySettingsSyncCapture,
                    captureStarted,
                    captureSucceeded);
            }

            var diagnosticsStarted = Stopwatch.GetTimestamp();
            var diagnosticsSucceeded = false;
            try
            {
                SyncErrorLoggingTab();
                diagnosticsSucceeded = true;
            }
            finally
            {
                _performanceState.RecordOperation(
                    AppPerformanceMetricIds.OverlaySettingsSyncDiagnostics,
                    diagnosticsStarted,
                    diagnosticsSucceeded);
            }

            succeeded = true;
        }
        finally
        {
            _performanceState.RecordOperation(
                AppPerformanceMetricIds.OverlaySettingsRefresh,
                started,
                succeeded);
        }
    }

    private void DrawSettingsTab(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabs.TabPages.Count)
        {
            return;
        }

        var selected = e.Index == _tabs.SelectedIndex;
        var bounds = e.Bounds;
        using var backgroundBrush = new SolidBrush(selected
            ? OverlayTheme.Colors.TabSelectedBackground
            : OverlayTheme.Colors.TabBackground);
        using var borderPen = new Pen(OverlayTheme.Colors.TabBorder);
        e.Graphics.FillRectangle(backgroundBrush, bounds);
        e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        if (selected)
        {
            using var accentBrush = new SolidBrush(OverlayTheme.Colors.InfoText);
            e.Graphics.FillRectangle(accentBrush, bounds.Left, bounds.Top + 4, 3, Math.Max(0, bounds.Height - 8));
        }

        var textBounds = Rectangle.Inflate(bounds, -12, 0);
        TextRenderer.DrawText(
            e.Graphics,
            _tabs.TabPages[e.Index].Text,
            _tabs.Font,
            textBounds,
            OverlayTheme.Colors.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private TabPage CreateGeneralTab()
    {
        var page = CreateTabPage("General");
        var title = CreateSectionLabel("General", 18, 18, 500);

        page.Controls.Add(title);

        var unitsLabel = CreateLabel("Units", 22, 60, 160);
        var unitsCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(180, 56),
            Size = new Size(160, 28),
            TabStop = true
        };
        unitsCombo.Items.Add("Metric");
        unitsCombo.Items.Add("Imperial");
        unitsCombo.SelectedItem = string.Equals(_applicationSettings.General.UnitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
            ? "Imperial"
            : "Metric";
        unitsCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || unitsCombo.SelectedItem is not string unitSystem)
            {
                return;
            }

            _applicationSettings.General.UnitSystem = unitSystem;
            SaveAndApply();
        };

        page.Controls.Add(unitsLabel);
        page.Controls.Add(unitsCombo);
        return page;
    }

    private void AddSupportCaptureControls(TabPage page, int x, int top, int width)
    {
        var title = CreateSectionLabel("Diagnostic telemetry", x, top, width);
        var note = CreateMutedLabel("If we ask for a repro, enable this before joining/driving, then create a diagnostics bundle after.", x + 4, top + 30, width);
        _rawCaptureCheckBox = CreateCheckBox("Capture diagnostic telemetry", _captureState.Snapshot().RawCaptureEnabled, x + 4, top + 66, 320);
        _rawCaptureCheckBox.CheckedChanged += (_, _) => RawCaptureCheckBoxChanged();

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(_rawCaptureCheckBox);
    }

    private void AddAdvancedDiagnosticsControls(TabPage page, int top)
    {
        var title = CreateSectionLabel("Advanced collection", 560, top, 300);
        var note = CreateMutedLabel("Compact diagnostics run by default. Use appsettings.json or TMR_ overrides to disable individual collectors.", 564, top + 30, 330);
        _advancedDiagnosticsLabel = CreateMultiLineValueLabel(AdvancedDiagnosticsText(), 564, top + 62, 330, 104);

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(_advancedDiagnosticsLabel);
    }

    private void AddSupportStorageControls(TabPage page, int x, int top, int width)
    {
        var title = CreateSectionLabel("Support folders", x, top, width);
        var note = CreateMutedLabel("Open local folders if we ask for a specific file instead of the diagnostics zip.", x + 4, top + 30, width);

        var logsButton = CreateActionButton("Logs", x + 4, top + 66, 96);
        logsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.LogsRoot, "logs");
        var diagnosticsButton = CreateActionButton("Diagnostics", x + 110, top + 66, 110);
        diagnosticsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.DiagnosticsRoot, "diagnostics");
        var capturesButton = CreateActionButton("Captures", x + 230, top + 66, 96);
        capturesButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.CaptureRoot, "captures");
        var historyButton = CreateActionButton("History", x + 336, top + 66, 96);
        historyButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.UserHistoryRoot, "history");

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(logsButton);
        page.Controls.Add(diagnosticsButton);
        page.Controls.Add(capturesButton);
        page.Controls.Add(historyButton);
    }

    private TabPage CreateSupportTab()
    {
        var page = CreateTabPage("Support");
        const int x = 18;
        const int width = 900;
        var title = CreateSectionLabel("Support", x, 18, width);
        var note = CreateMutedLabel("Use this tab when we ask for diagnostics or version details.", x + 4, 48, width);
        var versionLabel = CreateLabel("App version", x + 4, 86, 140);
        _appVersionLabel = CreateValueLabel(SupportStatusText.AppVersionText(AppVersionInfo.Current), x + 150, 82, 420, 28);

        AddSupportCaptureControls(page, x, 122, width);

        var actionsTitle = CreateSectionLabel("Diagnostics bundle", x, 232, width);
        var actionsNote = CreateMutedLabel("After testing or reproducing an issue, create a bundle and send back the zip path.", x + 4, 262, width);
        var createBundleButton = CreateActionButton("Create Bundle", x + 4, 298, 140);
        createBundleButton.Click += (_, _) => CreateDiagnosticsBundleFromTab();
        var copyBundleButton = CreateActionButton("Copy Latest Path", x + 154, 298, 140);
        copyBundleButton.Click += (_, _) => CopyLatestDiagnosticsBundlePath();
        var openDiagnosticsButton = CreateActionButton("Open Diagnostics", x + 304, 298, 150);
        openDiagnosticsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.DiagnosticsRoot, "diagnostics");
        _latestDiagnosticsBundleLabel = CreateMutedLabel(string.Empty, x + 4, 336, width);
        _supportStatusLabel = CreateMutedLabel(string.Empty, x + 4, 362, width);

        var stateTitle = CreateSectionLabel("Current state", x, 394, width);
        var statusLabel = CreateLabel("App status", x + 4, 430, 100);
        _appStatusLabel = CreateValueLabel(string.Empty, x + 104, 426, 180, 28);
        var sessionLabel = CreateLabel("Session", x + 320, 430, 90);
        _sessionStateLabel = CreateValueLabel(string.Empty, x + 410, 426, 320, 28);
        var issueLabel = CreateLabel("Issue", x + 4, 472, 100);
        _currentIssueLabel = CreateMultiLineValueLabel(string.Empty, x + 104, 466, 626, 42);

        AddSupportStorageControls(page, x, 520, width);

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(versionLabel);
        page.Controls.Add(_appVersionLabel);
        page.Controls.Add(actionsTitle);
        page.Controls.Add(actionsNote);
        page.Controls.Add(createBundleButton);
        page.Controls.Add(copyBundleButton);
        page.Controls.Add(openDiagnosticsButton);
        page.Controls.Add(_latestDiagnosticsBundleLabel);
        page.Controls.Add(_supportStatusLabel);
        page.Controls.Add(stateTitle);
        page.Controls.Add(statusLabel);
        page.Controls.Add(_appStatusLabel);
        page.Controls.Add(sessionLabel);
        page.Controls.Add(_sessionStateLabel);
        page.Controls.Add(issueLabel);
        page.Controls.Add(_currentIssueLabel);

        _nextSupportStatusRefreshAtUtc = DateTimeOffset.MinValue;
        _nextSupportHeavyRefreshAtUtc = DateTimeOffset.MinValue;
        SyncErrorLoggingTab();
        return page;
    }

    private TabPage CreateOverlayTab(OverlayDefinition definition)
    {
        var settings = _applicationSettings.GetOrAddOverlay(
            definition.Id,
            definition.DefaultWidth,
            definition.DefaultHeight,
            defaultEnabled: false);
        var page = CreateTabPage(definition.DisplayName);
        page.Tag = definition.Id;
        var title = CreateSectionLabel(definition.DisplayName, 18, 18, 500);
        var isStreamChat = string.Equals(definition.Id, StreamChatOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase);
        var isGarageCover = string.Equals(definition.Id, GarageCoverOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase);
        var controlTop = 58;

        if (isStreamChat)
        {
            page.Controls.Add(title);
            page.Controls.Add(CreateMutedLabel(
                "Browser-source surface for stream chat. Streamlabs uses your Chat Box widget URL; Twitch reads public chat by channel name.",
                22,
                54,
                680));
            AddLocalhostOptions(page, definition, 112);
            AddStreamChatOptions(page, settings, 212);
            return page;
        }

        if (isGarageCover)
        {
            page.Controls.Add(title);
            page.Controls.Add(CreateMutedLabel(
                "Localhost-only privacy cover for OBS. It appears in the browser source while iRacing reports the Garage screen as visible.",
                22,
                54,
                720));
            AddLocalhostOptions(page, definition, 112);
            AddGarageCoverOptions(page, settings, 212);
            return page;
        }

        var enabledCheckBox = CreateCheckBox("Visible", settings.Enabled, 22, controlTop, 220);
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            settings.Enabled = enabledCheckBox.Checked;
            SaveAndApply();
        };

        page.Controls.Add(title);
        page.Controls.Add(enabledCheckBox);

        var optionsTop = controlTop + 46;
        if (definition.ShowScaleControl)
        {
            var scaleLabel = CreateLabel("Scale", 22, optionsTop + 4, 160);
            var scaleInput = new NumericUpDown
            {
                DecimalPlaces = 0,
                Increment = 5,
                Location = new Point(180, optionsTop),
                Maximum = 200,
                Minimum = 60,
                Size = new Size(90, 28),
                TabStop = true,
                TextAlign = HorizontalAlignment.Right,
                Value = (decimal)Math.Round(Math.Clamp(settings.Scale, 0.6d, 2d) * 100d)
            };
            var percentLabel = CreateLabel("%", 278, optionsTop + 4, 40);
            scaleInput.ValueChanged += (_, _) =>
            {
                settings.Scale = Math.Clamp((double)scaleInput.Value / 100d, 0.6d, 2d);
                settings.Width = ScaleDimension(definition.DefaultWidth, settings.Scale);
                settings.Height = ScaleDimension(definition.DefaultHeight, settings.Scale);
                SaveAndApply();
            };

            page.Controls.Add(scaleLabel);
            page.Controls.Add(scaleInput);
            page.Controls.Add(percentLabel);
            optionsTop += 50;
        }

        if (definition.ShowOpacityControl)
        {
            var opacityLabel = CreateLabel(
                string.Equals(definition.Id, TrackMapOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase)
                    ? "Map fill"
                    : "Opacity",
                22,
                optionsTop + 4,
                160);
            var opacityInput = new NumericUpDown
            {
                DecimalPlaces = 0,
                Increment = 5,
                Location = new Point(180, optionsTop),
                Maximum = 100,
                Minimum = 20,
                Size = new Size(90, 28),
                TabStop = true,
                TextAlign = HorizontalAlignment.Right,
                Value = (decimal)Math.Round(Math.Clamp(settings.Opacity, 0.2d, 1d) * 100d)
            };
            var percentLabel = CreateLabel("%", 278, optionsTop + 4, 40);
            opacityInput.ValueChanged += (_, _) =>
            {
                settings.Opacity = Math.Clamp((double)opacityInput.Value / 100d, 0.2d, 1d);
                SaveAndApply();
            };

            page.Controls.Add(opacityLabel);
            page.Controls.Add(opacityInput);
            page.Controls.Add(percentLabel);
            optionsTop += 50;
        }

        if (definition.ShowSessionFilters)
        {
            var sessionBox = new GroupBox
            {
                ForeColor = OverlayTheme.Colors.TextControl,
                Location = new Point(22, optionsTop),
                Size = new Size(360, 154),
                Text = "Display in sessions"
            };

            var testCheckBox = CreateCheckBox("Test", settings.ShowInTest, 16, 28, 150);
            var practiceCheckBox = CreateCheckBox("Practice", settings.ShowInPractice, 180, 28, 150);
            var qualifyingCheckBox = CreateCheckBox("Qualifying", settings.ShowInQualifying, 16, 72, 150);
            var raceCheckBox = CreateCheckBox("Race", settings.ShowInRace, 180, 72, 150);

            testCheckBox.CheckedChanged += (_, _) =>
            {
                settings.ShowInTest = testCheckBox.Checked;
                SaveAndApply();
            };
            practiceCheckBox.CheckedChanged += (_, _) =>
            {
                settings.ShowInPractice = practiceCheckBox.Checked;
                SaveAndApply();
            };
            qualifyingCheckBox.CheckedChanged += (_, _) =>
            {
                settings.ShowInQualifying = qualifyingCheckBox.Checked;
                SaveAndApply();
            };
            raceCheckBox.CheckedChanged += (_, _) =>
            {
                settings.ShowInRace = raceCheckBox.Checked;
                SaveAndApply();
            };

            sessionBox.Controls.Add(testCheckBox);
            sessionBox.Controls.Add(practiceCheckBox);
            sessionBox.Controls.Add(qualifyingCheckBox);
            sessionBox.Controls.Add(raceCheckBox);
            page.Controls.Add(sessionBox);
            optionsTop += 176;
        }

        AddLocalhostOptions(page, definition, 58);
        AddOverlaySpecificOptions(page, definition, settings, optionsTop);
        return page;
    }

    private void AddLocalhostOptions(TabPage page, OverlayDefinition definition, int top)
    {
        const int x = 560;
        page.Controls.Add(CreateSectionLabel("Localhost browser source", x, top, 500));
        if (BrowserOverlayPageRenderer.TryGetRouteForOverlayId(definition.Id, out var route))
        {
            var url = $"{_localhostOverlayOptions.Prefix.TrimEnd('/')}{route}";
            page.Controls.Add(CreateLabel("URL", x + 4, top + 42, 120));
            page.Controls.Add(CreateSelectableValueBox(url, x + 92, top + 36, 300, 30));
            var copyButton = CreateActionButton("Copy", x + 402, top + 36, 76);
            copyButton.Click += (_, _) => CopyTextToClipboard(url);
            page.Controls.Add(copyButton);
            page.Controls.Add(CreateMutedLabel("This browser-source route does not require the native overlay to be visible. Disable LocalhostOverlays in configuration if the local server is not needed.", x + 4, top + 76, 500));
            return;
        }

        page.Controls.Add(CreateMutedLabel("No localhost route is available for this overlay yet.", x + 4, top + 42, 560));
    }

    private void AddOverlaySpecificOptions(TabPage page, OverlayDefinition definition, OverlaySettings settings, int top)
    {
        if (string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            AddFlagsOptions(page, settings, top);
            return;
        }

        if (string.Equals(definition.Id, TrackMapOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            AddTrackMapOptions(page, settings, top);
            return;
        }

        if (string.Equals(definition.Id, GarageCoverOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            AddGarageCoverOptions(page, settings, top);
            return;
        }

        AddDescriptorOptions(page, definition.SettingsOptions, settings, top);
    }

    private void AddGarageCoverOptions(TabPage page, OverlaySettings settings, int top)
    {
        page.Controls.Add(CreateSectionLabel("Cover image", 18, top, 500));
        var imagePath = settings.GetStringOption(OverlayOptionKeys.GarageCoverImagePath);
        var imageStatus = GarageCoverImageStore.InspectImage(imagePath);
        page.Controls.Add(CreateLabel("Image", 22, top + 42, 90));
        page.Controls.Add(CreateSelectableValueBox(
            imageStatus.IsUsable ? imagePath : GarageCoverImageStatusText(imageStatus),
            116,
            top + 36,
            500,
            30));

        var importButton = CreateActionButton("Import Image", 116, top + 78, 130);
        importButton.Click += (_, _) => ImportGarageCoverImage(settings);
        page.Controls.Add(importButton);

        var clearButton = CreateActionButton("Clear", 256, top + 78, 80);
        clearButton.Click += (_, _) =>
        {
            try
            {
                settings.SetStringOption(OverlayOptionKeys.GarageCoverImagePath, null);
                GarageCoverImageStore.ClearImportedImages(_storageOptions.SettingsRoot);
                SaveAndApply();
                BuildTabs();
                SelectOverlayTab(GarageCoverOverlayDefinition.Definition.Id);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "Garage Cover",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };
        page.Controls.Add(clearButton);

        var previewButton = CreateActionButton("Show Test Cover", 346, top + 78, 150);
        previewButton.Click += (_, _) => ShowGarageCoverPreview(settings);
        page.Controls.Add(previewButton);

        page.Controls.Add(CreateLabel("Detection", 22, top + 132, 90));
        _garageCoverStateLabel = CreateValueLabel("waiting", 116, top + 126, 238, 28);
        page.Controls.Add(_garageCoverStateLabel);
        SyncGarageCoverStateLabel();

        page.Controls.Add(CreateMutedLabel(
            "The browser source fails closed to the configured cover or TMR fallback while telemetry is unavailable or stale.",
            22,
            top + 170,
            600));

        page.Controls.Add(CreateSectionLabel("Preview", 650, top, 220));
        page.Controls.Add(CreateGarageCoverPreviewPanel(imagePath, 650, top + 36, 220, 124));
        page.Controls.Add(CreateMutedLabel(
            imageStatus.IsUsable ? "Selected cover image" : "Fallback cover",
            650,
            top + 168,
            220));
    }

    private void ShowGarageCoverPreview(OverlaySettings settings)
    {
        GarageCoverBrowserSettings.SetPreviewUntil(settings, DateTimeOffset.UtcNow.AddSeconds(10));
        _events.Record("garage_cover_preview_requested", properties: new Dictionary<string, string?>
        {
            ["durationSeconds"] = "10"
        });
        SaveAndApply();
        SetSupportStatus("Garage Cover test is visible in OBS for 10 seconds.", isError: false);
        BuildTabs();
        SelectOverlayTab(GarageCoverOverlayDefinition.Definition.Id);
    }

    private void SyncGarageCoverStateLabel()
    {
        if (_garageCoverStateLabel is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var detection = GarageCoverBrowserSettings.DetectGarageState(_liveTelemetrySource.Snapshot(), now);
        var settings = _applicationSettings.GetOrAddOverlay(
            GarageCoverOverlayDefinition.Definition.Id,
            GarageCoverOverlayDefinition.Definition.DefaultWidth,
            GarageCoverOverlayDefinition.Definition.DefaultHeight,
            defaultEnabled: false);
        var previewUntil = GarageCoverBrowserSettings.ReadPreviewUntilUtc(settings);
        var previewVisible = previewUntil is not null && previewUntil > now;
        SetLabelText(
            _garageCoverStateLabel,
            previewVisible ? $"{detection.DisplayText} (test visible)" : detection.DisplayText);
        SetLabelColor(
            _garageCoverStateLabel,
            previewVisible
                ? OverlayTheme.Colors.InfoText
                : detection.State switch
                {
                    "garage_visible" => OverlayTheme.Colors.SuccessText,
                    "garage_hidden" => OverlayTheme.Colors.TextSecondary,
                    _ => OverlayTheme.Colors.WarningText
                });
    }

    private static string GarageCoverImageStatusText(GarageCoverImageStatus status)
    {
        return status.Status switch
        {
            "not_configured" => "No image imported; using TMR fallback",
            "unsupported_extension" => "Saved image has an unsupported extension",
            "file_missing" => "Saved image is missing; using TMR fallback",
            _ => "Cover image unavailable; using TMR fallback"
        };
    }

    private static Panel CreateGarageCoverPreviewPanel(string? imagePath, int x, int y, int width, int height)
    {
        var panel = new Panel
        {
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(x, y),
            Size = new Size(width, height)
        };
        panel.Paint += (_, e) => DrawGarageCoverPreview(e.Graphics, panel.ClientRectangle, imagePath);
        return panel;
    }

    private static void DrawGarageCoverPreview(Graphics graphics, Rectangle bounds, string? imagePath)
    {
        graphics.Clear(Color.Black);
        using var image = GarageCoverImageStore.TryLoadPreviewImage(imagePath);
        if (image is not null && image.Width > 0 && image.Height > 0)
        {
            var scale = Math.Min(
                bounds.Width / (double)image.Width,
                bounds.Height / (double)image.Height);
            var width = (int)Math.Round(image.Width * scale);
            var height = (int)Math.Round(image.Height * scale);
            var x = bounds.Left + (bounds.Width - width) / 2;
            var y = bounds.Top + (bounds.Height - height) / 2;
            graphics.DrawImage(image, new Rectangle(x, y, width, height));
            return;
        }

        using var font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 26f, FontStyle.Bold);
        using var brush = new SolidBrush(OverlayTheme.Colors.TextPrimary);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString("TMR", font, brush, bounds, format);
    }

    private void ImportGarageCoverImage(OverlaySettings settings)
    {
        using var dialog = new OpenFileDialog
        {
            AddExtension = true,
            CheckFileExists = true,
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Multiselect = false,
            Title = "Import garage cover image"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var importedPath = GarageCoverImageStore.ImportImage(dialog.FileName, _storageOptions.SettingsRoot);
            settings.SetStringOption(OverlayOptionKeys.GarageCoverImagePath, importedPath);
            _events.Record("garage_cover_image_imported", properties: new Dictionary<string, string?>
            {
                ["extension"] = Path.GetExtension(importedPath)
            });
            SaveAndApply();
            BuildTabs();
            SelectOverlayTab(GarageCoverOverlayDefinition.Definition.Id);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Garage Cover",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void AddTrackMapOptions(TabPage page, OverlaySettings settings, int top)
    {
        page.Controls.Add(CreateSectionLabel("Map sources", 18, top, 500));
        page.Controls.Add(CreateLabel("Source", 22, top + 42, 120));
        page.Controls.Add(CreateValueLabel("Best available bundled or local map; circle fallback when none match", 150, top + 36, 560, 30));
        page.Controls.Add(CreateLabel("Generation", 22, top + 84, 120));
        page.Controls.Add(CreateValueLabel("Automatic after sessions; complete layouts are skipped", 150, top + 78, 520, 30));
        var buildCheckBox = CreateCheckBox(
            "Build local maps from IBT telemetry",
            settings.GetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, defaultValue: true),
            22,
            top + 122,
            320);
        buildCheckBox.CheckedChanged += (_, _) =>
        {
            settings.SetBooleanOption(OverlayOptionKeys.TrackMapBuildFromTelemetry, buildCheckBox.Checked);
            SaveAndApply();
        };
        page.Controls.Add(buildCheckBox);
        page.Controls.Add(CreateMutedLabel("Derived geometry stays on this PC and source IBT files are not copied into TMR storage. Turning this off still uses bundled app maps.", 22, top + 158, 680));

        const int coverageX = 560;
        const int coverageTop = 206;
        page.Controls.Add(CreateSectionLabel("Bundled coverage", coverageX, coverageTop, 500));
        page.Controls.Add(CreateMutedLabel("Reviewed app maps load automatically for matching tracks.", coverageX + 4, coverageTop + 36, 430));
    }

    private void AddStreamChatOptions(TabPage page, OverlaySettings settings, int top)
    {
        page.Controls.Add(CreateSectionLabel("Chat provider", 18, top, 500));
        page.Controls.Add(CreateLabel("Mode", 22, top + 42, 120));
        var providerCombo = new ComboBox
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(150, top + 36),
            Size = new Size(220, 30),
            TabStop = true
        };
        providerCombo.Items.AddRange([
            new StreamChatProviderItem("Not configured", StreamChatOverlaySettings.ProviderNone),
            new StreamChatProviderItem("Streamlabs Chat Box URL", StreamChatOverlaySettings.ProviderStreamlabs),
            new StreamChatProviderItem("Twitch channel", StreamChatOverlaySettings.ProviderTwitch)
        ]);
        var provider = StreamChatOverlaySettings.NormalizeProvider(
            settings.GetStringOption(OverlayOptionKeys.StreamChatProvider, StreamChatOverlaySettings.ProviderNone));
        providerCombo.SelectedIndex = providerCombo.Items
            .Cast<StreamChatProviderItem>()
            .Select((item, index) => new { item, index })
            .FirstOrDefault(candidate => string.Equals(candidate.item.Value, provider, StringComparison.OrdinalIgnoreCase))
            ?.index ?? 0;
        page.Controls.Add(providerCombo);

        page.Controls.Add(CreateLabel("Streamlabs URL", 22, top + 86, 120));
        var streamlabsBox = CreateEditableTextBox(
            settings.GetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl),
            150,
            top + 80,
            520,
            30);
        page.Controls.Add(streamlabsBox);
        page.Controls.Add(CreateMutedLabel("Paste the Streamlabs Chat Box widget URL, for example https://streamlabs.com/widgets/chat-box/...", 150, top + 114, 620));

        page.Controls.Add(CreateLabel("Twitch channel", 22, top + 166, 120));
        var twitchBox = CreateEditableTextBox(
            settings.GetStringOption(OverlayOptionKeys.StreamChatTwitchChannel),
            150,
            top + 160,
            220,
            30);
        page.Controls.Add(twitchBox);
        page.Controls.Add(CreateMutedLabel("Use the public channel name only. Streamlabs is the preferred no-login option for this first pass.", 150, top + 194, 620));

        void SyncProviderFields()
        {
            var selected = providerCombo.SelectedItem is StreamChatProviderItem item
                ? item.Value
                : StreamChatOverlaySettings.ProviderNone;
            streamlabsBox.Enabled = string.Equals(selected, StreamChatOverlaySettings.ProviderStreamlabs, StringComparison.Ordinal);
            twitchBox.Enabled = string.Equals(selected, StreamChatOverlaySettings.ProviderTwitch, StringComparison.Ordinal);
        }

        providerCombo.SelectedIndexChanged += (_, _) => SyncProviderFields();
        SyncProviderFields();

        var saveButton = CreateActionButton("Save Chat", 150, top + 236, 110);
        saveButton.Click += (_, _) => SaveStreamChatSettings(settings, providerCombo, streamlabsBox, twitchBox);
        page.Controls.Add(saveButton);
        page.Controls.Add(CreateMutedLabel("Open the localhost URL in a browser or OBS after saving. The overlay will show connected status, then append messages as they arrive.", 274, top + 242, 560));
    }

    private void SaveStreamChatSettings(
        OverlaySettings settings,
        ComboBox providerCombo,
        TextBox streamlabsBox,
        TextBox twitchBox)
    {
        var provider = providerCombo.SelectedItem is StreamChatProviderItem item
            ? item.Value
            : StreamChatOverlaySettings.ProviderNone;
        settings.SetStringOption(OverlayOptionKeys.StreamChatProvider, provider);
        settings.SetStringOption(OverlayOptionKeys.StreamChatStreamlabsUrl, streamlabsBox.Text);
        settings.SetStringOption(OverlayOptionKeys.StreamChatTwitchChannel, twitchBox.Text);
        SaveAndApply();
    }

    private sealed record StreamChatProviderItem(string Label, string Value)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private void AddFlagsOptions(TabPage page, OverlaySettings settings, int top)
    {
        page.Controls.Add(CreateSectionLabel("Displayed flags", 18, top, 500));

        AddFlagDisplayRow(
            page,
            settings,
            label: "Green start/resume",
            enabledKey: OverlayOptionKeys.FlagsShowGreen,
            defaultEnabled: true,
            rowTop: top + 38);
        AddFlagDisplayRow(
            page,
            settings,
            label: "Blue",
            enabledKey: OverlayOptionKeys.FlagsShowBlue,
            defaultEnabled: true,
            rowTop: top + 74);
        AddFlagDisplayRow(
            page,
            settings,
            label: "Yellow",
            enabledKey: OverlayOptionKeys.FlagsShowYellow,
            defaultEnabled: true,
            rowTop: top + 110);
        AddFlagDisplayRow(
            page,
            settings,
            label: "Red / black",
            enabledKey: OverlayOptionKeys.FlagsShowCritical,
            defaultEnabled: true,
            rowTop: top + 146);
        AddFlagDisplayRow(
            page,
            settings,
            label: "White / checkered",
            enabledKey: OverlayOptionKeys.FlagsShowFinish,
            defaultEnabled: true,
            rowTop: top + 182);

        var sizeTop = top + 236;
        page.Controls.Add(CreateSectionLabel("Overlay size", 18, sizeTop, 500));
        page.Controls.Add(CreateLabel("Width", 22, sizeTop + 42, 80));
        var widthInput = CreateIntegerInput(
            Math.Clamp(settings.Width, FlagsOverlayDefinition.MinimumWidth, FlagsOverlayDefinition.MaximumWidth),
            FlagsOverlayDefinition.MinimumWidth,
            FlagsOverlayDefinition.MaximumWidth,
            108,
            sizeTop + 38);
        widthInput.Width = 96;
        widthInput.ValueChanged += (_, _) =>
        {
            settings.Width = (int)widthInput.Value;
            settings.ScreenId = null;
            SaveAndApply();
        };
        page.Controls.Add(widthInput);

        page.Controls.Add(CreateLabel("Height", 238, sizeTop + 42, 80));
        var heightInput = CreateIntegerInput(
            Math.Clamp(settings.Height, FlagsOverlayDefinition.MinimumHeight, FlagsOverlayDefinition.MaximumHeight),
            FlagsOverlayDefinition.MinimumHeight,
            FlagsOverlayDefinition.MaximumHeight,
            324,
            sizeTop + 38);
        heightInput.Width = 96;
        heightInput.ValueChanged += (_, _) =>
        {
            settings.Height = (int)heightInput.Value;
            settings.ScreenId = null;
            SaveAndApply();
        };
        page.Controls.Add(heightInput);
    }

    private void AddFlagDisplayRow(
        TabPage page,
        OverlaySettings settings,
        string label,
        string enabledKey,
        bool defaultEnabled,
        int rowTop)
    {
        var checkBox = CreateCheckBox(
            label,
            settings.GetBooleanOption(enabledKey, defaultEnabled),
            22,
            rowTop,
            180);
        checkBox.CheckedChanged += (_, _) =>
        {
            settings.SetBooleanOption(enabledKey, checkBox.Checked);
            SaveAndApply();
        };

        page.Controls.Add(checkBox);
    }

    private void AddDescriptorOptions(
        TabPage page,
        IReadOnlyList<OverlaySettingsOptionDescriptor> options,
        OverlaySettings settings,
        int top)
    {
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var x = option.Kind == OverlaySettingsOptionKind.Boolean && index % 2 == 1 ? 260 : 22;
            var y = top + (option.Kind == OverlaySettingsOptionKind.Boolean ? index / 2 : index) * 40;

            if (option.Kind == OverlaySettingsOptionKind.Boolean)
            {
                var checkBox = CreateCheckBox(
                    option.Label,
                    settings.GetBooleanOption(option.Key, option.BooleanDefault),
                    x,
                    y,
                    220);
                checkBox.CheckedChanged += (_, _) =>
                {
                    settings.SetBooleanOption(option.Key, checkBox.Checked);
                    SaveAndApply();
                };
                page.Controls.Add(checkBox);
                continue;
            }

            if (option.Kind == OverlaySettingsOptionKind.Integer)
            {
                page.Controls.Add(CreateLabel(option.Label, 22, y + 4, 120));
                var input = CreateIntegerInput(
                    settings.GetIntegerOption(option.Key, option.IntegerDefault, option.Minimum, option.Maximum),
                    option.Minimum,
                    option.Maximum,
                    150,
                    y);
                input.ValueChanged += (_, _) =>
                {
                    settings.SetIntegerOption(option.Key, (int)input.Value, option.Minimum, option.Maximum);
                    SaveAndApply();
                };
                page.Controls.Add(input);
            }
        }
    }

    private void SaveAndApply()
    {
        if (_loading)
        {
            return;
        }

        _saveSettings();
        _applyOverlaySettings();
    }

    private static TabPage CreateTabPage(string text)
    {
        return new TabPage(text)
        {
            BackColor = OverlayTheme.Colors.PageBackground,
            ForeColor = OverlayTheme.Colors.TextSecondary
        };
    }

    private static Label CreateSectionLabel(string text, int x, int y, int width)
    {
        return new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextPrimary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 11f, FontStyle.Bold),
            Location = new Point(x, y),
            Size = new Size(width, 26),
            Text = text
        };
    }

    private static Label CreateLabel(string text, int x, int y, int width)
    {
        return new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9.25f),
            Location = new Point(x, y),
            Size = new Size(width, 24),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateMutedLabel(string text, int x, int y, int width)
    {
        return new Label
        {
            AutoSize = false,
            ForeColor = OverlayTheme.Colors.TextMuted,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 8.5f),
            Location = new Point(x, y),
            Size = new Size(width, 24),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateMultiLineValueLabel(string text, int x, int y, int width, int height)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 8.75f),
            Location = new Point(x, y),
            Padding = new Padding(8),
            Size = new Size(width, height),
            Text = text,
            TextAlign = ContentAlignment.TopLeft
        };
    }

    private static Label CreateWarningLabel(string text, int x, int y, int width, int height)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = Color.FromArgb(42, 35, 18),
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = OverlayTheme.Colors.WarningIndicator,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 8.75f, FontStyle.Bold),
            Location = new Point(x, y),
            Padding = new Padding(10, 8, 10, 6),
            Size = new Size(width, height),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label CreateValueLabel(string text, int x, int y, int width, int height)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9f, FontStyle.Bold),
            Location = new Point(x, y),
            Padding = new Padding(8, 5, 8, 4),
            Size = new Size(width, height),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static TextBox CreateSelectableValueBox(string text, int x, int y, int width, int height)
    {
        return new TextBox
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9f, FontStyle.Bold),
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Location = new Point(x, y),
            ReadOnly = true,
            Size = new Size(width, height),
            TabStop = true,
            Text = text
        };
    }

    private static TextBox CreateEditableTextBox(string text, int x, int y, int width, int height)
    {
        return new TextBox
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9f),
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(x, y),
            Size = new Size(width, height),
            TabStop = true,
            Text = text
        };
    }

    private static Button CreateActionButton(string text, int x, int y, int width)
    {
        var button = new Button
        {
            BackColor = OverlayTheme.Colors.ButtonBackground,
            FlatStyle = FlatStyle.Flat,
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(x, y),
            Size = new Size(width, 28),
            TabStop = true,
            Text = text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = OverlayTheme.Colors.TabBorder;
        return button;
    }

    private static CheckBox CreateCheckBox(string text, bool isChecked, int x, int y, int width)
    {
        return new CheckBox
        {
            AutoSize = false,
            Checked = isChecked,
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(x, y),
            Size = new Size(width, 28),
            TabStop = true,
            Text = text,
            UseVisualStyleBackColor = true
        };
    }

    private static NumericUpDown CreateIntegerInput(int value, int minimum, int maximum, int x, int y)
    {
        return new NumericUpDown
        {
            DecimalPlaces = 0,
            Increment = 1,
            Location = new Point(x, y),
            Maximum = maximum,
            Minimum = minimum,
            Size = new Size(78, 28),
            TabStop = true,
            TextAlign = HorizontalAlignment.Right,
            Value = Math.Clamp(value, minimum, maximum)
        };
    }

    private void RawCaptureCheckBoxChanged()
    {
        if (_syncingRawCaptureCheckBox || _rawCaptureCheckBox is null)
        {
            return;
        }

        var requested = _rawCaptureCheckBox.Checked;
        var accepted = _captureState.SetRawCaptureEnabled(requested);
        _events.Record("raw_capture_runtime_toggle", new Dictionary<string, string?>
        {
            ["requested"] = requested.ToString(),
            ["accepted"] = accepted.ToString(),
            ["source"] = "settings_overlay"
        });

        if (!accepted)
        {
            SyncRawCaptureCheckBox();
        }
    }

    private void SyncRawCaptureCheckBox()
    {
        if (_rawCaptureCheckBox is null)
        {
            return;
        }

        var snapshot = _captureState.Snapshot();
        _syncingRawCaptureCheckBox = true;
        try
        {
            _rawCaptureCheckBox.Checked = snapshot.RawCaptureEnabled || snapshot.RawCaptureActive;
            _rawCaptureCheckBox.Enabled = !snapshot.RawCaptureActive;
            _rawCaptureCheckBox.Text = snapshot.RawCaptureActive
                ? "Diagnostic telemetry capture active"
                : "Capture diagnostic telemetry";
        }
        finally
        {
            _syncingRawCaptureCheckBox = false;
        }
    }

    private void SyncErrorLoggingTab()
    {
        if (_appVersionLabel is null
            && _appStatusLabel is null
            && _sessionStateLabel is null
            && _currentIssueLabel is null
            && _advancedDiagnosticsLabel is null
            && _latestDiagnosticsBundleLabel is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var statusDue = now >= _nextSupportStatusRefreshAtUtc;
        var heavyDue = now >= _nextSupportHeavyRefreshAtUtc;
        if (!statusDue && !heavyDue)
        {
            return;
        }

        var captureSnapshot = _captureState.Snapshot();
        if (statusDue)
        {
            _nextSupportStatusRefreshAtUtc = now + SupportStatusRefreshInterval;
            if (_appVersionLabel is not null)
            {
                SetLabelText(_appVersionLabel, SupportStatusText.AppVersionText(AppVersionInfo.Current));
            }

            if (_appStatusLabel is not null)
            {
                var appStatus = SupportStatusText.AppStatus(captureSnapshot);
                SetLabelText(_appStatusLabel, appStatus.Text);
                SetLabelColor(_appStatusLabel, ColorForSupportStatus(appStatus.Level));
            }

            if (_sessionStateLabel is not null)
            {
                SetLabelText(_sessionStateLabel, SupportStatusText.SessionStateText(captureSnapshot));
            }

            if (_currentIssueLabel is not null)
            {
                SetLabelText(_currentIssueLabel, SupportStatusText.CurrentIssueText(captureSnapshot));
            }

            if (_advancedDiagnosticsLabel is not null)
            {
                SetLabelText(_advancedDiagnosticsLabel, AdvancedDiagnosticsText());
            }
        }

        if (heavyDue)
        {
            _nextSupportHeavyRefreshAtUtc = now + SupportHeavyRefreshInterval;
            var diagnosticsSnapshot = _diagnosticsBundleService.Snapshot();
            if (_latestDiagnosticsBundleLabel is not null)
            {
                var latestBundlePath = diagnosticsSnapshot.LastBundlePath ?? LatestDiagnosticsBundlePathCached(now) ?? string.Empty;
                SetLabelText(_latestDiagnosticsBundleLabel, SupportStatusText.LatestBundleDisplayText(latestBundlePath));
                ReportAutomaticDiagnosticsBundleStatus(diagnosticsSnapshot, latestBundlePath);
            }

        }
    }

    private void CreateDiagnosticsBundleFromTab()
    {
        try
        {
            var bundlePath = _diagnosticsBundleService.CreateBundle();
            _events.Record("diagnostics_bundle_created", new Dictionary<string, string?>
            {
                ["bundlePath"] = bundlePath,
                ["source"] = "settings_support_tab"
            });
            if (_latestDiagnosticsBundleLabel is not null)
            {
                _latestDiagnosticsBundleLabel.Text = SupportStatusText.LatestBundleDisplayText(bundlePath);
            }
            SetSupportStatus("Created diagnostics bundle.", isError: false);
            OpenSupportDirectory(Path.GetDirectoryName(bundlePath)!, "diagnostics");
        }
        catch (Exception exception)
        {
            _events.Record("diagnostics_bundle_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name,
                ["source"] = "settings_support_tab"
            });
            SetSupportStatus($"Bundle failed: {exception.Message}", isError: true);
        }
    }

    private void CopyLatestDiagnosticsBundlePath()
    {
        var bundlePath = _diagnosticsBundleService.Snapshot().LastBundlePath ?? LatestDiagnosticsBundlePath();
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            SetSupportStatus("No diagnostics bundle yet.", isError: true);
            return;
        }

        try
        {
            Clipboard.SetText(bundlePath);
            SetSupportStatus("Copied bundle path.", isError: false);
        }
        catch
        {
            SetSupportStatus("Clipboard unavailable. Select the path instead.", isError: true);
        }
    }

    private void CopyTextToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            SetSupportStatus("Copied URL.", isError: false);
        }
        catch
        {
            SetSupportStatus("Clipboard unavailable. Select the URL instead.", isError: true);
        }
    }

    private void OpenSupportDirectory(string path, string label)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            SetSupportStatus($"Opened {label} folder.", isError: false);
        }
        catch (Exception exception)
        {
            SetSupportStatus($"Open failed: {exception.Message}", isError: true);
        }
    }

    private string? LatestDiagnosticsBundlePath()
    {
        if (!Directory.Exists(_storageOptions.DiagnosticsRoot))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(_storageOptions.DiagnosticsRoot, "tmroverlay-diagnostics-*.zip")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()
            ?.FullName;
    }

    private string? LatestDiagnosticsBundlePathCached(DateTimeOffset now)
    {
        if (now < _nextLatestDiagnosticsScanAtUtc)
        {
            return _cachedLatestDiagnosticsBundlePath;
        }

        _nextLatestDiagnosticsScanAtUtc = now + LatestDiagnosticsScanInterval;
        _cachedLatestDiagnosticsBundlePath = LatestDiagnosticsBundlePath();
        return _cachedLatestDiagnosticsBundlePath;
    }

    private void ReportAutomaticDiagnosticsBundleStatus(
        DiagnosticsBundleStatus diagnosticsSnapshot,
        string latestBundlePath)
    {
        if (_supportStatusLabel is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(diagnosticsSnapshot.LastError)
            && string.Equals(
                diagnosticsSnapshot.LastErrorSource,
                DiagnosticsBundleSources.SessionFinalization,
                StringComparison.Ordinal)
            && diagnosticsSnapshot.LastErrorAtUtc != _lastDisplayedDiagnosticsBundleErrorAtUtc)
        {
            _lastDisplayedDiagnosticsBundleErrorAtUtc = diagnosticsSnapshot.LastErrorAtUtc;
            SetSupportStatus($"Auto bundle failed: {diagnosticsSnapshot.LastError}", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(latestBundlePath)
            || string.Equals(_lastDisplayedDiagnosticsBundlePath, latestBundlePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastDisplayedDiagnosticsBundlePath = latestBundlePath;
        if (string.Equals(
                diagnosticsSnapshot.LastBundleSource,
                DiagnosticsBundleSources.SessionFinalization,
                StringComparison.Ordinal))
        {
            SetSupportStatus("Auto bundle created after session.", isError: false);
        }
    }

    private void SetSupportStatus(string message, bool isError)
    {
        if (_supportStatusLabel is null)
        {
            return;
        }

        SetLabelColor(_supportStatusLabel, isError ? OverlayTheme.Colors.WarningText : OverlayTheme.Colors.SuccessText);
        SetLabelText(_supportStatusLabel, message);
    }

    private static void SetLabelText(Label label, string text)
    {
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            label.Text = text;
        }
    }

    private static void SetLabelColor(Label label, Color color)
    {
        if (label.ForeColor != color)
        {
            label.ForeColor = color;
        }
    }

    private string AdvancedDiagnosticsText()
    {
        var localhost = _localhostOverlayState.Snapshot();
        var localhostStatus = localhost.LastError is { Length: > 0 }
            ? $"{localhost.Status} ({localhost.LastError})"
            : $"{localhost.Status}, {localhost.TotalRequests:N0} requests";
        return string.Join(
            Environment.NewLine,
            $"edge clips: {Status(_telemetryEdgeCaseOptions.Enabled)}",
            $"model v2 parity: {Status(_liveModelParityOptions.Enabled)}",
            $"overlay signals: {Status(_liveOverlayDiagnosticsOptions.Enabled)}",
            $"post-race analysis: {Status(_postRaceAnalysisOptions.Enabled)}",
            $"localhost: {localhostStatus}",
            "Paths: logs + history folders below");
    }

    private static string Status(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }

    private static Color ColorForSupportStatus(SupportStatusLevel level)
    {
        return level switch
        {
            SupportStatusLevel.Error => OverlayTheme.Colors.ErrorText,
            SupportStatusLevel.Warning => OverlayTheme.Colors.WarningText,
            SupportStatusLevel.Success => OverlayTheme.Colors.SuccessText,
            SupportStatusLevel.Info => OverlayTheme.Colors.InfoText,
            _ => OverlayTheme.Colors.TextSecondary
        };
    }

    private static int ScaleDimension(int defaultDimension, double scale)
    {
        return Math.Max(80, (int)Math.Round(defaultDimension * Math.Clamp(scale, 0.6d, 2d)));
    }

    private void ApplyFontFamily(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            return;
        }

        ApplyFontFamilyRecursive(this, fontFamily);
    }

    private static void ApplyFontFamilyRecursive(Control control, string fontFamily)
    {
        var current = control.Font;
        control.Font = OverlayTheme.Font(fontFamily, current.Size, current.Style);

        foreach (Control child in control.Controls)
        {
            ApplyFontFamilyRecursive(child, fontFamily);
        }
    }

}
