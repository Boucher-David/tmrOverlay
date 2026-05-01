using TmrOverlay.Core.History;

namespace TmrOverlay.Core.Analysis;

internal static class PostRaceAnalysisBuilder
{
    public static PostRaceAnalysis Build(
        HistoricalSessionSummary summary,
        HistoricalSessionGroup? sessionGroup = null)
    {
        var track = FirstNonEmpty(summary.Track.TrackDisplayName, summary.Track.TrackName, summary.Combo.TrackKey);
        var session = FirstNonEmpty(summary.Session.SessionType, summary.Session.EventType, summary.Session.SessionName, summary.Combo.SessionKey);
        var car = FirstNonEmpty(summary.Car.CarScreenName, summary.Car.CarScreenNameShort, summary.Car.CarPath, summary.Combo.CarKey);
        var title = $"{track} - {session}";
        var subtitle = $"{car} | {summary.Quality.Confidence} confidence";
        var analysisId = sessionGroup?.GroupId ?? $"{summary.FinishedAtUtc:yyyyMMdd-HHmmss}-{summary.SourceCaptureId}";
        var finishedAtUtc = sessionGroup?.Segments.Count > 0
            ? sessionGroup.Segments.Max(segment => segment.FinishedAtUtc)
            : summary.FinishedAtUtc;
        var lines = new List<string>
        {
            title,
            subtitle,
            string.Empty,
            $"Duration: {FormatDuration(summary.Metrics.CaptureDurationSeconds)} captured, {summary.Metrics.CompletedValidLaps} completed valid laps, {FormatNumber(summary.Metrics.ValidDistanceLaps)} valid distance laps.",
            $"Telemetry mode: {BuildTelemetryMode(summary.Metrics.TelemetryAvailability)}.",
            $"Focus coverage: {BuildFocusCoverage(summary.Metrics.TelemetryAvailability)}.",
            $"Radar/gap inputs: {summary.Metrics.TelemetryAvailability.ClassTimingFrameCount} class-timing frames, {summary.Metrics.TelemetryAvailability.NearbyTimingFrameCount} nearby-timing frames, {summary.Metrics.TelemetryAvailability.CarLeftRightAvailableFrameCount} CarLeftRight frames ({summary.Metrics.TelemetryAvailability.CarLeftRightActiveFrameCount} active).",
            $"Weather: {BuildWeatherSummary(summary.Conditions)}.",
            $"Fuel model: {FormatFuelPerLap(summary.Metrics.FuelPerLapLiters)} average, {FormatFuel(summary.Car.DriverCarFuelMaxLiters)} tank, {FormatLapsPerTank(summary)}."
        };

        AddSessionGroupLines(lines, summary, sessionGroup);

        if (summary.Metrics.StintCount > 0)
        {
            lines.Add($"Stints: {summary.Metrics.StintCount} recorded, avg {FormatNumber(summary.Metrics.AverageStintLaps)} laps / {FormatDuration(summary.Metrics.AverageStintSeconds)}.");
        }

        if (summary.Metrics.PitRoadEntryCount > 0 || summary.Metrics.PitServiceCount > 0)
        {
            lines.Add($"Pit service: {summary.Metrics.PitServiceCount} service stops, {summary.Metrics.PitRoadEntryCount} pit-road entries, avg lane {FormatDuration(summary.Metrics.AveragePitLaneSeconds)}, service {FormatDuration(summary.Metrics.AveragePitServiceSeconds)}.");
        }
        else
        {
            lines.Add("Pit service: no completed pit service detected in this summary.");
        }

        if (summary.Metrics.ObservedFuelFillRateLitersPerSecond is { } fillRate)
        {
            lines.Add($"Observed fill rate: {fillRate:0.00} L/s.");
        }

        lines.Add(string.Empty);
        lines.Add("Strategy note:");
        lines.Add(BuildStrategyNote(summary));
        lines.Add(string.Empty);
        lines.Add($"Quality: {summary.Quality.Confidence}; {FormatReasons(summary.Quality.Reasons)}.");

        return new PostRaceAnalysis
        {
            Id = analysisId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FinishedAtUtc = finishedAtUtc,
            SourceId = sessionGroup?.GroupId ?? summary.SourceCaptureId,
            AppRunId = summary.AppRunId,
            CollectionId = summary.CollectionId,
            SessionGroupId = sessionGroup?.GroupId,
            Title = title,
            Subtitle = subtitle,
            Combo = summary.Combo,
            Lines = lines
        };
    }

    public static PostRaceAnalysis BuiltInFourHourSample()
    {
        return new PostRaceAnalysis
        {
            Id = "sample-nurburgring-4h",
            CreatedAtUtc = new DateTimeOffset(2026, 4, 26, 0, 0, 0, TimeSpan.Zero),
            FinishedAtUtc = new DateTimeOffset(2026, 4, 26, 0, 0, 0, TimeSpan.Zero),
            SourceId = "sample-nurburgring-4h",
            Title = "Nurburgring Combined Short B - 4h race",
            Subtitle = "Mercedes-AMG GT3 2020 | Team race",
            Combo = HistoricalComboIdentity.From(new HistoricalSessionContext
            {
                Car = new HistoricalCarIdentity
                {
                    CarId = 156,
                    CarPath = "mercedesamgevogt3",
                    CarScreenName = "Mercedes-AMG GT3 2020"
                },
                Track = new HistoricalTrackIdentity
                {
                    TrackId = 262,
                    TrackName = "nurburgring combinedshortb",
                    TrackDisplayName = "Gesamtstrecke VLN"
                },
                Session = new HistoricalSessionIdentity
                {
                    SessionType = "Race"
                },
                Conditions = new HistoricalSessionInfoConditions()
            }),
            Lines =
            [
                "Nurburgring Combined Short B - 4h race",
                "Mercedes-AMG GT3 2020 | Team race",
                string.Empty,
                "Result: P7 overall / P6 class, 30 completed laps.",
                "Fuel model: 13.36 L/lap average, 106 L tank, about 7.9 laps/tank.",
                "Executed stints: local 7-lap opening, teammate 8-lap middle stint.",
                "Pit service: 3 stops, about 64-67s pit lane each.",
                "Observed fill rate: about 2.68 L/s.",
                string.Empty,
                "Strategy note:",
                "An 8-lap rhythm avoided a likely extra stop versus a 7-lap rhythm, but needed about 0.1 L/lap saving versus the baseline average."
            ]
        };
    }

    private static string BuildStrategyNote(HistoricalSessionSummary summary)
    {
        var fuelPerLap = summary.Metrics.FuelPerLapLiters;
        var tank = summary.Car.DriverCarFuelMaxLiters;
        if (IsNonRaceSession(summary))
        {
            return summary.Metrics.TelemetryAvailability.HasFocusTiming
                ? "This was a non-race timing session: useful for overlay validation and pace context, but it should not be read as an executed race strategy."
                : "This was a non-race session, so treat any fuel or stint numbers as practice context rather than race strategy.";
        }

        if (fuelPerLap is null && summary.Metrics.TelemetryAvailability.HasFocusTiming)
        {
            return summary.Metrics.TelemetryAvailability.IsSpectatedTimingOnly
                ? "This was a spectated timing session: useful for gap/radar validation, but local fuel scalars were idle so no stint-rhythm recommendation was written."
                : "Timing data was available, but fuel data was not strong enough for a stint-rhythm recommendation yet.";
        }

        if (fuelPerLap is > 0d && tank is > 0d)
        {
            var tankLaps = tank.Value / fuelPerLap.Value;
            var conservative = Math.Max(1, (int)Math.Floor(tankLaps));
            var stretch = Math.Max(conservative, (int)Math.Ceiling(tankLaps));
            return stretch > conservative
                ? $"{stretch}-lap stints are plausible only with fuel saving versus the average; compare against a conservative {conservative}-lap rhythm before the next race."
                : $"{conservative}-lap stints match the current fuel model; review reserve before extending the rhythm.";
        }

        return "Fuel data was not strong enough for a stint-rhythm recommendation yet.";
    }

    private static void AddSessionGroupLines(
        List<string> lines,
        HistoricalSessionSummary summary,
        HistoricalSessionGroup? sessionGroup)
    {
        if (sessionGroup is null || sessionGroup.Segments.Count == 0)
        {
            return;
        }

        var segments = sessionGroup.Segments
            .OrderBy(segment => segment.StartedAtUtc)
            .ThenBy(segment => segment.SourceCaptureId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var latestSegment = segments.LastOrDefault(
            segment => string.Equals(segment.SourceCaptureId, summary.SourceCaptureId, StringComparison.OrdinalIgnoreCase))
            ?? segments[^1];
        var totalDurationSeconds = segments.Sum(segment => segment.CaptureDurationSeconds);
        var gapCount = segments.Count(segment => segment.GapFromPreviousSegmentSeconds is > 0d);
        var largestGapSeconds = segments
            .Select(segment => segment.GapFromPreviousSegmentSeconds ?? 0d)
            .DefaultIfEmpty(0d)
            .Max();

        var gapText = gapCount > 0
            ? $", {gapCount} reconnect gap(s), largest {FormatDuration(largestGapSeconds)}"
            : ", no reconnect gaps detected";
        lines.Add($"Session stitching: {segments.Length} capture segment(s), {FormatDuration(totalDurationSeconds)} total captured{gapText}.");
        lines.Add($"Latest segment: {latestSegment.SourceCaptureId}, ended by {FirstNonEmpty(latestSegment.EndedReason, "unknown")}.");

        if (segments.Any(segment => segment.PreviousAppRunUnclean))
        {
            var unclean = segments.First(segment => segment.PreviousAppRunUnclean);
            var heartbeat = unclean.PreviousAppLastHeartbeatAtUtc is { } lastHeartbeat
                ? $" last heartbeat {lastHeartbeat:yyyy-MM-dd HH:mm:ss} UTC"
                : " no previous heartbeat recorded";
            lines.Add($"Crash/reload context: previous app run was unclean before segment {unclean.SourceCaptureId};{heartbeat}.");
        }
    }

    private static bool IsNonRaceSession(HistoricalSessionSummary summary)
    {
        var sessionType = summary.Session.SessionType;
        var eventType = summary.Session.EventType;
        if (string.IsNullOrWhiteSpace(sessionType) && string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        return !ContainsRace(sessionType) && !ContainsRace(eventType);
    }

    private static bool ContainsRace(string? value)
    {
        return value?.Contains("race", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string BuildTelemetryMode(TelemetryAvailabilitySnapshot availability)
    {
        if (availability.SampleFrameCount == 0)
        {
            return "no telemetry frames";
        }

        if (availability.IsSpectatedTimingOnly)
        {
            return $"spectated timing, {availability.FocusCarFrameCount} focus frames across {availability.UniqueFocusCarCount} focus cars, {availability.FocusCarChangeCount} focus changes, no local driving, {BuildIdleFuelScalarSummary(availability)}";
        }

        if (availability.HasLocalDriving)
        {
            return $"local driving, {availability.LocalDrivingFrameCount} local frames, {availability.LocalFuelScalarFrameCount} fuel-scalar frames, {availability.FocusCarChangeCount} focus changes";
        }

        if (availability.HasFocusTiming)
        {
            return $"focus timing without local driving, {availability.FocusTimingFrameCount} focus-timing frames, {availability.ClassTimingFrameCount} class-timing frames";
        }

        if (availability.LocalScalarsIdle)
        {
            return "local scalars idle, no focus timing detected";
        }

        return $"partial telemetry, {availability.LocalDrivingFrameCount} local frames, {availability.FocusTimingFrameCount} focus-timing frames";
    }

    private static string BuildIdleFuelScalarSummary(TelemetryAvailabilitySnapshot availability)
    {
        if (availability.LocalFuelScalarWithoutDrivingFrameCount > 0)
        {
            return $"{availability.LocalFuelScalarWithoutDrivingFrameCount} idle fuel-scalar frames";
        }

        return "local fuel scalars unavailable";
    }

    private static string BuildFocusCoverage(TelemetryAvailabilitySnapshot availability)
    {
        if (availability.SampleFrameCount == 0)
        {
            return "no telemetry frames";
        }

        if (availability.FocusCarFrameCount == 0)
        {
            return $"no focused car on {availability.MissingFocusCarFrameCount} frames";
        }

        var currentFocus = availability.CurrentFocusCarIdx is { } carIdx
            ? $"current focus #{carIdx}"
            : "current focus unknown";
        return $"{currentFocus}, {availability.FocusSegments.Count} focus segments, {availability.FocusCarChangeCount} focus changes, {availability.NonTeamFocusFrameCount} non-team focus frames";
    }

    private static string BuildWeatherSummary(HistoricalConditions conditions)
    {
        var declaredWet = conditions.WeatherDeclaredWet switch
        {
            true => "declared wet",
            false => "not declared wet",
            _ => "wet declaration unknown"
        };
        var surface = conditions.TrackWetness is { } wetness
            ? $"{TrackWetnessLabel(wetness)} surface (wetness {wetness})"
            : "surface wetness unknown";
        var precipitation = conditions.PrecipitationPercent ?? conditions.TrackPrecipitationPercent;
        var precipitationLabel = precipitation is { } percent
            ? $"{percent:0.#}% precipitation"
            : "precipitation unknown";
        var skies = FirstNonEmpty(SkiesLabel(conditions.Skies), conditions.TrackSkies, "skies unknown");
        var temperature = conditions.TrackTempCrewC is { } trackTemp
            ? $", track {trackTemp:0.#} C"
            : string.Empty;

        return $"{declaredWet}, {surface}, {precipitationLabel}, {skies}{temperature}";
    }

    private static string TrackWetnessLabel(int trackWetness)
    {
        return trackWetness switch
        {
            <= 1 => "dry",
            <= 3 => "damp",
            _ => "wet"
        };
    }

    private static string? SkiesLabel(int? skies)
    {
        return skies switch
        {
            0 => "clear",
            1 => "partly cloudy",
            2 => "mostly cloudy",
            3 => "overcast",
            null => null,
            _ => $"skies code {skies.Value}"
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "unknown";
    }

    private static string FormatDuration(double? seconds)
    {
        if (seconds is null || double.IsNaN(seconds.Value) || double.IsInfinity(seconds.Value))
        {
            return "--";
        }

        if (seconds.Value >= 3600d)
        {
            return $"{seconds.Value / 3600d:0.0}h";
        }

        return seconds.Value >= 60d
            ? $"{seconds.Value / 60d:0.0}m"
            : $"{seconds.Value:0}s";
    }

    private static string FormatFuel(double? liters)
    {
        return liters is null || double.IsNaN(liters.Value) || double.IsInfinity(liters.Value)
            ? "--"
            : $"{liters.Value:0.0} L";
    }

    private static string FormatFuelPerLap(double? liters)
    {
        return liters is null || double.IsNaN(liters.Value) || double.IsInfinity(liters.Value)
            ? "--"
            : $"{liters.Value:0.00} L/lap";
    }

    private static string FormatLapsPerTank(HistoricalSessionSummary summary)
    {
        return summary.Car.DriverCarFuelMaxLiters is { } tank
            && summary.Metrics.FuelPerLapLiters is { } fuelPerLap
            && fuelPerLap > 0d
            ? $"about {tank / fuelPerLap:0.0} laps/tank"
            : "laps/tank unavailable";
    }

    private static string FormatNumber(double? value)
    {
        return value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
            ? "--"
            : $"{value.Value:0.0}";
    }

    private static string FormatReasons(IReadOnlyList<string> reasons)
    {
        return reasons.Count == 0 ? "no quality warnings" : string.Join(", ", reasons);
    }
}
