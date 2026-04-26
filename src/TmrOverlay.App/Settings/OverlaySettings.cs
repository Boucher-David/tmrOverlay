namespace TmrOverlay.App.Settings;

internal sealed class OverlaySettings
{
    public required string Id { get; init; }

    public bool Enabled { get; set; } = true;

    public int X { get; set; } = 24;

    public int Y { get; set; } = 24;

    public int Width { get; set; }

    public int Height { get; set; }

    public double Opacity { get; set; } = 0.88d;

    public bool AlwaysOnTop { get; set; } = true;

    public string? ScreenId { get; set; }
}
