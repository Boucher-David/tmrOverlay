using System.Globalization;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceOverlayViewModel
{
    private static readonly TimeSpan ChangeHighlightDuration = TimeSpan.FromSeconds(30);

    public static Func<LiveTelemetrySnapshot, DateTimeOffset, string, SimpleTelemetryOverlayViewModel> CreateBuilder()
    {
        var changeTracker = new ChangeTracker(ChangeHighlightDuration);
        return (snapshot, now, unitSystem) => From(snapshot, now, unitSystem, changeTracker);
    }

    public static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem)
    {
        return From(snapshot, now, unitSystem, changeTracker: null);
    }

    private static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem,
        ChangeTracker? changeTracker)
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

        return new SimpleTelemetryOverlayViewModel(
            Title: "Pit Service",
            Status: status,
            Source: BuildSource(),
            Tone: tone,
            Rows: rows);
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
        var sets = pit.Tires.TireSetsUsed is { } value && value >= 0
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
