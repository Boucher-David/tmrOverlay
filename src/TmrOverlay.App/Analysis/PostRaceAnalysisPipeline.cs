using Microsoft.Extensions.Logging;
using TmrOverlay.App.Events;
using TmrOverlay.Core.Analysis;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.Analysis;

internal sealed class PostRaceAnalysisPipeline
{
    private readonly PostRaceAnalysisStore _store;
    private readonly AppEventRecorder _events;
    private readonly ILogger<PostRaceAnalysisPipeline> _logger;

    public PostRaceAnalysisPipeline(
        PostRaceAnalysisStore store,
        AppEventRecorder events,
        ILogger<PostRaceAnalysisPipeline> logger)
    {
        _store = store;
        _events = events;
        _logger = logger;
    }

    public async Task<PostRaceAnalysis?> SaveFromSummaryAsync(
        HistoricalSessionSummary summary,
        HistoricalSessionGroup? sessionGroup,
        CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await _store.SaveFromSummaryAsync(summary, sessionGroup, cancellationToken).ConfigureAwait(false);
            if (analysis is null)
            {
                return null;
            }

            _events.Record("post_race_analysis_saved", new Dictionary<string, string?>
            {
                ["collectionId"] = summary.CollectionId,
                ["sourceId"] = summary.SourceCaptureId,
                ["sessionGroupId"] = sessionGroup?.GroupId,
                ["sessionSegmentCount"] = sessionGroup?.Segments.Count.ToString(),
                ["analysisId"] = analysis.Id
            });
            _logger.LogInformation(
                "Saved post-race analysis {AnalysisId} for {SourceId} in session group {SessionGroupId}.",
                analysis.Id,
                summary.SourceCaptureId,
                sessionGroup?.GroupId ?? "none");
            return analysis;
        }
        catch (Exception exception)
        {
            _events.Record("post_race_analysis_failed", new Dictionary<string, string?>
            {
                ["collectionId"] = summary.CollectionId,
                ["sourceId"] = summary.SourceCaptureId,
                ["error"] = exception.GetType().Name
            }, severity: "warning");
            _logger.LogWarning(
                exception,
                "Failed to save post-race analysis for {SourceId}.",
                summary.SourceCaptureId);
            return null;
        }
    }
}
