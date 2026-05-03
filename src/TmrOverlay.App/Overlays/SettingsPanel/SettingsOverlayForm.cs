using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Brand;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Performance;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal sealed class SettingsOverlayForm : PersistentOverlayForm
{
    private const int SideTabThickness = 38;
    private const int SideTabLength = 174;

    private static readonly string[] PreferredOverlayTabOrder =
    [
        "standings",
        "relative",
        "flags",
        "car-radar"
    ];

    private static readonly string[] PreferredFontFamilies =
    [
        "Segoe UI",
        "Arial",
        "Calibri",
        "Consolas",
        "Courier New",
        "Georgia",
        "Tahoma",
        "Times New Roman",
        "Trebuchet MS",
        "Verdana"
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
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly AppEventRecorder _events;
    private readonly Action _saveSettings;
    private readonly Action _applyOverlaySettings;
    private readonly Action _requestApplicationExit;
    private readonly Action<string?> _selectedOverlayChanged;
    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private readonly Button _closeButton;
    private readonly TabControl _tabs;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private CheckBox? _rawCaptureCheckBox;
    private string? _lastDisplayedDiagnosticsBundlePath;
    private DateTimeOffset? _lastDisplayedDiagnosticsBundleErrorAtUtc;
    private bool _loading = true;
    private bool _syncingRawCaptureCheckBox;
    private bool _applicationExitRequested;
    private Label? _appStatusLabel;
    private Label? _sessionStateLabel;
    private Label? _currentIssueLabel;
    private Label? _performanceSnapshotLabel;
    private Label? _latestDiagnosticsBundleLabel;
    private Label? _supportStatusLabel;

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
        Text = "TmrOverlay Settings";
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
            Location = new Point(14, 9),
            Size = new Size(ClientSize.Width - 60, 24),
            Text = "TMR Overlay",
            TextAlign = ContentAlignment.MiddleLeft
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

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_closeButton);
        Controls.Add(_titleBar);
        Controls.Add(_tabs);

        RegisterDragSurfaces(_titleBar, _titleLabel);

        BuildTabs();
        ReportSelectedOverlayTab();
        ApplyFontFamily(_applicationSettings.General.FontFamily);
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
            _titleLabel.Dispose();
            _titleBar.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_titleBar is null || _titleLabel is null || _closeButton is null || _tabs is null)
        {
            return;
        }

        _titleBar.Size = new Size(ClientSize.Width, OverlayTheme.Layout.SettingsTitleBarHeight);
        _titleLabel.Size = new Size(Math.Max(120, ClientSize.Width - 60), 24);
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
        var fontLabel = CreateLabel("Font family", 22, 60, 160);
        var fontCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(180, 56),
            Size = new Size(260, 28),
            TabStop = true
        };

        foreach (var family in BuildFontFamilyList())
        {
            fontCombo.Items.Add(family);
        }

        var selectedFont = SelectFontFamily(_applicationSettings.General.FontFamily, fontCombo);
        fontCombo.SelectedItem = selectedFont;
        fontCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || fontCombo.SelectedItem is not string fontFamily)
            {
                return;
            }

            _applicationSettings.General.FontFamily = fontFamily;
            ApplyFontFamily(fontFamily);
            SaveAndApply();
        };

        page.Controls.Add(title);
        page.Controls.Add(fontLabel);
        page.Controls.Add(fontCombo);

        var unitsLabel = CreateLabel("Units", 22, 106, 160);
        var unitsCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(180, 102),
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
        var note = CreateMutedLabel("Use only when asked to collect telemetry for debugging or analysis.", x + 4, top + 30, width);
        _rawCaptureCheckBox = CreateCheckBox("Capture diagnostic telemetry", _captureState.Snapshot().RawCaptureEnabled, x + 4, top + 66, 280);
        _rawCaptureCheckBox.CheckedChanged += (_, _) => RawCaptureCheckBoxChanged();

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(_rawCaptureCheckBox);
    }

    private void AddAdvancedDiagnosticsControls(TabPage page, int top)
    {
        var title = CreateSectionLabel("Advanced collection", 560, top, 300);
        var note = CreateMutedLabel("Disabled by default. Enable with appsettings.json or TMR_ overrides.", 564, top + 30, 330);
        var status = CreateMultiLineValueLabel(AdvancedDiagnosticsText(), 564, top + 62, 330, 104);

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(status);
    }

    private void AddSupportStorageControls(TabPage page, int top)
    {
        var title = CreateSectionLabel("Storage", 560, top, 300);
        var note = CreateMutedLabel("Open local folders used for support handoff.", 564, top + 30, 330);

        var logsButton = CreateActionButton("Logs", 564, top + 68, 96);
        logsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.LogsRoot, "logs");
        var diagnosticsButton = CreateActionButton("Diagnostics", 670, top + 68, 110);
        diagnosticsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.DiagnosticsRoot, "diagnostics");
        var capturesButton = CreateActionButton("Captures", 564, top + 106, 96);
        capturesButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.CaptureRoot, "captures");
        var historyButton = CreateActionButton("History", 670, top + 106, 110);
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
        var title = CreateSectionLabel("Support", 18, 18, 500);
        var note = CreateMutedLabel("Use this tab when sharing logs for overlay or telemetry issues.", 22, 48, 510);
        var statusLabel = CreateLabel("App status", 22, 88, 140);
        _appStatusLabel = CreateValueLabel(string.Empty, 180, 84, 350, 28);
        var sessionLabel = CreateLabel("Session state", 22, 128, 140);
        _sessionStateLabel = CreateValueLabel(string.Empty, 180, 124, 350, 28);
        var issueLabel = CreateLabel("Current issue", 22, 168, 140);
        _currentIssueLabel = CreateMultiLineValueLabel(string.Empty, 180, 162, 350, 58);

        var actionsTitle = CreateSectionLabel("Support actions", 18, 246, 500);
        var createBundleButton = CreateActionButton("Create Bundle", 22, 286, 140);
        createBundleButton.Click += (_, _) => CreateDiagnosticsBundleFromTab();
        var copyBundleButton = CreateActionButton("Copy Latest Path", 172, 286, 140);
        copyBundleButton.Click += (_, _) => CopyLatestDiagnosticsBundlePath();
        var openDiagnosticsButton = CreateActionButton("Open Diagnostics", 322, 286, 150);
        openDiagnosticsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.DiagnosticsRoot, "diagnostics");
        _latestDiagnosticsBundleLabel = CreateMutedLabel(string.Empty, 22, 324, 508);
        _supportStatusLabel = CreateMutedLabel(string.Empty, 22, 350, 508);

        AddSupportCaptureControls(page, 18, 388, 512);
        AddAdvancedDiagnosticsControls(page, 88);
        AddSupportStorageControls(page, 274);

        var performanceLabel = CreateSectionLabel("App activity", 560, 420, 300);
        _performanceSnapshotLabel = CreateMultiLineValueLabel(string.Empty, 564, 452, 330, 72);

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(statusLabel);
        page.Controls.Add(_appStatusLabel);
        page.Controls.Add(sessionLabel);
        page.Controls.Add(_sessionStateLabel);
        page.Controls.Add(issueLabel);
        page.Controls.Add(_currentIssueLabel);
        page.Controls.Add(openDiagnosticsButton);
        page.Controls.Add(actionsTitle);
        page.Controls.Add(createBundleButton);
        page.Controls.Add(copyBundleButton);
        page.Controls.Add(_latestDiagnosticsBundleLabel);
        page.Controls.Add(_supportStatusLabel);
        page.Controls.Add(performanceLabel);
        page.Controls.Add(_performanceSnapshotLabel);

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

        var enabledCheckBox = CreateCheckBox("Visible", settings.Enabled, 22, 58, 220);
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            settings.Enabled = enabledCheckBox.Checked;
            SaveAndApply();
        };

        page.Controls.Add(title);
        page.Controls.Add(enabledCheckBox);

        var optionsTop = 104;
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
            var opacityLabel = CreateLabel("Opacity", 22, optionsTop + 4, 160);
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

        AddOverlaySpecificOptions(page, definition, settings, optionsTop);
        return page;
    }

    private void AddOverlaySpecificOptions(TabPage page, OverlayDefinition definition, OverlaySettings settings, int top)
    {
        if (string.Equals(definition.Id, FlagsOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            AddFlagsOptions(page, settings, top);
            return;
        }

        AddDescriptorOptions(page, definition.SettingsOptions, settings, top);
    }

    private void AddFlagsOptions(TabPage page, OverlaySettings settings, int top)
    {
        page.Controls.Add(CreateSectionLabel("Display flags", 18, top, 500));

        AddFlagDisplayRow(
            page,
            settings,
            label: "Green",
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
        page.Controls.Add(CreateSectionLabel("Border size", 18, sizeTop, 500));
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
        if (_appStatusLabel is null
            && _sessionStateLabel is null
            && _currentIssueLabel is null
            && _latestDiagnosticsBundleLabel is null
            && _performanceSnapshotLabel is null)
        {
            return;
        }

        var captureSnapshot = _captureState.Snapshot();
        var diagnosticsSnapshot = _diagnosticsBundleService.Snapshot();
        if (_appStatusLabel is not null)
        {
            var appStatus = AppStatus(captureSnapshot);
            _appStatusLabel.Text = appStatus.Text;
            _appStatusLabel.ForeColor = appStatus.Color;
        }

        if (_sessionStateLabel is not null)
        {
            _sessionStateLabel.Text = SessionStateText(captureSnapshot);
        }

        if (_currentIssueLabel is not null)
        {
            _currentIssueLabel.Text = CurrentIssueText(captureSnapshot);
        }

        if (_latestDiagnosticsBundleLabel is not null)
        {
            var latestBundlePath = diagnosticsSnapshot.LastBundlePath ?? LatestDiagnosticsBundlePath() ?? string.Empty;
            _latestDiagnosticsBundleLabel.Text = LatestBundleDisplayText(latestBundlePath);
            ReportAutomaticDiagnosticsBundleStatus(diagnosticsSnapshot, latestBundlePath);
        }

        if (_performanceSnapshotLabel is not null)
        {
            _performanceSnapshotLabel.Text = PerformanceSnapshotText(_performanceState.Snapshot(), captureSnapshot);
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
                _latestDiagnosticsBundleLabel.Text = LatestBundleDisplayText(bundlePath);
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

        _supportStatusLabel.ForeColor = isError ? OverlayTheme.Colors.WarningText : OverlayTheme.Colors.SuccessText;
        _supportStatusLabel.Text = message;
    }

    private static string CurrentIssueText(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            return $"Error: {snapshot.LastError}";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastWarning))
        {
            return $"Warning: {snapshot.LastWarning}";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.AppWarning))
        {
            return $"App warning: {snapshot.AppWarning}";
        }

        return "No current error or warning recorded.";
    }

    private string AdvancedDiagnosticsText()
    {
        return string.Join(
            Environment.NewLine,
            $"edge clips: {Status(_telemetryEdgeCaseOptions.Enabled)}",
            $"model v2 parity: {Status(_liveModelParityOptions.Enabled)}",
            $"overlay signals: {Status(_liveOverlayDiagnosticsOptions.Enabled)}",
            $"post-race analysis: {Status(_postRaceAnalysisOptions.Enabled)}",
            "Paths: logs + history folders below");
    }

    private static string Status(bool enabled)
    {
        return enabled ? "enabled" : "disabled";
    }

    private static (string Text, Color Color) AppStatus(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LastError))
        {
            return ("Error", OverlayTheme.Colors.ErrorText);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastWarning)
            || !string.IsNullOrWhiteSpace(snapshot.AppWarning)
            || snapshot.DroppedFrameCount > 0)
        {
            return ("Warning", OverlayTheme.Colors.WarningText);
        }

        if (snapshot.IsCapturing)
        {
            return ("Live telemetry", OverlayTheme.Colors.SuccessText);
        }

        if (snapshot.IsConnected)
        {
            return ("Connected", OverlayTheme.Colors.InfoText);
        }

        return ("Waiting for iRacing", OverlayTheme.Colors.TextSecondary);
    }

    private static string SessionStateText(TelemetryCaptureStatusSnapshot snapshot)
    {
        if (snapshot.RawCaptureActive)
        {
            return $"Diagnostic telemetry active ({snapshot.WrittenFrameCount:N0} frames written)";
        }

        if (snapshot.RawCaptureEnabled)
        {
            return "Diagnostic telemetry requested";
        }

        if (snapshot.IsCapturing)
        {
            return $"Receiving live telemetry ({snapshot.FrameCount:N0} frames)";
        }

        if (snapshot.IsConnected)
        {
            return "iRacing connected; waiting for live session data";
        }

        return "Not connected";
    }

    private static string LatestBundleDisplayText(string? bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            return "Latest bundle: none created yet";
        }

        return $"Latest bundle: {Path.GetFileName(bundlePath)}";
    }

    private static string PerformanceSnapshotText(
        AppPerformanceSnapshot performance,
        TelemetryCaptureStatusSnapshot capture)
    {
        var telemetryState = capture.IsCapturing
            ? $"{performance.TelemetryFrameCount:N0} frames, {performance.TelemetryFramesPerSecond:0.##} fps"
            : "waiting for live telemetry";
        var rawState = capture.RawCaptureActive || capture.RawCaptureEnabled
            ? $"{capture.WrittenFrameCount:N0} written, {capture.DroppedFrameCount:N0} dropped, {FormatBytes(capture.TelemetryFileBytes)}"
            : "diagnostic capture off";

        return string.Join(
            Environment.NewLine,
            $"telemetry: {telemetryState}",
            $"iRacing: quality {ValueLast(performance, AppPerformanceValueIds.IRacingChanQuality)}, latency {ValueLast(performance, AppPerformanceValueIds.IRacingChanLatency, "s")}",
            $"raw: {rawState}",
            $"process: {FormatBytes(performance.Process.WorkingSetBytes)} working set");
    }

    private static string ValueLast(AppPerformanceSnapshot performance, string valueId, string suffix = "")
    {
        var value = FindValue(performance, valueId);
        return value?.Last is null ? "n/a" : FormatValue(value.Last, suffix);
    }

    private static PerformanceValueSnapshot? FindValue(AppPerformanceSnapshot performance, string valueId)
    {
        return performance.IRacingSystem
            .Concat(performance.OverlayUpdates)
            .FirstOrDefault(value => string.Equals(value.Id, valueId, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatValue(double? value, string suffix = "")
    {
        return value is null ? "n/a" : $"{value.Value:0.###}{suffix}";
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null)
        {
            return "n/a";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(0, bytes.Value);
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private static int ScaleDimension(int defaultDimension, double scale)
    {
        return Math.Max(80, (int)Math.Round(defaultDimension * Math.Clamp(scale, 0.6d, 2d)));
    }

    private static string SelectFontFamily(string? savedFontFamily, ComboBox fontCombo)
    {
        if (!string.IsNullOrWhiteSpace(savedFontFamily))
        {
            foreach (var item in fontCombo.Items)
            {
                if (item is string family && string.Equals(family, savedFontFamily, StringComparison.OrdinalIgnoreCase))
                {
                    return family;
                }
            }

            fontCombo.Items.Add(savedFontFamily);
            return savedFontFamily;
        }

        return fontCombo.Items.Count > 0 && fontCombo.Items[0] is string firstFont
            ? firstFont
            : "Segoe UI";
    }

    private static IReadOnlyList<string> BuildFontFamilyList()
    {
        try
        {
            using var installedFonts = new InstalledFontCollection();
            var installed = installedFonts.Families
                .Select(family => family.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var available = PreferredFontFamilies
                .Where(installed.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return available.Length > 0 ? available : PreferredFontFamilies;
        }
        catch
        {
            return PreferredFontFamilies;
        }
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
        control.Font = new Font(fontFamily, current.Size, current.Style, current.Unit);

        foreach (Control child in control.Controls)
        {
            ApplyFontFamilyRecursive(child, fontFamily);
        }
    }

}
