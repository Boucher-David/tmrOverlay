namespace TmrOverlay.App.Overlays.Abstractions;

internal sealed record OverlayChromeState(
    string Title,
    string Status,
    OverlayChromeTone Tone,
    string? Source,
    OverlayChromeFooterMode FooterMode)
{
    public bool ShowFooter => FooterMode switch
    {
        OverlayChromeFooterMode.Always => true,
        OverlayChromeFooterMode.DegradedOnly => Tone is OverlayChromeTone.Error or OverlayChromeTone.Warning or OverlayChromeTone.Waiting,
        _ => false
    };

    public bool ShowStatus => !string.IsNullOrWhiteSpace(Status);

    public static OverlayChromeState Normal(string title, string status, string? source, OverlayChromeFooterMode footerMode = OverlayChromeFooterMode.Always)
    {
        return new OverlayChromeState(title, status, OverlayChromeTone.Normal, source, footerMode);
    }

    public static OverlayChromeState Error(string title, string status, string? source, OverlayChromeFooterMode footerMode = OverlayChromeFooterMode.Always)
    {
        return new OverlayChromeState(title, status, OverlayChromeTone.Error, source, footerMode);
    }
}

internal enum OverlayChromeTone
{
    Normal,
    Waiting,
    Info,
    Success,
    Warning,
    Error
}

internal enum OverlayChromeFooterMode
{
    Never,
    DegradedOnly,
    Always
}
