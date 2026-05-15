using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.Core.Overlays;

internal sealed record LiveLocalStrategyContextSnapshot(
    bool IsAvailable,
    string Reason,
    string StatusText);

internal static class LiveLocalStrategyContext
{
    public const string FuelWaitingStatus = "waiting for local fuel context";
    public const string PitServiceWaitingStatus = "waiting for local pit-service context";
    public const string LocalInCarWaitingStatus = "waiting for local in-car context";
    public const string LocalInCarOrPitWaitingStatus = "waiting for local in-car or pit context";

    public static LiveLocalStrategyContextSnapshot ForFuelCalculator(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now)
    {
        return Evaluate(snapshot, now, FuelWaitingStatus);
    }

    public static LiveLocalStrategyContextSnapshot ForPitService(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now)
    {
        return Evaluate(snapshot, now, PitServiceWaitingStatus, allowPitContext: true);
    }

    public static LiveLocalStrategyContextSnapshot ForRequirement(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        OverlayContextRequirement requirement)
    {
        return requirement switch
        {
            OverlayContextRequirement.AnyTelemetry => new LiveLocalStrategyContextSnapshot(
                IsAvailable: true,
                Reason: "not_required",
                StatusText: "live"),
            OverlayContextRequirement.LocalPlayerInCar => Evaluate(
                snapshot,
                now,
                LocalInCarWaitingStatus,
                allowPitContext: false),
            OverlayContextRequirement.LocalPlayerInCarOrPit => Evaluate(
                snapshot,
                now,
                LocalInCarOrPitWaitingStatus,
                allowPitContext: true),
            _ => Unavailable("unknown_context_requirement", LocalInCarWaitingStatus)
        };
    }

    private static LiveLocalStrategyContextSnapshot Evaluate(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        string localWaitingStatus,
        bool allowPitContext = true)
    {
        var telemetryAvailability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);
        if (!telemetryAvailability.IsAvailable)
        {
            return Unavailable(ReasonCode(telemetryAvailability.Reason), telemetryAvailability.StatusText);
        }

        var playerCarIdx = ValidCarIdx(
            snapshot.Models.DriverDirectory.PlayerCarIdx
            ?? snapshot.LatestSample?.PlayerCarIdx);
        if (playerCarIdx is null)
        {
            return Unavailable("player_car_unavailable", localWaitingStatus);
        }

        var focusCarIdx = ValidCarIdx(
            snapshot.Models.DriverDirectory.FocusCarIdx
            ?? snapshot.LatestSample?.FocusCarIdx);
        if (focusCarIdx is null)
        {
            return Unavailable("focus_unavailable", localWaitingStatus);
        }

        if (focusCarIdx != playerCarIdx)
        {
            return Unavailable("focus_on_another_car", localWaitingStatus);
        }

        if (IsGarageContext(snapshot))
        {
            return Unavailable("garage", localWaitingStatus);
        }

        if (!IsLocalActiveContext(snapshot, allowPitContext))
        {
            return Unavailable("not_in_car", localWaitingStatus);
        }

        return new LiveLocalStrategyContextSnapshot(
            IsAvailable: true,
            Reason: "available",
            StatusText: "live");
    }

    private static bool IsGarageContext(LiveTelemetrySnapshot snapshot)
    {
        var race = snapshot.Models.RaceEvents;
        var sample = snapshot.LatestSample;
        return (race.HasData && (race.IsInGarage || race.IsGarageVisible))
            || sample?.IsInGarage == true
            || sample?.IsGarageVisible == true;
    }

    private static bool IsLocalActiveContext(LiveTelemetrySnapshot snapshot, bool allowPitContext)
    {
        var race = snapshot.Models.RaceEvents;
        var sample = snapshot.LatestSample;
        if (IsPitContext(snapshot))
        {
            return allowPitContext;
        }

        if ((race.HasData && race.IsOnTrack) || sample?.IsOnTrack == true)
        {
            return true;
        }

        return false;
    }

    private static bool IsPitContext(LiveTelemetrySnapshot snapshot)
    {
        var reference = snapshot.Models.Reference;
        var race = snapshot.Models.RaceEvents;
        var pit = snapshot.Models.FuelPit;
        var sample = snapshot.LatestSample;
        return (race.HasData && race.OnPitRoad)
            || reference.OnPitRoad == true
            || reference.PlayerOnPitRoad == true
            || reference.PlayerCarInPitStall
            || IsPitRoadTrackSurface(reference.TrackSurface)
            || IsPitRoadTrackSurface(reference.PlayerTrackSurface)
            || pit.OnPitRoad
            || pit.PitstopActive
            || pit.PlayerCarInPitStall
            || pit.TeamOnPitRoad == true
            || sample?.OnPitRoad == true
            || sample?.PitstopActive == true
            || sample?.PlayerCarInPitStall == true
            || sample?.TeamOnPitRoad == true
            || sample?.FocusOnPitRoad == true
            || IsPitRoadTrackSurface(sample?.FocusTrackSurface)
            || IsPitRoadTrackSurface(sample?.PlayerTrackSurface);
    }

    private static bool IsPitRoadTrackSurface(int? trackSurface)
    {
        return trackSurface is 1 or 2;
    }

    private static int? ValidCarIdx(int? carIdx)
    {
        return carIdx is >= 0 and < 64 ? carIdx : null;
    }

    private static string ReasonCode(OverlayAvailabilityReason reason)
    {
        return reason switch
        {
            OverlayAvailabilityReason.Disconnected => "disconnected",
            OverlayAvailabilityReason.WaitingForTelemetry => "waiting_for_telemetry",
            OverlayAvailabilityReason.StaleTelemetry => "stale_telemetry",
            OverlayAvailabilityReason.HiddenForSession => "hidden_for_session",
            OverlayAvailabilityReason.NotInCar => "not_in_car",
            OverlayAvailabilityReason.NoData => "no_data",
            OverlayAvailabilityReason.Error => "error",
            _ => "available"
        };
    }

    private static LiveLocalStrategyContextSnapshot Unavailable(string reason, string statusText)
    {
        return new LiveLocalStrategyContextSnapshot(
            IsAvailable: false,
            Reason: reason,
            StatusText: statusText);
    }
}
