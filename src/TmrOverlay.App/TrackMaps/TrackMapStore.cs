using System.Text.Json;
using System.Text.Json.Serialization;
using TmrOverlay.App.Storage;
using TmrOverlay.Core.History;
using TmrOverlay.Core.TrackMaps;

namespace TmrOverlay.App.TrackMaps;

internal sealed class TrackMapStore
{
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
