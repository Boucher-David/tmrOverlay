import Foundation

enum BrowserOverlayCatalog {
    private static let routesByOverlayId: [String: String] = [
        "standings": "/overlays/standings",
        "relative": "/overlays/relative",
        "fuel-calculator": "/overlays/fuel-calculator",
        "session-weather": "/overlays/session-weather",
        "pit-service": "/overlays/pit-service",
        "input-state": "/overlays/input-state",
        "car-radar": "/overlays/car-radar",
        "gap-to-leader": "/overlays/gap-to-leader",
        "track-map": "/overlays/track-map",
        "flags": "/overlays/flags",
        "stream-chat": "/overlays/stream-chat",
        "garage-cover": "/overlays/garage-cover"
    ]

    static func route(for overlayId: String) -> String? {
        routesByOverlayId[overlayId]
    }
}
