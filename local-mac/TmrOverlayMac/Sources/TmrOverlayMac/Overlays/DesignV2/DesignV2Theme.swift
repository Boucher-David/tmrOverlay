import AppKit

// Designer-facing tokens for the mac-only design v2 proving ground.
// Keep semantic names stable so Windows can later map the same roles to WinForms.
enum DesignV2Theme {
    enum Layout {
        static let previewSize = NSSize(width: 720, height: 420)
        static let cornerRadius: CGFloat = 8
        static let panelRadius: CGFloat = 6
        static let pageInset: CGFloat = 18
        static let metricColumnWidth: CGFloat = 238
        static let bodyGap: CGFloat = 22
    }

    enum Colors {
        static let surface = NSColor(red255: 12, green: 17, blue: 19, alpha: 0.94)
        static let surfaceRaised = NSColor(red255: 20, green: 27, blue: 31, alpha: 0.94)
        static let surfaceInset = NSColor(red255: 8, green: 13, blue: 15, alpha: 0.92)
        static let border = NSColor(red255: 74, green: 88, blue: 96, alpha: 0.72)
        static let borderMuted = NSColor(red255: 43, green: 55, blue: 62, alpha: 0.78)
        static let gridLine = NSColor(red255: 70, green: 83, blue: 89, alpha: 0.34)

        static let textPrimary = NSColor(red255: 241, green: 247, blue: 248)
        static let textSecondary = NSColor(red255: 188, green: 202, blue: 209)
        static let textMuted = NSColor(red255: 124, green: 142, blue: 151)
        static let textDisabled = NSColor(red255: 86, green: 101, blue: 110)

        static let live = NSColor(red255: 74, green: 214, blue: 126)
        static let measured = NSColor(red255: 94, green: 199, blue: 255)
        static let modeled = NSColor(red255: 177, green: 148, blue: 255)
        static let history = NSColor(red255: 236, green: 190, blue: 96)
        static let partial = NSColor(red255: 247, green: 176, blue: 70)
        static let stale = NSColor(red255: 197, green: 143, blue: 92)
        static let unavailable = NSColor(red255: 122, green: 137, blue: 145)
        static let error = NSColor(red255: 249, green: 92, blue: 94)
    }

    static func color(for evidence: DesignV2EvidenceKind) -> NSColor {
        switch evidence {
        case .live:
            return Colors.live
        case .measured:
            return Colors.measured
        case .modeled:
            return Colors.modeled
        case .history:
            return Colors.history
        case .partial:
            return Colors.partial
        case .stale:
            return Colors.stale
        case .unavailable:
            return Colors.unavailable
        case .error:
            return Colors.error
        }
    }
}
