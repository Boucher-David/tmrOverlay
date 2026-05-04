using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.App.Overlays.Standings;

internal sealed record StandingsOverlayViewModel(
    string Status,
    string Source,
    IReadOnlyList<StandingsOverlayRowViewModel> Rows)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(1.5d);

    public static StandingsOverlayViewModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        int maximumRows = 8)
    {
        if (!snapshot.IsConnected)
        {
            return Waiting("waiting for iRacing");
        }

        if (!snapshot.IsCollecting)
        {
            return Waiting("waiting for telemetry");
        }

        if (snapshot.LastUpdatedAtUtc is null || now - snapshot.LastUpdatedAtUtc.Value > StaleAfter)
        {
            return Waiting("waiting for fresh timing");
        }

        var timing = snapshot.Models.Timing;
        if (!timing.HasData)
        {
            return Waiting("waiting for timing");
        }

        var referenceCarIdx = timing.FocusRow?.CarIdx
            ?? timing.PlayerRow?.CarIdx
            ?? timing.FocusCarIdx
            ?? timing.PlayerCarIdx
            ?? snapshot.Models.DriverDirectory.FocusCarIdx
            ?? snapshot.Models.DriverDirectory.PlayerCarIdx;

        var candidateRows = PreferredRows(timing)
            .Where(row => row.HasTiming || row.OverallPosition is not null || row.ClassPosition is not null)
            .GroupBy(row => row.CarIdx)
            .Select(group => group
                .OrderByDescending(row => row.CarIdx == referenceCarIdx)
                .ThenBy(row => row.ClassPosition ?? int.MaxValue)
                .ThenBy(row => row.OverallPosition ?? int.MaxValue)
                .First())
            .OrderBy(row => row.ClassPosition ?? int.MaxValue)
            .ThenBy(row => row.OverallPosition ?? int.MaxValue)
            .ThenBy(row => row.GapSecondsToClassLeader ?? double.MaxValue)
            .ThenBy(row => row.GapLapsToClassLeader ?? double.MaxValue)
            .ThenBy(row => row.CarIdx)
            .Take(Math.Clamp(maximumRows, 1, 20))
            .Select(row => ToRow(row, referenceCarIdx))
            .ToArray();

        if (candidateRows.Length == 0)
        {
            return Waiting("waiting for timing rows");
        }

        var reference = candidateRows.FirstOrDefault(row => row.IsReference);
        var status = reference?.ClassPosition is { Length: > 1 } classPosition
            ? $"{classPosition} - {candidateRows.Length} rows"
            : $"{candidateRows.Length} rows";
        return new StandingsOverlayViewModel(
            status,
            SourceText(timing),
            candidateRows);
    }

    private static IEnumerable<LiveTimingRow> PreferredRows(LiveTimingModel timing)
    {
        if (timing.ClassRows.Count > 0)
        {
            return timing.ClassRows;
        }

        if (timing.OverallRows.Count > 0)
        {
            return timing.OverallRows;
        }

        return new[]
        {
            timing.FocusRow,
            timing.PlayerRow
        }
            .Where(row => row is not null)
            .Select(row => row!);
    }

    private static StandingsOverlayRowViewModel ToRow(LiveTimingRow row, int? referenceCarIdx)
    {
        var isReference = referenceCarIdx is not null && row.CarIdx == referenceCarIdx;
        return new StandingsOverlayRowViewModel(
            ClassPosition: row.ClassPosition is { } classPosition ? $"C{classPosition}" : "--",
            CarNumber: FormatCarNumber(row),
            Driver: ShortDriverName(row.DriverName, row.TeamName, row.CarIdx),
            Gap: FormatGap(row),
            Interval: FormatInterval(row, isReference),
            Pit: row.OnPitRoad == true ? "IN" : string.Empty,
            IsReference: isReference,
            IsLeader: row.IsClassLeader,
            CarClassColorHex: row.CarClassColorHex);
    }

    private static string SourceText(LiveTimingModel timing)
    {
        if (timing.Quality == LiveModelQuality.Reliable)
        {
            return "source: live timing telemetry";
        }

        if (timing.Quality == LiveModelQuality.Inferred)
        {
            return "source: inferred timing";
        }

        return "source: partial timing";
    }

    private static string FormatCarNumber(LiveTimingRow row)
    {
        return string.IsNullOrWhiteSpace(row.CarNumber)
            ? $"#{row.CarIdx}"
            : $"#{row.CarNumber.Trim().TrimStart('#')}";
    }

    private static string ShortDriverName(string? driverName, string? teamName, int carIdx)
    {
        var name = FirstNonEmpty(driverName, teamName) ?? $"Car {carIdx}";
        name = name.Trim();
        return name.Length <= 24 ? name : $"{name[..21]}...";
    }

    private static string FormatGap(LiveTimingRow row)
    {
        if (row.IsClassLeader)
        {
            return "Leader";
        }

        if (row.GapSecondsToClassLeader is { } seconds && IsFinite(seconds))
        {
            return $"+{seconds:0.0}";
        }

        if (row.GapLapsToClassLeader is { } laps && IsFinite(laps))
        {
            return $"+{laps:0.0}L";
        }

        if (row.F2TimeSeconds is { } f2 && IsFinite(f2))
        {
            return $"{f2:0.0}";
        }

        return "--";
    }

    private static string FormatInterval(LiveTimingRow row, bool isReference)
    {
        if (isReference)
        {
            return "0.0";
        }

        if (row.DeltaSecondsToFocus is { } delta && IsFinite(delta))
        {
            return delta > 0d ? $"+{delta:0.0}" : $"{delta:0.0}";
        }

        return "--";
    }

    private static StandingsOverlayViewModel Waiting(string status)
    {
        return new StandingsOverlayViewModel(status, "source: waiting", []);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

internal sealed record StandingsOverlayRowViewModel(
    string ClassPosition,
    string CarNumber,
    string Driver,
    string Gap,
    string Interval,
    string Pit,
    bool IsReference,
    bool IsLeader,
    string? CarClassColorHex);
