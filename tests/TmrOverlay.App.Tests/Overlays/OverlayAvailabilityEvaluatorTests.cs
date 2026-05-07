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
    public void IsAllowedForSession_HonorsOverlaySessionSettings()
    {
        var settings = new OverlaySettings
        {
            Id = "test",
            ShowInPractice = false,
            ShowInRace = true
        };

        Assert.False(OverlayAvailabilityEvaluator.IsAllowedForSession(settings, OverlaySessionKind.Practice));
        Assert.True(OverlayAvailabilityEvaluator.IsAllowedForSession(settings, OverlaySessionKind.Race));
    }

    [Fact]
    public void OverlayChromeSettings_HonorsSessionScopedHeaderAndFooterItems()
    {
        var settings = new OverlaySettings { Id = "relative" };
        settings.SetBooleanOption(OverlayOptionKeys.ChromeHeaderStatusPractice, false);
        settings.SetBooleanOption(OverlayOptionKeys.ChromeFooterSourceRace, false);
        var practice = SnapshotForSession("Practice");
        var race = SnapshotForSession("Race");

        Assert.False(OverlayChromeSettings.ShowHeaderStatus(settings, practice));
        Assert.True(OverlayChromeSettings.ShowFooterSource(settings, practice));
        Assert.True(OverlayChromeSettings.ShowHeaderStatus(settings, race));
        Assert.False(OverlayChromeSettings.ShowFooterSource(settings, race));
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
}
