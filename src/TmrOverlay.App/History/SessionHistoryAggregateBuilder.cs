using TmrOverlay.Core.History;

namespace TmrOverlay.App.History;

internal static class SessionHistoryAggregateBuilder
{
    public static HistoricalSessionAggregate Rebuild(
        IEnumerable<HistoricalSessionSummary> summaries,
        DateTimeOffset updatedAtUtc)
    {
        var aggregate = new HistoricalSessionAggregate();
        foreach (var summary in summaries.OrderBy(summary => summary.StartedAtUtc))
        {
            AddSummary(aggregate, summary, updatedAtUtc);
        }

        return aggregate;
    }

    public static HistoricalCarRadarCalibrationAggregate RebuildCarRadarCalibration(
        IEnumerable<HistoricalSessionSummary> summaries,
        DateTimeOffset updatedAtUtc)
    {
        var aggregate = new HistoricalCarRadarCalibrationAggregate();
        foreach (var summary in summaries.OrderBy(summary => summary.StartedAtUtc))
        {
            AddRadarCalibrationSummary(aggregate, summary, updatedAtUtc);
        }

        return aggregate;
    }

    public static void AddSummary(
        HistoricalSessionAggregate aggregate,
        HistoricalSessionSummary summary,
        DateTimeOffset updatedAtUtc)
    {
        aggregate.AggregateVersion = HistoricalDataVersions.AggregateVersion;
        aggregate.Combo = summary.Combo;
        aggregate.Car = summary.Car;
        aggregate.Track = summary.Track;
        aggregate.Session = summary.Session;
        aggregate.UpdatedAtUtc = updatedAtUtc;
        aggregate.SessionCount++;

        if (!summary.Quality.ContributesToBaseline)
        {
            return;
        }

        aggregate.BaselineSessionCount++;
        aggregate.FuelPerLapLiters.Add(summary.Metrics.FuelPerLapLiters);
        aggregate.FuelPerHourLiters.Add(summary.Metrics.FuelPerHourLiters);
        aggregate.AverageLapSeconds.Add(summary.Metrics.AverageLapSeconds);
        aggregate.MedianLapSeconds.Add(summary.Metrics.MedianLapSeconds);
        aggregate.PitRoadEntryCount.Add(summary.Metrics.PitRoadEntryCount);
        aggregate.PitServiceCount.Add(summary.Metrics.PitServiceCount);
        aggregate.AverageStintLaps.Add(summary.Metrics.AverageStintLaps);
        aggregate.AverageStintSeconds.Add(summary.Metrics.AverageStintSeconds);
        aggregate.AverageStintFuelPerLapLiters.Add(summary.Metrics.AverageStintFuelPerLapLiters);
        foreach (var stint in summary.Stints)
        {
            if (stint.DistanceLaps <= 0d)
            {
                continue;
            }

            if (IsLocalDriverStint(stint.DriverRole))
            {
                aggregate.LocalDriverStintLaps.Add(stint.DistanceLaps);
            }
            else if (IsTeammateDriverStint(stint.DriverRole))
            {
                aggregate.TeammateDriverStintLaps.Add(stint.DistanceLaps);
            }
        }

        aggregate.AveragePitLaneSeconds.Add(summary.Metrics.AveragePitLaneSeconds);
        aggregate.AveragePitStallSeconds.Add(summary.Metrics.AveragePitStallSeconds);
        aggregate.AveragePitServiceSeconds.Add(summary.Metrics.AveragePitServiceSeconds);
        aggregate.ObservedFuelFillRateLitersPerSecond.Add(summary.Metrics.ObservedFuelFillRateLitersPerSecond);
        aggregate.AverageTireChangePitServiceSeconds.Add(summary.Metrics.AverageTireChangePitServiceSeconds);
        aggregate.AverageNoTirePitServiceSeconds.Add(summary.Metrics.AverageNoTirePitServiceSeconds);
    }

    public static bool AddRadarCalibrationSummary(
        HistoricalCarRadarCalibrationAggregate aggregate,
        HistoricalSessionSummary summary,
        DateTimeOffset updatedAtUtc)
    {
        if (summary.RadarCalibration is null
            || !HasRadarCalibrationData(summary.RadarCalibration)
            || HistoricalRadarCalibrationTrust.TryGetTrustedBodyLengthMeters(
                aggregate.RadarCalibration?.EstimatedBodyLengthMeters,
                out _))
        {
            return false;
        }

        aggregate.AggregateVersion = HistoricalDataVersions.CarRadarCalibrationAggregateVersion;
        aggregate.CarKey = summary.Combo.CarKey;
        aggregate.Car = summary.Car;
        aggregate.UpdatedAtUtc = updatedAtUtc;
        aggregate.SessionCount++;
        aggregate.RadarCalibration ??= new HistoricalRadarCalibrationAggregate();
        aggregate.RadarCalibration.Add(summary.RadarCalibration);
        return true;
    }

    private static bool HasRadarCalibrationData(HistoricalRadarCalibrationSummary summary)
    {
        return summary.SideOverlapWindowSeconds.SampleCount > 0
            || summary.EstimatedBodyLengthMeters.SampleCount > 0;
    }

    private static bool IsLocalDriverStint(string driverRole)
    {
        return driverRole.Contains("local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTeammateDriverStint(string driverRole)
    {
        return driverRole.Contains("team", StringComparison.OrdinalIgnoreCase)
            || driverRole.Contains("teammate", StringComparison.OrdinalIgnoreCase);
    }
}
