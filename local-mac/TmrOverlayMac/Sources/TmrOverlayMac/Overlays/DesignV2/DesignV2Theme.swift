import AppKit

// Designer-facing tokens for mac Design V2 surfaces and proving-ground components.
// Keep semantic names stable so Windows can later map the same roles to WinForms.
struct DesignV2Theme {
    let id: String
    let displayName: String
    let layout: Layout
    let colors: Colors

    struct Layout {
        let previewSize: NSSize
        let componentGallerySize: NSSize
        let cornerRadius: CGFloat
        let panelRadius: CGFloat
        let pageInset: CGFloat
        let metricColumnWidth: CGFloat
        let bodyGap: CGFloat

        static let standard = Layout(
            previewSize: NSSize(width: 720, height: 420),
            componentGallerySize: NSSize(width: 960, height: 640),
            cornerRadius: 8,
            panelRadius: 6,
            pageInset: 18,
            metricColumnWidth: 238,
            bodyGap: 22
        )
    }

    struct Colors {
        let surface: NSColor
        let surfaceRaised: NSColor
        let surfaceInset: NSColor
        let titleBar: NSColor
        let border: NSColor
        let borderMuted: NSColor
        let gridLine: NSColor

        let textPrimary: NSColor
        let textSecondary: NSColor
        let textMuted: NSColor
        let textDisabled: NSColor

        let accentPrimary: NSColor
        let accentSecondary: NSColor
        let accentTertiary: NSColor

        let live: NSColor
        let measured: NSColor
        let modeled: NSColor
        let history: NSColor
        let partial: NSColor
        let stale: NSColor
        let unavailable: NSColor
        let error: NSColor
    }

    static let current = DesignV2Theme(
        id: "current",
        displayName: "Current",
        layout: .standard,
        colors: Colors(
            surface: NSColor(red255: 12, green: 17, blue: 19, alpha: 0.94),
            surfaceRaised: NSColor(red255: 20, green: 27, blue: 31, alpha: 0.94),
            surfaceInset: NSColor(red255: 8, green: 13, blue: 15, alpha: 0.92),
            titleBar: NSColor(red255: 24, green: 30, blue: 34, alpha: 0.96),
            border: NSColor(red255: 74, green: 88, blue: 96, alpha: 0.72),
            borderMuted: NSColor(red255: 43, green: 55, blue: 62, alpha: 0.78),
            gridLine: NSColor(red255: 70, green: 83, blue: 89, alpha: 0.34),
            textPrimary: NSColor(red255: 241, green: 247, blue: 248),
            textSecondary: NSColor(red255: 188, green: 202, blue: 209),
            textMuted: NSColor(red255: 124, green: 142, blue: 151),
            textDisabled: NSColor(red255: 86, green: 101, blue: 110),
            accentPrimary: NSColor(red255: 94, green: 199, blue: 255),
            accentSecondary: NSColor(red255: 177, green: 148, blue: 255),
            accentTertiary: NSColor(red255: 236, green: 190, blue: 96),
            live: NSColor(red255: 74, green: 214, blue: 126),
            measured: NSColor(red255: 94, green: 199, blue: 255),
            modeled: NSColor(red255: 177, green: 148, blue: 255),
            history: NSColor(red255: 236, green: 190, blue: 96),
            partial: NSColor(red255: 247, green: 176, blue: 70),
            stale: NSColor(red255: 197, green: 143, blue: 92),
            unavailable: NSColor(red255: 122, green: 137, blue: 145),
            error: NSColor(red255: 249, green: 92, blue: 94)
        )
    )

    static let outrun = DesignV2Theme(
        id: "outrun",
        displayName: "Outrun",
        layout: .standard,
        colors: Colors(
            surface: NSColor(red255: 7, green: 11, blue: 28, alpha: 1.0),
            surfaceRaised: NSColor(red255: 12, green: 22, blue: 48, alpha: 1.0),
            surfaceInset: NSColor(red255: 4, green: 8, blue: 19, alpha: 1.0),
            titleBar: NSColor(red255: 6, green: 9, blue: 24, alpha: 1.0),
            border: NSColor(red255: 40, green: 72, blue: 108, alpha: 0.92),
            borderMuted: NSColor(red255: 30, green: 52, blue: 82, alpha: 0.78),
            gridLine: NSColor(red255: 0, green: 232, blue: 255, alpha: 0.24),
            textPrimary: NSColor(red255: 255, green: 247, blue: 255),
            textSecondary: NSColor(red255: 208, green: 230, blue: 255),
            textMuted: NSColor(red255: 140, green: 174, blue: 212),
            textDisabled: NSColor(red255: 82, green: 112, blue: 148),
            accentPrimary: NSColor(red255: 0, green: 232, blue: 255),
            accentSecondary: NSColor(red255: 255, green: 42, blue: 167),
            accentTertiary: NSColor(red255: 141, green: 92, blue: 255),
            live: NSColor(red255: 98, green: 255, blue: 159),
            measured: NSColor(red255: 0, green: 232, blue: 255),
            modeled: NSColor(red255: 141, green: 92, blue: 255),
            history: NSColor(red255: 255, green: 209, blue: 91),
            partial: NSColor(red255: 255, green: 125, blue: 73),
            stale: NSColor(red255: 255, green: 168, blue: 88),
            unavailable: NSColor(red255: 140, green: 174, blue: 212),
            error: NSColor(red255: 255, green: 76, blue: 92)
        )
    )

    func color(for evidence: DesignV2EvidenceKind) -> NSColor {
        switch evidence {
        case .live:
            return colors.live
        case .measured:
            return colors.measured
        case .modeled:
            return colors.modeled
        case .history:
            return colors.history
        case .partial:
            return colors.partial
        case .stale:
            return colors.stale
        case .unavailable:
            return colors.unavailable
        case .error:
            return colors.error
        }
    }

}
