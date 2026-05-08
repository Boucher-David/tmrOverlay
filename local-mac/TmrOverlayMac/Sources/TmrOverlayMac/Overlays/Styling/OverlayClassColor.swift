import AppKit

enum OverlayClassColor {
    private static var parsedColors: [String: NSColor] = [:]
    private static let lock = NSLock()

    static func color(_ token: String?, alpha: CGFloat = 1) -> NSColor? {
        guard let key = normalizedKey(token) else {
            return nil
        }

        lock.lock()
        if let cached = parsedColors[key] {
            lock.unlock()
            return cached.withAlphaComponent(alpha)
        }
        lock.unlock()

        guard let rgb = Int(key, radix: 16) else {
            return nil
        }

        let color = NSColor(
            red255: CGFloat((rgb >> 16) & 0xff),
            green: CGFloat((rgb >> 8) & 0xff),
            blue: CGFloat(rgb & 0xff),
            alpha: 1
        )

        lock.lock()
        parsedColors[key] = color
        lock.unlock()
        return color.withAlphaComponent(alpha)
    }

    static func blend(panel: NSColor, accent: NSColor, panelWeight: CGFloat, accentWeight: CGFloat) -> NSColor {
        let total = max(1, panelWeight + accentWeight)
        let panelRGBA = rgba(panel)
        let accentRGBA = rgba(accent)
        return NSColor(
            red: (panelRGBA.red * panelWeight + accentRGBA.red * accentWeight) / total,
            green: (panelRGBA.green * panelWeight + accentRGBA.green * accentWeight) / total,
            blue: (panelRGBA.blue * panelWeight + accentRGBA.blue * accentWeight) / total,
            alpha: (panelRGBA.alpha * panelWeight + accentRGBA.alpha * accentWeight) / total
        )
    }

    private static func normalizedKey(_ token: String?) -> String? {
        guard var value = token?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }

        if value.hasPrefix("#") {
            value.removeFirst()
        } else if value.lowercased().hasPrefix("0x") {
            value.removeFirst(2)
        }

        guard value.count == 6, value.allSatisfy(\.isHexDigit) else {
            return nil
        }

        return value.uppercased()
    }

    private static func rgba(_ color: NSColor) -> (red: CGFloat, green: CGFloat, blue: CGFloat, alpha: CGFloat) {
        let converted = color.usingColorSpace(.deviceRGB) ?? color
        return (converted.redComponent, converted.greenComponent, converted.blueComponent, converted.alphaComponent)
    }
}
