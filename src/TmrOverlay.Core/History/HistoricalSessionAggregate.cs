namespace TmrOverlay.Core.History;

internal sealed class HistoricalSessionAggregate
{
    public int AggregateVersion { get; set; } = HistoricalDataVersions.AggregateVersion;

    public HistoricalComboIdentity? Combo { get; set; }

    public HistoricalCarIdentity? Car { get; set; }

    public HistoricalTrackIdentity? Track { get; set; }

    public HistoricalSessionIdentity? Session { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public int SessionCount { get; set; }

    public int BaselineSessionCount { get; set; }

    public RunningHistoricalMetric FuelPerLapLiters { get; set; } = new();

    public RunningHistoricalMetric FuelPerHourLiters { get; set; } = new();

    public RunningHistoricalMetric AverageLapSeconds { get; set; } = new();

    public RunningHistoricalMetric MedianLapSeconds { get; set; } = new();

    public RunningHistoricalMetric PitRoadEntryCount { get; set; } = new();

    public RunningHistoricalMetric PitServiceCount { get; set; } = new();

    public RunningHistoricalMetric AverageStintLaps { get; set; } = new();

    public RunningHistoricalMetric AverageStintSeconds { get; set; } = new();

    public RunningHistoricalMetric AverageStintFuelPerLapLiters { get; set; } = new();

    public RunningHistoricalMetric LocalDriverStintLaps { get; set; } = new();

    public RunningHistoricalMetric TeammateDriverStintLaps { get; set; } = new();

    public RunningHistoricalMetric AveragePitLaneSeconds { get; set; } = new();

    public RunningHistoricalMetric AveragePitStallSeconds { get; set; } = new();

    public RunningHistoricalMetric AveragePitServiceSeconds { get; set; } = new();

    public RunningHistoricalMetric ObservedFuelFillRateLitersPerSecond { get; set; } = new();

    public RunningHistoricalMetric AverageTireChangePitServiceSeconds { get; set; } = new();

    public RunningHistoricalMetric AverageNoTirePitServiceSeconds { get; set; } = new();
}

internal sealed class HistoricalCarRadarCalibrationAggregate
{
    public int AggregateVersion { get; set; } = HistoricalDataVersions.CarRadarCalibrationAggregateVersion;

    public string? CarKey { get; set; }

    public HistoricalCarIdentity? Car { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public int SessionCount { get; set; }

    public HistoricalRadarCalibrationAggregate RadarCalibration { get; set; } = new();
}

internal sealed class HistoricalRadarCalibrationAggregate
{
    public int SourceSessionCount { get; set; }

    public HistoricalRadarCalibrationMetric SideOverlapWindowSeconds { get; set; } = new();

    public HistoricalRadarCalibrationMetric EstimatedBodyLengthMeters { get; set; } = new();

    public string[] ConfidenceFlags { get; set; } = [];

    public void Add(HistoricalRadarCalibrationSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var hadData = summary.SideOverlapWindowSeconds.SampleCount > 0
            || summary.EstimatedBodyLengthMeters.SampleCount > 0;
        if (!hadData)
        {
            return;
        }

        SourceSessionCount++;
        SideOverlapWindowSeconds.Add(summary.SideOverlapWindowSeconds);
        EstimatedBodyLengthMeters.Add(summary.EstimatedBodyLengthMeters);
        ConfidenceFlags = ConfidenceFlags
            .Concat(summary.ConfidenceFlags)
            .Where(flag => !string.IsNullOrWhiteSpace(flag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

internal static class HistoricalRadarCalibrationTrust
{
    private const int MinimumEstimatedBodyLengthSamples = 3;
    private const double MinimumEstimatedBodyLengthMeters = 3.5d;
    private const double MaximumEstimatedBodyLengthMeters = 6.5d;
    private const double MaximumEstimatedBodyLengthSpreadMeters = 1.5d;

    public static bool TryGetTrustedBodyLengthMeters(
        HistoricalRadarCalibrationMetric? metric,
        out double bodyLengthMeters)
    {
        bodyLengthMeters = 0d;
        if (metric is null
            || metric.SampleCount < MinimumEstimatedBodyLengthSamples
            || metric.Mean is not { } mean
            || metric.Minimum is not { } minimum
            || metric.Maximum is not { } maximum)
        {
            return false;
        }

        if (double.IsNaN(mean)
            || double.IsInfinity(mean)
            || double.IsNaN(minimum)
            || double.IsInfinity(minimum)
            || double.IsNaN(maximum)
            || double.IsInfinity(maximum))
        {
            return false;
        }

        if (mean < MinimumEstimatedBodyLengthMeters
            || mean > MaximumEstimatedBodyLengthMeters
            || minimum < MinimumEstimatedBodyLengthMeters
            || maximum > MaximumEstimatedBodyLengthMeters
            || maximum - minimum > MaximumEstimatedBodyLengthSpreadMeters)
        {
            return false;
        }

        bodyLengthMeters = mean;
        return true;
    }
}

internal sealed class RunningHistoricalMetric
{
    public int SampleCount { get; set; }

    public double? Mean { get; set; }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }

    public void Add(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return;
        }

        if (SampleCount == 0)
        {
            SampleCount = 1;
            Mean = value;
            Minimum = value;
            Maximum = value;
            return;
        }

        Mean = ((Mean ?? 0d) * SampleCount + value.Value) / (SampleCount + 1);
        Minimum = Math.Min(Minimum ?? value.Value, value.Value);
        Maximum = Math.Max(Maximum ?? value.Value, value.Value);
        SampleCount++;
    }
}
