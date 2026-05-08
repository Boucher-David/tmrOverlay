import Foundation

enum StreamChatProviderOptions {
    static let choices: [(label: String, value: String)] = [
        ("Not configured", "none"),
        ("Streamlabs Chat Box URL", "streamlabs"),
        ("Twitch channel", "twitch")
    ]

    static let compactLabels = ["None", "Streamlabs", "Twitch"]

    static func normalize(_ value: String) -> String {
        let normalized = value.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return normalized == "streamlabs" || normalized == "twitch" ? normalized : "none"
    }

    static func compactLabel(for value: String) -> String {
        switch normalize(value) {
        case "streamlabs":
            return "Streamlabs"
        case "twitch":
            return "Twitch"
        default:
            return "None"
        }
    }
}
