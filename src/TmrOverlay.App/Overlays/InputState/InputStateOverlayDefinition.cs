using TmrOverlay.Core.Overlays;

namespace TmrOverlay.App.Overlays.InputState;

internal static class InputStateOverlayDefinition
{
    public static OverlayDefinition Definition { get; } = new(
        Id: "input-state",
        DisplayName: "Input / Car State",
        DefaultWidth: 440,
        DefaultHeight: 285);
}
