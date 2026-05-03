using System.Text.Json;
using TmrOverlay.App.History;
using TmrOverlay.Core.Analysis;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.Analysis;

internal sealed class PostRaceAnalysisStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly SessionHistoryOptions _historyOptions;
    private readonly PostRaceAnalysisOptions _analysisOptions;

    public PostRaceAnalysisStore(
        SessionHistoryOptions historyOptions,
        PostRaceAnalysisOptions analysisOptions)
    {
        _historyOptions = historyOptions;
        _analysisOptions = analysisOptions;
    }

    public async Task<PostRaceAnalysis?> SaveFromSummaryAsync(
        HistoricalSessionSummary summary,
        CancellationToken cancellationToken)
    {
        if (!_historyOptions.Enabled || !_analysisOptions.Enabled)
        {
            return null;
        }

        var analysis = PostRaceAnalysisBuilder.Build(summary);
        Directory.CreateDirectory(AnalysisDirectory);
        var path = Path.Combine(AnalysisDirectory, $"{SessionHistoryPath.Slug(analysis.Id)}.json");
        await File.WriteAllTextAsync(
                path,
                JsonSerializer.Serialize(analysis, JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);
        return analysis;
    }

    public IReadOnlyList<PostRaceAnalysis> LoadRecent(int maximumCount = 12)
    {
        var analyses = new List<PostRaceAnalysis>();
        if (Directory.Exists(AnalysisDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(AnalysisDirectory, "*.json"))
            {
                var analysis = ReadAnalysis(path);
                if (analysis is not null)
                {
                    analyses.Add(analysis);
                }
            }
        }

        analyses.Sort(static (left, right) => right.FinishedAtUtc.CompareTo(left.FinishedAtUtc));
        analyses.Add(PostRaceAnalysisBuilder.BuiltInFourHourSample());
        return analyses
            .DistinctBy(analysis => analysis.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maximumCount))
            .ToArray();
    }

    private string AnalysisDirectory => Path.Combine(_historyOptions.ResolvedHistoryRoot, "analysis");

    private static PostRaceAnalysis? ReadAnalysis(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<PostRaceAnalysis>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

}
