using System.IO.Compression;
using System.Text.Json;
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
            car.StintSource
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
        AddFileIfExists(archive, Path.Combine(captureDirectory, "telemetry-schema.json"), "latest-capture/telemetry-schema.json");
        AddFileIfExists(archive, Path.Combine(captureDirectory, "latest-session.yaml"), "latest-capture/latest-session.yaml");
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
