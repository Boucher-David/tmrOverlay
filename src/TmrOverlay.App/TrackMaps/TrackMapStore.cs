using System.Text.Json;
using System.Text.Json.Serialization;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.History;
using TmrOverlay.Core.TrackMaps;

namespace TmrOverlay.App.TrackMaps;

internal sealed class TrackMapStore
{
    private const int MaxDiagnosticsItems = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly string _bundledRoot;

    public TrackMapStore(AppStorageOptions storageOptions, string? bundledRoot = null)
    {
        _storageOptions = storageOptions;
        _bundledRoot = bundledRoot ?? Path.Combine(AppContext.BaseDirectory, "Assets", "TrackMaps");
    }

    public TrackMapDocument? TryReadBest(HistoricalTrackIdentity track, bool includeUserMaps = true)
    {
        var identity = TrackMapIdentity.From(track);
        return CandidatePaths(identity, includeUserMaps)
            .Select(TryRead)
            .Where(document => document is not null)
            .Select(document => document!)
            .OrderByDescending(document => document.Quality.Confidence)
            .ThenBy(document => document.Quality.MissingBinCount)
            .ThenByDescending(document => document.GeneratedAtUtc)
            .FirstOrDefault();
    }

    public bool HasCompleteMap(HistoricalTrackIdentity track)
    {
        return TryReadBest(track)?.IsCompleteForRuntime == true;
    }

    public TrackMapStoreDiagnosticsSnapshot DiagnosticsSnapshot()
    {
        var userMaps = InspectDirectory(_storageOptions.TrackMapRoot, "user").ToArray();
        var bundledMaps = InspectDirectory(_bundledRoot, "bundled").ToArray();
        return new TrackMapStoreDiagnosticsSnapshot(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            UserRoot: _storageOptions.TrackMapRoot,
            BundledRoot: _bundledRoot,
            UserMapCount: userMaps.Count(item => item.Readable),
            BundledMapCount: bundledMaps.Count(item => item.Readable),
            InvalidUserMapCount: userMaps.Count(item => !item.Readable),
            InvalidBundledMapCount: bundledMaps.Count(item => !item.Readable),
            RecentMaps: userMaps
                .Concat(bundledMaps)
                .OrderByDescending(item => item.LastWriteAtUtc)
                .Take(MaxDiagnosticsItems)
                .ToArray());
    }

    public TrackMapSaveResult SaveIfImproved(TrackMapDocument document, bool force = false)
    {
        Directory.CreateDirectory(_storageOptions.TrackMapRoot);
        var path = UserPath(document.Identity);
        var existing = TryRead(path);
        if (!force
            && existing?.IsCompleteForRuntime == true
            && existing.Quality.Confidence > document.Quality.Confidence)
        {
            return new TrackMapSaveResult(false, path, "complete_map_already_exists");
        }

        if (!force
            && existing?.IsCompleteForRuntime == true
            && existing.Quality.Confidence == document.Quality.Confidence
            && existing.Quality.MissingBinCount <= document.Quality.MissingBinCount)
        {
            return new TrackMapSaveResult(false, path, "complete_map_already_exists");
        }

        if (!force
            && existing is not null
            && existing.Quality.Confidence > document.Quality.Confidence)
        {
            return new TrackMapSaveResult(false, path, "existing_map_has_higher_confidence");
        }

        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        return new TrackMapSaveResult(true, path, null);
    }

    private IEnumerable<string> CandidatePaths(TrackMapIdentity identity, bool includeUserMaps)
    {
        if (includeUserMaps)
        {
            yield return UserPath(identity);
        }

        yield return Path.Combine(_bundledRoot, $"{identity.Key}.json");
    }

    private string UserPath(TrackMapIdentity identity)
    {
        return Path.Combine(_storageOptions.TrackMapRoot, $"{identity.Key}.json");
    }

    private IEnumerable<TrackMapDiagnosticsItem> InspectDirectory(string directory, string source)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var info = new FileInfo(path);
            TrackMapDocument? document = null;
            string? error = null;
            try
            {
                document = JsonSerializer.Deserialize<TrackMapDocument>(File.ReadAllText(path), JsonOptions);
                if (document?.SchemaVersion != TrackMapDocument.CurrentSchemaVersion)
                {
                    error = document is null ? "empty_or_unreadable_document" : "unsupported_schema_version";
                    document = null;
                }
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name;
            }

            yield return new TrackMapDiagnosticsItem(
                Source: source,
                FileName: info.Name,
                LastWriteAtUtc: info.LastWriteTimeUtc,
                Bytes: info.Length,
                Readable: document is not null,
                Error: error,
                Key: document?.Identity.Key,
                TrackId: document?.Identity.TrackId,
                TrackDisplayName: document?.Identity.TrackDisplayName,
                TrackConfigName: document?.Identity.TrackConfigName,
                TrackLengthKm: document?.Identity.TrackLengthKm,
                TrackVersion: document?.Identity.TrackVersion,
                GeneratedAtUtc: document?.GeneratedAtUtc,
                Confidence: document?.Quality.Confidence.ToString(),
                IsCompleteForRuntime: document?.IsCompleteForRuntime,
                BinCount: document?.Quality.BinCount,
                MissingBinCount: document?.Quality.MissingBinCount,
                RacingLinePointCount: document?.RacingLine.Points.Count,
                HasPitLane: document?.PitLane is not null);
        }
    }

    private static TrackMapDocument? TryRead(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<TrackMapDocument>(File.ReadAllText(path), JsonOptions);
            return document?.SchemaVersion == TrackMapDocument.CurrentSchemaVersion
                ? document
                : null;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record TrackMapSaveResult(
    bool Saved,
    string Path,
    string? Reason);

internal sealed record TrackMapStoreDiagnosticsSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string UserRoot,
    string BundledRoot,
    int UserMapCount,
    int BundledMapCount,
    int InvalidUserMapCount,
    int InvalidBundledMapCount,
    IReadOnlyList<TrackMapDiagnosticsItem> RecentMaps);

internal sealed record TrackMapDiagnosticsItem(
    string Source,
    string FileName,
    DateTimeOffset LastWriteAtUtc,
    long Bytes,
    bool Readable,
    string? Error,
    string? Key,
    int? TrackId,
    string? TrackDisplayName,
    string? TrackConfigName,
    double? TrackLengthKm,
    string? TrackVersion,
    DateTimeOffset? GeneratedAtUtc,
    string? Confidence,
    bool? IsCompleteForRuntime,
    int? BinCount,
    int? MissingBinCount,
    int? RacingLinePointCount,
    bool? HasPitLane);
