import Foundation

final class SessionHistoryQueryService {
    private let userHistoryRoot: URL
    private let baselineHistoryRoot: URL?
    private let useBaselineHistory: Bool

    init(
        userHistoryRoot: URL,
        baselineHistoryRoot: URL? = nil,
        useBaselineHistory: Bool = false
    ) {
        self.userHistoryRoot = userHistoryRoot
        self.baselineHistoryRoot = baselineHistoryRoot
        self.useBaselineHistory = useBaselineHistory
    }

    func lookup(_ combo: HistoricalComboIdentity) -> SessionHistoryLookupResult {
        let userAggregate = readAggregate(root: userHistoryRoot, combo: combo)
        let baselineAggregate = useBaselineHistory
            ? baselineHistoryRoot.flatMap { readAggregate(root: $0, combo: combo) }
            : nil

        return SessionHistoryLookupResult(
            combo: combo,
            userAggregate: userAggregate,
            baselineAggregate: baselineAggregate
        )
    }

    private func readAggregate(root: URL, combo: HistoricalComboIdentity) -> HistoricalSessionAggregate? {
        let url = root
            .appendingPathComponent("cars", isDirectory: true)
            .appendingPathComponent(combo.carKey, isDirectory: true)
            .appendingPathComponent("tracks", isDirectory: true)
            .appendingPathComponent(combo.trackKey, isDirectory: true)
            .appendingPathComponent("sessions", isDirectory: true)
            .appendingPathComponent(combo.sessionKey, isDirectory: true)
            .appendingPathComponent("aggregate.json")

        guard let data = try? Data(contentsOf: url) else {
            return nil
        }

        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return try? decoder.decode(HistoricalSessionAggregate.self, from: data)
    }
}

struct SessionHistoryLookupResult {
    let combo: HistoricalComboIdentity
    let userAggregate: HistoricalSessionAggregate?
    let baselineAggregate: HistoricalSessionAggregate?

    var preferredAggregate: HistoricalSessionAggregate? {
        userAggregate ?? baselineAggregate
    }

    var preferredAggregateSource: String? {
        if userAggregate != nil {
            return "user"
        }

        if baselineAggregate != nil {
            return "baseline"
        }

        return nil
    }

    var hasAnyData: Bool {
        preferredAggregate != nil
    }

    static func empty(_ combo: HistoricalComboIdentity) -> SessionHistoryLookupResult {
        SessionHistoryLookupResult(combo: combo, userAggregate: nil, baselineAggregate: nil)
    }
}
