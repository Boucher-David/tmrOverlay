using TmrOverlay.App.Overlays.BrowserSources;
using TmrOverlay.App.Overlays.Content;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.Core.Settings;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class OverlayContentColumnSettingsTests
{
    [Fact]
    public void VisibleColumnsFor_PreservesUserOrderAndDisabledColumns()
    {
        var settings = new ApplicationSettings();
        var standings = settings.GetOrAddOverlay("standings", 780, 520);
        foreach (var column in OverlayContentColumnSettings.Standings.Columns)
        {
            standings.SetIntegerOption(column.OrderKey(standings.Id), column.DefaultOrder, 1, 6);
        }

        var driver = Column(OverlayContentColumnSettings.StandingsDriverColumnId);
        var car = Column(OverlayContentColumnSettings.StandingsCarNumberColumnId);
        var @class = Column(OverlayContentColumnSettings.StandingsClassPositionColumnId);
        var interval = Column(OverlayContentColumnSettings.StandingsIntervalColumnId);
        var pit = Column(OverlayContentColumnSettings.StandingsPitColumnId);
        var gap = Column(OverlayContentColumnSettings.StandingsGapColumnId);
        standings.SetIntegerOption(driver.OrderKey(standings.Id), 1, 1, 6);
        standings.SetIntegerOption(car.OrderKey(standings.Id), 2, 1, 6);
        standings.SetIntegerOption(@class.OrderKey(standings.Id), 3, 1, 6);
        standings.SetIntegerOption(interval.OrderKey(standings.Id), 4, 1, 6);
        standings.SetIntegerOption(pit.OrderKey(standings.Id), 5, 1, 6);
        standings.SetIntegerOption(gap.OrderKey(standings.Id), 6, 1, 6);
        standings.SetBooleanOption(gap.EnabledKey(standings.Id), false);
        standings.SetIntegerOption(driver.WidthKey(standings.Id), 360, driver.MinimumWidth, driver.MaximumWidth);

        var columns = OverlayContentColumnSettings.VisibleColumnsFor(
            standings,
            OverlayContentColumnSettings.Standings);

        Assert.Collection(
            columns.Select(column => column.Id),
            id => Assert.Equal(OverlayContentColumnSettings.StandingsDriverColumnId, id),
            id => Assert.Equal(OverlayContentColumnSettings.StandingsCarNumberColumnId, id),
            id => Assert.Equal(OverlayContentColumnSettings.StandingsClassPositionColumnId, id),
            id => Assert.Equal(OverlayContentColumnSettings.StandingsIntervalColumnId, id),
            id => Assert.Equal(OverlayContentColumnSettings.StandingsPitColumnId, id));
        Assert.All(columns, column => Assert.StartsWith("standings.", column.Id, StringComparison.Ordinal));
        Assert.Equal(360, columns[0].Width);
        Assert.DoesNotContain(columns, column => column.Id == OverlayContentColumnSettings.StandingsGapColumnId);
    }

    [Fact]
    public void BrowserRecommendedSize_ExpandsToConfiguredVisibleColumnWidths()
    {
        var settings = new ApplicationSettings();
        var standings = settings.GetOrAddOverlay("standings", 780, 520);
        foreach (var column in OverlayContentColumnSettings.Standings.Columns)
        {
            standings.SetIntegerOption(column.WidthKey(standings.Id), column.MaximumWidth, column.MinimumWidth, column.MaximumWidth);
        }

        var size = BrowserOverlayRecommendedSize.For(StandingsOverlayDefinition.Definition, standings);

        Assert.Equal(1212, size.Width);
        Assert.Equal(520, size.Height);
    }

    [Fact]
    public void RelativeDefaultBrowserColumnsUseOverlayOwnedIds()
    {
        var settings = RelativeBrowserSettings.From(new ApplicationSettings());

        Assert.Collection(
            settings.Columns.Select(column => column.Id),
            id => Assert.Equal(OverlayContentColumnSettings.RelativePositionColumnId, id),
            id => Assert.Equal(OverlayContentColumnSettings.RelativeDriverColumnId, id),
            id => Assert.Equal(OverlayContentColumnSettings.RelativeGapColumnId, id));
        Assert.Collection(
            settings.Columns.Select(column => column.DataKey),
            dataKey => Assert.Equal(OverlayContentColumnSettings.DataRelativePosition, dataKey),
            dataKey => Assert.Equal(OverlayContentColumnSettings.DataDriver, dataKey),
            dataKey => Assert.Equal(OverlayContentColumnSettings.DataGap, dataKey));
    }

    private static OverlayContentColumnDefinition Column(string id)
    {
        return OverlayContentColumnSettings.Standings.Columns.Single(column => column.Id == id);
    }
}
