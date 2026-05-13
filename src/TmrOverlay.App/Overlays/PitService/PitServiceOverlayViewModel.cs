using System.Globalization;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceOverlayViewModel
{
    private static readonly TimeSpan ChangeHighlightDuration = TimeSpan.FromSeconds(30);

    public static StatefulBuilder CreateStatefulBuilder()
    {
        return new StatefulBuilder();
    }

    public static Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> CreateBuilder(
        OverlaySettings? settings = null)
    {
        var builder = CreateStatefulBuilder();
        return (snapshot, now, unitSystem) => builder.Build(snapshot, now, unitSystem, settings);
    }

    public static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem,
        OverlaySettings? settings = null)
    {
        return From(snapshot, now, unitSystem, changeTracker: null, settings);
    }

    private static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem,
        ChangeTracker? changeTracker,
        OverlaySettings? settings)
    {
        if (!SimpleTelemetryOverlayViewModel.IsFresh(snapshot, now, out var waitingStatus))
        {
            changeTracker?.Reset();
            return SimpleTelemetryOverlayViewModel.Waiting("Pit Service", waitingStatus);
        }

        var localContext = LiveLocalStrategyContext.ForPitService(snapshot, now);
        if (!localContext.IsAvailable)
        {
            changeTracker?.Reset();
            return SimpleTelemetryOverlayViewModel.Waiting("Pit Service", localContext.StatusText);
        }

        var pit = snapshot.Models.PitService;
        if (!pit.HasData)
        {
            changeTracker?.Reset();
            return SimpleTelemetryOverlayViewModel.Waiting("Pit Service", "waiting for pit telemetry");
        }

        var release = BuildReleaseState(pit);
        var status = BuildStatus(pit, release);
        var tone = ToneFor(pit, release);
        var service = FormatService(pit);
        var fuelRequest = FormatFuelRequest(pit, unitSystem);
        var repair = FormatRepair(pit);
        var tires = FormatTires(pit);
        var fastRepair = FormatFastRepair(pit);
        var serviceChanged = IsChanged(changeTracker, "service", service, now);
        var fuelRequestChanged = IsChanged(changeTracker, "fuel-request", fuelRequest, now);
        var repairChanged = IsChanged(changeTracker, "repair", repair, now);
        var tiresChanged = IsChanged(changeTracker, "tires", tires, now);
        var fastRepairChanged = IsChanged(changeTracker, "fast-repair", fastRepair, now);
        var rows = new[]
        {
            new SimpleTelemetryRowViewModel("Release", release.Value, release.Tone),
            new SimpleTelemetryRowViewModel("Location", FormatLocation(pit), tone),
            new SimpleTelemetryRowViewModel("Service", service, HighlightTone(tone, serviceChanged)),
            new SimpleTelemetryRowViewModel("Pit status", PitServiceStatusFormatter.Format(pit.Status), tone),
            new SimpleTelemetryRowViewModel("Fuel request", fuelRequest, HighlightTone(SimpleTelemetryTone.Normal, fuelRequestChanged)),
            new SimpleTelemetryRowViewModel("Repair", repair, HighlightTone(RepairTone(pit), repairChanged)),
            new SimpleTelemetryRowViewModel("Tires", tires, HighlightTone(SimpleTelemetryTone.Normal, tiresChanged)),
            new SimpleTelemetryRowViewModel("Fast repair", fastRepair, HighlightTone(SimpleTelemetryTone.Normal, fastRepairChanged))
        };
        var tireAnalysisRows = BuildTireAnalysisRows(pit, snapshot.Models.TireCondition, unitSystem, PitServiceContentOptions.From(settings));
        IReadOnlyList<SimpleTelemetryGridSectionViewModel> sections = tireAnalysisRows.Count == 0
            ? Array.Empty<SimpleTelemetryGridSectionViewModel>()
            : new[]
            {
                new SimpleTelemetryGridSectionViewModel(
                    "Tire Analysis",
                    new[] { "Info", "FL", "FR", "RL", "RR" },
                    tireAnalysisRows)
            };

        return new SimpleTelemetryOverlayViewModel(
            Title: "Pit Service",
            Status: status,
            Source: BuildSource(),
            Tone: tone,
            Rows: rows,
            Sections: sections);
    }

    private static string BuildSource()
    {
        return "source: player/team pit service telemetry";
    }

    private static string BuildStatus(LivePitServiceModel pit, PitReleaseState release)
    {
        if (PitServiceStatusFormatter.IsError(pit.Status))
        {
            return "pit stall error";
        }

        if (release.Kind == PitReleaseKind.Go)
        {
            return "release ready";
        }

        if (release.Kind == PitReleaseKind.Advisory)
        {
            return "optional repair";
        }

        if (release.Kind == PitReleaseKind.Hold)
        {
            return "hold";
        }

        if (pit.PlayerCarInPitStall)
        {
            return "in pit stall";
        }

        if (pit.PitstopActive)
        {
            return "service active";
        }

        if (pit.OnPitRoad || pit.TeamOnPitRoad == true)
        {
            return "on pit road";
        }

        return HasRequestedService(pit) ? "service requested" : "pit ready";
    }

    private static string FormatLocation(LivePitServiceModel pit)
    {
        if (pit.PlayerCarInPitStall)
        {
            return "player in stall";
        }

        if (pit.OnPitRoad && pit.TeamOnPitRoad == true)
        {
            return "player/team on pit road";
        }

        if (pit.OnPitRoad)
        {
            return "player on pit road";
        }

        if (pit.TeamOnPitRoad == true)
        {
            return "team on pit road";
        }

        return "off pit road";
    }

    private static string FormatService(LivePitServiceModel pit)
    {
        var service = pit.Flags is null && pit.Request.FuelLiters is not > 0d
            ? "--"
            : FormatServiceRequest(pit.Request);

        if (IsServiceActive(pit))
        {
            return service is "--" or "none" ? "active" : $"active | {service}";
        }

        if (HasRequestedService(pit))
        {
            return service is "--" or "none" ? "requested" : $"requested | {service}";
        }

        return service;
    }

    private static string FormatFuelRequest(LivePitServiceModel pit, string unitSystem)
    {
        return pit.Request.FuelLiters is > 0d
            ? SimpleTelemetryOverlayViewModel.FormatFuelVolume(pit.Request.FuelLiters, unitSystem)
            : "--";
    }

    private static string FormatRepair(LivePitServiceModel pit)
    {
        string? required = pit.Repair.RequiredSeconds is { } requiredSeconds && requiredSeconds > 0d
            ? $"{requiredSeconds.ToString("0", CultureInfo.InvariantCulture)}s required"
            : null;
        string? optional = pit.Repair.OptionalSeconds is { } optionalSeconds && optionalSeconds > 0d
            ? $"{optionalSeconds.ToString("0", CultureInfo.InvariantCulture)}s optional"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(required, optional);
    }

    private static string FormatTires(LivePitServiceModel pit)
    {
        var service = pit.Tires.RequestedTireCount switch
        {
            4 => "four tires",
            > 0 => $"{pit.Tires.RequestedTireCount.ToString(CultureInfo.InvariantCulture)} tires",
            _ => null
        };
        var sets = pit.Tires.TireSetsUsed is { } value && value > 0
            ? $"{value.ToString(CultureInfo.InvariantCulture)} sets used"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(service, sets);
    }

    private static string FormatFastRepair(LivePitServiceModel pit)
    {
        var selected = pit.FastRepair.Selected
            ? "selected"
            : null;
        var local = pit.FastRepair.LocalUsed is { } used && used >= 0
            ? $"local {used.ToString(CultureInfo.InvariantCulture)}"
            : null;
        var team = pit.FastRepair.TeamUsed is { } teamUsed && teamUsed >= 0
            ? $"team {teamUsed.ToString(CultureInfo.InvariantCulture)}"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(selected, local, team);
    }

    private static string FormatServiceRequest(LivePitServiceRequest request)
    {
        var active = new List<string>();
        var tireCount = request.RequestedTireCount;
        if (tireCount == 4)
        {
            active.Add("tires");
        }
        else if (tireCount > 0)
        {
            active.Add($"{tireCount.ToString(CultureInfo.InvariantCulture)} tires");
        }

        if (request.Fuel || request.FuelLiters is > 0d)
        {
            active.Add("fuel");
        }

        if (request.Tearoff)
        {
            active.Add("tearoff");
        }

        if (request.FastRepair)
        {
            active.Add("fast repair");
        }

        return active.Count == 0 ? "none" : string.Join(", ", active);
    }

    private static IReadOnlyList<SimpleTelemetryGridRowViewModel> BuildTireAnalysisRows(
        LivePitServiceModel pit,
        LiveTireConditionModel tireCondition,
        string unitSystem,
        PitServiceContentOptions options)
    {
        var rows = new List<SimpleTelemetryGridRowViewModel>();
        AddIfPresent(rows, BuildCompoundRow(pit, options));
        AddIfPresent(rows, BuildChangeRow(pit, tireCondition, options));
        AddIfPresent(rows, BuildSetLimitRow(pit, options));
        AddIfPresent(rows, BuildSetsAvailableRow(pit, options));
        AddIfPresent(rows, BuildSetsUsedRow(pit, options));
        AddIfPresent(rows, BuildPressureRow(tireCondition, unitSystem, options));
        AddIfPresent(rows, BuildTemperatureRow(tireCondition, unitSystem, options));
        AddIfPresent(rows, BuildWearRow(tireCondition, options));
        AddIfPresent(rows, BuildDistanceRow(tireCondition, unitSystem, options));
        return rows;
    }

    private static void AddIfPresent(List<SimpleTelemetryGridRowViewModel> rows, SimpleTelemetryGridRowViewModel? row)
    {
        if (row is not null)
        {
            rows.Add(row);
        }
    }

    private static SimpleTelemetryGridRowViewModel? BuildCompoundRow(
        LivePitServiceModel pit,
        PitServiceContentOptions options)
    {
        if (!options.ShowTireCompound)
        {
            return null;
        }

        var current = FirstAvailable(pit.Tires.CurrentCompoundShortLabel, pit.Tires.CurrentCompoundLabel);
        var requested = FirstAvailable(pit.Tires.RequestedCompoundShortLabel, pit.Tires.RequestedCompoundLabel);
        var value = SimpleTelemetryOverlayViewModel.JoinAvailable(
            current is null ? null : $"now {current}",
            requested is null ? null : $"req {requested}");
        return value == "--" ? null : Row("Compound", value, value, value, value);
    }

    private static SimpleTelemetryGridRowViewModel? BuildChangeRow(
        LivePitServiceModel pit,
        LiveTireConditionModel tireCondition,
        PitServiceContentOptions options)
    {
        if (!options.ShowTireChange)
        {
            return null;
        }

        var leftFront = tireCondition.LeftFront.ChangeRequested ?? pit.Tires.LeftFrontChangeRequested;
        var rightFront = tireCondition.RightFront.ChangeRequested ?? pit.Tires.RightFrontChangeRequested;
        var leftRear = tireCondition.LeftRear.ChangeRequested ?? pit.Tires.LeftRearChangeRequested;
        var rightRear = tireCondition.RightRear.ChangeRequested ?? pit.Tires.RightRearChangeRequested;
        if (leftFront != true && rightFront != true && leftRear != true && rightRear != true)
        {
            return null;
        }

        return Row(
            "Change",
            FormatBooleanCell(leftFront),
            FormatBooleanCell(rightFront),
            FormatBooleanCell(leftRear),
            FormatBooleanCell(rightRear));
    }

    private static SimpleTelemetryGridRowViewModel? BuildSetLimitRow(
        LivePitServiceModel pit,
        PitServiceContentOptions options)
    {
        if (!options.ShowTireSetLimit)
        {
            return null;
        }

        var limit = RepresentativeLimit(pit);
        if (limit is null)
        {
            return null;
        }

        return Row("Set limit", limit, limit, limit, limit);
    }

    private static SimpleTelemetryGridRowViewModel? BuildSetsAvailableRow(
        LivePitServiceModel pit,
        PitServiceContentOptions options)
    {
        if (!options.ShowTireSetsAvailable)
        {
            return null;
        }

        var showZeroAvailable = pit.Tires.DryTireSetLimit is > 0;
        var leftFront = FormatAvailableForCorner(
            pit.Tires.LeftFrontTiresAvailable,
            pit.Tires.LeftTireSetsAvailable,
            "L",
            pit.Tires.FrontTireSetsAvailable,
            "F",
            pit.Tires.TireSetsAvailable,
            showZeroAvailable);
        var rightFront = FormatAvailableForCorner(
            pit.Tires.RightFrontTiresAvailable,
            pit.Tires.RightTireSetsAvailable,
            "R",
            pit.Tires.FrontTireSetsAvailable,
            "F",
            pit.Tires.TireSetsAvailable,
            showZeroAvailable);
        var leftRear = FormatAvailableForCorner(
            pit.Tires.LeftRearTiresAvailable,
            pit.Tires.LeftTireSetsAvailable,
            "L",
            pit.Tires.RearTireSetsAvailable,
            "Rr",
            pit.Tires.TireSetsAvailable,
            showZeroAvailable);
        var rightRear = FormatAvailableForCorner(
            pit.Tires.RightRearTiresAvailable,
            pit.Tires.RightTireSetsAvailable,
            "R",
            pit.Tires.RearTireSetsAvailable,
            "Rr",
            pit.Tires.TireSetsAvailable,
            showZeroAvailable);
        return AnyCellHasValue(leftFront, rightFront, leftRear, rightRear)
            ? Row("Available", leftFront, rightFront, leftRear, rightRear)
            : null;
    }

    private static SimpleTelemetryGridRowViewModel? BuildSetsUsedRow(
        LivePitServiceModel pit,
        PitServiceContentOptions options)
    {
        if (!options.ShowTireSetsUsed)
        {
            return null;
        }

        var leftFront = FormatUsedForCorner(
            pit.Tires.LeftFrontTiresUsed,
            pit.Tires.LeftTireSetsUsed,
            "L",
            pit.Tires.FrontTireSetsUsed,
            "F",
            pit.Tires.TireSetsUsed);
        var rightFront = FormatUsedForCorner(
            pit.Tires.RightFrontTiresUsed,
            pit.Tires.RightTireSetsUsed,
            "R",
            pit.Tires.FrontTireSetsUsed,
            "F",
            pit.Tires.TireSetsUsed);
        var leftRear = FormatUsedForCorner(
            pit.Tires.LeftRearTiresUsed,
            pit.Tires.LeftTireSetsUsed,
            "L",
            pit.Tires.RearTireSetsUsed,
            "Rr",
            pit.Tires.TireSetsUsed);
        var rightRear = FormatUsedForCorner(
            pit.Tires.RightRearTiresUsed,
            pit.Tires.RightTireSetsUsed,
            "R",
            pit.Tires.RearTireSetsUsed,
            "Rr",
            pit.Tires.TireSetsUsed);
        return AnyCellHasValue(leftFront, rightFront, leftRear, rightRear)
            ? Row("Used", leftFront, rightFront, leftRear, rightRear)
            : null;
    }

    private static SimpleTelemetryGridRowViewModel? BuildPressureRow(
        LiveTireConditionModel tireCondition,
        string unitSystem,
        PitServiceContentOptions options)
    {
        if (!options.ShowTirePressure)
        {
            return null;
        }

        return BuildTireConditionRow(
            "Pressure",
            tireCondition,
            corner =>
            {
                var pressureKpa = FirstRepresentative(
                    corner.PitServicePressureKpa,
                    corner.ColdPressureKpa,
                    corner.BlackBoxColdPressurePa / 1000d);
                return pressureKpa is { } value
                    ? SimpleTelemetryOverlayViewModel.FormatPressure(value / 100d, unitSystem)
                    : "--";
            });
    }

    private static SimpleTelemetryGridRowViewModel? BuildTemperatureRow(
        LiveTireConditionModel tireCondition,
        string unitSystem,
        PitServiceContentOptions options)
    {
        return options.ShowTireTemperature
            ? BuildTireConditionRow("Temp", tireCondition, corner => FormatTemperatureAcross(corner.TemperatureC, unitSystem))
            : null;
    }

    private static SimpleTelemetryGridRowViewModel? BuildWearRow(
        LiveTireConditionModel tireCondition,
        PitServiceContentOptions options)
    {
        return options.ShowTireWear
            ? BuildTireConditionRow("Wear", tireCondition, corner => FormatWearAcross(corner.Wear))
            : null;
    }

    private static SimpleTelemetryGridRowViewModel? BuildDistanceRow(
        LiveTireConditionModel tireCondition,
        string unitSystem,
        PitServiceContentOptions options)
    {
        return options.ShowTireDistance
            ? BuildTireConditionRow("Distance", tireCondition, corner => FormatDistance(corner.OdometerMeters, unitSystem))
            : null;
    }

    private static SimpleTelemetryGridRowViewModel? BuildTireConditionRow(
        string label,
        LiveTireConditionModel tireCondition,
        Func<LiveTireCornerCondition, string> format)
    {
        var leftFront = format(tireCondition.LeftFront);
        var rightFront = format(tireCondition.RightFront);
        var leftRear = format(tireCondition.LeftRear);
        var rightRear = format(tireCondition.RightRear);
        return AnyCellHasValue(leftFront, rightFront, leftRear, rightRear)
            ? Row(label, leftFront, rightFront, leftRear, rightRear)
            : null;
    }

    private static SimpleTelemetryGridRowViewModel Row(
        string label,
        string leftFront,
        string rightFront,
        string leftRear,
        string rightRear,
        SimpleTelemetryTone tone = SimpleTelemetryTone.Normal)
    {
        return new SimpleTelemetryGridRowViewModel(
            label,
            [
                new SimpleTelemetryGridCellViewModel(leftFront, tone),
                new SimpleTelemetryGridCellViewModel(rightFront, tone),
                new SimpleTelemetryGridCellViewModel(leftRear, tone),
                new SimpleTelemetryGridCellViewModel(rightRear, tone)
            ],
            tone);
    }

    private static string? RepresentativeLimit(LivePitServiceModel pit)
    {
        if (pit.Tires.DryTireSetLimit is > 0)
        {
            return $"{pit.Tires.DryTireSetLimit.Value.ToString(CultureInfo.InvariantCulture)} sets";
        }

        return HasUnlimitedAvailability(pit)
            ? "unlim"
            : null;
    }

    private static bool HasUnlimitedAvailability(LivePitServiceModel pit)
    {
        return pit.Tires.TireSetsAvailable == 255
            || pit.Tires.LeftTireSetsAvailable == 255
            || pit.Tires.RightTireSetsAvailable == 255
            || pit.Tires.FrontTireSetsAvailable == 255
            || pit.Tires.RearTireSetsAvailable == 255
            || pit.Tires.LeftFrontTiresAvailable == 255
            || pit.Tires.RightFrontTiresAvailable == 255
            || pit.Tires.LeftRearTiresAvailable == 255
            || pit.Tires.RightRearTiresAvailable == 255;
    }

    private static string FormatAvailableForCorner(
        int? perCorner,
        int? side,
        string sideLabel,
        int? axle,
        string axleLabel,
        int? global,
        bool allowZero)
    {
        if (IsRepresentativeAvailable(perCorner, allowZero))
        {
            return FormatTireCounter(perCorner);
        }

        var parts = new List<string>();
        if (IsRepresentativeAvailable(side, allowZero))
        {
            parts.Add($"{sideLabel} {FormatTireCounter(side)}");
        }

        if (IsRepresentativeAvailable(axle, allowZero) && axle != side)
        {
            parts.Add($"{axleLabel} {FormatTireCounter(axle)}");
        }

        if (parts.Count > 0)
        {
            return string.Join("/", parts);
        }

        return IsRepresentativeAvailable(global, allowZero) ? FormatTireCounter(global) : "--";
    }

    private static string FormatUsedForCorner(
        int? perCorner,
        int? side,
        string sideLabel,
        int? axle,
        string axleLabel,
        int? global)
    {
        if (IsRepresentativeUsed(perCorner))
        {
            return FormatTireCounter(perCorner);
        }

        var parts = new List<string>();
        if (IsRepresentativeUsed(side))
        {
            parts.Add($"{sideLabel} {FormatTireCounter(side)}");
        }

        if (IsRepresentativeUsed(axle) && axle != side)
        {
            parts.Add($"{axleLabel} {FormatTireCounter(axle)}");
        }

        if (parts.Count > 0)
        {
            return string.Join("/", parts);
        }

        return IsRepresentativeUsed(global) ? FormatTireCounter(global) : "--";
    }

    private static string FormatTireCounter(int? value)
    {
        return value == 255
            ? "unlim"
            : value?.ToString(CultureInfo.InvariantCulture) ?? "--";
    }

    private static string FormatBooleanCell(bool? value)
    {
        return value == true ? "yes" : "--";
    }

    private static string FormatTemperatureAcross(LiveTireAcrossTreadValues values, string unitSystem)
    {
        var raw = new[] { values.Left, values.Middle, values.Right }
            .Where(IsRepresentativeTemperature)
            .Select(value => SimpleTelemetryOverlayViewModel.IsImperial(unitSystem)
                ? value!.Value * 9d / 5d + 32d
                : value!.Value)
            .ToArray();
        return FormatAcrossValues(
            raw,
            "0",
            SimpleTelemetryOverlayViewModel.IsImperial(unitSystem) ? " F" : " C");
    }

    private static string FormatWearAcross(LiveTireAcrossTreadValues values)
    {
        var raw = new[] { values.Left, values.Middle, values.Right }
            .Where(IsRepresentativeWear)
            .Select(value => value!.Value * 100d)
            .ToArray();
        return FormatAcrossValues(raw, "0", "%");
    }

    private static string FormatAcrossValues(IReadOnlyList<double> values, string format, string suffix)
    {
        if (values.Count == 0)
        {
            return "--";
        }

        var formatted = values
            .Select(value => value.ToString(format, CultureInfo.InvariantCulture))
            .ToArray();
        var distinct = formatted.Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 1
            ? $"{distinct[0]}{suffix}"
            : $"{string.Join("/", formatted)}{suffix}";
    }

    private static string FormatDistance(double? meters, string unitSystem)
    {
        if (meters is not { } value || !SimpleTelemetryOverlayViewModel.IsFinite(value) || value <= 0d)
        {
            return "--";
        }

        if (SimpleTelemetryOverlayViewModel.IsImperial(unitSystem))
        {
            return $"{(value / 1609.344d).ToString("0.0", CultureInfo.InvariantCulture)} mi";
        }

        return $"{(value / 1000d).ToString("0.0", CultureInfo.InvariantCulture)} km";
    }

    private static double? FirstRepresentative(params double?[] values)
    {
        foreach (var value in values)
        {
            if (value is { } number && number > 0d && SimpleTelemetryOverlayViewModel.IsFinite(number))
            {
                return number;
            }
        }

        return null;
    }

    private static string? FirstAvailable(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static bool AnyCellHasValue(params string[] values)
    {
        return values.Any(value => !string.IsNullOrWhiteSpace(value) && value != "--");
    }

    private static bool IsRepresentativeAvailable(int? value, bool allowZero = false)
    {
        return value is > 0 || (allowZero && value == 0);
    }

    private static bool IsRepresentativeUsed(int? value)
    {
        return value is > 0;
    }

    private static bool IsRepresentativeWear(double? value)
    {
        return value is > 0d and <= 1d;
    }

    private static bool IsRepresentativeTemperature(double? value)
    {
        return value is > 0d and < 250d;
    }

    private static SimpleTelemetryTone ToneFor(LivePitServiceModel pit, PitReleaseState release)
    {
        if (release.Tone is SimpleTelemetryTone.Info or SimpleTelemetryTone.Success or SimpleTelemetryTone.Warning or SimpleTelemetryTone.Error)
        {
            return release.Tone;
        }

        if (HasRequiredRepair(pit) || HasOptionalRepair(pit))
        {
            return SimpleTelemetryTone.Warning;
        }

        if (pit.PlayerCarInPitStall || pit.PitstopActive || pit.OnPitRoad || pit.TeamOnPitRoad == true)
        {
            return SimpleTelemetryTone.Info;
        }

        return HasRequestedService(pit) ? SimpleTelemetryTone.Success : SimpleTelemetryTone.Normal;
    }

    private static SimpleTelemetryTone RepairTone(LivePitServiceModel pit)
    {
        if (HasRequiredRepair(pit))
        {
            return SimpleTelemetryTone.Error;
        }

        return HasOptionalRepair(pit) ? SimpleTelemetryTone.Warning : SimpleTelemetryTone.Normal;
    }

    private static bool HasRequestedService(LivePitServiceModel pit)
    {
        return pit.Request.HasAnyRequest || (pit.Flags is { } flags && flags != 0);
    }

    private static PitReleaseState BuildReleaseState(LivePitServiceModel pit)
    {
        if (PitServiceStatusFormatter.IsError(pit.Status))
        {
            return new PitReleaseState(
                PitReleaseKind.Hold,
                $"RED - {PitServiceStatusFormatter.Format(pit.Status)}",
                SimpleTelemetryTone.Error);
        }

        if (PitServiceStatusFormatter.IsComplete(pit.Status))
        {
            return new PitReleaseState(
                PitReleaseKind.Go,
                "GREEN - go",
                SimpleTelemetryTone.Success);
        }

        if (IsServiceActive(pit))
        {
            return new PitReleaseState(
                PitReleaseKind.Hold,
                "RED - service active",
                SimpleTelemetryTone.Error);
        }

        if (HasRequiredRepair(pit))
        {
            return new PitReleaseState(
                PitReleaseKind.Hold,
                "RED - repair active",
                SimpleTelemetryTone.Error);
        }

        if (HasOptionalRepair(pit))
        {
            return new PitReleaseState(
                PitReleaseKind.Advisory,
                "YELLOW - optional repair",
                SimpleTelemetryTone.Warning);
        }

        if (pit.PlayerCarInPitStall)
        {
            return new PitReleaseState(
                PitReleaseKind.Go,
                pit.Status is null ? "GREEN - go (inferred)" : "GREEN - go",
                SimpleTelemetryTone.Success);
        }

        if (pit.OnPitRoad || pit.TeamOnPitRoad == true)
        {
            return new PitReleaseState(
                PitReleaseKind.Pending,
                "pit road",
                SimpleTelemetryTone.Info);
        }

        return HasRequestedService(pit)
            ? new PitReleaseState(
                PitReleaseKind.Pending,
                "armed",
                SimpleTelemetryTone.Info)
            : new PitReleaseState(
                PitReleaseKind.Pending,
                "--",
                SimpleTelemetryTone.Normal);
    }

    private static bool IsServiceActive(LivePitServiceModel pit)
    {
        return PitServiceStatusFormatter.IsInProgress(pit.Status) || pit.PitstopActive;
    }

    private static bool HasRequiredRepair(LivePitServiceModel pit)
    {
        return pit.Repair.RequiredSeconds is > 0d;
    }

    private static bool HasOptionalRepair(LivePitServiceModel pit)
    {
        return pit.Repair.OptionalSeconds is > 0d;
    }

    private static bool IsChanged(ChangeTracker? tracker, string key, string value, DateTimeOffset now)
    {
        return tracker?.IsHighlighted(key, value, now) == true;
    }

    private static SimpleTelemetryTone HighlightTone(SimpleTelemetryTone baseTone, bool changed)
    {
        return StrongestTone(baseTone, changed ? SimpleTelemetryTone.Info : SimpleTelemetryTone.Normal);
    }

    private static SimpleTelemetryTone StrongestTone(SimpleTelemetryTone left, SimpleTelemetryTone right)
    {
        return Weight(left) >= Weight(right) ? left : right;
    }

    private static int Weight(SimpleTelemetryTone tone)
    {
        return tone switch
        {
            SimpleTelemetryTone.Error => 50,
            SimpleTelemetryTone.Warning => 40,
            SimpleTelemetryTone.Info => 30,
            SimpleTelemetryTone.Success => 20,
            SimpleTelemetryTone.Waiting => 10,
            _ => 0
        };
    }

    private enum PitReleaseKind
    {
        Pending,
        Advisory,
        Hold,
        Go
    }

    private sealed record PitReleaseState(
        PitReleaseKind Kind,
        string Value,
        SimpleTelemetryTone Tone);

    internal sealed class StatefulBuilder
    {
        private readonly ChangeTracker _changeTracker = new(ChangeHighlightDuration);

        public SimpleTelemetryOverlayViewModel Build(
            LiveTelemetrySnapshot snapshot,
            DateTimeOffset now,
            string unitSystem,
            OverlaySettings? settings = null)
        {
            return From(snapshot, now, unitSystem, _changeTracker, settings);
        }
    }

    private sealed record PitServiceContentOptions(
        bool ShowTireCompound,
        bool ShowTireChange,
        bool ShowTireSetLimit,
        bool ShowTireSetsAvailable,
        bool ShowTireSetsUsed,
        bool ShowTirePressure,
        bool ShowTireTemperature,
        bool ShowTireWear,
        bool ShowTireDistance)
    {
        public static PitServiceContentOptions From(OverlaySettings? settings)
        {
            return new PitServiceContentOptions(
                ShowTireCompound: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireCompound, defaultValue: true) ?? true,
                ShowTireChange: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireChange, defaultValue: true) ?? true,
                ShowTireSetLimit: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireSetLimit, defaultValue: true) ?? true,
                ShowTireSetsAvailable: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireSetsAvailable, defaultValue: true) ?? true,
                ShowTireSetsUsed: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireSetsUsed, defaultValue: true) ?? true,
                ShowTirePressure: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTirePressure, defaultValue: true) ?? true,
                ShowTireTemperature: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireTemperature, defaultValue: true) ?? true,
                ShowTireWear: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireWear, defaultValue: true) ?? true,
                ShowTireDistance: settings?.GetBooleanOption(OverlayOptionKeys.PitServiceShowTireDistance, defaultValue: true) ?? true);
        }
    }

    private sealed class ChangeTracker
    {
        private readonly TimeSpan _duration;
        private readonly Dictionary<string, string> _lastValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeOffset> _highlightUntil = new(StringComparer.OrdinalIgnoreCase);

        public ChangeTracker(TimeSpan duration)
        {
            _duration = duration;
        }

        public bool IsHighlighted(string key, string value, DateTimeOffset now)
        {
            var normalized = value.Trim();
            if (_lastValues.TryGetValue(key, out var previous)
                && !string.Equals(previous, normalized, StringComparison.Ordinal))
            {
                _highlightUntil[key] = now.Add(_duration);
            }

            _lastValues[key] = normalized;
            return _highlightUntil.TryGetValue(key, out var until) && until >= now;
        }

        public void Reset()
        {
            _lastValues.Clear();
            _highlightUntil.Clear();
        }
    }
}
