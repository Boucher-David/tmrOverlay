using TmrOverlay.App.Overlays.Relative;
using TmrOverlay.App.Overlays.Standings;
using TmrOverlay.App.Overlays.InputState;
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
    OverlayContentColumnAlignment Alignment = OverlayContentColumnAlignment.Right)
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

    public static OverlayContentDefinition Standings { get; } = new(
        OverlayId: StandingsOverlayDefinition.Definition.Id,
        BrowserWidthPadding: 66,
        BrowserMinimumHeight: 520,
        NativeMinimumTableHeight: 390,
        FallbackColumnId: StandingsDriverColumnId,
        Columns:
    [
        new(StandingsClassPositionColumnId, "CLS", DataClassPosition, true, 1, 35, 30, 110, OverlayOptionKeys.StandingsColumnClassWidth),
        new(StandingsCarNumberColumnId, "CAR", DataCarNumber, true, 2, 50, 42, 130, OverlayOptionKeys.StandingsColumnCarWidth),
        new(StandingsDriverColumnId, "Driver", DataDriver, true, 3, 250, 180, 520, OverlayOptionKeys.StandingsColumnDriverWidth, Alignment: OverlayContentColumnAlignment.Left),
        new(StandingsGapColumnId, "GAP", DataGap, true, 4, 60, 50, 160, OverlayOptionKeys.StandingsColumnGapWidth),
        new(StandingsIntervalColumnId, "INT", DataInterval, true, 5, 60, 50, 160, OverlayOptionKeys.StandingsColumnIntervalWidth),
        new(StandingsPitColumnId, "PIT", DataPit, true, 6, 30, 24, 90, OverlayOptionKeys.StandingsColumnPitWidth)
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
        new(RelativePositionColumnId, "Pos", DataRelativePosition, true, 1, 38, 32, 100),
        new(RelativeDriverColumnId, "Driver", DataDriver, true, 2, 250, 180, 520, Alignment: OverlayContentColumnAlignment.Left),
        new(RelativeGapColumnId, "Gap", DataGap, true, 3, 70, 60, 160),
        new(RelativePitColumnId, "Pit", DataPit, false, 4, 30, 24, 90)
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

    public static IReadOnlyList<OverlayContentColumnDefinition> StandingsColumns => Standings.Columns;

    public static IReadOnlyList<OverlayContentColumnDefinition> RelativeColumns => Relative.Columns;

    public static IReadOnlyList<OverlayContentDefinition> All { get; } = [Standings, Relative, InputState];

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
            DataKey: definition.DataKey,
            Enabled: definition.DefaultEnabled,
            Order: definition.DefaultOrder,
            Width: definition.DefaultWidth,
            MinimumWidth: definition.MinimumWidth,
            MaximumWidth: definition.MaximumWidth,
            Alignment: definition.Alignment);
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
