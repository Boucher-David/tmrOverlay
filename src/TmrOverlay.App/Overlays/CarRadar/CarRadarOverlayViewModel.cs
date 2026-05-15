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
    private const double MulticlassWarningRangeSeconds = 5d;
    public const double FocusedCarLengthMeters = CarRadarCalibrationProfile.DefaultBodyLengthMeters;
    public const double PhysicalRadarRangeMeters = FocusedCarLengthMeters * 6d;
    public const double TimingAwareVisibilitySeconds = 2d;
    public const double MaximumTimingAwareRangeMeters = FocusedCarLengthMeters * 15d;

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
        bool showMulticlassWarning,
        CarRadarCalibrationProfile? calibrationProfile = null)
    {
        snapshot = snapshot with { Models = snapshot.CompleteModels() };
        var calibration = calibrationProfile ?? CarRadarCalibrationProfile.Default;
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
            .Where(car => IsInRadarRange(car, calibration))
            .GroupBy(car => car.CarIdx)
            .Select(group => group.MinBy(car => Math.Abs(RangeRatio(car, calibration)))!)
            .ToArray();
        LiveMulticlassApproach? multiclass = showMulticlassWarning
            ? spatial.MulticlassApproaches
                .Where(IsInMulticlassWarningRange)
                .OrderBy(approach => approach.RelativeSeconds is { } seconds ? Math.Abs(seconds) : double.MaxValue)
                .ThenByDescending(approach => approach.Urgency)
                .FirstOrDefault()
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
                            ? "faster class"
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
        return IsInRadarRange(car, CarRadarCalibrationProfile.Default);
    }

    public static bool IsInRadarRange(LiveSpatialCar car, CarRadarCalibrationProfile calibration)
    {
        if (ReliableRelativeMeters(car) is { } meters)
        {
            return Math.Abs(meters) <= VisualRadarRangeMeters(car, calibration);
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
        return RangeRatio(car, CarRadarCalibrationProfile.Default);
    }

    private static double RangeRatio(LiveSpatialCar car, CarRadarCalibrationProfile calibration)
    {
        if (ReliableRelativeMeters(car) is { } meters)
        {
            return Math.Clamp(meters / VisualRadarRangeMeters(car, calibration), -1d, 1d);
        }

        return Math.Sign(car.RelativeLaps);
    }

    public static double VisualRadarRangeMeters(LiveSpatialCar car)
    {
        return VisualRadarRangeMeters(car, CarRadarCalibrationProfile.Default);
    }

    public static double VisualRadarRangeMeters(LiveSpatialCar car, CarRadarCalibrationProfile calibration)
    {
        var bodyLengthMeters = Math.Max(0.001d, calibration.BodyLengthMeters);
        var range = bodyLengthMeters * 6d;
        if (ReliableRelativeMeters(car) is not { } meters
            || car.RelativeSeconds is not { } seconds
            || double.IsNaN(seconds)
            || double.IsInfinity(seconds))
        {
            return range;
        }

        var absMeters = Math.Abs(meters);
        var absSeconds = Math.Abs(seconds);
        if (absMeters <= range || absSeconds <= 0.05d)
        {
            return range;
        }

        var inferredMetersPerSecond = absMeters / absSeconds;
        if (double.IsNaN(inferredMetersPerSecond)
            || double.IsInfinity(inferredMetersPerSecond)
            || inferredMetersPerSecond <= 0d)
        {
            return range;
        }

        var timingAwareRange = inferredMetersPerSecond * TimingAwareVisibilitySeconds;
        return Math.Clamp(
            Math.Max(range, timingAwareRange),
            range,
            bodyLengthMeters * 15d);
    }

    public static bool IsInMulticlassWarningRange(LiveMulticlassApproach approach)
    {
        if (approach.RelativeSeconds is not { } seconds || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return false;
        }

        return seconds < -RadarRangeSeconds && seconds >= -MulticlassWarningRangeSeconds;
    }
}
