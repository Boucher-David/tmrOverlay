namespace TmrOverlay.Core.Telemetry.Live;

internal enum LiveTimingColumnAlignment
{
    Left,
    Center,
    Right
}

internal sealed record LiveTimingColumnDescriptor(
    string Key,
    string Label,
    LiveTimingColumnAlignment Alignment,
    bool DefaultVisible,
    int DefaultOrder,
    Func<LiveTimingRow, string> FormatValue);

internal static class TimingColumnRegistry
{
    public const string OverallPosition = "overall-position";
    public const string ClassPosition = "class-position";
    public const string CarNumber = "car-number";
    public const string Driver = "driver";
    public const string CarClass = "car-class";
    public const string Gap = "gap";
    public const string Interval = "interval";
    public const string LastLap = "last-lap";
    public const string BestLap = "best-lap";
    public const string Pit = "pit";
    public const string Source = "source";

    public static IReadOnlyList<LiveTimingColumnDescriptor> All { get; } =
    [
        new LiveTimingColumnDescriptor(
            Key: OverallPosition,
            Label: "P",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 10,
            FormatValue: row => FormatPosition(row.OverallPosition)),
        new LiveTimingColumnDescriptor(
            Key: ClassPosition,
            Label: "Class",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 20,
            FormatValue: row => FormatPosition(row.ClassPosition)),
        new LiveTimingColumnDescriptor(
            Key: CarNumber,
            Label: "#",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 30,
            FormatValue: row => row.CarNumber ?? string.Empty),
        new LiveTimingColumnDescriptor(
            Key: Driver,
            Label: "Driver",
            Alignment: LiveTimingColumnAlignment.Left,
            DefaultVisible: true,
            DefaultOrder: 40,
            FormatValue: row => row.DriverName ?? row.TeamName ?? $"Car {row.CarIdx}"),
        new LiveTimingColumnDescriptor(
            Key: CarClass,
            Label: "Class",
            Alignment: LiveTimingColumnAlignment.Left,
            DefaultVisible: true,
            DefaultOrder: 50,
            FormatValue: row => row.CarClassName ?? FormatNullableInt(row.CarClass)),
        new LiveTimingColumnDescriptor(
            Key: Gap,
            Label: "Gap",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 60,
            FormatValue: row => FormatGap(row)),
        new LiveTimingColumnDescriptor(
            Key: Interval,
            Label: "Int",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 70,
            FormatValue: row => row.IsClassLeader || row.ClassPosition == 1
                ? "0.000"
                : FormatInterval(row)),
        new LiveTimingColumnDescriptor(
            Key: LastLap,
            Label: "Last",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 80,
            FormatValue: row => FormatLapTime(row.LastLapTimeSeconds)),
        new LiveTimingColumnDescriptor(
            Key: BestLap,
            Label: "Best",
            Alignment: LiveTimingColumnAlignment.Right,
            DefaultVisible: true,
            DefaultOrder: 90,
            FormatValue: row => FormatLapTime(row.BestLapTimeSeconds)),
        new LiveTimingColumnDescriptor(
            Key: Pit,
            Label: "Pit",
            Alignment: LiveTimingColumnAlignment.Center,
            DefaultVisible: true,
            DefaultOrder: 100,
            FormatValue: row => row.OnPitRoad == true ? "PIT" : string.Empty),
        new LiveTimingColumnDescriptor(
            Key: Source,
            Label: "Src",
            Alignment: LiveTimingColumnAlignment.Left,
            DefaultVisible: false,
            DefaultOrder: 900,
            FormatValue: row => row.Source)
    ];

    public static LiveTimingColumnDescriptor? Find(string key)
    {
        return All.FirstOrDefault(column => string.Equals(column.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatPosition(int? value)
    {
        return value is > 0 ? value.Value.ToString() : string.Empty;
    }

    private static string FormatNullableInt(int? value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static string FormatGap(LiveTimingRow row)
    {
        if (row.IsClassLeader)
        {
            return "Leader";
        }

        if (row.GapLapsToClassLeader is { } laps)
        {
            return FormatLaps(laps);
        }

        if (row.GapSecondsToClassLeader is { } seconds)
        {
            return FormatSeconds(seconds);
        }

        return string.Empty;
    }

    private static string FormatInterval(LiveTimingRow row)
    {
        if (row.IntervalLapsToPreviousClassRow is { } laps)
        {
            return FormatLaps(laps);
        }

        return FormatPositiveSeconds(row.IntervalSecondsToPreviousClassRow);
    }

    private static string FormatSignedSeconds(double? value)
    {
        if (value is null || !IsFinite(value.Value))
        {
            return string.Empty;
        }

        return value.Value switch
        {
            > 0d => $"+{FormatSeconds(value.Value)}",
            < 0d => $"-{FormatSeconds(Math.Abs(value.Value))}",
            _ => "0.000"
        };
    }

    private static string FormatPositiveSeconds(double? value)
    {
        if (value is null || !IsFinite(value.Value))
        {
            return string.Empty;
        }

        return $"+{FormatSeconds(Math.Max(0d, value.Value))}";
    }

    private static string FormatSeconds(double seconds)
    {
        return IsFinite(seconds) ? $"{seconds:0.000}" : string.Empty;
    }

    private static string FormatLaps(double laps)
    {
        if (!IsFinite(laps))
        {
            return string.Empty;
        }

        var positiveLaps = Math.Max(0d, laps);
        if (positiveLaps < 0.9999d)
        {
            return string.Empty;
        }

        return Math.Abs(positiveLaps - Math.Round(positiveLaps)) <= 0.0001d
            ? $"+{positiveLaps:0}L"
            : $"+{positiveLaps:0.000}L";
    }

    private static string FormatLapTime(double? seconds)
    {
        if (seconds is null || !IsFinite(seconds.Value) || seconds.Value <= 0d)
        {
            return string.Empty;
        }

        var minutes = (int)(seconds.Value / 60d);
        var remainder = seconds.Value - minutes * 60d;
        return minutes > 0 ? $"{minutes}:{remainder:00.000}" : $"{remainder:0.000}";
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
