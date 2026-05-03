using TmrOverlay.App.Overlays.Flags;
using TmrOverlay.App.Overlays.InputState;
using TmrOverlay.App.Overlays.PitService;
using TmrOverlay.App.Overlays.SessionWeather;
using TmrOverlay.App.Overlays.SimpleTelemetry;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class SimpleTelemetryOverlayViewModelTests
{
    [Fact]
    public void Flags_FromTelemetry_LabelsActiveFlagState()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 4,
                SessionFlags = 0x00000004,
                SessionTimeRemainSeconds = 125d,
                SessionLapsRemain = 12,
                RaceLaps = 40
            }
        });

        var viewModel = FlagsOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("green", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Flags" && row.Value == "green");
        Assert.Contains(viewModel.Rows, row => row.Label == "Raw" && row.Value == "0x00000004");
    }

    [Fact]
    public void SessionWeather_FromTelemetry_FormatsSessionAndWeatherRows()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionType = "Race",
                SessionName = "Endurance",
                TeamRacing = true,
                SessionTimeSeconds = 60d,
                SessionTimeRemainSeconds = 3600d,
                SessionLapsRemain = 20,
                RaceLaps = 50,
                TrackDisplayName = "Road Atlanta",
                TrackLengthKm = 4.088d
            },
            Weather = LiveWeatherModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                AirTempC = 21d,
                TrackTempCrewC = 31d,
                TrackWetness = 0,
                TrackWetnessLabel = "dry",
                WeatherDeclaredWet = false,
                WeatherType = "constant",
                SkiesLabel = "partly cloudy",
                PrecipitationPercent = 0d,
                RubberState = "moderate usage"
            }
        });

        var viewModel = SessionWeatherOverlayViewModel.From(snapshot, now, "Imperial");

        Assert.Equal("Race", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Session" && row.Value.Contains("team", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Temps" && row.Value.Contains("70 F", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Surface" && row.Value == "dry");
    }

    [Fact]
    public void SessionWeather_FromTelemetry_TreatsUnknownWetFlagAsNormalTelemetry()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionType = "Practice"
            },
            Weather = LiveWeatherModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                WeatherDeclaredWet = null,
                TrackWetness = null
            }
        });

        var viewModel = SessionWeatherOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("Practice", viewModel.Status);
        Assert.Equal(SimpleTelemetryTone.Normal, viewModel.Tone);
        Assert.Contains(viewModel.Rows, row => row.Label == "Surface" && row.Value == "--");
    }

    [Fact]
    public void PitService_FromTelemetry_FormatsRequestedService()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                OnPitRoad = true,
                PitstopActive = true,
                PitServiceFlags = 0x1f,
                PitServiceFuelLiters = 45.5d,
                PitRepairLeftSeconds = 12.2d,
                TireSetsUsed = 2,
                FastRepairUsed = 0,
                TeamFastRepairsUsed = 1
            }
        });

        var viewModel = PitServiceOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("service active", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Service" && row.Value.Contains("active", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Fuel request" && row.Value == "45.5 L");
        Assert.Contains(viewModel.Rows, row => row.Label == "Repair" && row.Value.Contains("12s required", StringComparison.Ordinal));
    }

    [Fact]
    public void InputState_FromTelemetry_FormatsLocalCarState()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Inputs = LiveInputTelemetryModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SpeedMetersPerSecond = 44.7d,
                Gear = 4,
                Rpm = 7_250d,
                Throttle = 0.75d,
                Brake = 0.1d,
                Clutch = 0d,
                HasPedalInputs = true,
                SteeringWheelAngle = Math.PI / 6d,
                HasSteeringInput = true,
                EngineWarnings = 0,
                Voltage = 13.8d,
                WaterTempC = 88d,
                OilTempC = 96d,
                OilPressureBar = 5.4d,
                FuelPressureBar = 4.1d
            }
        });

        var viewModel = InputStateOverlayViewModel.From(snapshot, now, "Imperial");

        Assert.Equal("4 | 7250 rpm", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Speed" && row.Value == "100 mph");
        Assert.Contains(viewModel.Rows, row => row.Label == "Pedals" && row.Value.Contains("T 75%", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Steering" && row.Value == "+30 deg");
    }

    private static LiveTelemetrySnapshot Snapshot(DateTimeOffset now, LiveRaceModels models)
    {
        return LiveTelemetrySnapshot.Empty with
        {
            IsConnected = true,
            IsCollecting = true,
            LastUpdatedAtUtc = now,
            Sequence = 1,
            Models = models
        };
    }
}
