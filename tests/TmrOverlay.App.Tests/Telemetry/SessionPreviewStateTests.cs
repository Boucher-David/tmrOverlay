using TmrOverlay.App.Events;
using TmrOverlay.App.Storage;
using TmrOverlay.App.Telemetry;
using TmrOverlay.Core.Overlays;
using Xunit;

namespace TmrOverlay.App.Tests.Telemetry;

public sealed class SessionPreviewStateTests
{
    [Theory]
    [InlineData(nameof(OverlaySessionKind.Practice))]
    [InlineData(nameof(OverlaySessionKind.Qualifying))]
    [InlineData(nameof(OverlaySessionKind.Race))]
    public void TryBuildSnapshot_ReturnsFreshSessionFixtureForSelectedMode(string modeName)
    {
        var state = CreateState();
        var now = DateTimeOffset.Parse("2026-05-10T15:30:00Z");
        var mode = Enum.Parse<OverlaySessionKind>(modeName);

        state.SetMode(mode);
        var snapshot = state.TryBuildSnapshot(now);
        Assert.NotNull(snapshot);
        var availability = OverlayAvailabilityEvaluator.FromSnapshot(snapshot, now);

        Assert.True(snapshot.IsConnected);
        Assert.True(snapshot.IsCollecting);
        Assert.Equal(now, snapshot.LastUpdatedAtUtc);
        Assert.StartsWith("session-preview-", snapshot.SourceId, StringComparison.Ordinal);
        Assert.True(availability.IsAvailable);
        Assert.Equal(mode, OverlayAvailabilityEvaluator.CurrentSessionKind(snapshot));
        Assert.True(snapshot.Models.Session.HasData);
        Assert.True(snapshot.Models.Scoring.HasData);
        Assert.True(snapshot.Models.Relative.HasData);
    }

    [Fact]
    public void SetMode_NullDisablesPreviewAndDiagnosticsKeepVisibilityGuardrails()
    {
        var state = CreateState();

        Assert.Null(state.TryBuildSnapshot(DateTimeOffset.UtcNow));

        state.SetMode(OverlaySessionKind.Race);
        var active = state.Snapshot();
        Assert.True(active.Active);
        Assert.Equal(nameof(OverlaySessionKind.Race), active.Mode);
        Assert.True(active.UsesNormalOverlayVisibility);
        Assert.False(active.OverridesOverlayEnabledState);
        Assert.False(active.OverridesOverlaySessionFilters);

        state.SetMode(null);
        var inactive = state.Snapshot();
        Assert.False(inactive.Active);
        Assert.Null(inactive.Mode);
        Assert.Null(state.TryBuildSnapshot(DateTimeOffset.UtcNow));
        Assert.True(inactive.UsesNormalOverlayVisibility);
        Assert.False(inactive.OverridesOverlayEnabledState);
        Assert.False(inactive.OverridesOverlaySessionFilters);
    }

    [Fact]
    public void TryBuildSnapshot_UsesMinedExtremeFixturesInsideSdkCarIndexRange()
    {
        var state = CreateState();
        var now = DateTimeOffset.Parse("2026-05-10T15:30:00Z");

        state.SetMode(OverlaySessionKind.Race);
        var snapshot = state.TryBuildSnapshot(now);
        Assert.NotNull(snapshot);
        snapshot = snapshot!;

        Assert.Equal("nurburgring combinedshortb", snapshot.Context.Track.TrackName);
        Assert.Equal("Gesamtstrecke 24h", snapshot.Context.Track.TrackDisplayName);
        Assert.Equal("Aston Martin Vantage GT3 EVO", snapshot.Context.Car.CarScreenName);
        Assert.Contains(snapshot.Context.Drivers, driver => string.Equals(driver.UserName, "Kauan Vigliazzi Teixeira Lemos", StringComparison.Ordinal));
        Assert.Contains(snapshot.Context.Drivers, driver => string.Equals(driver.TeamName, "Gladius Competitions Powered by ATS Esport", StringComparison.Ordinal));
        Assert.InRange(snapshot.LatestSample.FuelLevelLiters, 104.93d, 104.95d);
        Assert.InRange(snapshot.LatestSample.SpeedMetersPerSecond, 77.88d, 77.90d);
        Assert.Equal(127, snapshot.LatestSample.PitServiceFlags);
        Assert.Equal(32767, snapshot.LatestSample.SessionLapsTotal);

        var allCars = snapshot.LatestSample.AllCars;
        Assert.NotNull(allCars);
        Assert.All(snapshot.Context.Drivers, driver => Assert.True(driver.CarIdx is >= 0 and <= 63));
        Assert.All(allCars, car => Assert.True(car.CarIdx is >= 0 and <= 63));
    }

    private static SessionPreviewState CreateState()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmr-session-preview-tests", Guid.NewGuid().ToString("N"));
        return new SessionPreviewState(new AppEventRecorder(new AppStorageOptions
        {
            AppDataRoot = root,
            CaptureRoot = Path.Combine(root, "captures"),
            UserHistoryRoot = Path.Combine(root, "history", "user"),
            BaselineHistoryRoot = Path.Combine(root, "history", "baseline"),
            LogsRoot = Path.Combine(root, "logs"),
            SettingsRoot = Path.Combine(root, "settings"),
            DiagnosticsRoot = Path.Combine(root, "diagnostics"),
            TrackMapRoot = Path.Combine(root, "track-maps"),
            EventsRoot = Path.Combine(root, "events"),
            RuntimeStatePath = Path.Combine(root, "runtime-state.json")
        }));
    }
}
