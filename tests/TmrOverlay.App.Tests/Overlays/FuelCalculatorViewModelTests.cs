using TmrOverlay.App.Overlays.FuelCalculator;
using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.History;
using TmrOverlay.Core.Telemetry.Live;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class FuelCalculatorViewModelTests
{
    [Fact]
    public void From_WhenFuelTelemetryAndHistoryAreUnavailable_HasNoStintRows()
    {
        var live = LiveTelemetrySnapshot.Empty;
        var history = SessionHistoryLookupResult.Empty(live.Combo);
        var strategy = FuelStrategyCalculator.From(live, history);

        var viewModel = FuelCalculatorViewModel.From(
            strategy,
            history,
            showAdvice: true,
            unitSystem: "Metric",
            maximumRows: 6);

        Assert.False(strategy.HasData);
        Assert.Equal("waiting for fuel", viewModel.Status);
        Assert.Empty(viewModel.Rows);
        Assert.Contains("history none", viewModel.Source, StringComparison.Ordinal);
    }
}
