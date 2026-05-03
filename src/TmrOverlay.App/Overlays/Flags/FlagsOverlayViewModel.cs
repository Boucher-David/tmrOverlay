using System.Globalization;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Flags;

internal static class FlagsOverlayViewModel
{
    private static readonly FlagLabel[] FlagLabels =
    [
        new(0x00000001, "checkered"),
        new(0x00000002, "white"),
        new(0x00000004, "green"),
        new(0x00000008, "yellow"),
        new(0x00000010, "red"),
        new(0x00000020, "blue"),
        new(0x00000040, "debris"),
        new(0x00000080, "crossed"),
        new(0x00000100, "waving yellow"),
        new(0x00000200, "one to green"),
        new(0x00000400, "green held"),
        new(0x00000800, "ten to go"),
        new(0x00001000, "five to go"),
        new(0x00002000, "random waving"),
        new(0x00004000, "caution"),
        new(0x00008000, "waving caution"),
        new(0x00010000, "black"),
        new(0x00020000, "disqualify"),
        new(0x00040000, "service"),
        new(0x00080000, "furled"),
        new(0x00100000, "repair"),
        new(0x10000000, "start hidden"),
        new(0x20000000, "start ready"),
        new(0x40000000, "start set"),
        new(unchecked((int)0x80000000), "start go")
    ];

    public static SimpleTelemetryOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string unitSystem)
    {
        if (!SimpleTelemetryOverlayViewModel.IsFresh(snapshot, now, out var waitingStatus))
        {
            return SimpleTelemetryOverlayViewModel.Waiting("Flags", waitingStatus);
        }

        var session = snapshot.Models.Session;
        if (!session.HasData && session.SessionFlags is null && session.SessionState is null)
        {
            return SimpleTelemetryOverlayViewModel.Waiting("Flags", "waiting for session state");
        }

        var flags = session.SessionFlags;
        var activeFlags = FormatFlags(flags);
        var sessionState = FormatSessionState(session.SessionState);
        var tone = ToneFor(flags, session.SessionState);
        var status = flags is null
            ? sessionState
            : PrimaryFlagLabel(flags.Value) ?? sessionState;
        var rows = new[]
        {
            new SimpleTelemetryRowViewModel("State", sessionState, tone),
            new SimpleTelemetryRowViewModel("Flags", activeFlags, tone),
            new SimpleTelemetryRowViewModel("Raw", FormatRawFlags(flags)),
            new SimpleTelemetryRowViewModel("Time left", SimpleTelemetryOverlayViewModel.FormatDuration(session.SessionTimeRemainSeconds, compact: true)),
            new SimpleTelemetryRowViewModel("Laps left", FormatLaps(session.SessionLapsRemain, session.RaceLaps))
        };

        return new SimpleTelemetryOverlayViewModel(
            Title: "Flags",
            Status: status,
            Source: "source: session flags telemetry",
            Tone: tone,
            Rows: rows);
    }

    private static string FormatSessionState(int? state)
    {
        return state switch
        {
            0 => "invalid (0)",
            1 => "get in car (1)",
            2 => "warmup (2)",
            3 => "parade laps (3)",
            4 => "racing (4)",
            5 => "checkered (5)",
            6 => "cool down (6)",
            null => "--",
            _ => $"state {state.Value.ToString(CultureInfo.InvariantCulture)}"
        };
    }

    private static string FormatFlags(int? flags)
    {
        if (flags is null)
        {
            return "--";
        }

        var active = FlagLabels
            .Where(label => HasFlag(flags.Value, label.Bit))
            .Select(label => label.Label)
            .ToArray();
        return active.Length == 0 ? "none" : string.Join(", ", active.Take(3)) + (active.Length > 3 ? $" +{active.Length - 3}" : string.Empty);
    }

    private static string FormatRawFlags(int? flags)
    {
        return flags is { } value
            ? $"0x{unchecked((uint)value).ToString("X8", CultureInfo.InvariantCulture)}"
            : "--";
    }

    private static string FormatLaps(int? lapsRemain, int? raceLaps)
    {
        if (lapsRemain is null && raceLaps is null)
        {
            return "--";
        }

        var remain = lapsRemain is { } left && left >= 0 ? left.ToString(CultureInfo.InvariantCulture) : "--";
        var total = raceLaps is { } laps && laps > 0 ? laps.ToString(CultureInfo.InvariantCulture) : "--";
        return $"{remain} / {total}";
    }

    private static string? PrimaryFlagLabel(int flags)
    {
        if (HasFlag(flags, 0x00010000) || HasFlag(flags, 0x00100000))
        {
            return "service flag";
        }

        if (HasFlag(flags, 0x00020000))
        {
            return "disqualified";
        }

        if (HasFlag(flags, 0x00000010))
        {
            return "red flag";
        }

        if (HasFlag(flags, 0x00008000) || HasFlag(flags, 0x00004000) || HasFlag(flags, 0x00000100) || HasFlag(flags, 0x00000008))
        {
            return "yellow/caution";
        }

        if (HasFlag(flags, 0x00000020))
        {
            return "blue flag";
        }

        if (HasFlag(flags, 0x00000001))
        {
            return "checkered";
        }

        if (HasFlag(flags, 0x00000002))
        {
            return "white flag";
        }

        return HasFlag(flags, 0x00000004) ? "green" : null;
    }

    private static SimpleTelemetryTone ToneFor(int? flags, int? sessionState)
    {
        if (flags is { } value)
        {
            if (HasFlag(value, 0x00010000) || HasFlag(value, 0x00100000) || HasFlag(value, 0x00020000) || HasFlag(value, 0x00000010))
            {
                return SimpleTelemetryTone.Error;
            }

            if (HasFlag(value, 0x00008000) || HasFlag(value, 0x00004000) || HasFlag(value, 0x00000100) || HasFlag(value, 0x00000008))
            {
                return SimpleTelemetryTone.Warning;
            }

            if (HasFlag(value, 0x00000001))
            {
                return SimpleTelemetryTone.Info;
            }

            if (HasFlag(value, 0x00000004))
            {
                return SimpleTelemetryTone.Success;
            }
        }

        return sessionState == 4 ? SimpleTelemetryTone.Success : SimpleTelemetryTone.Info;
    }

    private static bool HasFlag(int flags, int bit)
    {
        return (flags & bit) != 0;
    }

    private sealed record FlagLabel(int Bit, string Label);
}
