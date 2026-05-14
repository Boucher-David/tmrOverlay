using TmrOverlay.App.Overlays.DesignV2;
using TmrOverlay.App.Overlays.TrackMap;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using TmrOverlay.Core.TrackMaps;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class TrackMapMarkerPolicyTests
{
    [Fact]
    public void ShouldRenderTimingMarker_HidesOpponentUntilItHasTakenGrid()
    {
        var pending = Row(carIdx: 12, hasTakenGrid: false);
        var racing = pending with { HasTakenGrid = true };

        Assert.False(TrackMapMarkerPolicy.ShouldRenderTimingMarker(pending, isFocus: false));
        Assert.True(TrackMapMarkerPolicy.ShouldRenderTimingMarker(racing, isFocus: false));
    }

    [Fact]
    public void ShouldRenderTimingMarker_KeepsFocusMarkerWhenLocalProgressIsReal()
    {
        Assert.True(TrackMapMarkerPolicy.ShouldRenderTimingMarker(
            Row(carIdx: 10, hasTakenGrid: false),
            isFocus: true));
    }

    [Fact]
    public void ShouldRenderFocusSampleMarker_HidesLocalPitRoadFallbackButAllowsRemoteFocus()
    {
        var localPit = Sample(focusCarIdx: 10, playerCarIdx: 10, onPitRoad: true, playerTrackSurface: 1);
        var remoteFocus = Sample(focusCarIdx: 12, playerCarIdx: 10, onPitRoad: true, playerTrackSurface: 1);
        var localOnTrack = Sample(focusCarIdx: 10, playerCarIdx: 10, onPitRoad: false, playerTrackSurface: 3);

        Assert.False(TrackMapMarkerPolicy.ShouldRenderFocusSampleMarker(localPit));
        Assert.True(TrackMapMarkerPolicy.ShouldRenderFocusSampleMarker(remoteFocus));
        Assert.True(TrackMapMarkerPolicy.ShouldRenderFocusSampleMarker(localOnTrack));
    }

    [Fact]
    public void DesignV2TrackMapMarkers_DoNotInventScoringOnlyStartingGridDots()
    {
        var snapshot = LiveTelemetrySnapshot.Empty with
        {
            Models = LiveRaceModels.Empty with
            {
                Scoring = LiveScoringModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Partial,
                    Source = LiveScoringSource.StartingGrid,
                    Rows =
                    [
                        ScoringRow(carIdx: 10, position: 1, isFocus: true),
                        ScoringRow(carIdx: 11, position: 2, isFocus: false)
                    ]
                }
            }
        };

        var markers = DesignV2LiveOverlayForm.BuildTrackMapMarkers(snapshot);

        Assert.Empty(markers);
    }

    [Fact]
    public void TrackMapRenderModel_UsesGeneratedGeometryAndNativeFocusLabel()
    {
        var viewModel = new TrackMapOverlayViewModel(
            Title: "Track Map",
            Status: "live",
            Source: "source: live position telemetry",
            IsAvailable: true,
            Markers:
            [
                new TrackMapOverlayMarker(10, 0.25d, IsFocus: true, ClassColorHex: null, Position: 5),
                new TrackMapOverlayMarker(11, 0.50d, IsFocus: false, ClassColorHex: "#FFDA59", Position: null)
            ],
            Sectors:
            [
                new LiveTrackSectorSegment(0, 0d, 0.5d, LiveTrackSectorHighlights.PersonalBest),
                new LiveTrackSectorSegment(1, 0.5d, 1d, LiveTrackSectorHighlights.None)
            ],
            ShowSectorBoundaries: true,
            InternalOpacity: 0.88d,
            IncludeUserMaps: true,
            TrackMap: TestTrackMapDocument());

        var renderModel = TrackMapRenderModel.FromViewModel(viewModel);

        Assert.Equal("generated", renderModel.MapKind);
        Assert.Contains(renderModel.Primitives, primitive => primitive.Kind == "path" && primitive.Fill is not null);
        Assert.True(renderModel.Primitives.Count(primitive => primitive.Kind == "line") >= 4);

        var focus = Assert.Single(renderModel.Markers, marker => marker.IsFocus);
        Assert.Equal("5", focus.Label);

        var opponent = Assert.Single(renderModel.Markers, marker => !marker.IsFocus);
        Assert.Equal(255, opponent.Fill.Red);
        Assert.Equal(218, opponent.Fill.Green);
        Assert.Equal(89, opponent.Fill.Blue);
        Assert.Equal(245, opponent.Fill.Alpha);
    }

    private static LiveTimingRow Row(int carIdx, bool hasTakenGrid)
    {
        return new LiveTimingRow(
            CarIdx: carIdx,
            Quality: LiveModelQuality.Reliable,
            Source: "test",
            IsPlayer: carIdx == 10,
            IsFocus: carIdx == 10,
            IsOverallLeader: false,
            IsClassLeader: false,
            HasTiming: true,
            HasSpatialProgress: true,
            CanUseForRadarPlacement: hasTakenGrid,
            TimingEvidence: LiveSignalEvidence.Reliable("test"),
            SpatialEvidence: LiveSignalEvidence.Reliable("test"),
            RadarPlacementEvidence: LiveSignalEvidence.Reliable("test"),
            GapEvidence: LiveSignalEvidence.Reliable("test"),
            DriverName: $"Driver {carIdx}",
            TeamName: null,
            CarNumber: carIdx.ToString(),
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            OverallPosition: carIdx,
            ClassPosition: carIdx,
            CarClass: 4098,
            LapCompleted: 0,
            LapDistPct: 0.25d,
            ProgressLaps: 0.25d,
            F2TimeSeconds: 25d,
            EstimatedTimeSeconds: 25d,
            LastLapTimeSeconds: null,
            BestLapTimeSeconds: null,
            GapSecondsToClassLeader: null,
            GapLapsToClassLeader: null,
            IntervalSecondsToPreviousClassRow: null,
            IntervalLapsToPreviousClassRow: null,
            DeltaSecondsToFocus: null,
            TrackSurface: 3,
            OnPitRoad: false,
            HasTakenGrid: hasTakenGrid);
    }

    private static LiveScoringRow ScoringRow(int carIdx, int position, bool isFocus)
    {
        return new LiveScoringRow(
            CarIdx: carIdx,
            OverallPositionRaw: position,
            ClassPositionRaw: position,
            OverallPosition: position,
            ClassPosition: position,
            CarClass: 4098,
            DriverName: $"Driver {carIdx}",
            TeamName: null,
            CarNumber: carIdx.ToString(),
            CarClassName: "GT3",
            CarClassColorHex: "#FFDA59",
            IsPlayer: isFocus,
            IsFocus: isFocus,
            IsReferenceClass: true,
            Lap: null,
            LapsComplete: null,
            LastLapTimeSeconds: null,
            BestLapTimeSeconds: null,
            ReasonOut: null,
            HasTakenGrid: false);
    }

    private static HistoricalTelemetrySample Sample(
        int focusCarIdx,
        int playerCarIdx,
        bool onPitRoad,
        int playerTrackSurface)
    {
        return new HistoricalTelemetrySample(
            CapturedAtUtc: DateTimeOffset.UtcNow,
            SessionTime: 10d,
            SessionTick: 1,
            SessionInfoUpdate: 1,
            IsOnTrack: true,
            IsInGarage: false,
            OnPitRoad: onPitRoad,
            PitstopActive: false,
            PlayerCarInPitStall: onPitRoad,
            FuelLevelLiters: 0d,
            FuelLevelPercent: 0d,
            FuelUsePerHourKg: 0d,
            SpeedMetersPerSecond: 0d,
            Lap: 0,
            LapCompleted: 0,
            LapDistPct: 0.20d,
            LapLastLapTimeSeconds: null,
            LapBestLapTimeSeconds: null,
            AirTempC: 20d,
            TrackTempCrewC: 24d,
            TrackWetness: 1,
            WeatherDeclaredWet: false,
            PlayerTireCompound: 0,
            PlayerCarIdx: playerCarIdx,
            FocusCarIdx: focusCarIdx,
            FocusLapCompleted: 0,
            FocusLapDistPct: 0.20d,
            PlayerTrackSurface: playerTrackSurface);
    }

    private static TrackMapDocument TestTrackMapDocument()
    {
        return new TrackMapDocument(
            SchemaVersion: TrackMapDocument.CurrentSchemaVersion,
            GenerationVersion: TrackMapDocument.CurrentGenerationVersion,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Identity: new TrackMapIdentity(
                Key: "unit-test",
                TrackId: 1,
                TrackName: "Unit Test",
                TrackDisplayName: "Unit Test",
                TrackConfigName: "Road",
                TrackLengthKm: 4d,
                TrackVersion: null),
            RacingLine: new TrackMapGeometry(
                [
                    new TrackMapPoint(0d, 0d, 0d),
                    new TrackMapPoint(0.25d, 100d, 0d),
                    new TrackMapPoint(0.5d, 100d, 100d),
                    new TrackMapPoint(0.75d, 0d, 100d)
                ],
                Closed: true),
            PitLane: new TrackMapGeometry(
                [
                    new TrackMapPoint(0.1d, 10d, 10d),
                    new TrackMapPoint(0.2d, 40d, 10d)
                ],
                Closed: false),
            Quality: new TrackMapQuality(
                Confidence: TrackMapConfidence.High,
                CompleteLapCount: 2,
                SelectedPointCount: 4,
                BinCount: 4,
                MissingBinCount: 0,
                MissingBinPercent: 0d,
                ClosureMeters: 1d,
                LengthDeltaPercent: 0d,
                RepeatabilityMedianMeters: 0.5d,
                RepeatabilityP95Meters: 1d,
                PitLaneSampleCount: 2,
                PitLanePassCount: 1,
                PitLaneRepeatabilityP95Meters: 1d,
                Reasons: []),
            Provenance: new TrackMapProvenance("unit-test", null, null, null, null));
    }
}
