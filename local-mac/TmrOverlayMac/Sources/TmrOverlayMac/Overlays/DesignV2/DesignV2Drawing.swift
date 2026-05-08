import AppKit

enum DesignV2Drawing {
    static func font(
        family: String,
        size: CGFloat,
        weight: NSFont.Weight = .regular
    ) -> NSFont {
        OverlayTheme.font(family: family, size: size, weight: weight)
    }

    static func rounded(
        _ rect: NSRect,
        radius: CGFloat,
        fill: NSColor?,
        stroke: NSColor?,
        lineWidth: CGFloat = 1
    ) {
        let path = NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
        if let fill {
            fill.setFill()
            path.fill()
        }
        if let stroke, lineWidth > 0 {
            stroke.setStroke()
            path.lineWidth = lineWidth
            path.stroke()
        }
    }

    static func text(
        _ text: String,
        in rect: NSRect,
        font: NSFont,
        color: NSColor,
        alignment: NSTextAlignment = .left,
        lineBreakMode: NSLineBreakMode = .byTruncatingTail
    ) {
        let paragraph = NSMutableParagraphStyle()
        paragraph.alignment = alignment
        paragraph.lineBreakMode = lineBreakMode
        NSString(string: text).draw(
            in: rect,
            withAttributes: [
                .font: font,
                .foregroundColor: color,
                .paragraphStyle: paragraph
            ]
        )
    }

    static func line(from start: NSPoint, to end: NSPoint, color: NSColor, width: CGFloat) {
        let path = NSBezierPath()
        path.move(to: start)
        path.line(to: end)
        color.setStroke()
        path.lineWidth = width
        path.stroke()
    }

    static func polyline(points: [NSPoint], color: NSColor, width: CGFloat) {
        guard let first = points.first else {
            return
        }

        let path = NSBezierPath()
        path.move(to: first)
        for point in points.dropFirst() {
            path.line(to: point)
        }
        color.setStroke()
        path.lineWidth = width
        path.lineCapStyle = .round
        path.lineJoinStyle = .round
        path.stroke()
    }
}
