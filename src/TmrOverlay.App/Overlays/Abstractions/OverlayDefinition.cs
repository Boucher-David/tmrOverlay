namespace TmrOverlay.App.Overlays.Abstractions;

internal sealed record OverlayDefinition(
    string Id,
    string DisplayName,
    int DefaultWidth,
    int DefaultHeight);
