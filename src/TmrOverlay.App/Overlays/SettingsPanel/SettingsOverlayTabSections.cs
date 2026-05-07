using System.Drawing;
using TmrOverlay.App.Localhost;
using TmrOverlay.App.Overlays.BrowserSources;
using TmrOverlay.App.Overlays.Styling;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal static class SettingsOverlayTabSections
{
    public static int AddOverlayBasics(
        Control parent,
        OverlayDefinition definition,
        OverlaySettings settings,
        int controlTop,
        Action saveAndApply)
    {
        var enabledCheckBox = SettingsUi.CreateCheckBox("Visible", settings.Enabled, 22, controlTop, 220);
        enabledCheckBox.CheckedChanged += (_, _) =>
        {
            settings.Enabled = enabledCheckBox.Checked;
            saveAndApply();
        };
        parent.Controls.Add(enabledCheckBox);

        var optionsTop = controlTop + 46;
        optionsTop = AddScaleOption(parent, definition, settings, optionsTop, saveAndApply);
        optionsTop = AddOpacityOption(parent, definition, settings, optionsTop, saveAndApply);
        optionsTop = AddSessionFilterOptions(parent, definition, settings, optionsTop, saveAndApply);
        return optionsTop;
    }

    public static int AddScaleOption(
        Control parent,
        OverlayDefinition definition,
        OverlaySettings settings,
        int optionsTop,
        Action saveAndApply)
    {
        if (!definition.ShowScaleControl)
        {
            return optionsTop;
        }

        var scaleLabel = SettingsUi.CreateLabel("Scale", 22, optionsTop + 4, 160);
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
        var percentLabel = SettingsUi.CreateLabel("%", 278, optionsTop + 4, 40);
        scaleInput.ValueChanged += (_, _) =>
        {
            settings.Scale = Math.Clamp((double)scaleInput.Value / 100d, 0.6d, 2d);
            settings.Width = ScaleDimension(definition.DefaultWidth, settings.Scale);
            settings.Height = ScaleDimension(definition.DefaultHeight, settings.Scale);
            saveAndApply();
        };

        parent.Controls.Add(scaleLabel);
        parent.Controls.Add(scaleInput);
        parent.Controls.Add(percentLabel);
        return optionsTop + 50;
    }

    private static int AddOpacityOption(
        Control parent,
        OverlayDefinition definition,
        OverlaySettings settings,
        int optionsTop,
        Action saveAndApply)
    {
        if (!definition.ShowOpacityControl)
        {
            return optionsTop;
        }

        var opacityLabel = SettingsUi.CreateLabel(
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
        var percentLabel = SettingsUi.CreateLabel("%", 278, optionsTop + 4, 40);
        opacityInput.ValueChanged += (_, _) =>
        {
            settings.Opacity = Math.Clamp((double)opacityInput.Value / 100d, 0.2d, 1d);
            saveAndApply();
        };

        parent.Controls.Add(opacityLabel);
        parent.Controls.Add(opacityInput);
        parent.Controls.Add(percentLabel);
        return optionsTop + 50;
    }

    private static int AddSessionFilterOptions(
        Control parent,
        OverlayDefinition definition,
        OverlaySettings settings,
        int optionsTop,
        Action saveAndApply)
    {
        if (!definition.ShowSessionFilters)
        {
            return optionsTop;
        }

        var sessionBox = new GroupBox
        {
            ForeColor = OverlayTheme.Colors.TextControl,
            Location = new Point(22, optionsTop),
            Size = new Size(360, 154),
            Text = "Display in sessions"
        };

        var testCheckBox = SettingsUi.CreateCheckBox("Test", settings.ShowInTest, 16, 28, 150);
        var practiceCheckBox = SettingsUi.CreateCheckBox("Practice", settings.ShowInPractice, 180, 28, 150);
        var qualifyingCheckBox = SettingsUi.CreateCheckBox("Qualifying", settings.ShowInQualifying, 16, 72, 150);
        var raceCheckBox = SettingsUi.CreateCheckBox("Race", settings.ShowInRace, 180, 72, 150);

        testCheckBox.CheckedChanged += (_, _) =>
        {
            settings.ShowInTest = testCheckBox.Checked;
            saveAndApply();
        };
        practiceCheckBox.CheckedChanged += (_, _) =>
        {
            settings.ShowInPractice = practiceCheckBox.Checked;
            saveAndApply();
        };
        qualifyingCheckBox.CheckedChanged += (_, _) =>
        {
            settings.ShowInQualifying = qualifyingCheckBox.Checked;
            saveAndApply();
        };
        raceCheckBox.CheckedChanged += (_, _) =>
        {
            settings.ShowInRace = raceCheckBox.Checked;
            saveAndApply();
        };

        sessionBox.Controls.Add(testCheckBox);
        sessionBox.Controls.Add(practiceCheckBox);
        sessionBox.Controls.Add(qualifyingCheckBox);
        sessionBox.Controls.Add(raceCheckBox);
        parent.Controls.Add(sessionBox);
        return optionsTop + 176;
    }

    public static void AddChromeSettingsPage(
        Control parent,
        OverlaySettings settings,
        string title,
        string itemLabel,
        string testKey,
        string practiceKey,
        string qualifyingKey,
        string raceKey,
        Action saveAndApply)
    {
        parent.Controls.Add(SettingsUi.CreateSectionLabel(title, 18, 18, 500));
        parent.Controls.Add(SettingsUi.CreateLabel("Item", 22, 62, 120));
        parent.Controls.Add(SettingsUi.CreateLabel("Test", 196, 62, 90));
        parent.Controls.Add(SettingsUi.CreateLabel("Practice", 296, 62, 110));
        parent.Controls.Add(SettingsUi.CreateLabel("Qualifying", 416, 62, 120));
        parent.Controls.Add(SettingsUi.CreateLabel("Race", 548, 62, 90));

        parent.Controls.Add(SettingsUi.CreateLabel(itemLabel, 22, 104, 150));
        AddChromeSessionCheckBox(parent, settings, testKey, 196, 100, saveAndApply);
        AddChromeSessionCheckBox(parent, settings, practiceKey, 296, 100, saveAndApply);
        AddChromeSessionCheckBox(parent, settings, qualifyingKey, 416, 100, saveAndApply);
        AddChromeSessionCheckBox(parent, settings, raceKey, 548, 100, saveAndApply);
    }

    public static void AddLocalhostOptions(
        Control parent,
        OverlayDefinition definition,
        LocalhostOverlayOptions localhostOptions,
        int top,
        Action<string> copyTextToClipboard)
    {
        const int x = 560;
        parent.Controls.Add(SettingsUi.CreateSectionLabel("Localhost browser source", x, top, 500));
        if (BrowserOverlayPageRenderer.TryGetRouteForOverlayId(definition.Id, out var route))
        {
            var url = $"{localhostOptions.Prefix.TrimEnd('/')}{route}";
            parent.Controls.Add(SettingsUi.CreateLabel("URL", x + 4, top + 42, 120));
            parent.Controls.Add(SettingsUi.CreateSelectableValueBox(url, x + 92, top + 36, 300, 30));
            var copyButton = SettingsUi.CreateActionButton("Copy", x + 402, top + 36, 76);
            copyButton.Click += (_, _) => copyTextToClipboard(url);
            parent.Controls.Add(copyButton);
            parent.Controls.Add(SettingsUi.CreateMutedLabel("This browser-source route does not require the native overlay to be visible. Disable LocalhostOverlays in configuration if the local server is not needed.", x + 4, top + 76, 500));
            return;
        }

        parent.Controls.Add(SettingsUi.CreateMutedLabel("No localhost route is available for this overlay yet.", x + 4, top + 42, 560));
    }

    public static void AddDescriptorOptions(
        Control parent,
        IReadOnlyList<OverlaySettingsOptionDescriptor> options,
        OverlaySettings settings,
        int top,
        Action saveAndApply)
    {
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            var x = option.Kind == OverlaySettingsOptionKind.Boolean && index % 2 == 1 ? 260 : 22;
            var y = top + (option.Kind == OverlaySettingsOptionKind.Boolean ? index / 2 : index) * 40;

            if (option.Kind == OverlaySettingsOptionKind.Boolean)
            {
                var checkBox = SettingsUi.CreateCheckBox(
                    option.Label,
                    settings.GetBooleanOption(option.Key, option.BooleanDefault),
                    x,
                    y,
                    220);
                checkBox.CheckedChanged += (_, _) =>
                {
                    settings.SetBooleanOption(option.Key, checkBox.Checked);
                    saveAndApply();
                };
                parent.Controls.Add(checkBox);
                continue;
            }

            if (option.Kind == OverlaySettingsOptionKind.Integer)
            {
                parent.Controls.Add(SettingsUi.CreateLabel(option.Label, 22, y + 4, 120));
                var input = SettingsUi.CreateIntegerInput(
                    settings.GetIntegerOption(option.Key, option.IntegerDefault, option.Minimum, option.Maximum),
                    option.Minimum,
                    option.Maximum,
                    150,
                    y);
                input.ValueChanged += (_, _) =>
                {
                    settings.SetIntegerOption(option.Key, (int)input.Value, option.Minimum, option.Maximum);
                    saveAndApply();
                };
                parent.Controls.Add(input);
            }
        }
    }

    private static void AddChromeSessionCheckBox(
        Control parent,
        OverlaySettings settings,
        string optionKey,
        int x,
        int y,
        Action saveAndApply)
    {
        var checkBox = SettingsUi.CreateCheckBox(
            string.Empty,
            settings.GetBooleanOption(optionKey, defaultValue: true),
            x,
            y,
            32);
        checkBox.CheckedChanged += (_, _) =>
        {
            settings.SetBooleanOption(optionKey, checkBox.Checked);
            saveAndApply();
        };
        parent.Controls.Add(checkBox);
    }

    private static int ScaleDimension(int defaultDimension, double scale)
    {
        return Math.Max(80, (int)Math.Round(defaultDimension * Math.Clamp(scale, 0.6d, 2d)));
    }
}
