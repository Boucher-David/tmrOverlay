using System.Globalization;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceOverlayViewModel
{
    private static readonly PitFlagLabel[] ServiceFlagLabels =
    [
        new(0x01, "LF tire"),
        new(0x02, "RF tire"),
        new(0x04, "LR tire"),
        new(0x08, "RR tire"),
        new(0x10, "fuel"),
        new(0x20, "tearoff"),
        new(0x40, "fast repair")
    ];

    public static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem)
    {
        if (!SimpleTelemetryOverlayViewModel.IsFresh(snapshot, now, out var waitingStatus))
        {
            return SimpleTelemetryOverlayViewModel.Waiting("Pit Service", waitingStatus);
        }

        var pit = snapshot.Models.FuelPit;
        if (!pit.HasData)
        {
            return SimpleTelemetryOverlayViewModel.Waiting("Pit Service", "waiting for pit telemetry");
        }

        var release = BuildReleaseState(pit);
        var status = BuildStatus(pit, release);
        var tone = ToneFor(pit, release);
        var rows = new[]
        {
            new SimpleTelemetryRowViewModel("Release", release.Value, release.Tone),
            new SimpleTelemetryRowViewModel("Location", FormatLocation(pit), tone),
            new SimpleTelemetryRowViewModel("Service", FormatService(pit), tone),
            new SimpleTelemetryRowViewModel("Pit status", PitServiceStatusFormatter.Format(pit.PitServiceStatus), tone),
            new SimpleTelemetryRowViewModel("Fuel request", SimpleTelemetryOverlayViewModel.FormatFuelVolume(pit.PitServiceFuelLiters, unitSystem)),
            new SimpleTelemetryRowViewModel("Repair", FormatRepair(pit), RepairTone(pit)),
            new SimpleTelemetryRowViewModel("Tires", FormatTires(pit)),
            new SimpleTelemetryRowViewModel("Fast repair", FormatFastRepair(pit))
        };

        return new SimpleTelemetryOverlayViewModel(
            Title: "Pit Service",
            Status: status,
            Source: "source: pit service telemetry",
            Tone: tone,
            Rows: rows);
    }

    private static string BuildStatus(LiveFuelPitModel pit, PitReleaseState release)
    {
        if (release.Kind == PitReleaseKind.Go)
        {
            return "release ready";
        }

        if (release.Kind == PitReleaseKind.Hold)
        {
            return "hold";
        }

        if (PitServiceStatusFormatter.IsError(pit.PitServiceStatus))
        {
            return "pit stall error";
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

    private static string FormatLocation(LiveFuelPitModel pit)
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

    private static string FormatService(LiveFuelPitModel pit)
    {
        var service = FormatServiceFlags(pit.PitServiceFlags);
        if (pit.PitstopActive)
        {
            return service == "none" ? "active" : $"active | {service}";
        }

        return service;
    }

    private static string FormatRepair(LiveFuelPitModel pit)
    {
        string? required = pit.PitRepairLeftSeconds is { } requiredSeconds && requiredSeconds > 0d
            ? $"{requiredSeconds.ToString("0", CultureInfo.InvariantCulture)}s required"
            : null;
        string? optional = pit.PitOptRepairLeftSeconds is { } optionalSeconds && optionalSeconds > 0d
            ? $"{optionalSeconds.ToString("0", CultureInfo.InvariantCulture)}s optional"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(required, optional);
    }

    private static string FormatTires(LiveFuelPitModel pit)
    {
        var service = TireServiceCount(pit.PitServiceFlags) switch
        {
            4 => "four tires",
            > 0 => $"{TireServiceCount(pit.PitServiceFlags).ToString(CultureInfo.InvariantCulture)} tires",
            _ => null
        };
        var sets = pit.TireSetsUsed is { } value && value >= 0
            ? $"{value.ToString(CultureInfo.InvariantCulture)} sets used"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(service, sets);
    }

    private static string FormatFastRepair(LiveFuelPitModel pit)
    {
        var local = pit.FastRepairUsed is { } used && used >= 0
            ? $"local {used.ToString(CultureInfo.InvariantCulture)}"
            : null;
        var team = pit.TeamFastRepairsUsed is { } teamUsed && teamUsed >= 0
            ? $"team {teamUsed.ToString(CultureInfo.InvariantCulture)}"
            : null;
        return SimpleTelemetryOverlayViewModel.JoinAvailable(local, team);
    }

    private static string FormatServiceFlags(int? flags)
    {
        if (flags is null)
        {
            return "--";
        }

        var active = ServiceFlagLabels
            .Where(label => (flags.Value & label.Bit) != 0)
            .Select(label => label.Label)
            .ToArray();
        return active.Length == 0 ? "none" : string.Join(", ", active);
    }

    private static SimpleTelemetryTone ToneFor(LiveFuelPitModel pit, PitReleaseState release)
    {
        if (release.Tone is SimpleTelemetryTone.Success or SimpleTelemetryTone.Error)
        {
            return release.Tone;
        }

        if (pit.PitRepairLeftSeconds is > 0d || pit.PitOptRepairLeftSeconds is > 0d)
        {
            return SimpleTelemetryTone.Warning;
        }

        if (pit.PlayerCarInPitStall || pit.PitstopActive || pit.OnPitRoad || pit.TeamOnPitRoad == true)
        {
            return SimpleTelemetryTone.Info;
        }

        return HasRequestedService(pit) ? SimpleTelemetryTone.Success : SimpleTelemetryTone.Normal;
    }

    private static SimpleTelemetryTone RepairTone(LiveFuelPitModel pit)
    {
        return pit.PitRepairLeftSeconds is > 0d || pit.PitOptRepairLeftSeconds is > 0d
            ? SimpleTelemetryTone.Warning
            : SimpleTelemetryTone.Normal;
    }

    private static bool HasRequestedService(LiveFuelPitModel pit)
    {
        return pit.PitServiceFlags is { } flags && flags != 0
            || pit.PitServiceFuelLiters is > 0d;
    }

    private static PitReleaseState BuildReleaseState(LiveFuelPitModel pit)
    {
        if (PitServiceStatusFormatter.IsError(pit.PitServiceStatus))
        {
            return new PitReleaseState(
                PitReleaseKind.Hold,
                $"RED - {PitServiceStatusFormatter.Format(pit.PitServiceStatus)}",
                SimpleTelemetryTone.Error);
        }

        if (PitServiceStatusFormatter.IsComplete(pit.PitServiceStatus))
        {
            return new PitReleaseState(
                PitReleaseKind.Go,
                "GREEN - go",
                SimpleTelemetryTone.Success);
        }

        if (PitServiceStatusFormatter.IsInProgress(pit.PitServiceStatus) || pit.PitstopActive)
        {
            return new PitReleaseState(
                PitReleaseKind.Hold,
                "RED - service active",
                SimpleTelemetryTone.Error);
        }

        if (pit.PlayerCarInPitStall && HasBlockingRepair(pit))
        {
            return new PitReleaseState(
                PitReleaseKind.Hold,
                "RED - repair active",
                SimpleTelemetryTone.Error);
        }

        if (pit.PlayerCarInPitStall)
        {
            return new PitReleaseState(
                PitReleaseKind.Go,
                pit.PitServiceStatus is null ? "GREEN - go (inferred)" : "GREEN - go",
                SimpleTelemetryTone.Success);
        }

        if (pit.OnPitRoad || pit.TeamOnPitRoad == true)
        {
            return new PitReleaseState(
                PitReleaseKind.Pending,
                "RED - not in stall",
                SimpleTelemetryTone.Warning);
        }

        return HasRequestedService(pit)
            ? new PitReleaseState(
                PitReleaseKind.Pending,
                "armed",
                SimpleTelemetryTone.Normal)
            : new PitReleaseState(
                PitReleaseKind.Pending,
                "--",
                SimpleTelemetryTone.Waiting);
    }

    private static bool HasBlockingRepair(LiveFuelPitModel pit)
    {
        return pit.PitRepairLeftSeconds is > 0d || pit.PitOptRepairLeftSeconds is > 0d;
    }

    private static int TireServiceCount(int? flags)
    {
        if (flags is null)
        {
            return 0;
        }

        var tireFlags = flags.Value & 0x0f;
        var count = 0;
        for (var bit = 1; bit <= 0x08; bit <<= 1)
        {
            if ((tireFlags & bit) != 0)
            {
                count++;
            }
        }

        return count;
    }

    private sealed record PitFlagLabel(int Bit, string Label);

    private enum PitReleaseKind
    {
        Pending,
        Hold,
        Go
    }

    private sealed record PitReleaseState(
        PitReleaseKind Kind,
        string Value,
        SimpleTelemetryTone Tone);
}
