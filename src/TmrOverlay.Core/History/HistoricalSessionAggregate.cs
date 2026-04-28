namespace TmrOverlay.Core.History;

internal sealed class HistoricalSessionAggregate
{
    public int AggregateVersion { get; set; } = 1;

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
