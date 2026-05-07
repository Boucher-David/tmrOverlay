using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class RelativeBrowserSettingsTests
{
    [Fact]
    public void From_UsesConfiguredAheadAndBehindCounts()
    {
        var settings = new ApplicationSettings();
        var relative = settings.GetOrAddOverlay("relative", 520, 360);
        relative.SetIntegerOption(OverlayOptionKeys.RelativeCarsAhead, 3, 0, 8);
        relative.SetIntegerOption(OverlayOptionKeys.RelativeCarsBehind, 7, 0, 8);

        var browserSettings = RelativeBrowserSettings.From(settings);

        Assert.Equal(3, browserSettings.CarsAhead);
        Assert.Equal(7, browserSettings.CarsBehind);
    }

    [Fact]
    public void From_ClampsConfiguredCounts()
    {
        var settings = new ApplicationSettings();
        var relative = settings.GetOrAddOverlay("relative", 520, 360);
        relative.Options[OverlayOptionKeys.RelativeCarsAhead] = "99";
        relative.Options[OverlayOptionKeys.RelativeCarsBehind] = "-2";

        var browserSettings = RelativeBrowserSettings.From(settings);

        Assert.Equal(8, browserSettings.CarsAhead);
        Assert.Equal(0, browserSettings.CarsBehind);
    }

    [Fact]
    public void From_DefaultsWhenRelativeOverlayIsMissing()
    {
        var browserSettings = RelativeBrowserSettings.From(new ApplicationSettings());

        Assert.Equal(5, browserSettings.CarsAhead);
        Assert.Equal(5, browserSettings.CarsBehind);
    }
}
