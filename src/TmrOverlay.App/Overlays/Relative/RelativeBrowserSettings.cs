using TmrOverlay.App.Overlays.Content;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.Relative;

internal sealed record RelativeBrowserSettings(
    int CarsAhead,
    int CarsBehind,
    IReadOnlyList<OverlayContentBrowserColumn> Columns)
{
    public static RelativeBrowserSettings Default { get; } = new(
        CarsAhead: 5,
        CarsBehind: 5,
        Columns: OverlayContentColumnSettings.BrowserColumnsFor(null, OverlayContentColumnSettings.Relative));

    public static RelativeBrowserSettings From(ApplicationSettings settings)
    {
        var relative = settings.Overlays.FirstOrDefault(
            overlay => string.Equals(overlay.Id, RelativeOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase));
        return new RelativeBrowserSettings(
            CarsAhead: relative?.GetIntegerOption(
                OverlayOptionKeys.RelativeCarsAhead,
                defaultValue: Default.CarsAhead,
                minimum: 0,
                maximum: 8) ?? Default.CarsAhead,
            CarsBehind: relative?.GetIntegerOption(
                OverlayOptionKeys.RelativeCarsBehind,
                defaultValue: Default.CarsBehind,
                minimum: 0,
                maximum: 8) ?? Default.CarsBehind,
            Columns: OverlayContentColumnSettings.BrowserColumnsFor(
                relative,
                OverlayContentColumnSettings.Relative));
    }
}
