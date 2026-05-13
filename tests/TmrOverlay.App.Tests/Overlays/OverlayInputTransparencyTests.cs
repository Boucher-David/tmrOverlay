using System;
using System.Drawing;
using TmrOverlay.App.Overlays.DesignV2;
using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.StreamChat;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class OverlayInputTransparencyTests
{
    [Fact]
    public void DesignV2InputTransparentKind_IncludesStreamChatClickThroughOverlay()
    {
        Assert.True(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.StreamChat));
    }

    [Fact]
    public void DesignV2InputTransparentKind_ExcludesDraggableDataOverlays()
    {
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.Flags));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.Standings));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.Relative));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.TrackMap));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.GapToLeader));
    }

    [Fact]
    public void StreamChatHeaderHitRegion_AllowsDraggingWhileContentStaysClickThrough()
    {
        var size = new Size(420, 320);

        Assert.True(StreamChatForm.IsHeaderDragHit(new Point(24, 18), size));
        Assert.True(DesignV2LiveOverlayForm.IsStreamChatDragHit(new Point(24, 18), size));
        Assert.False(StreamChatForm.IsHeaderDragHit(new Point(24, 58), size));
        Assert.False(DesignV2LiveOverlayForm.IsStreamChatDragHit(new Point(24, 58), size));
    }

    [Fact]
    public void DesignV2RelativeStableRows_PadsAheadSlotsBeforeReference()
    {
        var ahead = new RelativeOverlayRowViewModel("4", "#4 Ahead", "-0.400", "GT3", null, false, true, false, true, false, false);
        var reference = new RelativeOverlayRowViewModel("5", "#5 Focus", "0.000", "GT3", null, true, false, false, true, false, false);
        var behind = new RelativeOverlayRowViewModel("6", "#6 Behind", "+0.500", "GT3", null, false, false, true, true, false, false);
        var viewModel = new RelativeOverlayViewModel("live relative", "source: test", [ahead, reference, behind]);

        var rows = DesignV2LiveOverlayForm.StableRelativeRows(viewModel, carsAhead: 3, carsBehind: 2);

        Assert.Equal(6, rows.Count);
        Assert.Null(rows[0]);
        Assert.Null(rows[1]);
        Assert.Same(ahead, rows[2]);
        Assert.Same(reference, rows[3]);
        Assert.Same(behind, rows[4]);
        Assert.Null(rows[5]);
    }

    [Fact]
    public void DesignV2RadarBody_UsesSpatialCarsOnly()
    {
        var spatialCar = new LiveSpatialCar(
            CarIdx: 12,
            Quality: LiveModelQuality.Reliable,
            PlacementEvidence: LiveSignalEvidence.Reliable("test"),
            RelativeLaps: 0.01d,
            RelativeSeconds: 1.2d,
            RelativeMeters: 8d,
            OverallPosition: 6,
            ClassPosition: 5,
            CarClass: 4098,
            TrackSurface: null,
            OnPitRoad: false,
            CarClassColorHex: "#FFDA59");
        var approach = new LiveMulticlassApproach(
            CarIdx: 51,
            CarClass: 4099,
            RelativeLaps: -0.03d,
            RelativeSeconds: -4.5d,
            ClosingRateSecondsPerSecond: null,
            Urgency: 0.2d);
        var spatial = LiveSpatialModel.Empty with
        {
            HasData = true,
            Quality = LiveModelQuality.Reliable,
            HasCarLeft = true,
            HasCarRight = false,
            Cars = [spatialCar],
            StrongestMulticlassApproach = approach
        };
        var settings = new OverlaySettings { Id = "car-radar" };

        var body = DesignV2LiveOverlayForm.RadarBodyFromSpatial(spatial, overlayAvailable: true, previewVisible: false, settings);

        Assert.True(body.IsAvailable);
        Assert.True(body.HasLeft);
        Assert.False(body.HasRight);
        Assert.Same(spatialCar, Assert.Single(body.Cars));
        Assert.Same(approach, body.StrongestMulticlassApproach);
    }

    [Fact]
    public void DesignV2RadarBody_DoesNotRenderSecondsOnlyRowsAsSpatialCars()
    {
        var secondsOnly = new LiveSpatialCar(
            CarIdx: 12,
            Quality: LiveModelQuality.Partial,
            PlacementEvidence: LiveSignalEvidence.Partial("test", "no_meters"),
            RelativeLaps: 0.01d,
            RelativeSeconds: 1.2d,
            RelativeMeters: null,
            OverallPosition: 6,
            ClassPosition: 5,
            CarClass: 4098,
            TrackSurface: null,
            OnPitRoad: false,
            CarClassColorHex: "#FFDA59");
        var spatial = LiveSpatialModel.Empty with
        {
            HasData = true,
            Cars = [secondsOnly]
        };

        var body = DesignV2LiveOverlayForm.RadarBodyFromSpatial(
            spatial,
            overlayAvailable: true,
            previewVisible: false,
            new OverlaySettings { Id = "car-radar" });

        Assert.True(body.IsAvailable);
        Assert.Empty(body.Cars);
    }

    [Fact]
    public void DesignV2InputRailLayout_KeepsDefaultInputControlsInsideRail()
    {
        var rail = new RectangleF(0, 0, 204, 188);

        var layout = DesignV2LiveOverlayForm.BuildInputRailLayout(
            rail,
            showThrottle: true,
            showBrake: true,
            showClutch: true,
            showSteering: true,
            showGear: true,
            showSpeed: true);

        Assert.Contains(layout.Items, item => item.Kind == DesignV2InputRailItemKind.SteeringWheel);
        Assert.Contains(layout.Items, item => item.Kind == DesignV2InputRailItemKind.Gear);
        Assert.Contains(layout.Items, item => item.Kind == DesignV2InputRailItemKind.Speed);
        Assert.All(layout.Items, item =>
        {
            Assert.True(item.Bounds.Top >= rail.Top, $"{item.Kind} starts above rail.");
            Assert.True(item.Bounds.Bottom <= rail.Bottom + 0.001f, $"{item.Kind} ends below rail.");
        });
    }

    [Fact]
    public void DesignV2GapGraphPoint_MapsLeaderTopAndMaxGapBottom()
    {
        var graph = new DesignV2GraphBody(
            Points: [],
            Series: [],
            Weather: [],
            LeaderChanges: [],
            DriverChanges: [],
            StartSeconds: 0d,
            EndSeconds: 10d,
            MaxGapSeconds: 20d,
            LapReferenceSeconds: 60d,
            SelectedSeriesCount: 0,
            TrendMetrics: [],
            ActiveThreat: null,
            ThreatCarIdx: null,
            MetricDeadbandSeconds: 0.25d);
        var plot = new RectangleF(0, 10, 100, 200);
        var leaderPoint = new DesignV2GapTrendPoint(DateTimeOffset.UtcNow, 5d, 0d, 1, false, true, 1, false);
        var trailingPoint = leaderPoint with { GapSeconds = 20d, IsClassLeader = false, ClassPosition = 10 };

        Assert.Equal(plot.Top, DesignV2LiveOverlayForm.GapGraphPoint(leaderPoint, graph, plot, 20d).Y);
        Assert.Equal(plot.Bottom, DesignV2LiveOverlayForm.GapGraphPoint(trailingPoint, graph, plot, 20d).Y);
    }

    [Fact]
    public void DesignV2Chrome_UsesPerOverlayPerSessionHeaderFooterSettings()
    {
        var settings = new OverlaySettings { Id = "standings" };
        settings.SetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusRace, false);
        settings.SetBooleanOption(OverlayOptionKeys.ChromeHeaderTimeRemainingRace, true);
        settings.SetBooleanOption(OverlayOptionKeys.ChromeFooterSourceRace, false);
        var snapshot = RaceSnapshot(timeRemainingSeconds: 600d);

        var header = DesignV2LiveOverlayForm.BuildHeaderText(settings, snapshot, "live standings");
        var showFooter = DesignV2LiveOverlayForm.ShowFooterForSettings(
            DesignV2LiveOverlayKind.Standings,
            settings,
            snapshot);

        Assert.Equal("00:10", header);
        Assert.False(showFooter);
    }

    [Fact]
    public void DesignV2Chrome_HonorsFuelSourceToggleInAdditionToFooterSessionToggle()
    {
        var settings = new OverlaySettings { Id = "fuel-calculator" };
        settings.SetBooleanOption(OverlayOptionKeys.ChromeFooterSourceRace, true);
        settings.SetBooleanOption(OverlayOptionKeys.FuelSource, false);

        Assert.False(DesignV2LiveOverlayForm.ShowFooterForSettings(
            DesignV2LiveOverlayKind.FuelCalculator,
            settings,
            RaceSnapshot(timeRemainingSeconds: 600d)));
    }

    private static LiveTelemetrySnapshot RaceSnapshot(double timeRemainingSeconds)
    {
        return LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            Models = LiveRaceModels.Empty with
            {
                Session = LiveSessionModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Reliable,
                    SessionType = "Race",
                    SessionName = "Race",
                    EventType = "Race",
                    SessionTimeRemainSeconds = timeRemainingSeconds
                }
            }
        };
    }
}
