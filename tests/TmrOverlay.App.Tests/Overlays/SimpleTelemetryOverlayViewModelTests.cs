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
    public void Flags_ForDisplay_IgnoresBackgroundOnlyFlags()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 4,
                SessionFlags = 0x00040000 | 0x10000000
            }
        });

        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);

        Assert.False(viewModel.HasDisplayFlags);
        Assert.Equal("none", viewModel.Status);
    }

    [Fact]
    public void Flags_ForDisplay_IgnoresSteadyGreenRunning()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 4,
                SessionFlags = 0x00000004
            }
        });

        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);

        Assert.False(viewModel.HasDisplayFlags);
        Assert.Equal("none", viewModel.Status);
    }

    [Fact]
    public void Flags_ForDisplay_ShowsExplicitGreenStartBits()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 4,
                SessionFlags = unchecked((int)0x80000000)
            }
        });

        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);

        Assert.Single(viewModel.Flags);
        Assert.Equal(FlagDisplayKind.Green, viewModel.Flags[0].Kind);
        Assert.Equal("Start", viewModel.Status);
    }

    [Fact]
    public void Flags_ForDisplay_ReturnsMultipleActiveDisplayFlags()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 4,
                SessionFlags = 0x00100000 | 0x00010000 | 0x00000020 | 0x00000002
            }
        });

        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);

        Assert.Equal(
            new[]
            {
                FlagDisplayKind.Meatball,
                FlagDisplayKind.Black,
                FlagDisplayKind.Blue,
                FlagDisplayKind.White
            },
            viewModel.Flags.Select(flag => flag.Kind).ToArray());
        Assert.Equal("Repair + Black + Blue + White", viewModel.Status);
    }

    [Fact]
    public void Flags_ForDisplay_DeduplicatesYellowFamily()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 4,
                SessionFlags = 0x00000008 | 0x00000040 | 0x00004000
            }
        });

        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);

        Assert.Single(viewModel.Flags);
        Assert.Equal(FlagDisplayKind.Caution, viewModel.Flags[0].Kind);
        Assert.Equal("Caution", viewModel.Status);
    }

    [Fact]
    public void Flags_ForDisplay_UsesCheckeredWhenSessionStateCompletes()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionState = 5,
                SessionFlags = 0
            }
        });

        var viewModel = FlagsOverlayViewModel.ForDisplay(snapshot, now);

        Assert.Single(viewModel.Flags);
        Assert.Equal(FlagDisplayKind.Checkered, viewModel.Flags[0].Kind);
        Assert.Equal("Checkered", viewModel.Status);
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
                WindVelocityMetersPerSecond = 4.2d,
                WindDirectionRadians = Math.PI,
                RelativeHumidityPercent = 67d,
                FogLevelPercent = 0d,
                RubberState = "moderate usage"
            }
        });

        var viewModel = SessionWeatherOverlayViewModel.From(snapshot, now, "Imperial");

        Assert.Equal("Race", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Session" && row.Value.Contains("team", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Temps" && row.Value.Contains("70 F", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Surface" && row.Value.Contains("rubber moderate usage", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Wind" && row.Value.Contains("S", StringComparison.Ordinal));
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
    public void SessionWeather_CreateBuilder_HighlightsWeatherRowsAfterChange()
    {
        var now = DateTimeOffset.UtcNow;
        var builder = SessionWeatherOverlayViewModel.CreateBuilder();
        var initial = Snapshot(now, LiveRaceModels.Empty with
        {
            Session = LiveSessionModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                SessionType = "Race"
            },
            Weather = LiveWeatherModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                AirTempC = 20d,
                TrackWetness = 1,
                TrackWetnessLabel = "dry",
                WeatherDeclaredWet = false
            }
        });
        var changed = initial with
        {
            Models = initial.Models with
            {
                Weather = initial.Models.Weather with
                {
                    AirTempC = 22d
                }
            }
        };

        var first = builder(initial, now, "Metric");
        var second = builder(changed, now.AddSeconds(1), "Metric");

        Assert.Contains(first.Rows, row => row.Label == "Surface" && row.Tone == SimpleTelemetryTone.Normal);
        Assert.Contains(second.Rows, row => row.Label == "Surface" && row.Tone == SimpleTelemetryTone.Normal);
        Assert.Contains(second.Rows, row => row.Label == "Temps" && row.Tone == SimpleTelemetryTone.Info);
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
                PitServiceStatus = PitServiceStatusFormatter.InProgress,
                PitServiceFlags = 0x1f,
                PitServiceFuelLiters = 45.5d,
                PitRepairLeftSeconds = 12.2d,
                TireSetsUsed = 2,
                FastRepairUsed = 0,
                TeamFastRepairsUsed = 1
            }
        });

        var viewModel = PitServiceOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("hold", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Release" && row.Value == "RED - service active");
        Assert.Contains(viewModel.Rows, row => row.Label == "Pit status" && row.Value == "in progress");
        Assert.Contains(viewModel.Rows, row => row.Label == "Service" && row.Value.Contains("active", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Fuel request" && row.Value == "45.5 L");
        Assert.Contains(viewModel.Rows, row => row.Label == "Repair" && row.Value.Contains("12s required", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Fast repair" && row.Value == "local 0 | team 1");
    }

    [Fact]
    public void PitService_FromTelemetry_ShowsGreenReleaseWhenServiceComplete()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                PlayerCarInPitStall = true,
                PitstopActive = false,
                PitServiceStatus = PitServiceStatusFormatter.Complete,
                PitServiceFlags = 0x1f,
                PitServiceFuelLiters = 0d
            }
        });

        var viewModel = PitServiceOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("release ready", viewModel.Status);
        Assert.Equal(SimpleTelemetryTone.Success, viewModel.Tone);
        Assert.Contains(viewModel.Rows, row => row.Label == "Release" && row.Value == "GREEN - go");
        Assert.Contains(viewModel.Rows, row => row.Label == "Pit status" && row.Value == "complete");
    }

    [Fact]
    public void PitService_FromTelemetry_TreatsOptionalRepairAsAdvisory()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                PlayerCarInPitStall = true,
                PitstopActive = false,
                PitServiceStatus = PitServiceStatusFormatter.None,
                PitOptRepairLeftSeconds = 18.4d,
                PitServiceFlags = 0
            }
        });

        var viewModel = PitServiceOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("optional repair", viewModel.Status);
        Assert.Equal(SimpleTelemetryTone.Warning, viewModel.Tone);
        Assert.Contains(viewModel.Rows, row => row.Label == "Release" && row.Value == "YELLOW - optional repair");
        Assert.Contains(viewModel.Rows, row => row.Label == "Repair" && row.Value == "18s optional" && row.Tone == SimpleTelemetryTone.Warning);
    }

    [Fact]
    public void PitService_FromTelemetry_HoldsForRequiredRepair()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                PlayerCarInPitStall = true,
                PitstopActive = false,
                PitServiceStatus = PitServiceStatusFormatter.None,
                PitRepairLeftSeconds = 9.6d,
                PitOptRepairLeftSeconds = 18.4d,
                PitServiceFlags = 0
            }
        });

        var viewModel = PitServiceOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("hold", viewModel.Status);
        Assert.Equal(SimpleTelemetryTone.Error, viewModel.Tone);
        Assert.Contains(viewModel.Rows, row => row.Label == "Release" && row.Value == "RED - repair active");
        Assert.Contains(viewModel.Rows, row => row.Label == "Repair" && row.Value.Contains("10s required", StringComparison.Ordinal) && row.Tone == SimpleTelemetryTone.Error);
    }

    [Fact]
    public void PitService_FromTelemetry_ShowsFastRepairSelected()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                PitServiceFlags = 0x40,
                FastRepairUsed = 0,
                TeamFastRepairsUsed = 1
            }
        });

        var viewModel = PitServiceOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("service requested", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Release" && row.Value == "armed");
        Assert.Contains(viewModel.Rows, row => row.Label == "Service" && row.Value == "requested | fast repair");
        Assert.Contains(viewModel.Rows, row => row.Label == "Fast repair" && row.Value == "selected | local 0 | team 1");
    }

    [Fact]
    public void PitService_CreateBuilder_HighlightsChangedRequestValues()
    {
        var now = DateTimeOffset.UtcNow;
        var builder = PitServiceOverlayViewModel.CreateBuilder();
        var initial = Snapshot(now, LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                PitServiceFlags = 0x10,
                PitServiceFuelLiters = 25d,
                FastRepairUsed = 0,
                TeamFastRepairsUsed = 0
            }
        });
        var changed = Snapshot(now.AddSeconds(1), LiveRaceModels.Empty with
        {
            FuelPit = LiveFuelPitModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                PitServiceFlags = 0x1f,
                PitServiceFuelLiters = 45d,
                FastRepairUsed = 0,
                TeamFastRepairsUsed = 0
            }
        });

        var first = builder(initial, now, "Metric");
        var second = builder(changed, now.AddSeconds(1), "Metric");

        Assert.Contains(first.Rows, row => row.Label == "Fuel request" && row.Tone == SimpleTelemetryTone.Normal);
        Assert.Contains(second.Rows, row => row.Label == "Fuel request" && row.Value == "45.0 L" && row.Tone == SimpleTelemetryTone.Info);
        Assert.Contains(second.Rows, row => row.Label == "Tires" && row.Value == "four tires" && row.Tone == SimpleTelemetryTone.Info);
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
                BrakeAbsActive = true,
                EngineWarnings = 0,
                Voltage = 13.8d,
                WaterTempC = 88d,
                OilTempC = 96d,
                OilPressureBar = 5.4d,
                FuelPressureBar = 4.1d
            }
        });

        var viewModel = InputStateOverlayViewModel.From(snapshot, now, "Imperial");

        Assert.Equal("4 | 7250 rpm | ABS", viewModel.Status);
        Assert.Contains(viewModel.Rows, row => row.Label == "Speed" && row.Value == "100 mph");
        Assert.Contains(viewModel.Rows, row => row.Label == "Pedals" && row.Value.Contains("T 75%", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Pedals" && row.Value.Contains("B 10% ABS", StringComparison.Ordinal));
        Assert.Contains(viewModel.Rows, row => row.Label == "Steering" && row.Value == "+30 deg");
    }

    [Fact]
    public void InputState_FromTelemetry_WaitsWhenPlayerIsNotInCar()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(now, LiveRaceModels.Empty with
        {
            RaceEvents = LiveRaceEventModel.Empty with
            {
                HasData = true,
                IsOnTrack = false,
                IsInGarage = true
            },
            Inputs = LiveInputTelemetryModel.Empty with
            {
                HasData = true,
                Quality = LiveModelQuality.Reliable,
                Throttle = 0.75d,
                Brake = 0.1d,
                HasPedalInputs = true
            }
        });

        var viewModel = InputStateOverlayViewModel.From(snapshot, now, "Metric");

        Assert.Equal("waiting for player in car", viewModel.Status);
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
