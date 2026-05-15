using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Settings;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class OverlayAvailabilityEvaluatorTests
{
    [Fact]
    public void FromSnapshot_ReturnsDisconnectedWhenIRacingIsUnavailable()
    {
        var now = DateTimeOffset.UtcNow;

        var availability = OverlayAvailabilityEvaluator.FromSnapshot(LiveTelemetrySnapshot.Empty, now);

        Assert.False(availability.IsAvailable);
        Assert.Equal(OverlayAvailabilityReason.Disconnected, availability.Reason);
        Assert.Equal("waiting for iRacing", availability.StatusText);
    }

    [Fact]
    public void FromSnapshot_ReturnsStaleWhenLastTelemetryFrameIsOld()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now.AddSeconds(-3)
        };

        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);

        Assert.False(availability.IsAvailable);
        Assert.Equal(OverlayAvailabilityReason.StaleTelemetry, availability.Reason);
        Assert.Equal("waiting for fresh telemetry", availability.StatusText);
    }

    [Fact]
    public void FromSnapshot_ReturnsAvailableForFreshTelemetry()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now.AddMilliseconds(-250)
        };

        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);

        Assert.True(availability.IsAvailable);
        Assert.True(availability.IsFresh);
        Assert.Equal(OverlayAvailabilityReason.Available, availability.Reason);
        Assert.Equal("live", availability.StatusText);
    }

    [Fact]
    public void CurrentSessionKind_PrefersPromotedSessionModel()
    {
        var snapshot = LiveTelemetrySnapshot.Empty with
        {
            Context = new HistoricalSessionContext
            {
                Car = new HistoricalCarIdentity(),
                Track = new HistoricalTrackIdentity(),
                Session = new HistoricalSessionIdentity { SessionType = "Practice" },
                Conditions = new HistoricalSessionInfoConditions()
            },
            Models = LiveRaceModels.Empty with
            {
                Session = LiveSessionModel.Empty with
                {
                    HasData = true,
                    SessionType = "Race"
                }
            }
        };

        Assert.Equal(OverlaySessionKind.Race, OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot));
    }

    [Fact]
    public void IsAllowedForSession_IgnoresLegacyOverlaySessionSettings()
    {
        var settings = new OverlaySettings
        {
            Id = "test",
            ShowInTest = true,
            ShowInPractice = false,
            ShowInRace = true
        };

        Assert.Equal(OverlaySessionKind.Practice, OverlayAvailabilityEvaluator.ClassifySession("Test"));
        Assert.True(OverlayAvailabilityEvaluator.IsAllowedForSession(settings, OverlaySessionKind.Test));
        Assert.True(OverlayAvailabilityEvaluator.IsAllowedForSession(settings, OverlaySessionKind.Practice));
        Assert.True(OverlayAvailabilityEvaluator.IsAllowedForSession(settings, OverlaySessionKind.Race));
    }

    [Fact]
    public void OverlayChromeSettings_HonorsSessionScopedHeaderAndFooterItems()
    {
        var settings = new OverlaySettings { Id = "relative" };
        settings.SetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusTest, true);
        settings.SetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusPractice, false);
        settings.SetBooleanOption(OverlayOptionKeys.ChromeFooterSourceRace, false);
        var test = SnapshotForSession("Test");
        var practice = SnapshotForSession("Practice");
        var race = SnapshotForSession("Race");

        Assert.False(OverlayChromeSettings.ShowHeaderStatus(settings, test));
        Assert.False(OverlayChromeSettings.ShowHeaderStatus(settings, practice));
        Assert.True(OverlayChromeSettings.ShowFooterSource(settings, practice));
        Assert.True(OverlayChromeSettings.ShowHeaderStatus(settings, race));
        Assert.False(OverlayChromeSettings.ShowFooterSource(settings, race));
    }

    [Fact]
    public void LiveLocalStrategyContext_WaitsWhenFocusIsAnotherCar()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LocalStrategySnapshot(now, playerCarIdx: 10, focusCarIdx: 42);

        var fuel = LiveLocalStrategyContext.ForFuelCalculator(snapshot, now);
        var pitService = LiveLocalStrategyContext.ForPitService(snapshot, now);

        Assert.False(fuel.IsAvailable);
        Assert.Equal("focus_on_another_car", fuel.Reason);
        Assert.Equal(LiveLocalStrategyContext.FuelWaitingStatus, fuel.StatusText);
        Assert.False(pitService.IsAvailable);
        Assert.Equal("focus_on_another_car", pitService.Reason);
        Assert.Equal(LiveLocalStrategyContext.PitServiceWaitingStatus, pitService.StatusText);
    }

    [Fact]
    public void LiveLocalStrategyContext_AllowsLocalPitRoadContext()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LocalStrategySnapshot(
            now,
            playerCarIdx: 10,
            focusCarIdx: 10,
            isOnTrack: false,
            isInGarage: false,
            onPitRoad: true);

        var pitService = LiveLocalStrategyContext.ForPitService(snapshot, now);

        Assert.True(pitService.IsAvailable);
        Assert.Equal("available", pitService.Reason);
    }

    [Fact]
    public void LiveLocalStrategyContext_RequirementDistinguishesInCarFromPitAllowed()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LocalStrategySnapshot(
            now,
            playerCarIdx: 10,
            focusCarIdx: 10,
            isOnTrack: false,
            isInGarage: false,
            onPitRoad: true);

        var inCarOnly = LiveLocalStrategyContext.ForRequirement(
            snapshot,
            now,
            OverlayContextRequirement.LocalPlayerInCar);
        var inCarOrPit = LiveLocalStrategyContext.ForRequirement(
            snapshot,
            now,
            OverlayContextRequirement.LocalPlayerInCarOrPit);

        Assert.False(inCarOnly.IsAvailable);
        Assert.Equal("not_in_car", inCarOnly.Reason);
        Assert.Equal(LiveLocalStrategyContext.LocalInCarWaitingStatus, inCarOnly.StatusText);
        Assert.True(inCarOrPit.IsAvailable);
        Assert.Equal("available", inCarOrPit.Reason);
    }

    [Fact]
    public void LiveLocalStrategyContext_InCarRequirementRejectsPitRoadEvenWhenOnTrackIsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LocalStrategySnapshot(
            now,
            playerCarIdx: 10,
            focusCarIdx: 10,
            isOnTrack: true,
            isInGarage: false,
            onPitRoad: true);

        var inCarOnly = LiveLocalStrategyContext.ForRequirement(
            snapshot,
            now,
            OverlayContextRequirement.LocalPlayerInCar);
        var inCarOrPit = LiveLocalStrategyContext.ForRequirement(
            snapshot,
            now,
            OverlayContextRequirement.LocalPlayerInCarOrPit);

        Assert.False(inCarOnly.IsAvailable);
        Assert.Equal("not_in_car", inCarOnly.Reason);
        Assert.True(inCarOrPit.IsAvailable);
        Assert.Equal("available", inCarOrPit.Reason);
    }

    [Fact]
    public void LiveLocalStrategyContext_WaitsInGarageEvenWithPlayerFocus()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = LocalStrategySnapshot(
            now,
            playerCarIdx: 10,
            focusCarIdx: 10,
            isOnTrack: false,
            isInGarage: true,
            onPitRoad: false);

        var fuel = LiveLocalStrategyContext.ForFuelCalculator(snapshot, now);

        Assert.False(fuel.IsAvailable);
        Assert.Equal("garage", fuel.Reason);
        Assert.Equal(LiveLocalStrategyContext.FuelWaitingStatus, fuel.StatusText);
    }

    private static LiveTelemetrySnapshot SnapshotForSession(string sessionType)
    {
        return LiveTelemetrySnapshot.Empty with
        {
            Models = LiveRaceModels.Empty with
            {
                Session = LiveSessionModel.Empty with
                {
                    HasData = true,
                    SessionType = sessionType
                }
            }
        };
    }

    private static LiveTelemetrySnapshot LocalStrategySnapshot(
        DateTimeOffset now,
        int? playerCarIdx,
        int? focusCarIdx,
        bool isOnTrack = true,
        bool isInGarage = false,
        bool onPitRoad = false)
    {
        return LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now,
            Models = LiveRaceModels.Empty with
            {
                DriverDirectory = LiveDriverDirectoryModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Reliable,
                    PlayerCarIdx = playerCarIdx,
                    FocusCarIdx = focusCarIdx
                },
                RaceEvents = LiveRaceEventModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Reliable,
                    IsOnTrack = isOnTrack,
                    IsInGarage = isInGarage,
                    OnPitRoad = onPitRoad
                },
                FuelPit = LiveFuelPitModel.Empty with
                {
                    HasData = true,
                    Quality = LiveModelQuality.Reliable,
                    OnPitRoad = onPitRoad
                }
            }
        };
    }
}
