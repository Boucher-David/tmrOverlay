using System.Drawing;
using System.Reflection;
using TmrOverlay.App.Overlays;
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

        Assert.Equal(1276, size.Width);
        Assert.Equal(520, size.Height);
    }

    [Fact]
    public void BrowserRecommendedSize_UsesCompactDefaultVisibleColumnWidths()
    {
        var settings = new ApplicationSettings();
        var standings = settings.GetOrAddOverlay("standings", 780, 520);
        var relative = settings.GetOrAddOverlay("relative", 520, 360);

        var standingsSize = BrowserOverlayRecommendedSize.For(StandingsOverlayDefinition.Definition, standings);
        var relativeSize = BrowserOverlayRecommendedSize.For(RelativeOverlayDefinition.Definition, relative);

        Assert.Equal(591, standingsSize.Width);
        Assert.Equal(520, standingsSize.Height);
        Assert.Equal(440, relativeSize.Width);
        Assert.Equal(360, relativeSize.Height);
    }

    [Fact]
    public void ContentColumnsKeepCompactOverlayHeadersAndHumanSettingsLabels()
    {
        var settings = new ApplicationSettings();
        var standings = settings.GetOrAddOverlay("standings", 780, 520);
        var relative = settings.GetOrAddOverlay("relative", 520, 360);

        var standingsColumns = OverlayContentColumnSettings.ColumnsFor(standings, OverlayContentColumnSettings.Standings);
        var relativeColumns = OverlayContentColumnSettings.ColumnsFor(relative, OverlayContentColumnSettings.Relative);
        var standingsBrowserColumns = OverlayContentColumnSettings.BrowserColumnsFor(standings, OverlayContentColumnSettings.Standings);

        Assert.Contains(standingsColumns, column =>
            column.Id == OverlayContentColumnSettings.StandingsClassPositionColumnId
            && column.Label == "CLS"
            && column.SettingsLabel == "Class position");
        Assert.Contains(standingsColumns, column =>
            column.Id == OverlayContentColumnSettings.StandingsCarNumberColumnId
            && column.Label == "CAR"
            && column.SettingsLabel == "Car number");
        Assert.Contains(standingsColumns, column =>
            column.Id == OverlayContentColumnSettings.StandingsGapColumnId
            && column.Label == "GAP"
            && column.SettingsLabel == "Class gap");
        Assert.Contains(standingsColumns, column =>
            column.Id == OverlayContentColumnSettings.StandingsIntervalColumnId
            && column.Label == "INT"
            && column.SettingsLabel == "Focus interval");
        Assert.Contains(standingsColumns, column =>
            column.Id == OverlayContentColumnSettings.StandingsPitColumnId
            && column.Label == "PIT"
            && column.SettingsLabel == "Pit status");
        Assert.Contains(relativeColumns, column =>
            column.Id == OverlayContentColumnSettings.RelativePositionColumnId
            && column.Label == "Pos"
            && column.SettingsLabel == "Relative position");
        Assert.Contains(relativeColumns, column =>
            column.Id == OverlayContentColumnSettings.RelativeGapColumnId
            && column.Label == "Delta"
            && column.SettingsLabel == "Relative delta");
        Assert.Contains(relativeColumns, column =>
            column.Id == OverlayContentColumnSettings.RelativePitColumnId
            && column.Label == "Pit"
            && column.SettingsLabel == "Pit status");
        Assert.Contains(standingsBrowserColumns, column =>
            column.Id == OverlayContentColumnSettings.StandingsClassPositionColumnId
            && column.Label == "CLS");
    }

    [Fact]
    public void OverlayManagerScaledOverlaySize_AppliesScaleAfterColumnDrivenBaseWidth()
    {
        var method = typeof(OverlayManager).GetMethod(
            "ScaledOverlaySize",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var standings = new ApplicationSettings().GetOrAddOverlay("standings", 780, 520);
        standings.Scale = 1.25d;

        var size = (Size)method.Invoke(null, [StandingsOverlayDefinition.Definition, standings])!;

        Assert.Equal(736, size.Width);
        Assert.Equal(650, size.Height);
    }

    [Fact]
    public void OverlayManagerPreservesExpandedStandingsHeightWithoutFreezingOtherSizes()
    {
        Assert.True(OverlayManager.ShouldPreserveExpandedOverlayHeight(
            StandingsOverlayDefinition.Definition,
            new Size(780, 720),
            new Size(780, 520)));
        Assert.False(OverlayManager.ShouldPreserveExpandedOverlayHeight(
            StandingsOverlayDefinition.Definition,
            new Size(760, 720),
            new Size(780, 520)));
        Assert.False(OverlayManager.ShouldPreserveExpandedOverlayHeight(
            RelativeOverlayDefinition.Definition,
            new Size(520, 520),
            new Size(520, 360)));
    }

    [Fact]
    public void DefaultTableColumnWidthsMatchV2CompactProductionLayout()
    {
        Assert.Collection(
            OverlayContentColumnSettings.Standings.Columns.Select(column => column.DefaultWidth),
            width => Assert.Equal(35, width),
            width => Assert.Equal(50, width),
            width => Assert.Equal(250, width),
            width => Assert.Equal(60, width),
            width => Assert.Equal(60, width),
            width => Assert.Equal(30, width));
        Assert.Collection(
            OverlayContentColumnSettings.Relative.Columns.Select(column => column.DefaultWidth),
            width => Assert.Equal(38, width),
            width => Assert.Equal(250, width),
            width => Assert.Equal(70, width),
            width => Assert.Equal(30, width));
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
