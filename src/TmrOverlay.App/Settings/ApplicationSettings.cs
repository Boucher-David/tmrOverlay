namespace TmrOverlay.App.Settings;

internal sealed class ApplicationSettings
{
    public int SettingsVersion { get; init; } = 1;

    public List<OverlaySettings> Overlays { get; init; } = [];

    public OverlaySettings GetOrAddOverlay(
        string id,
        int defaultWidth,
        int defaultHeight,
        int defaultX = 24,
        int defaultY = 24)
    {
        var existing = Overlays.FirstOrDefault(overlay => string.Equals(overlay.Id, id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var overlay = new OverlaySettings
        {
            Id = id,
            X = defaultX,
            Y = defaultY,
            Width = defaultWidth,
            Height = defaultHeight
        };
        Overlays.Add(overlay);
        return overlay;
    }
}
