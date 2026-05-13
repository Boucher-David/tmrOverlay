using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;

namespace TmrOverlay.App.Overlays.Content;

internal enum OverlayContentColumnAlignment
{
    Left,
    Center,
    Right
}

internal sealed record OverlayContentDefinition(
    string OverlayId,
    IReadOnlyList<OverlayContentColumnDefinition> Columns,
    int BrowserWidthPadding,
    int BrowserMinimumHeight,
    int NativeMinimumTableHeight,
    string FallbackColumnId,
    IReadOnlyList<OverlayContentBlockDefinition>? Blocks = null);

internal sealed record OverlayContentBlockDefinition(
    string Id,
    string Label,
    string Description,
    string EnabledOptionKey,
    bool DefaultEnabled,
    string? CountOptionKey = null,
    string? CountLabel = null,
    int DefaultCount = 0,
    int MinimumCount = 0,
    int MaximumCount = 0);

internal sealed record OverlayContentColumnDefinition(
    string Id,
    string Label,
    string DataKey,
    bool DefaultEnabled,
    int DefaultOrder,
    int DefaultWidth,
    int MinimumWidth,
    int MaximumWidth,
    string? WidthOptionKey = null,
    OverlayContentColumnAlignment Alignment = OverlayContentColumnAlignment.Right,
    string? SettingsLabel = null)
{
    public string EnabledKey(string overlayId)
    {
        return $"{overlayId}.content.{Id}.enabled";
    }

    public string OrderKey(string overlayId)
    {
        return $"{overlayId}.content.{Id}.order";
    }

    public string WidthKey(string overlayId)
    {
        return WidthOptionKey ?? $"{overlayId}.content.{Id}.width";
    }
}

internal sealed record OverlayContentColumnState(
    string Id,
    string Label,
    string SettingsLabel,
    string DataKey,
    bool Enabled,
    int Order,
    int Width,
    int MinimumWidth,
    int MaximumWidth,
    OverlayContentColumnAlignment Alignment);

internal sealed record OverlayContentBrowserColumn(
    string Id,
    string Label,
    string DataKey,
    int Width,
    string Alignment);

internal static class OverlayContentColumnSettings
{
    public const string DataClassPosition = "class-position";
    public const string DataCarNumber = "car-number";
    public const string DataDriver = "driver";
    public const string DataGap = "gap";
    public const string DataInterval = "interval";
    public const string DataPit = "pit";
    public const string DataRelativePosition = "relative-position";
    public const string StandingsClassPositionColumnId = "standings.class-position";
    public const string StandingsCarNumberColumnId = "standings.car-number";
    public const string StandingsDriverColumnId = "standings.driver";
    public const string StandingsGapColumnId = "standings.gap";
    public const string StandingsIntervalColumnId = "standings.interval";
    public const string StandingsPitColumnId = "standings.pit";
    public const string RelativePositionColumnId = "relative.position";
    public const string RelativeDriverColumnId = "relative.driver";
    public const string RelativeGapColumnId = "relative.gap";
    public const string RelativePitColumnId = "relative.pit";
    public const string StandingsClassSeparatorBlockId = "standings.class-separators";
    public const string InputThrottleBlockId = "input-state.throttle";
    public const string InputBrakeBlockId = "input-state.brake";
    public const string InputClutchBlockId = "input-state.clutch";
    public const string InputSteeringBlockId = "input-state.steering";
    public const string InputGearBlockId = "input-state.gear";
    public const string InputSpeedBlockId = "input-state.speed";
    public const string PitServiceTireCompoundBlockId = "pit-service.tire-compound";
    public const string PitServiceTireChangeBlockId = "pit-service.tire-change";
    public const string PitServiceTireSetLimitBlockId = "pit-service.tire-set-limit";
    public const string PitServiceTireSetsAvailableBlockId = "pit-service.tire-sets-available";
    public const string PitServiceTireSetsUsedBlockId = "pit-service.tire-sets-used";
    public const string PitServiceTirePressureBlockId = "pit-service.tire-pressure";
    public const string PitServiceTireTemperatureBlockId = "pit-service.tire-temperature";
    public const string PitServiceTireWearBlockId = "pit-service.tire-wear";
    public const string PitServiceTireDistanceBlockId = "pit-service.tire-distance";

    public static OverlayContentDefinition Standings { get; } = new(
        OverlayId: StandingsOverlayDefinition.Definition.Id,
        BrowserWidthPadding: 66,
        BrowserMinimumHeight: 520,
        NativeMinimumTableHeight: 390,
        FallbackColumnId: StandingsDriverColumnId,
        Columns:
    [
        new(StandingsClassPositionColumnId, "CLS", DataClassPosition, true, 1, 35, 30, 110, OverlayOptionKeys.StandingsColumnClassWidth, SettingsLabel: "Class position"),
        new(StandingsCarNumberColumnId, "CAR", DataCarNumber, true, 2, 50, 42, 130, OverlayOptionKeys.StandingsColumnCarWidth, SettingsLabel: "Car number"),
        new(StandingsDriverColumnId, "Driver", DataDriver, true, 3, 250, 180, 520, OverlayOptionKeys.StandingsColumnDriverWidth, Alignment: OverlayContentColumnAlignment.Left),
        new(StandingsGapColumnId, "GAP", DataGap, true, 4, 60, 50, 160, OverlayOptionKeys.StandingsColumnGapWidth, SettingsLabel: "Class gap"),
        new(StandingsIntervalColumnId, "INT", DataInterval, true, 5, 60, 50, 160, OverlayOptionKeys.StandingsColumnIntervalWidth, SettingsLabel: "Focus interval"),
        new(StandingsPitColumnId, "PIT", DataPit, true, 6, 30, 24, 90, OverlayOptionKeys.StandingsColumnPitWidth, SettingsLabel: "Pit status")
    ],
        Blocks:
    [
        new(
            StandingsClassSeparatorBlockId,
            "Class separators",
            "Show iRacing class-colored separators and a limited sample of other multiclass rows.",
            OverlayOptionKeys.StandingsClassSeparatorsEnabled,
            DefaultEnabled: true,
            CountOptionKey: OverlayOptionKeys.StandingsOtherClassRows,
            CountLabel: "Other-class cars",
            DefaultCount: 2,
            MinimumCount: 0,
            MaximumCount: 6)
    ]);

    public static OverlayContentDefinition Relative { get; } = new(
        OverlayId: RelativeOverlayDefinition.Definition.Id,
        BrowserWidthPadding: 66,
        BrowserMinimumHeight: 360,
        NativeMinimumTableHeight: 180,
        FallbackColumnId: RelativeDriverColumnId,
        Columns:
    [
        new(RelativePositionColumnId, "Pos", DataRelativePosition, true, 1, 38, 32, 100, SettingsLabel: "Relative position"),
        new(RelativeDriverColumnId, "Driver", DataDriver, true, 2, 250, 180, 520, Alignment: OverlayContentColumnAlignment.Left),
        new(RelativeGapColumnId, "Delta", DataGap, true, 3, 70, 60, 160, SettingsLabel: "Relative delta"),
        new(RelativePitColumnId, "Pit", DataPit, false, 4, 30, 24, 90, SettingsLabel: "Pit status")
    ]);

    public static OverlayContentDefinition InputState { get; } = new(
        OverlayId: InputStateOverlayDefinition.Definition.Id,
        BrowserWidthPadding: 42,
        BrowserMinimumHeight: 220,
        NativeMinimumTableHeight: 160,
        FallbackColumnId: string.Empty,
        Columns: [],
        Blocks:
    [
        new(
            InputThrottleBlockId,
            "Throttle",
            "Show the live throttle percentage in the right-side input rail.",
            OverlayOptionKeys.InputShowThrottle,
            DefaultEnabled: true),
        new(
            InputBrakeBlockId,
            "Brake",
            "Show the live brake percentage in the right-side input rail.",
            OverlayOptionKeys.InputShowBrake,
            DefaultEnabled: true),
        new(
            InputClutchBlockId,
            "Clutch",
            "Show the live clutch percentage in the right-side input rail.",
            OverlayOptionKeys.InputShowClutch,
            DefaultEnabled: true),
        new(
            InputSteeringBlockId,
            "Steering wheel",
            "Show the live steering wheel visualization in the right-side input rail.",
            OverlayOptionKeys.InputShowSteering,
            DefaultEnabled: true),
        new(
            InputGearBlockId,
            "Gear",
            "Show the live gear readout in the right-side input rail.",
            OverlayOptionKeys.InputShowGear,
            DefaultEnabled: true),
        new(
            InputSpeedBlockId,
            "Speed",
            "Show the live speed readout in the right-side input rail.",
            OverlayOptionKeys.InputShowSpeed,
            DefaultEnabled: true)
    ]);

    public static OverlayContentDefinition PitService { get; } = new(
        OverlayId: PitServiceOverlayDefinition.Definition.Id,
        BrowserWidthPadding: 42,
        BrowserMinimumHeight: 360,
        NativeMinimumTableHeight: 260,
        FallbackColumnId: string.Empty,
        Columns: [],
        Blocks:
    [
        new(
            PitServiceTireCompoundBlockId,
            "Compound",
            "Show current and requested tire compound when compound telemetry is available.",
            OverlayOptionKeys.PitServiceShowTireCompound,
            DefaultEnabled: true),
        new(
            PitServiceTireChangeBlockId,
            "Change request",
            "Show requested tire changes by corner when pit-service tire commands are available.",
            OverlayOptionKeys.PitServiceShowTireChange,
            DefaultEnabled: true),
        new(
            PitServiceTireSetLimitBlockId,
            "Set limit",
            "Show the session tire-set limit when the SDK reports a representative limit.",
            OverlayOptionKeys.PitServiceShowTireSetLimit,
            DefaultEnabled: true),
        new(
            PitServiceTireSetsAvailableBlockId,
            "Sets available",
            "Show remaining tire sets by corner or axle when representative availability data exists.",
            OverlayOptionKeys.PitServiceShowTireSetsAvailable,
            DefaultEnabled: true),
        new(
            PitServiceTireSetsUsedBlockId,
            "Sets used",
            "Show tire sets or corner tires used when counters have moved from zero.",
            OverlayOptionKeys.PitServiceShowTireSetsUsed,
            DefaultEnabled: true),
        new(
            PitServiceTirePressureBlockId,
            "Pressure",
            "Show tire pressure from tire condition or pit-service pressure channels.",
            OverlayOptionKeys.PitServiceShowTirePressure,
            DefaultEnabled: true),
        new(
            PitServiceTireTemperatureBlockId,
            "Temperature",
            "Show tire temperatures when tire condition telemetry is populated.",
            OverlayOptionKeys.PitServiceShowTireTemperature,
            DefaultEnabled: true),
        new(
            PitServiceTireWearBlockId,
            "Wear",
            "Show tire wear percentages when tire condition telemetry is populated.",
            OverlayOptionKeys.PitServiceShowTireWear,
            DefaultEnabled: true),
        new(
            PitServiceTireDistanceBlockId,
            "Distance",
            "Show tire odometer values when tire distance telemetry is populated.",
            OverlayOptionKeys.PitServiceShowTireDistance,
            DefaultEnabled: true)
    ]);

    public static IReadOnlyList<OverlayContentColumnDefinition> StandingsColumns => Standings.Columns;

    public static IReadOnlyList<OverlayContentColumnDefinition> RelativeColumns => Relative.Columns;

    public static IReadOnlyList<OverlayContentDefinition> All { get; } = [Standings, Relative, InputState, PitService];

    public static bool TryGetContentDefinition(string overlayId, out OverlayContentDefinition definition)
    {
        var match = All.FirstOrDefault(definition => string.Equals(definition.OverlayId, overlayId, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            definition = match;
            return true;
        }

        definition = null!;
        return false;
    }

    public static IReadOnlyList<OverlayContentColumnDefinition> DefinitionsFor(string overlayId)
    {
        return TryGetContentDefinition(overlayId, out var definition) ? definition.Columns : [];
    }

    public static IReadOnlyList<OverlayContentColumnState> ColumnsFor(
        OverlaySettings settings,
        OverlayContentDefinition definition)
    {
        return ColumnsFor(settings, definition.Columns);
    }

    public static IReadOnlyList<OverlayContentColumnState> ColumnsFor(
        OverlaySettings settings,
        IReadOnlyList<OverlayContentColumnDefinition> definitions)
    {
        return definitions
            .Select(definition => ToState(settings, definition, definitions.Count))
            .OrderBy(column => column.Order)
            .ThenBy(column => definitions.First(definition => string.Equals(definition.Id, column.Id, StringComparison.Ordinal)).DefaultOrder)
            .ToArray();
    }

    public static IReadOnlyList<OverlayContentColumnState> EnabledColumnsFor(
        OverlaySettings settings,
        IReadOnlyList<OverlayContentColumnDefinition> definitions)
    {
        return ColumnsFor(settings, definitions)
            .Where(column => column.Enabled)
            .ToArray();
    }

    public static IReadOnlyList<OverlayContentColumnState> VisibleColumnsFor(
        OverlaySettings settings,
        OverlayContentDefinition definition)
    {
        var enabled = EnabledColumnsFor(settings, definition.Columns);
        return enabled.Count > 0
            ? enabled
            : ColumnsFor(settings, definition.Columns)
                .Where(column => string.Equals(column.Id, definition.FallbackColumnId, StringComparison.Ordinal))
                .Take(1)
                .ToArray();
    }

    public static IReadOnlyList<OverlayContentBrowserColumn> BrowserColumnsFor(
        OverlaySettings? settings,
        OverlayContentDefinition definition)
    {
        var columns = settings is null
            ? DefaultVisibleColumnsFor(definition.Columns)
            : VisibleColumnsFor(settings, definition);
        return columns
            .Select(column => new OverlayContentBrowserColumn(
                column.Id,
                column.Label,
                column.DataKey,
                column.Width,
                BrowserAlignment(column.Alignment)))
            .ToArray();
    }

    public static int TotalVisibleWidth(
        OverlaySettings settings,
        OverlayContentDefinition definition)
    {
        return VisibleColumnsFor(settings, definition).Sum(column => column.Width);
    }

    public static int TotalVisibleTableWidth(
        OverlaySettings settings,
        OverlayContentDefinition definition)
    {
        var columns = VisibleColumnsFor(settings, definition);
        var columnGaps = Math.Max(0, columns.Count - 1) * 8;
        return columns.Sum(column => column.Width) + columnGaps;
    }

    public static bool BlockEnabled(OverlaySettings settings, OverlayContentBlockDefinition block)
    {
        return settings.GetBooleanOption(block.EnabledOptionKey, block.DefaultEnabled);
    }

    public static int BlockCount(OverlaySettings settings, OverlayContentBlockDefinition block)
    {
        return block.CountOptionKey is null
            ? block.DefaultCount
            : settings.GetIntegerOption(
                block.CountOptionKey,
                block.DefaultCount,
                block.MinimumCount,
                block.MaximumCount);
    }

    public static OverlayContentColumnState ToState(
        OverlaySettings settings,
        OverlayContentColumnDefinition definition)
    {
        return ToState(settings, definition, DefinitionsFor(settings.Id).Count);
    }

    public static OverlayContentColumnState ToState(
        OverlaySettings settings,
        OverlayContentColumnDefinition definition,
        int definitionCount)
    {
        return new OverlayContentColumnState(
            Id: definition.Id,
            Label: definition.Label,
            SettingsLabel: HumanLabel(definition),
            DataKey: definition.DataKey,
            Enabled: settings.GetBooleanOption(definition.EnabledKey(settings.Id), definition.DefaultEnabled),
            Order: settings.GetIntegerOption(
                definition.OrderKey(settings.Id),
                definition.DefaultOrder,
                minimum: 1,
                maximum: Math.Max(1, definitionCount)),
            Width: settings.GetIntegerOption(
                definition.WidthKey(settings.Id),
                definition.DefaultWidth,
                definition.MinimumWidth,
                definition.MaximumWidth),
            MinimumWidth: definition.MinimumWidth,
            MaximumWidth: definition.MaximumWidth,
            Alignment: definition.Alignment);
    }

    private static IReadOnlyList<OverlayContentColumnState> DefaultVisibleColumnsFor(
        IReadOnlyList<OverlayContentColumnDefinition> definitions)
    {
        return definitions
            .Where(definition => definition.DefaultEnabled)
            .OrderBy(definition => definition.DefaultOrder)
            .Select(ToDefaultState)
            .ToArray();
    }

    private static OverlayContentColumnState ToDefaultState(OverlayContentColumnDefinition definition)
    {
        return new OverlayContentColumnState(
            Id: definition.Id,
            Label: definition.Label,
            SettingsLabel: HumanLabel(definition),
            DataKey: definition.DataKey,
            Enabled: definition.DefaultEnabled,
            Order: definition.DefaultOrder,
            Width: definition.DefaultWidth,
            MinimumWidth: definition.MinimumWidth,
            MaximumWidth: definition.MaximumWidth,
            Alignment: definition.Alignment);
    }

    private static string HumanLabel(OverlayContentColumnDefinition definition)
    {
        return string.IsNullOrWhiteSpace(definition.SettingsLabel)
            ? definition.Label
            : definition.SettingsLabel;
    }

    private static string BrowserAlignment(OverlayContentColumnAlignment alignment)
    {
        return alignment switch
        {
            OverlayContentColumnAlignment.Left => "left",
            OverlayContentColumnAlignment.Center => "center",
            _ => "right"
        };
    }
}
