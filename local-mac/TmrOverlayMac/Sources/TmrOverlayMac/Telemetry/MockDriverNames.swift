import Foundation

enum MockDriverNames {
    static let dafyddFullName = "Dafydd Hughes"
    static let simonFullName = "Simon Evans"

    private static let fixedNames: [Int: String] = [
        FourHourRacePreview.classLeaderCarIdx: "Avery Rowan",
        FourHourRacePreview.teamCarIdx: "Tech Mates Racing",
        12: "Maya Keller",
        14: "Noah Chen",
        51: "Luca Moretti"
    ]

    private static let firstNames = [
        "Avery", "Maya", "Noah", "Luca", "Sofia", "Elliot",
        "Riley", "Jordan", "Morgan", "Casey", "Taylor", "Quinn",
        "Emery", "Rowan", "Harper", "Reese", "Parker", "Cameron"
    ]

    private static let lastNames = [
        "Rowan", "Keller", "Chen", "Moretti", "Blake", "Park",
        "Stone", "Vale", "Ellis", "Brooks", "Reed", "Nordin",
        "Hayes", "Patel", "Wright", "Morgan", "Carter", "Foster"
    ]

    static func displayName(for carIdx: Int) -> String {
        if let fixed = fixedNames[carIdx] {
            return fixed
        }

        let normalized = abs(carIdx)
        let first = firstNames[normalized % firstNames.count]
        let last = lastNames[(normalized / max(1, firstNames.count)) % lastNames.count]
        return "\(first) \(last)"
    }
}
