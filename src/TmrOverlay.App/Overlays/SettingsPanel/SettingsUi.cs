using System.Drawing;
using TmrOverlay.App.Overlays.Styling;

namespace TmrOverlay.App.Overlays.SettingsPanel;

internal static class SettingsUi
{
    public static TabPage CreateTabPage(string text)
    {
        return new TabPage(text)
        {
            BackColor = OverlayTheme.Colors.PageBackground,
            ForeColor = OverlayTheme.Colors.TextSecondary
        };
    }

    public static Label CreateSectionLabel(string text, int x, int y, int width)
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

    public static Label CreateLabel(string text, int x, int y, int width)
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

    public static Label CreateMutedLabel(string text, int x, int y, int width)
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

    public static Label CreateMultiLineValueLabel(string text, int x, int y, int width, int height)
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

    public static Label CreateWarningLabel(string text, int x, int y, int width, int height)
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

    public static Label CreateValueLabel(string text, int x, int y, int width, int height)
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

    public static TextBox CreateSelectableValueBox(string text, int x, int y, int width, int height)
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

    public static TextBox CreateEditableTextBox(string text, int x, int y, int width, int height)
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

    public static Button CreateActionButton(string text, int x, int y, int width)
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

    public static CheckBox CreateCheckBox(string text, bool isChecked, int x, int y, int width)
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

    public static NumericUpDown CreateIntegerInput(int value, int minimum, int maximum, int x, int y)
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
}
