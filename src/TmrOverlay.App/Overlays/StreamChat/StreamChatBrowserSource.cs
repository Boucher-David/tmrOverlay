using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.StreamChat;

internal static class StreamChatBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: StreamChatOverlayDefinition.Definition.Id,
        title: StreamChatOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/stream-chat",
        requiresTelemetry: false,
        moduleAssetName: "stream-chat",
        bodyClass: "stream-chat-page");
}
