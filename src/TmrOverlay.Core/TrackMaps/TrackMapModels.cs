using TmrOverlay.Core.History;

namespace TmrOverlay.Core.TrackMaps;

internal enum TrackMapConfidence
{
    Rejected = 0,
    Placeholder = 1,
    Low = 2,
    Medium = 3,
    High = 4
}

internal sealed record TrackMapIdentity(
    string Key,
    int? TrackId,
    string? TrackName,
    string? TrackDisplayName,
    string? TrackConfigName,
    double? TrackLengthKm,
    string? TrackVersion)
{
    public static TrackMapIdentity From(HistoricalTrackIdentity track)
    {
        var trackName = FirstNonEmpty(track.TrackName, track.TrackDisplayName);
        var displayName = FirstNonEmpty(track.TrackDisplayName, track.TrackName);
        var configName = FirstNonEmpty(track.TrackConfigName, track.TrackDisplayName, track.TrackName);
        var lengthMeters = track.TrackLengthKm is { } km && IsPositiveFinite(km)
            ? (int?)Math.Round(km * 1000d)
            : null;
        var keyParts = new[]
        {
            track.TrackId is { } id ? $"track-{id}" : "track-unknown",
            SessionHistoryPath.Slug(trackName),
            SessionHistoryPath.Slug(configName),
            lengthMeters is { } meters ? $"{meters}m" : "unknown-length",
            SessionHistoryPath.Slug(track.TrackVersion)
        };
        var key = string.Join("-", keyParts.Where(part => !string.IsNullOrWhiteSpace(part) && part != "unknown"));
        return new TrackMapIdentity(
            Key: string.IsNullOrWhiteSpace(key) ? "track-unknown" : key,
            TrackId: track.TrackId,
            TrackName: trackName,
            TrackDisplayName: displayName,
            TrackConfigName: configName,
            TrackLengthKm: track.TrackLengthKm,
            TrackVersion: track.TrackVersion);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static bool IsPositiveFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
    }
}

internal sealed record TrackMapDocument(
    int SchemaVersion,
    int GenerationVersion,
    DateTimeOffset GeneratedAtUtc,
    TrackMapIdentity Identity,
    TrackMapGeometry RacingLine,
    TrackMapGeometry? PitLane,
    TrackMapQuality Quality,
    TrackMapProvenance Provenance)
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentGenerationVersion = 1;

    public bool IsCompleteForRuntime =>
        SchemaVersion == CurrentSchemaVersion
        && GenerationVersion == CurrentGenerationVersion
        && Quality.Confidence >= TrackMapConfidence.Medium
        && RacingLine.Points.Count >= Math.Max(1, Quality.BinCount);
}

internal sealed record TrackMapGeometry(
    IReadOnlyList<TrackMapPoint> Points,
    bool Closed);

internal sealed record TrackMapPoint(
    double LapDistPct,
    double X,
    double Y,
    double? DistanceMeters = null);

internal sealed record TrackMapQuality(
    TrackMapConfidence Confidence,
    int CompleteLapCount,
    int SelectedPointCount,
    int BinCount,
    int MissingBinCount,
    double MissingBinPercent,
    double ClosureMeters,
    double? LengthDeltaPercent,
    double? RepeatabilityMedianMeters,
    double? RepeatabilityP95Meters,
    int PitLaneSampleCount,
    int PitLanePassCount,
    double? PitLaneRepeatabilityP95Meters,
    IReadOnlyList<string> Reasons);

internal sealed record TrackMapProvenance(
    string SourceKind,
    string? SourcePath,
    long? SourceBytes,
    int? SourceRecordCount,
    string? CaptureId);

internal static class TrackMapQualityClassifier
{
    public static TrackMapConfidence Classify(
        int completeLapCount,
        double missingBinPercent,
        double closureMeters,
        double? lengthDeltaPercent,
        double? repeatabilityP95Meters)
    {
        if (completeLapCount <= 0 || missingBinPercent >= 0.10d)
        {
            return TrackMapConfidence.Rejected;
        }

        if (completeLapCount == 1 || repeatabilityP95Meters is null)
        {
            return TrackMapConfidence.Low;
        }

        var lengthDelta = lengthDeltaPercent is { } value ? Math.Abs(value) : 0d;
        if (completeLapCount >= 5
            && missingBinPercent <= 0.005d
            && closureMeters <= 10d
            && lengthDelta <= 0.015d
            && repeatabilityP95Meters <= 3.5d)
        {
            return TrackMapConfidence.High;
        }

        if (completeLapCount >= 2
            && missingBinPercent <= 0.02d
            && closureMeters <= 25d
            && lengthDelta <= 0.03d
            && repeatabilityP95Meters <= 10d)
        {
            return TrackMapConfidence.Medium;
        }

        return TrackMapConfidence.Low;
    }
}
