using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Diagnostics;
using TmrOverlay.App.Events;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.Analysis;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal sealed class SettingsOverlayForm : PersistentOverlayForm
{
    private const string WindowsCleanCommand = "dotnet clean .\\tmrOverlay.sln -c Release; Remove-Item .\\src\\TmrOverlay.App\\bin, .\\src\\TmrOverlay.App\\obj, .\\src\\TmrOverlay.Core\\bin, .\\src\\TmrOverlay.Core\\obj, .\\artifacts\\TmrOverlay-win-x64, .\\artifacts\\TmrOverlay-win-x64.zip -Recurse -Force -ErrorAction SilentlyContinue";
    private const string WindowsBuildCommand = "dotnet build .\\src\\TmrOverlay.App\\TmrOverlay.App.csproj -c Release";
    private const string WindowsPublishCommand = "dotnet publish .\\src\\TmrOverlay.App\\TmrOverlay.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\\artifacts\\TmrOverlay-win-x64";
    private const string WindowsZipCommand = "Compress-Archive -Path .\\artifacts\\TmrOverlay-win-x64\\* -DestinationPath .\\artifacts\\TmrOverlay-win-x64.zip -Force";
    private const int SideTabThickness = 38;
    private const int SideTabLength = 174;

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
    private readonly PostRaceAnalysisStore _analysisStore;
    private readonly TelemetryCaptureState _captureState;
    private readonly AppStorageOptions _storageOptions;
    private readonly DiagnosticsBundleService _diagnosticsBundleService;
    private readonly AppEventRecorder _events;
    private readonly Action _saveSettings;
    private readonly Action _applyOverlaySettings;
    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private readonly Button _hideButton;
    private readonly TabControl _tabs;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private CheckBox? _rawCaptureCheckBox;
    private ComboBox? _analysisCombo;
    private Label? _analysisText;
    private DateTimeOffset _lastAnalysisRefreshUtc = DateTimeOffset.MinValue;
    private string _analysisFingerprint = string.Empty;
    private bool _loading = true;
    private bool _syncingRawCaptureCheckBox;
    private bool _syncingAnalysis;
    private Label? _buildCommandStatusLabel;
    private Label? _currentIssueLabel;
    private Label? _runtimeInfoLabel;
    private TextBox? _latestDiagnosticsBundleTextBox;
    private Label? _supportStatusLabel;

    public SettingsOverlayForm(
        ApplicationSettings applicationSettings,
        IReadOnlyList<OverlayDefinition> managedOverlays,
        PostRaceAnalysisStore analysisStore,
        TelemetryCaptureState captureState,
        AppStorageOptions storageOptions,
        DiagnosticsBundleService diagnosticsBundleService,
        AppEventRecorder events,
        OverlaySettings settings,
        Action saveSettings,
        Action applyOverlaySettings)
        : base(
            settings,
            saveSettings,
            SettingsOverlayDefinition.Definition.DefaultWidth,
            SettingsOverlayDefinition.Definition.DefaultHeight)
    {
        _applicationSettings = applicationSettings;
        _managedOverlays = managedOverlays;
        _analysisStore = analysisStore;
        _captureState = captureState;
        _storageOptions = storageOptions;
        _diagnosticsBundleService = diagnosticsBundleService;
        _events = events;
        _saveSettings = saveSettings;
        _applyOverlaySettings = applyOverlaySettings;

        BackColor = OverlayTheme.Colors.SettingsBackground;
        Padding = Padding.Empty;

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
            Text = "Settings",
            TextAlign = ContentAlignment.MiddleLeft
        };

        _hideButton = new Button
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
        _hideButton.FlatAppearance.BorderSize = 0;
        _hideButton.Cursor = Cursors.Hand;
        _hideButton.Click += (_, _) => Hide();

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

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_hideButton);
        Controls.Add(_titleBar);
        Controls.Add(_tabs);

        RegisterDragSurfaces(_titleBar, _titleLabel);

        BuildTabs();
        ApplyFontFamily(_applicationSettings.General.FontFamily);
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };
        _refreshTimer.Tick += (_, _) =>
        {
            SyncRawCaptureCheckBox();
            SyncErrorLoggingTab();
            SyncPostRaceAnalysis();
        };
        _refreshTimer.Start();
        SyncRawCaptureCheckBox();
        SyncErrorLoggingTab();
        SyncPostRaceAnalysis(force: true);
        _loading = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _tabs.Dispose();
            _hideButton.Dispose();
            _titleLabel.Dispose();
            _titleBar.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_titleBar is null || _titleLabel is null || _hideButton is null || _tabs is null)
        {
            return;
        }

        _titleBar.Size = new Size(ClientSize.Width, OverlayTheme.Layout.SettingsTitleBarHeight);
        _titleLabel.Size = new Size(Math.Max(120, ClientSize.Width - 60), 24);
        _hideButton.Location = new Point(ClientSize.Width - 36, 8);
        _tabs.Location = new Point(OverlayTheme.Layout.SettingsTabInset, OverlayTheme.Layout.SettingsTabTop);
        _tabs.Size = new Size(Math.Max(360, ClientSize.Width - 24), Math.Max(320, ClientSize.Height - 66));
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
        _tabs.TabPages.Add(CreateErrorLoggingTab());

        foreach (var overlay in _managedOverlays)
        {
            _tabs.TabPages.Add(CreateOverlayTab(overlay));
        }

        _tabs.TabPages.Add(CreateOverlayBridgeTab());
        _tabs.TabPages.Add(CreatePostRaceAnalysisTab());
        _tabs.SelectedIndex = 0;
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
        AddWindowsBuildCommands(page);
        return page;
    }

    private void AddWindowsBuildCommands(TabPage page)
    {
        var title = CreateSectionLabel("Windows local build", 18, 154, 500);
        var note = CreateMutedLabel("PowerShell from the repo root. The app only copies these commands.", 22, 184, 510);
        var cleanLabel = CreateLabel("Clean", 22, 224, 70);
        var cleanCommand = CreateCommandTextBox(92, 220, 350, WindowsCleanCommand);
        var cleanCopy = CreateCopyButton(452, 220, WindowsCleanCommand);
        var buildLabel = CreateLabel("Build", 22, 270, 70);
        var buildCommand = CreateCommandTextBox(92, 266, 350, WindowsBuildCommand);
        var buildCopy = CreateCopyButton(452, 266, WindowsBuildCommand);
        var publishLabel = CreateLabel("Publish", 22, 316, 70);
        var publishCommand = CreateCommandTextBox(92, 312, 350, WindowsPublishCommand);
        var publishCopy = CreateCopyButton(452, 312, WindowsPublishCommand);
        var zipLabel = CreateLabel("Zip", 22, 362, 70);
        var zipCommand = CreateCommandTextBox(92, 358, 350, WindowsZipCommand);
        var zipCopy = CreateCopyButton(452, 358, WindowsZipCommand);
        _buildCommandStatusLabel = CreateMutedLabel(string.Empty, 92, 398, 350);

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(cleanLabel);
        page.Controls.Add(cleanCommand);
        page.Controls.Add(cleanCopy);
        page.Controls.Add(buildLabel);
        page.Controls.Add(buildCommand);
        page.Controls.Add(buildCopy);
        page.Controls.Add(publishLabel);
        page.Controls.Add(publishCommand);
        page.Controls.Add(publishCopy);
        page.Controls.Add(zipLabel);
        page.Controls.Add(zipCommand);
        page.Controls.Add(zipCopy);
        page.Controls.Add(_buildCommandStatusLabel);
    }

    private TabPage CreateErrorLoggingTab()
    {
        var page = CreateTabPage("Error Logging");
        var title = CreateSectionLabel("Error Logging", 18, 18, 500);
        var note = CreateMutedLabel("Use this tab when sharing logs for overlay or telemetry issues.", 22, 48, 510);
        var issueLabel = CreateLabel("Current issue", 22, 88, 140);
        _currentIssueLabel = CreateMultiLineValueLabel(string.Empty, 180, 82, 350, 58);

        var logsLabel = CreateLabel("Logs folder", 22, 166, 140);
        var logsPath = CreateCommandTextBox(180, 162, 250, _storageOptions.LogsRoot);
        var openLogsButton = CreateActionButton("Open", 440, 162, 90);
        openLogsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.LogsRoot, "logs");

        var diagnosticsLabel = CreateLabel("Diagnostics folder", 22, 212, 140);
        var diagnosticsPath = CreateCommandTextBox(180, 208, 250, _storageOptions.DiagnosticsRoot);
        var openDiagnosticsButton = CreateActionButton("Open", 440, 208, 90);
        openDiagnosticsButton.Click += (_, _) => OpenSupportDirectory(_storageOptions.DiagnosticsRoot, "diagnostics");

        var latestBundleLabel = CreateLabel("Latest bundle", 22, 258, 140);
        _latestDiagnosticsBundleTextBox = CreateCommandTextBox(180, 254, 250, LatestDiagnosticsBundlePath() ?? string.Empty);
        var copyBundleButton = CreateActionButton("Copy Path", 440, 254, 90);
        copyBundleButton.Click += (_, _) => CopyLatestDiagnosticsBundlePath();

        var createBundleButton = CreateActionButton("Create Bundle", 180, 306, 140);
        createBundleButton.Click += (_, _) => CreateDiagnosticsBundleFromTab();
        _supportStatusLabel = CreateMutedLabel(string.Empty, 330, 308, 200);

        var runtimeLabel = CreateLabel("Runtime", 22, 350, 140);
        _runtimeInfoLabel = CreateMultiLineValueLabel(string.Empty, 180, 344, 350, 138);

        page.Controls.Add(title);
        page.Controls.Add(note);
        page.Controls.Add(issueLabel);
        page.Controls.Add(_currentIssueLabel);
        page.Controls.Add(logsLabel);
        page.Controls.Add(logsPath);
        page.Controls.Add(openLogsButton);
        page.Controls.Add(diagnosticsLabel);
        page.Controls.Add(diagnosticsPath);
        page.Controls.Add(openDiagnosticsButton);
        page.Controls.Add(latestBundleLabel);
        page.Controls.Add(_latestDiagnosticsBundleTextBox);
        page.Controls.Add(copyBundleButton);
        page.Controls.Add(createBundleButton);
        page.Controls.Add(_supportStatusLabel);
        page.Controls.Add(runtimeLabel);
        page.Controls.Add(_runtimeInfoLabel);

        SyncErrorLoggingTab();
        return page;
    }

    private TabPage CreateOverlayTab(OverlayDefinition definition)
    {
        var settings = _applicationSettings.GetOrAddOverlay(
            definition.Id,
            definition.DefaultWidth,
            definition.DefaultHeight);
        var page = CreateTabPage(definition.DisplayName);
        var title = CreateSectionLabel(definition.DisplayName, 18, 18, 500);

        var enabledCheckBox = CreateCheckBox("Visible", settings.Enabled, 22, 58, 220);
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            settings.Enabled = enabledCheckBox.Checked;
            SaveAndApply();
        };

        var scaleLabel = CreateLabel("Scale", 22, 104, 160);
        var scaleInput = new NumericUpDown
        {
            DecimalPlaces = 0,
            Increment = 5,
            Location = new Point(180, 100),
            Maximum = 200,
            Minimum = 60,
            Size = new Size(90, 28),
            TabStop = true,
            TextAlign = HorizontalAlignment.Right,
            Value = (decimal)Math.Round(Math.Clamp(settings.Scale, 0.6d, 2d) * 100d)
        };
        var percentLabel = CreateLabel("%", 278, 104, 40);
        scaleInput.ValueChanged += (_, _) =>
        {
            settings.Scale = Math.Clamp((double)scaleInput.Value / 100d, 0.6d, 2d);
            settings.Width = ScaleDimension(definition.DefaultWidth, settings.Scale);
            settings.Height = ScaleDimension(definition.DefaultHeight, settings.Scale);
            SaveAndApply();
        };

        var sessionBox = new GroupBox
        {
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(22, 154),
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

        page.Controls.Add(title);
        page.Controls.Add(enabledCheckBox);
        page.Controls.Add(scaleLabel);
        page.Controls.Add(scaleInput);
        page.Controls.Add(percentLabel);
        page.Controls.Add(sessionBox);

        AddOverlaySpecificOptions(page, definition, settings);
        return page;
    }

    private void AddOverlaySpecificOptions(TabPage page, OverlayDefinition definition, OverlaySettings settings)
    {
        var top = 330;
        if (string.Equals(definition.Id, "status", StringComparison.OrdinalIgnoreCase))
        {
            _rawCaptureCheckBox = CreateCheckBox("Raw capture", _captureState.Snapshot().RawCaptureEnabled, 22, top, 180);
            _rawCaptureCheckBox.CheckedChanged += (_, _) => RawCaptureCheckBoxChanged();
            page.Controls.Add(_rawCaptureCheckBox);
            top += 40;
        }

        AddDescriptorOptions(page, definition.SettingsOptions, settings, top);
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

    private void CopyBuildCommand(string command)
    {
        try
        {
            Clipboard.SetText(command);
            if (_buildCommandStatusLabel is not null)
            {
                _buildCommandStatusLabel.ForeColor = OverlayTheme.Colors.SuccessText;
                _buildCommandStatusLabel.Text = "Copied command.";
            }
        }
        catch
        {
            if (_buildCommandStatusLabel is not null)
            {
                _buildCommandStatusLabel.ForeColor = OverlayTheme.Colors.WarningText;
                _buildCommandStatusLabel.Text = "Clipboard unavailable. Select the command text instead.";
            }
        }
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

    private static TextBox CreateCommandTextBox(int x, int y, int width, string command)
    {
        return new TextBox
        {
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Font = OverlayTheme.Font("Consolas", 8.25f),
            Location = new Point(x, y),
            ReadOnly = true,
            Size = new Size(width, 28),
            TabStop = true,
            Text = command,
            WordWrap = false
        };
    }

    private Button CreateCopyButton(int x, int y, string command)
    {
        var button = new Button
        {
            BackColor = OverlayTheme.Colors.ButtonBackground,
            FlatStyle = FlatStyle.Flat,
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(x, y),
            Size = new Size(78, 28),
            TabStop = true,
            Text = "Copy",
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = OverlayTheme.Colors.TabBorder;
        button.Click += (_, _) => CopyBuildCommand(command);
        return button;
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

    private TabPage CreateOverlayBridgeTab()
    {
        var page = CreateTabPage("Overlay Bridge");
        var title = CreateSectionLabel("Overlay Bridge", 18, 18, 500);
        var statusLabel = CreateLabel("Status", 22, 60, 140);
        var statusValue = CreateBridgeValueLabel("Reserved for post-v1.0", 180, 58, 280);
        var portLabel = CreateLabel("Port", 22, 106, 140);
        var portValue = CreateBridgeValueLabel("Configured in appsettings", 180, 104, 280);
        var clientsLabel = CreateLabel("Clients", 22, 152, 140);
        var clientsValue = CreateBridgeValueLabel("Not tracked yet", 180, 150, 280);
        var note = new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = OverlayTheme.Colors.TextMuted,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9.25f),
            Location = new Point(22, 214),
            Padding = new Padding(12),
            Size = new Size(510, 104),
            Text = "Bridge controls will live here after v1.0.",
            TextAlign = ContentAlignment.MiddleLeft
        };

        page.Controls.Add(title);
        page.Controls.Add(statusLabel);
        page.Controls.Add(statusValue);
        page.Controls.Add(portLabel);
        page.Controls.Add(portValue);
        page.Controls.Add(clientsLabel);
        page.Controls.Add(clientsValue);
        page.Controls.Add(note);
        return page;
    }

    private static Label CreateBridgeValueLabel(string text, int x, int y, int width)
    {
        return new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9.25f),
            Location = new Point(x, y),
            Padding = new Padding(8, 0, 8, 0),
            Size = new Size(width, 28),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private TabPage CreatePostRaceAnalysisTab()
    {
        var page = CreateTabPage("Post-race Analysis");
        var title = CreateSectionLabel("Post-race Analysis", 18, 18, 500);
        var sessionLabel = CreateLabel("Session", 22, 60, 140);
        _analysisCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(180, 56),
            Size = new Size(300, 28),
            TabStop = true
        };
        _analysisCombo.SelectedIndexChanged += (_, _) => ApplySelectedAnalysis();

        _analysisText = new Label
        {
            AutoSize = false,
            BackColor = OverlayTheme.Colors.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = OverlayTheme.Colors.TextSecondary,
            Font = OverlayTheme.Font(OverlayTheme.DefaultFontFamily, 9.25f),
            Location = new Point(22, 104),
            Padding = new Padding(12),
            Size = new Size(510, 350),
            Text = "Analysis will update after an iRacing session closes.",
            TextAlign = ContentAlignment.TopLeft
        };

        page.Controls.Add(title);
        page.Controls.Add(sessionLabel);
        page.Controls.Add(_analysisCombo);
        page.Controls.Add(_analysisText);
        SyncPostRaceAnalysis(force: true);
        return page;
    }

    private void SyncPostRaceAnalysis(bool force = false)
    {
        if (_analysisCombo is null || _analysisText is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (!force && (now - _lastAnalysisRefreshUtc).TotalSeconds < 2d)
        {
            return;
        }

        _lastAnalysisRefreshUtc = now;
        var analyses = _analysisStore.LoadRecent();
        var fingerprint = string.Join("|", analyses.Select(analysis => analysis.Id));
        if (!force && string.Equals(_analysisFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        _analysisFingerprint = fingerprint;
        var selectedId = (_analysisCombo.SelectedItem as AnalysisListItem)?.Analysis.Id;
        _syncingAnalysis = true;
        try
        {
            _analysisCombo.Items.Clear();
            foreach (var analysis in analyses)
            {
                _analysisCombo.Items.Add(new AnalysisListItem(analysis));
            }

            var selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                for (var index = 0; index < _analysisCombo.Items.Count; index++)
                {
                    if (_analysisCombo.Items[index] is AnalysisListItem item
                        && string.Equals(item.Analysis.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            _analysisCombo.SelectedIndex = _analysisCombo.Items.Count > 0 ? selectedIndex : -1;
        }
        finally
        {
            _syncingAnalysis = false;
        }

        ApplySelectedAnalysis();
    }

    private void ApplySelectedAnalysis()
    {
        if (_syncingAnalysis || _analysisText is null || _analysisCombo is null)
        {
            return;
        }

        _analysisText.Text = _analysisCombo.SelectedItem is AnalysisListItem item
            ? item.Analysis.Body
            : "Analysis will update after an iRacing session closes.";
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
            _rawCaptureCheckBox.Text = snapshot.RawCaptureActive ? "Raw capture active" : "Raw capture";
        }
        finally
        {
            _syncingRawCaptureCheckBox = false;
        }
    }

    private void SyncErrorLoggingTab()
    {
        if (_currentIssueLabel is null && _latestDiagnosticsBundleTextBox is null && _runtimeInfoLabel is null)
        {
            return;
        }

        var captureSnapshot = _captureState.Snapshot();
        if (_currentIssueLabel is not null)
        {
            _currentIssueLabel.Text = CurrentIssueText(captureSnapshot);
        }

        if (_latestDiagnosticsBundleTextBox is not null)
        {
            _latestDiagnosticsBundleTextBox.Text = LatestDiagnosticsBundlePath() ?? string.Empty;
        }

        if (_runtimeInfoLabel is not null)
        {
            _runtimeInfoLabel.Text = RuntimeInfoText(captureSnapshot, _storageOptions);
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
                ["source"] = "settings_error_logging_tab"
            });
            if (_latestDiagnosticsBundleTextBox is not null)
            {
                _latestDiagnosticsBundleTextBox.Text = bundlePath;
            }

            SetSupportStatus("Created diagnostics bundle.", isError: false);
            OpenSupportDirectory(Path.GetDirectoryName(bundlePath)!, "diagnostics");
        }
        catch (Exception exception)
        {
            _events.Record("diagnostics_bundle_failed", new Dictionary<string, string?>
            {
                ["error"] = exception.GetType().Name,
                ["source"] = "settings_error_logging_tab"
            });
            SetSupportStatus($"Bundle failed: {exception.Message}", isError: true);
        }
    }

    private void CopyLatestDiagnosticsBundlePath()
    {
        var bundlePath = _latestDiagnosticsBundleTextBox?.Text;
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

    private static string RuntimeInfoText(TelemetryCaptureStatusSnapshot capture, AppStorageOptions storage)
    {
        var version = AppVersionInfo.Current;
        return string.Join(
            Environment.NewLine,
            $"app: {version.InformationalVersion}",
            $"runtime: {version.RuntimeVersion}, {version.ProcessArchitecture}",
            $"os: {version.OperatingSystem}",
            $"telemetry: {capture.FrameCount:N0} frames, raw {capture.WrittenFrameCount:N0} written, {capture.DroppedFrameCount:N0} drops",
            $"capture file: {FormatBytes(capture.TelemetryFileBytes)}",
            $"storage: {(storage.UseRepositoryLocalStorage ? "repo-local" : "app-data")}");
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

    private sealed class AnalysisListItem
    {
        public AnalysisListItem(PostRaceAnalysis analysis)
        {
            Analysis = analysis;
        }

        public PostRaceAnalysis Analysis { get; }

        public override string ToString()
        {
            return Analysis.DisplayName;
        }
    }
}
