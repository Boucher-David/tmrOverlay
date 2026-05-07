namespace TmrOverlay.Core.Telemetry.EdgeCases;

internal sealed record RawTelemetryWatchSnapshot(IReadOnlyDictionary<string, double> Values)
{
    public static RawTelemetryWatchSnapshot Empty { get; } = new(new Dictionary<string, double>());

    public IReadOnlyDictionary<string, string> VariableGroups { get; init; } = new Dictionary<string, string>();

    public double? Get(string name)
    {
        return Values.TryGetValue(name, out var value) && IsFinite(value)
            ? value
            : null;
    }

    public IReadOnlyDictionary<string, double> Pick(IEnumerable<string> names)
    {
        var selected = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (Get(name) is { } value)
            {
                selected[name] = Math.Round(value, 6);
            }
        }

        return selected;
    }

    public string GroupFor(string name)
    {
        return VariableGroups.TryGetValue(name, out var group)
            ? group
            : RawTelemetryWatchVariables.GroupFor(name);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record RawTelemetryWatchedVariable(
    string Name,
    string Group,
    string TypeName,
    int Count,
    string? Unit,
    string? Description);

internal sealed record RawTelemetrySchemaSnapshot(
    IReadOnlyList<RawTelemetryWatchedVariable> WatchedVariables,
    IReadOnlyList<string> MissingWatchedVariables)
{
    public static RawTelemetrySchemaSnapshot Empty { get; } = new([], RawTelemetryWatchVariables.Names);
}

internal static class RawTelemetryWatchVariables
{
    public static readonly IReadOnlyDictionary<string, string[]> Groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["session.state"] =
        [
            "SessionNum", "SessionState", "SessionFlags",
            "SessionTimeRemain", "SessionLapsRemainEx",
            "IsOnTrack", "IsOnTrackCar", "IsInGarage", "IsGarageVisible", "OnPitRoad", "PitstopActive", "PlayerCarInPitStall",
            "PlayerTrackSurface", "PlayerTrackSurfaceMaterial",
            "CamCarIdx", "CamGroupNumber", "CamCameraNumber", "CamCameraState",
            "CarLeftRight"
        ],
        ["car.motion"] =
        [
            "Speed", "RPM", "Gear",
            "Lap", "LapCompleted", "LapDistPct",
            "PlayerCarPosition", "PlayerCarClassPosition"
        ],
        ["driver.inputs"] =
        [
            "Throttle", "Brake", "Clutch",
            "ThrottleRaw", "BrakeRaw", "ClutchRaw",
            "SteeringWheelAngle", "BrakeABSactive"
        ],
        ["driver.controls"] =
        [
            "dcBrakeBias", "dcABS", "dcTractionControl", "dcTractionControl2",
            "dcTractionControlToggle", "dcABSToggle",
            "dcFrontARB", "dcRearARB", "dcARBFront", "dcARBRear",
            "dcAntiRollFront", "dcAntiRollRear",
            "dcFrontWing", "dcRearWing", "dcWing",
            "dpBrakeBias"
        ],
        ["incidents"] =
        [
            "PlayerCarDriverIncidentCount", "PlayerCarTeamIncidentCount", "PlayerCarMyIncidentCount"
        ],
        ["fuel"] = ["FuelLevel", "FuelLevelPct", "FuelUsePerHour"],
        ["tires.wear"] =
        [
            "LFwearL", "LFwearM", "LFwearR",
            "RFwearL", "RFwearM", "RFwearR",
            "LRwearL", "LRwearM", "LRwearR",
            "RRwearL", "RRwearM", "RRwearR"
        ],
        ["tires.temperature"] =
        [
            "LFtempCL", "LFtempCM", "LFtempCR",
            "RFtempCL", "RFtempCM", "RFtempCR",
            "LRtempCL", "LRtempCM", "LRtempCR",
            "RRtempCL", "RRtempCM", "RRtempCR"
        ],
        ["tires.pressure"] =
        [
            "LFcoldPressure", "RFcoldPressure", "LRcoldPressure", "RRcoldPressure",
            "dpLFTireColdPress", "dpRFTireColdPress", "dpLRTireColdPress", "dpRRTireColdPress"
        ],
        ["tires.odometer"] = ["LFodometer", "RFodometer", "LRodometer", "RRodometer"],
        ["suspension"] =
        [
            "LFshockDefl", "RFshockDefl", "LRshockDefl", "RRshockDefl",
            "LFshockVel", "RFshockVel", "LRshockVel", "RRshockVel"
        ],
        ["brakes"] =
        [
            "LFbrakeLinePress", "RFbrakeLinePress", "LRbrakeLinePress", "RRbrakeLinePress",
            "LFbrakeTemp", "RFbrakeTemp", "LRbrakeTemp", "RRbrakeTemp"
        ],
        ["wheels"] = ["LFwheelSpeed", "RFwheelSpeed", "LRwheelSpeed", "RRwheelSpeed"],
        ["pit.commands"] =
        [
            "dpLFTireChange", "dpRFTireChange", "dpLRTireChange", "dpRRTireChange",
            "dpFuelFill", "dpFuelAddKg", "dpFastRepair", "dpWindshieldTearoff",
            "dpLFTireColdPress", "dpRFTireColdPress", "dpLRTireColdPress", "dpRRTireColdPress",
            "dpFuelAutoFillEnabled", "dpFuelAutoFillActive",
            "PitSvFlags", "PitSvFuel", "PitSvTireCompound",
            "PitSvLFP", "PitSvRFP", "PitSvLRP", "PitSvRRP"
        ],
        ["pit.state"] =
        [
            "PlayerCarPitSvStatus", "PitRepairLeft", "PitOptRepairLeft",
            "FastRepairUsed", "FastRepairAvailable",
            "TireSetsUsed", "TireSetsAvailable",
            "LeftTireSetsUsed", "RightTireSetsUsed", "FrontTireSetsUsed", "RearTireSetsUsed",
            "LFTiresUsed", "RFTiresUsed", "LRTiresUsed", "RRTiresUsed"
        ],
        ["reset-tow"] = ["EnterExitReset", "PlayerCarTowTime"],
        ["engine"] =
        [
            "EngineWarnings", "OilTemp", "OilPress", "OilLevel",
            "WaterTemp", "WaterLevel", "FuelPress", "Voltage", "ManifoldPress"
        ],
        ["weather"] =
        [
            "AirTemp", "TrackTemp", "TrackTempCrew",
            "TrackWetness", "WeatherDeclaredWet",
            "Precipitation", "Skies", "FogLevel",
            "WindVel", "WindDir", "RelativeHumidity", "AirPressure",
            "SolarAltitude", "SolarAzimuth"
        ],
        ["replay"] = ["IsReplayPlaying", "IsDiskLoggingEnabled", "IsDiskLoggingActive"],
        ["system"] =
        [
            "FrameRate", "CpuUsageFG", "GpuUsage",
            "ChanLatency", "ChanAvgLatency", "ChanQuality", "ChanPartnerQuality", "ChanClockSkew",
            "MemPageFaultSec", "MemSoftPageFaultSec"
        ],
        ["driver-change"] = ["DCDriversSoFar", "DCLapStatus"]
    };

    public static readonly IReadOnlyList<string> Names = Groups
        .SelectMany(group => group.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static string GroupFor(string variableName)
    {
        return GroupFor(variableName, description: null);
    }

    public static string GroupFor(string variableName, string? description)
    {
        foreach (var group in Groups)
        {
            if (group.Value.Contains(variableName, StringComparer.OrdinalIgnoreCase))
            {
                return group.Key;
            }
        }

        if (IsForecastCandidate(variableName)
            || (!string.IsNullOrWhiteSpace(description) && IsForecastCandidate(description)))
        {
            return "weather.forecast";
        }

        if (IsDriverInputCandidate(variableName)
            || (!string.IsNullOrWhiteSpace(description) && IsDriverInputCandidate(description)))
        {
            return "driver.inputs";
        }

        if (IsDriverControlCandidate(variableName)
            || (!string.IsNullOrWhiteSpace(description) && IsDriverControlCandidate(description)))
        {
            return "driver.controls";
        }

        return "other";
    }

    public static bool ShouldWatch(string variableName, string? description = null)
    {
        return Names.Contains(variableName, StringComparer.OrdinalIgnoreCase)
            || IsForecastCandidate(variableName)
            || (!string.IsNullOrWhiteSpace(description) && IsForecastCandidate(description))
            || IsDriverInputCandidate(variableName)
            || (!string.IsNullOrWhiteSpace(description) && IsDriverInputCandidate(description))
            || IsDriverControlCandidate(variableName)
            || (!string.IsNullOrWhiteSpace(description) && IsDriverControlCandidate(description));
    }

    private static bool IsForecastCandidate(string value)
    {
        return value.Contains("forecast", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDriverControlCandidate(string value)
    {
        var normalized = value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains("arb", StringComparison.Ordinal)
            || normalized.Contains("antiroll", StringComparison.Ordinal)
            || normalized.Contains("wing", StringComparison.Ordinal);
    }

    private static bool IsDriverInputCandidate(string value)
    {
        var normalized = value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains("clutch", StringComparison.Ordinal);
    }
}
