using TmrOverlay.App.TrackMaps;
using TmrOverlay.Core.History;
using TmrOverlay.Core.TrackMaps;
using Xunit;

namespace TmrOverlay.App.Tests.TrackMaps;

public sealed class IbtTrackMapBuilderTests
{
    private const double EarthRadiusMeters = 6_371_000d;

    [Fact]
    public void BuildFromSamples_GeneratesCompleteHighConfidenceMapFromRepeatableLaps()
    {
        var samples = BuildCircularSamples(lapCount: 5, samplesPerLap: 720, radiusMeters: 240d);
        var builder = new IbtTrackMapBuilder();

        var result = builder.BuildFromSamples(
            samples,
            TestTrack(),
            new TrackMapProvenance("unit-test", null, null, samples.Count, "capture-test"),
            TestSectors());

        Assert.NotNull(result.Document);
        var document = result.Document!;
        Assert.True(document.IsCompleteForRuntime);
        Assert.Equal(TrackMapConfidence.High, document.Quality.Confidence);
        Assert.Equal(5, document.Quality.CompleteLapCount);
        Assert.Equal(0, document.Quality.MissingBinCount);
        Assert.True(document.RacingLine.Points.Count >= 400);
        Assert.Collection(
            document.Sectors!,
            sector =>
            {
                Assert.Equal(0, sector.SectorNum);
                Assert.Equal(0d, sector.StartPct);
                Assert.Equal(0.5d, sector.EndPct);
            },
            sector =>
            {
                Assert.Equal(1, sector.SectorNum);
                Assert.Equal(0.5d, sector.StartPct);
                Assert.Equal(0.75d, sector.EndPct);
            },
            sector =>
            {
                Assert.Equal(2, sector.SectorNum);
                Assert.Equal(0.75d, sector.StartPct);
                Assert.Equal(1d, sector.EndPct);
            });
        Assert.Empty(result.RejectionReasons);
    }

    [Fact]
    public void BuildFromSamples_RejectsWhenNoCompleteLapExists()
    {
        var samples = BuildCircularSamples(lapCount: 1, samplesPerLap: 90, radiusMeters: 240d)
            .Where(sample => sample.LapDistPct is > 0.20d and < 0.55d)
            .ToArray();
        var builder = new IbtTrackMapBuilder();

        var result = builder.BuildFromSamples(
            samples,
            TestTrack(),
            new TrackMapProvenance("unit-test", null, null, samples.Length, "capture-test"));

        Assert.Null(result.Document);
        Assert.Contains("no_complete_positive_lap", result.RejectionReasons);
    }

    [Fact]
    public void ChooseLapNumber_FallsBackToStartedLapWhenCompletedLapIsInvalid()
    {
        Assert.Equal(3, IbtTrackMapBuilder.ChooseLapNumber(3, 4));
        Assert.Equal(4, IbtTrackMapBuilder.ChooseLapNumber(-1, 4));
        Assert.Equal(-1, IbtTrackMapBuilder.ChooseLapNumber(-1, -1));
    }

    private static IReadOnlyList<IbtTrackMapSample> BuildCircularSamples(
        int lapCount,
        int samplesPerLap,
        double radiusMeters)
    {
        const double baseLatitude = 35d;
        const double baseLongitude = -80d;
        var samples = new List<IbtTrackMapSample>(lapCount * samplesPerLap);
        for (var lap = 0; lap < lapCount; lap++)
        {
            for (var index = 0; index < samplesPerLap; index++)
            {
                var lapDistPct = index / (double)samplesPerLap;
                var angle = lapDistPct * Math.PI * 2d;
                var x = Math.Cos(angle) * radiusMeters;
                var y = Math.Sin(angle) * radiusMeters;
                var latitude = baseLatitude + RadiansToDegrees(y / EarthRadiusMeters);
                var longitude = baseLongitude + RadiansToDegrees(x / (EarthRadiusMeters * Math.Cos(DegreesToRadians(baseLatitude))));
                samples.Add(new IbtTrackMapSample(
                    Sequence: lap * samplesPerLap + index,
                    LapNumber: lap + 1,
                    LapDistPct: lapDistPct,
                    LapDistMeters: lapDistPct * 2d * Math.PI * radiusMeters,
                    Latitude: latitude,
                    Longitude: longitude,
                    SpeedMetersPerSecond: 35d,
                    OnPitRoad: false));
            }
        }

        return samples;
    }

    private static HistoricalTrackIdentity TestTrack()
    {
        return new HistoricalTrackIdentity
        {
            TrackId = 42,
            TrackName = "synthetic_circle",
            TrackDisplayName = "Synthetic Circle",
            TrackConfigName = "Full",
            TrackLengthKm = 2d * Math.PI * 240d / 1000d,
            TrackVersion = "2026.05"
        };
    }

    private static IReadOnlyList<HistoricalTrackSector> TestSectors()
    {
        return
        [
            new HistoricalTrackSector { SectorNum = 0, SectorStartPct = 0d },
            new HistoricalTrackSector { SectorNum = 1, SectorStartPct = 0.5d },
            new HistoricalTrackSector { SectorNum = 2, SectorStartPct = 0.75d }
        ];
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private static double RadiansToDegrees(double radians)
    {
        return radians * 180d / Math.PI;
    }
}
