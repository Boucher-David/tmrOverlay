namespace TmrOverlay.Core.Telemetry.EdgeCases;

internal sealed record RawTelemetryWatchSnapshot(IReadOnlyDictionary<string, double> Values)
{
    public static RawTelemetryWatchSnapshot Empty { get; } = new(new Dictionary<string, double>());

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
            "CamCarIdx", "CarLeftRight"
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
            "dpFuelFill", "dpFuelAddKg", "dpFastRepair",
            "PitSvFlags", "PitSvFuel", "PitSvTireCompound"
        ],
        ["pit.state"] =
        [
            "PlayerCarPitSvStatus", "PitRepairLeft", "PitOptRepairLeft",
            "FastRepairUsed", "FastRepairAvailable",
            "TireSetsUsed", "TireSetsAvailable",
            "LeftTireSetsUsed", "RightTireSetsUsed", "FrontTireSetsUsed", "RearTireSetsUsed",
            "LFTiresUsed", "RFTiresUsed", "LRTiresUsed", "RRTiresUsed"
        ],
        ["engine"] =
        [
            "EngineWarnings", "OilTemp", "OilPress", "OilLevel",
            "WaterTemp", "WaterLevel", "FuelPress", "Voltage", "ManifoldPress"
        ],
        ["weather"] = ["TrackWetness", "WeatherDeclaredWet", "Precipitation", "Skies", "FogLevel"],
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
        foreach (var group in Groups)
        {
            if (group.Value.Contains(variableName, StringComparer.OrdinalIgnoreCase))
            {
                return group.Key;
            }
        }

        return "other";
    }
}
