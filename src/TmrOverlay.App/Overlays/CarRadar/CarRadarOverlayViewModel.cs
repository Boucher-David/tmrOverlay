using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.CarRadar;

internal sealed record CarRadarOverlayViewModel(
    string Title,
    string Status,
    string Source,
    bool IsAvailable,
    bool HasCarLeft,
    bool HasCarRight,
    IReadOnlyList<LiveSpatialCar> Cars,
    LiveMulticlassApproach? StrongestMulticlassApproach,
    bool ShowMulticlassWarning,
    bool PreviewVisible,
    LiveSpatialModel Spatial)
{
    private const double RadarRangeSeconds = 2d;
    private const double FocusedCarLengthMeters = 4.746d;
    private const double RadarRangeMeters = FocusedCarLengthMeters * 6d;
    private const double MulticlassWarningRangeSeconds = 5d;

    public bool HasCurrentSignal =>
        PreviewVisible
        || HasCarLeft
        || HasCarRight
        || Cars.Count > 0
        || StrongestMulticlassApproach is not null;

    public static CarRadarOverlayViewModel Empty { get; } = new(
        Title: "Car Radar",
        Status: "waiting",
        Source: "source: waiting",
        IsAvailable: false,
        HasCarLeft: false,
        HasCarRight: false,
        Cars: [],
        StrongestMulticlassApproach: null,
        ShowMulticlassWarning: true,
        PreviewVisible: false,
        Spatial: LiveSpatialModel.Empty);

    public static CarRadarOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        bool previewVisible,
        bool showMulticlassWarning)
    {
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        var localContext = LiveLocalStrategyContext.ForRequirement(
            snapshot,
            now,
            OverlayContextRequirement.LocalPlayerInCar);
        var localContextAvailable = previewVisible || localContext.IsAvailable;
        var canRender = availability.IsAvailable && localContextAvailable;
        var spatial = canRender ? snapshot.Models.Spatial : LiveSpatialModel.Empty;
        var hasSpatialData = spatial.HasData;
        var cars = spatial.Cars
            .Where(IsInRadarRange)
            .GroupBy(car => car.CarIdx)
            .Select(group => group.MinBy(car => Math.Abs(RangeRatio(car)))!)
            .ToArray();
        var multiclass = showMulticlassWarning
            ? spatial.MulticlassApproaches
                .Where(IsInMulticlassWarningRange)
                .MaxBy(approach => approach.Urgency)
            : null;
        var status = !availability.IsAvailable && !previewVisible
            ? availability.StatusText
            : !localContextAvailable
                ? localContext.StatusText
            : !hasSpatialData
                ? "waiting for radar"
            : spatial.HasCarLeft && spatial.HasCarRight
                ? "cars both sides"
                : spatial.HasCarLeft
                    ? "car left"
                    : spatial.HasCarRight
                        ? "car right"
                        : multiclass is not null
                            ? "class traffic"
                            : "clear";

        return new CarRadarOverlayViewModel(
            Title: "Car Radar",
            Status: status,
            Source: hasSpatialData ? "source: spatial telemetry" : "source: waiting",
            IsAvailable: canRender || previewVisible,
            HasCarLeft: spatial.HasCarLeft,
            HasCarRight: spatial.HasCarRight,
            Cars: cars,
            StrongestMulticlassApproach: multiclass,
            ShowMulticlassWarning: showMulticlassWarning,
            PreviewVisible: previewVisible,
            Spatial: spatial);
    }

    public static bool IsInRadarRange(LiveSpatialCar car)
    {
        if (ReliableRelativeMeters(car) is { } meters)
        {
            return Math.Abs(meters) <= RadarRangeMeters;
        }

        return false;
    }

    public static double? ReliableRelativeMeters(LiveSpatialCar car)
    {
        return car.RelativeMeters is { } meters && !double.IsNaN(meters) && !double.IsInfinity(meters)
            ? meters
            : null;
    }

    private static double RangeRatio(LiveSpatialCar car)
    {
        if (ReliableRelativeMeters(car) is { } meters)
        {
            return Math.Clamp(meters / RadarRangeMeters, -1d, 1d);
        }

        return Math.Sign(car.RelativeLaps);
    }

    private static bool IsInMulticlassWarningRange(LiveMulticlassApproach approach)
    {
        if (approach.RelativeSeconds is not { } seconds || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return false;
        }

        return seconds < -RadarRangeSeconds && seconds >= -MulticlassWarningRangeSeconds;
    }
}
