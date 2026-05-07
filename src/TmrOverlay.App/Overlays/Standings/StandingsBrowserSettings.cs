using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.Standings;

internal sealed record StandingsBrowserSettings(
    int MaximumRows,
    int OtherClassRowsPerClass)
{
    private const int DefaultMaximumRows = 14;

    public static StandingsBrowserSettings Default { get; } = new(
        MaximumRows: DefaultMaximumRows,
        OtherClassRowsPerClass: 2);

    public static StandingsBrowserSettings From(ApplicationSettings settings)
    {
        var standings = settings.Overlays.FirstOrDefault(
            overlay => string.Equals(overlay.Id, StandingsOverlayDefinition.Definition.Id, StringComparison.OrdinalIgnoreCase));
        return new StandingsBrowserSettings(
            MaximumRows: DefaultMaximumRows,
            OtherClassRowsPerClass: standings?.GetIntegerOption(
                OverlayOptionKeys.StandingsOtherClassRows,
                defaultValue: Default.OtherClassRowsPerClass,
                minimum: 0,
                maximum: 6) ?? Default.OtherClassRowsPerClass);
    }
}
