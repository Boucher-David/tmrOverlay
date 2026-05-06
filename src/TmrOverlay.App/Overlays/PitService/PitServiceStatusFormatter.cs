using System.Globalization;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceStatusFormatter
{
    public const int None = 0;
    public const int InProgress = 1;
    public const int Complete = 2;
    public const int TooFarLeft = 100;
    public const int TooFarRight = 101;
    public const int TooFarForward = 102;
    public const int TooFarBack = 103;
    public const int BadAngle = 104;
    public const int CantFixThat = 105;

    public static bool IsComplete(int? status)
    {
        return status == Complete;
    }

    public static bool IsInProgress(int? status)
    {
        return status == InProgress;
    }

    public static bool IsError(int? status)
    {
        return status is >= TooFarLeft;
    }

    public static string Format(int? status)
    {
        return status switch
        {
            null => "--",
            None => "none",
            InProgress => "in progress",
            Complete => "complete",
            TooFarLeft => "too far left",
            TooFarRight => "too far right",
            TooFarForward => "too far forward",
            TooFarBack => "too far back",
            BadAngle => "bad angle",
            CantFixThat => "cannot repair",
            _ => $"status {status.Value.ToString(CultureInfo.InvariantCulture)}"
        };
    }
}
