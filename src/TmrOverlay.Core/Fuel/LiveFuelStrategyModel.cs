using TmrOverlay.Core.History;
using TmrOverlay.Core.Overlays;
using TmrOverlay.Core.Telemetry.Live;

namespace TmrOverlay.Core.Fuel;

internal sealed record LiveFuelStrategyModel(
    bool IsAvailable,
    string Status,
    string Reason,
    SessionHistoryLookupResult History,
    FuelStrategySnapshot? Strategy)
{
    public static LiveFuelStrategyModel From(
        LiveTelemetrySnapshot snapshot,
        DateTimeOffset now,
        Func<HistoricalComboIdentity, SessionHistoryLookupResult> lookupHistory)
    {
        var localContext = LiveLocalStrategyContext.ForFuelCalculator(snapshot, now);
        if (!localContext.IsAvailable)
        {
            return new LiveFuelStrategyModel(
                IsAvailable: false,
                Status: localContext.StatusText,
                Reason: localContext.Reason,
                History: SessionHistoryLookupResult.Empty(snapshot.Combo),
                Strategy: null);
        }

        var history = lookupHistory(snapshot.Combo);
        var strategy = FuelStrategyCalculator.From(snapshot, history);
        return new LiveFuelStrategyModel(
            IsAvailable: true,
            Status: strategy.Status,
            Reason: localContext.Reason,
            History: history,
            Strategy: strategy);
    }

    public static LiveFuelStrategyModel From(
        LiveTelemetrySnapshot snapshot,
        SessionHistoryLookupResult history,
        DateTimeOffset now)
    {
        var localContext = LiveLocalStrategyContext.ForFuelCalculator(snapshot, now);
        if (!localContext.IsAvailable)
        {
            return new LiveFuelStrategyModel(
                IsAvailable: false,
                Status: localContext.StatusText,
                Reason: localContext.Reason,
                History: history,
                Strategy: null);
        }

        var strategy = FuelStrategyCalculator.From(snapshot, history);
        return new LiveFuelStrategyModel(
            IsAvailable: true,
            Status: strategy.Status,
            Reason: localContext.Reason,
            History: history,
            Strategy: strategy);
    }
}
