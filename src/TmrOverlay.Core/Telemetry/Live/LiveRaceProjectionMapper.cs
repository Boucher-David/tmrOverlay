namespace TmrOverlay.Core.Telemetry.Live;

internal static class LiveRaceProjectionMapper
{
    public static LiveRaceProgressModel ApplyToRaceProgress(
        LiveRaceProgressModel progress,
        LiveRaceProjectionModel projection)
    {
        if (!projection.HasData)
        {
            return progress;
        }

        return progress with
        {
            StrategyLapTimeSeconds = projection.TeamPaceSeconds ?? progress.StrategyLapTimeSeconds,
            StrategyLapTimeSource = projection.TeamPaceSeconds is not null
                ? projection.TeamPaceSource
                : progress.StrategyLapTimeSource,
            RacePaceSeconds = projection.OverallLeaderPaceSeconds ?? progress.RacePaceSeconds,
            RacePaceSource = projection.OverallLeaderPaceSeconds is not null
                ? projection.OverallLeaderPaceSource
                : progress.RacePaceSource,
            RaceLapsRemaining = projection.EstimatedTeamLapsRemaining ?? progress.RaceLapsRemaining,
            RaceLapsRemainingSource = projection.EstimatedTeamLapsRemaining is not null
                ? projection.EstimatedTeamLapsRemainingSource
                : progress.RaceLapsRemainingSource
        };
    }
}
