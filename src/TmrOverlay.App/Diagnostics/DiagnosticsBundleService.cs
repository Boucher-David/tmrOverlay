using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TmrOverlay.Core.AppInfo;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Diagnostics;

internal sealed class DiagnosticsBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppStorageOptions _storageOptions;
    private readonly TelemetryCaptureState _captureState;
    private readonly ILiveTelemetrySource _liveTelemetrySource;
    private readonly ILogger<DiagnosticsBundleService> _logger;

    public DiagnosticsBundleService(
        AppStorageOptions storageOptions,
        TelemetryCaptureState captureState,
        ILiveTelemetrySource liveTelemetrySource,
        ILogger<DiagnosticsBundleService> logger)
    {
        _storageOptions = storageOptions;
        _captureState = captureState;
        _liveTelemetrySource = liveTelemetrySource;
        _logger = logger;
    }

    public string CreateBundle()
    {
        Directory.CreateDirectory(_storageOptions.DiagnosticsRoot);
        var bundlePath = Path.Combine(
            _storageOptions.DiagnosticsRoot,
            $"tmroverlay-diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");

        using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);
        AddTextEntry(archive, "metadata/app-version.json", JsonSerializer.Serialize(AppVersionInfo.Current, JsonOptions));
        AddTextEntry(archive, "metadata/storage.json", JsonSerializer.Serialize(_storageOptions, JsonOptions));
        AddFileIfExists(archive, _storageOptions.RuntimeStatePath, "runtime/runtime-state.json");
        AddTextEntry(archive, "runtime/telemetry-capture-state.json", JsonSerializer.Serialize(_captureState.Snapshot(), JsonOptions));
        AddLiveTelemetrySnapshot(archive);
        AddFileIfExists(archive, Path.Combine(_storageOptions.SettingsRoot, "settings.json"), "settings/settings.json");
        AddRecentFiles(archive, _storageOptions.LogsRoot, "*.log", "logs", maxFiles: 10);
        AddRecentFiles(archive, _storageOptions.EventsRoot, "*.jsonl", "events", maxFiles: 10);
        AddRecentFiles(archive, Path.Combine(_storageOptions.LogsRoot, "performance"), "*.jsonl", "performance", maxFiles: 10);
        AddRecentFiles(archive, Path.Combine(_storageOptions.LogsRoot, "edge-cases"), "*.json", "edge-cases", maxFiles: 5);
        AddLatestCaptureMetadata(archive);

        _logger.LogInformation("Created diagnostics bundle {DiagnosticsBundlePath}.", bundlePath);
        return bundlePath;
    }

    private void AddLiveTelemetrySnapshot(ZipArchive archive)
    {
        try
        {
            var snapshot = _liveTelemetrySource.Snapshot();
            AddTextEntry(
                archive,
                "live/live-telemetry-snapshot.json",
                JsonSerializer.Serialize(snapshot, JsonOptions));
            AddTextEntry(
                archive,
                "live/overlay-state-summary.json",
                JsonSerializer.Serialize(BuildOverlayStateSummary(snapshot), JsonOptions));
            AddTextEntry(
                archive,
                "live/telemetry-availability.json",
                JsonSerializer.Serialize(snapshot.TelemetryAvailability, JsonOptions));
            AddTextEntry(
                archive,
                "live/weather-snapshot.json",
                JsonSerializer.Serialize(BuildWeatherSnapshot(snapshot), JsonOptions));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to add live telemetry snapshot to diagnostics bundle.");
            AddTextEntry(
                archive,
                "live/live-telemetry-snapshot-error.json",
                JsonSerializer.Serialize(new { error = exception.Message }, JsonOptions));
        }
    }

    private static object BuildOverlayStateSummary(LiveTelemetrySnapshot snapshot)
    {
        return new
        {
            snapshot.LiveMode,
            snapshot.IsLocalDriverInCar,
            snapshot.IsSpectating,
            snapshot.IsSpectatingFocusedCar,
            telemetryAvailability = snapshot.TelemetryAvailability,
            teamCar = BuildCarSummary(snapshot.TeamCar),
            focusCar = BuildCarSummary(snapshot.FocusCar),
            observedSessionCars = snapshot.ObservedCars
                .Take(32)
                .Select(BuildObservedCarSummary)
                .ToArray(),
            local = new
            {
                isOnTrack = snapshot.LatestSample?.IsOnTrack,
                isInGarage = snapshot.LatestSample?.IsInGarage,
                speedMetersPerSecond = snapshot.LatestSample?.SpeedMetersPerSecond,
                lapDistPct = snapshot.LatestSample?.LapDistPct,
                carLeftRight = snapshot.LatestSample?.CarLeftRight
            },
            fuel = new
            {
                snapshot.Fuel.HasValidFuel,
                snapshot.Fuel.Source,
                snapshot.Fuel.Confidence,
                snapshot.Fuel.FuelLevelLiters,
                snapshot.Fuel.FuelPerLapLiters,
                snapshot.Fuel.LapTimeSeconds,
                snapshot.Fuel.LapTimeSource
            },
            weather = BuildWeatherSnapshot(snapshot),
            radar = new
            {
                snapshot.Proximity.HasData,
                snapshot.Proximity.SideStatus,
                nearbyCarCount = snapshot.Proximity.NearbyCars.Count,
                strongestMulticlassApproach = snapshot.Proximity.StrongestMulticlassApproach
            },
            gap = new
            {
                snapshot.LeaderGap.HasData,
                snapshot.LeaderGap.ReferenceOverallPosition,
                snapshot.LeaderGap.ReferenceClassPosition,
                snapshot.LeaderGap.OverallLeaderCarIdx,
                snapshot.LeaderGap.ClassLeaderCarIdx,
                snapshot.LeaderGap.OverallLeaderGap,
                snapshot.LeaderGap.ClassLeaderGap,
                classCarCount = snapshot.LeaderGap.ClassCars.Count
            }
        };
    }

    private static object BuildWeatherSnapshot(LiveTelemetrySnapshot snapshot)
    {
        var weather = snapshot.Weather;
        return new
        {
            weather.HasData,
            weather.CapturedAtUtc,
            weather.SessionTime,
            live = new
            {
                weather.TrackTempC,
                weather.TrackTempCrewC,
                weather.AirTempC,
                weather.TrackWetness,
                weather.SurfaceMoistureClass,
                weather.WeatherDeclaredWet,
                weather.DeclaredWetSurfaceMismatch,
                weather.Skies,
                weather.SkiesLabel,
                weather.WindVelMetersPerSecond,
                weather.WindDirRadians,
                weather.RelativeHumidityPercent,
                weather.FogLevelPercent,
                weather.PrecipitationPercent,
                weather.AirDensityKgPerCubicMeter,
                weather.AirPressurePa,
                weather.SolarAltitudeRadians,
                weather.SolarAzimuthRadians
            },
            sessionInfo = new
            {
                weather.SessionTrackWeatherType,
                weather.SessionTrackSkies,
                weather.SessionTrackSurfaceTempC,
                weather.SessionTrackSurfaceTempCrewC,
                weather.SessionTrackAirTempC,
                weather.SessionTrackWindVelMetersPerSecond,
                weather.SessionTrackWindDirRadians,
                weather.SessionTrackRelativeHumidityPercent,
                weather.SessionTrackFogLevelPercent,
                weather.SessionTrackPrecipitationPercent,
                weather.SessionTrackRubberState
            },
            notes = new[]
            {
                "This is a scalar telemetry snapshot, not the iRacing on-screen radar image.",
                "Use declaredWetSurfaceMismatch and the raw scalar values to compare iRacing's wet declaration with average surface wetness and precipitation."
            }
        };
    }

    private static object BuildCarSummary(LiveCarContextSnapshot car)
    {
        return new
        {
            car.HasData,
            car.CarIdx,
            car.Role,
            car.IsTeamCar,
            car.OverallPosition,
            car.ClassPosition,
            car.CarClass,
            car.OnPitRoad,
            car.ProgressLaps,
            car.CurrentStintLaps,
            car.CurrentStintSeconds,
            car.ObservedPitStopCount,
            car.StintSource,
            car.CompletedStintCount,
            car.AverageCompletedStintLaps
        };
    }

    private static object BuildObservedCarSummary(LiveObservedCar car)
    {
        return new
        {
            car.CarIdx,
            car.OverallPosition,
            car.ClassPosition,
            car.CarClass,
            car.OnPitRoad,
            car.ProgressLaps,
            car.CurrentStintLaps,
            car.CurrentStintSeconds,
            car.ObservedPitStopCount,
            car.StintSource,
            car.CompletedStintCount,
            car.AverageCompletedStintLaps
        };
    }

    private void AddLatestCaptureMetadata(ZipArchive archive)
    {
        var snapshot = _captureState.Snapshot();
        var captureDirectory = snapshot.CurrentCaptureDirectory ?? snapshot.LastCaptureDirectory;
        if (string.IsNullOrWhiteSpace(captureDirectory) || !Directory.Exists(captureDirectory))
        {
            return;
        }

        AddFileIfExists(archive, Path.Combine(captureDirectory, "capture-manifest.json"), "latest-capture/capture-manifest.json");
        var schemaPath = Path.Combine(captureDirectory, "telemetry-schema.json");
        AddFileIfExists(archive, schemaPath, "latest-capture/telemetry-schema.json");
        AddTelemetrySchemaSummary(archive, schemaPath);
        AddFileIfExists(archive, Path.Combine(captureDirectory, "latest-session.yaml"), "latest-capture/latest-session.yaml");
    }

    private static void AddTelemetrySchemaSummary(ZipArchive archive, string schemaPath)
    {
        if (!File.Exists(schemaPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var fields = document.RootElement
            .EnumerateArray()
            .Select(ToSchemaFieldSummary)
            .ToArray();
        var weatherFields = fields
            .Where(field => ContainsCandidateTerm(field.SearchText, WeatherTerms))
            .ToArray();
        var radarLikeFields = fields
            .Where(field => ContainsCandidateTerm(field.SearchText, RadarTerms))
            .ToArray();
        var output = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            totalFieldCount = fields.Length,
            fieldTypeCounts = fields
                .GroupBy(field => field.TypeName ?? "unknown")
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count()),
            arrayFieldCount = fields.Count(field => field.Count > 1),
            broadCategories = BuildSchemaCategoryCounts(fields),
            weatherRelatedTelemetryFields = weatherFields,
            radarLikeTelemetryFields = radarLikeFields,
            hasExplicitRadarTelemetryField = radarLikeFields.Any(field => ContainsCandidateTerm(field.SearchText, ["radar"])),
            notes = new[]
            {
                "This scans all telemetry variable names, units, and descriptions into broad debugging categories.",
                "If no explicit radar field appears here, the iRacing screen radar is not exposed in the raw telemetry schema captured by this build."
            }
        };
        AddTextEntry(
            archive,
            "latest-capture/telemetry-schema-summary.json",
            JsonSerializer.Serialize(output, JsonOptions));
    }

    private static Dictionary<string, int> BuildSchemaCategoryCounts(IReadOnlyList<SchemaFieldSummary> fields)
    {
        var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            foreach (var category in SchemaCategories)
            {
                if (ContainsCandidateTerm(field.SearchText, category.Value))
                {
                    categories[category.Key] = categories.GetValueOrDefault(category.Key) + 1;
                }
            }
        }

        return categories
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static SchemaFieldSummary ToSchemaFieldSummary(JsonElement field)
    {
        return new SchemaFieldSummary(
            ReadJsonString(field, "name"),
            ReadJsonString(field, "typeName"),
            ReadJsonInt(field, "count") ?? 1,
            ReadJsonString(field, "unit"),
            ReadJsonString(field, "description"));
    }

    private static string? ReadJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? ReadJsonInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool ContainsCandidateTerm(string? value, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] WeatherTerms =
    [
        "weather",
        "rain",
        "precip",
        "wet",
        "moisture",
        "skies",
        "sky",
        "cloud",
        "fog",
        "humidity",
        "wind",
        "airtemp",
        "tracktemp",
        "solar",
        "radar"
    ];

    private static readonly string[] RadarTerms =
    [
        "radar",
        "rain",
        "precip",
        "wet",
        "moisture",
        "cloud"
    ];

    private static readonly Dictionary<string, string[]> SchemaCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weather"] = WeatherTerms,
        ["session"] = ["session", "flag", "pace", "caution"],
        ["timing"] = ["lap", "time", "position", "distance", "estimated", "f2"],
        ["cars"] = ["caridx", "player", "driver", "team", "class"],
        ["pit"] = ["pit", "service", "repair", "tire", "fuel"],
        ["controls"] = ["throttle", "brake", "clutch", "steering", "gear", "input"],
        ["vehicle"] = ["rpm", "speed", "accel", "yaw", "pitch", "roll", "velocity"],
        ["tires"] = ["tire", "tyre", "wheel"],
        ["damage"] = ["damage", "repair"],
        ["overlay-useful"] = ["carleft", "tracksurface", "lapdist", "classposition", "f2time", "esttime"]
    };

    private sealed record SchemaFieldSummary(
        string? Name,
        string? TypeName,
        int Count,
        string? Unit,
        string? Description)
    {
        [JsonIgnore]
        public string SearchText => $"{Name} {Unit} {Description}";
    }

    private static void AddRecentFiles(
        ZipArchive archive,
        string directory,
        string searchPattern,
        string entryDirectory,
        int maxFiles)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(directory, searchPattern)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(maxFiles);

        foreach (var file in files)
        {
            AddFileIfExists(archive, file.FullName, $"{entryDirectory}/{file.Name}");
        }
    }

    private static void AddFileIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Fastest);
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }
}
