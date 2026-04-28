using TmrOverlay.Core.Fuel;
using TmrOverlay.Core.History;

namespace TmrOverlay.App.Overlays.FuelCalculator;

internal sealed record FuelCalculatorViewModel(
    string Status,
    string Overview,
    string Source,
    IReadOnlyList<FuelDisplayRow> Rows)
{
    public static FuelCalculatorViewModel From(
        FuelStrategySnapshot strategy,
        SessionHistoryLookupResult history,
        bool showAdvice,
        string unitSystem,
        int maximumRows)
    {
        return new FuelCalculatorViewModel(
            Status: strategy.Status,
            Overview: BuildOverview(strategy, unitSystem),
            Source: BuildSourceText(strategy, history, unitSystem),
            Rows: BuildDisplayRows(strategy, showAdvice, unitSystem, maximumRows));
    }

    private static string BuildOverview(FuelStrategySnapshot strategy, string unitSystem)
    {
        if (strategy.PlannedRaceLaps is { } plannedLaps
            && strategy.PlannedStintCount is { } stintCount
            && strategy.FinalStintTargetLaps is { } finalStintLaps)
        {
            return stintCount <= 1
                ? $"{plannedLaps} laps | no stop"
                : $"{plannedLaps} laps | {stintCount} stints | final {finalStintLaps}";
        }

        var fuel = FormatFuelVolume(strategy.CurrentFuelLiters, unitSystem);
        var remaining = FuelStrategyCalculator.FormatNumber(strategy.RaceLapsRemaining, " laps");
        var needed = strategy.AdditionalFuelNeededLiters is > 0.1d
            ? $"+{FormatFuelVolume(strategy.AdditionalFuelNeededLiters, unitSystem)}"
            : "covered";
        return $"{fuel} | {remaining} | {needed}";
    }

    private static IReadOnlyList<FuelDisplayRow> BuildDisplayRows(
        FuelStrategySnapshot strategy,
        bool showAdvice,
        string unitSystem,
        int maximumRows)
    {
        var rows = new List<FuelDisplayRow>(maximumRows);
        if (strategy.RhythmComparison is { AdditionalStopCount: > 0 } comparison)
        {
            rows.Add(new FuelDisplayRow(
                "Strategy",
                BuildRhythmText(comparison),
                BuildRhythmAdvice(comparison, unitSystem)));
        }

        foreach (var stint in strategy.Stints.Take(Math.Max(0, maximumRows - rows.Count)))
        {
            rows.Add(new FuelDisplayRow(
                $"Stint {stint.Number}",
                BuildStintText(stint, unitSystem),
                showAdvice ? FormatTireAdvice(stint.TireAdvice, unitSystem) : string.Empty));
        }

        return rows;
    }

    private static string BuildStintText(FuelStintEstimate stint, string unitSystem)
    {
        if (string.Equals(stint.Source, "finish", StringComparison.OrdinalIgnoreCase))
        {
            return "no fuel stop needed";
        }

        if (stint.TargetLaps is { } targetLaps)
        {
            var target = FormatFuelPerLap(stint.TargetFuelPerLapLiters, unitSystem);
            var suffix = stint.Source == "final" ? " final" : string.Empty;
            return $"{targetLaps} laps{suffix} | target {target}";
        }

        return $"{stint.LengthLaps:0.0} laps";
    }

    private static string BuildRhythmText(FuelRhythmComparison comparison)
    {
        return $"{comparison.LongTargetLaps}-lap rhythm avoids +{comparison.AdditionalStopCount} {Pluralize("stop", comparison.AdditionalStopCount)}";
    }

    private static string BuildRhythmAdvice(FuelRhythmComparison comparison, string unitSystem)
    {
        var time = comparison.EstimatedTimeLossSeconds is { } seconds && seconds > 0d
            ? $"~{seconds:0}s"
            : "--";
        return comparison.RequiredSavingLitersPerLap > 0.01d
            ? $"{time} | save {FormatFuelPerLap(comparison.RequiredSavingLitersPerLap, unitSystem)}"
            : time;
    }

    private static string BuildSourceText(
        FuelStrategySnapshot strategy,
        SessionHistoryLookupResult history,
        string unitSystem)
    {
        var fuelPerLap = FormatFuelPerLap(strategy.FuelPerLapLiters, unitSystem);
        var fullTank = FuelStrategyCalculator.FormatNumber(strategy.FullTankStintLaps, " laps/tank");
        var historySource = history.UserAggregate is not null
            ? "user"
            : history.BaselineAggregate is not null
                ? "baseline"
                : "none";
        var historicalRange = strategy.FuelPerLapMinimumLiters is not null || strategy.FuelPerLapMaximumLiters is not null
            ? $" | min/avg/max {FormatFuelNumber(strategy.FuelPerLapMinimumLiters, unitSystem)}/{FormatFuelNumber(strategy.FuelPerLapLiters, unitSystem)}/{FormatFuelNumber(strategy.FuelPerLapMaximumLiters, unitSystem)} {FuelPerLapSuffix(unitSystem)}"
            : string.Empty;
        var gaps = strategy.OverallLeaderGapLaps is not null || strategy.ClassLeaderGapLaps is not null
            ? $" | gap O{FormatPlain(strategy.OverallLeaderGapLaps)} C{FormatPlain(strategy.ClassLeaderGapLaps)}"
            : string.Empty;
        var tireModel = strategy.TireChangeServiceSeconds is not null || strategy.FuelFillRateLitersPerSecond is not null
            ? $" | tires {strategy.TireModelSource}"
            : string.Empty;
        return $"burn {fuelPerLap} ({strategy.FuelPerLapSource}) | {fullTank} | history {historySource}{historicalRange}{tireModel}{gaps}";
    }

    private static string FormatTireAdvice(TireChangeAdvice? advice, string unitSystem)
    {
        if (advice is null)
        {
            return "--";
        }

        if (advice == TireChangeAdvice.Pending && advice.FuelToAddLiters is { } pendingFuel)
        {
            return $"tire data pending ({FormatFuelVolume(pendingFuel, unitSystem)})";
        }

        if (advice.FuelToAddLiters is { } fuelToAdd
            && advice.TimeLossSeconds is { } timeLoss
            && timeLoss <= 1d)
        {
            return $"tires free ({FormatFuelVolume(fuelToAdd, unitSystem)})";
        }

        return advice.Text.Replace(" L", $" {FuelVolumeSuffix(unitSystem)}", StringComparison.Ordinal);
    }

    private static string FormatFuelVolume(double? liters, string unitSystem)
    {
        return $"{FormatFuelNumber(liters, unitSystem)} {FuelVolumeSuffix(unitSystem)}";
    }

    private static string FormatFuelPerLap(double? liters, string unitSystem)
    {
        return $"{FormatFuelNumber(liters, unitSystem)} {FuelPerLapSuffix(unitSystem)}";
    }

    private static string FormatFuelNumber(double? liters, string unitSystem)
    {
        if (liters is null || double.IsNaN(liters.Value) || double.IsInfinity(liters.Value))
        {
            return "--";
        }

        var value = string.Equals(unitSystem, "Imperial", StringComparison.OrdinalIgnoreCase)
            ? liters.Value * 0.264172052d
            : liters.Value;
        return FormattableString.Invariant($"{value:0.0}");
    }

    private static string FuelVolumeSuffix(string unitSystem)
    {
        return string.Equals(unitSystem, "Imperial", StringComparison.OrdinalIgnoreCase) ? "gal" : "L";
    }

    private static string FuelPerLapSuffix(string unitSystem)
    {
        return string.Equals(unitSystem, "Imperial", StringComparison.OrdinalIgnoreCase) ? "gal/lap" : "L/lap";
    }

    private static string FormatPlain(double? value)
    {
        return value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
            ? "--"
            : FormattableString.Invariant($"{value.Value:0.0}");
    }

    private static string Pluralize(string singular, int count)
    {
        return count == 1 ? singular : $"{singular}s";
    }
}

internal sealed record FuelDisplayRow(string Label, string Value, string Advice);
