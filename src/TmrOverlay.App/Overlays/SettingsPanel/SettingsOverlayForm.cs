using System.Drawing;
using System.Drawing.Text;
using TmrOverlay.App.Analysis;
using TmrOverlay.App.Events;
using TmrOverlay.App.Overlays.Abstractions;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.Core.Analysis;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal sealed class SettingsOverlayForm : PersistentOverlayForm
{
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

    public SettingsOverlayForm(
        ApplicationSettings applicationSettings,
        IReadOnlyList<OverlayDefinition> managedOverlays,
        PostRaceAnalysisStore analysisStore,
        TelemetryCaptureState captureState,
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
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(126, 30),
            Location = new Point(OverlayTheme.Layout.SettingsTabInset, OverlayTheme.Layout.SettingsTabTop),
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
            SyncPostRaceAnalysis();
        };
        _refreshTimer.Start();
        SyncRawCaptureCheckBox();
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

        TextRenderer.DrawText(
            e.Graphics,
            _tabs.TabPages[e.Index].Text,
            _tabs.Font,
            bounds,
            OverlayTheme.Colors.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
