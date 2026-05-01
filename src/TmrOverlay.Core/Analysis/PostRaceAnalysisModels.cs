using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Analysis;

internal sealed class PostRaceAnalysis
{
    public int AnalysisVersion { get; init; } = 1;

    public string Id { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public string SourceId { get; init; } = string.Empty;

    public string? AppRunId { get; init; }

    public string? CollectionId { get; init; }

    public string? SessionGroupId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public HistoricalComboIdentity? Combo { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Title)
        ? SourceId
        : $"{FinishedAtUtc:yyyy-MM-dd} {Title}";

    public string Body => string.Join(Environment.NewLine, Lines);
}
