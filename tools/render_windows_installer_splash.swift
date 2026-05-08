import AppKit
import Foundation

private enum SplashTheme {
    static let background = NSColor(calibratedRed: 12 / 255, green: 15 / 255, blue: 18 / 255, alpha: 1)
    static let panel = NSColor(calibratedRed: 19 / 255, green: 24 / 255, blue: 28 / 255, alpha: 1)
    static let border = NSColor(white: 1, alpha: 0.18)
    static let text = NSColor.white
    static let muted = NSColor(calibratedRed: 128 / 255, green: 145 / 255, blue: 153 / 255, alpha: 1)
    static let accent = NSColor(calibratedRed: 69 / 255, green: 203 / 255, blue: 250 / 255, alpha: 1)

    static func font(_ size: CGFloat, weight: NSFont.Weight = .regular) -> NSFont {
        NSFont(name: "SF Pro", size: size) ?? NSFont.systemFont(ofSize: size, weight: weight)
    }
}

private final class SplashCanvas {
    let size: CGSize

    init(size: CGSize) {
        self.size = size
    }

    func fill(_ rect: CGRect, _ color: NSColor, radius: CGFloat = 0) {
        color.setFill()
        path(rect, radius).fill()
    }

    func stroke(_ rect: CGRect, _ color: NSColor, width: CGFloat = 1, radius: CGFloat = 0) {
        color.setStroke()
        let p = path(rect.insetBy(dx: width / 2, dy: width / 2), radius)
        p.lineWidth = width
        p.stroke()
    }

    func text(
        _ value: String,
        in rect: CGRect,
        size: CGFloat,
        weight: NSFont.Weight = .regular,
        color: NSColor = SplashTheme.text
    ) {
        let paragraph = NSMutableParagraphStyle()
        paragraph.lineBreakMode = .byTruncatingTail
        let attributes: [NSAttributedString.Key: Any] = [
            .font: SplashTheme.font(size, weight: weight),
            .foregroundColor: color,
            .paragraphStyle: paragraph
        ]
        NSString(string: value).draw(in: rect, withAttributes: attributes)
    }

    func line(from: CGPoint, to: CGPoint, color: NSColor, width: CGFloat = 1) {
        color.setStroke()
        let p = NSBezierPath()
        p.move(to: from)
        p.line(to: to)
        p.lineWidth = width
        p.stroke()
    }

    private func path(_ rect: CGRect, _ radius: CGFloat) -> NSBezierPath {
        radius <= 0 ? NSBezierPath(rect: rect) : NSBezierPath(roundedRect: rect, xRadius: radius, yRadius: radius)
    }
}

private func renderSplash(_ canvas: SplashCanvas, logo: NSImage?) {
    let bounds = CGRect(origin: .zero, size: canvas.size)
    canvas.fill(bounds, SplashTheme.background)

    for x in stride(from: CGFloat(0), through: bounds.width, by: 64) {
        canvas.line(from: CGPoint(x: x, y: 0), to: CGPoint(x: x, y: bounds.height), color: NSColor(white: 1, alpha: 0.018))
    }
    for y in stride(from: CGFloat(0), through: bounds.height, by: 64) {
        canvas.line(from: CGPoint(x: 0, y: y), to: CGPoint(x: bounds.width, y: y), color: NSColor(white: 1, alpha: 0.018))
    }

    let panel = bounds.insetBy(dx: 34, dy: 32)
    canvas.fill(panel, SplashTheme.panel, radius: 12)
    canvas.stroke(panel, SplashTheme.border, width: 1, radius: 12)
    canvas.fill(CGRect(x: panel.minX, y: panel.maxY - 4, width: panel.width, height: 4), SplashTheme.accent, radius: 12)

    if let logo {
        logo.draw(in: CGRect(x: panel.minX + 44, y: panel.maxY - 151, width: 216, height: 122), from: .zero, operation: .sourceOver, fraction: 1)
    }

    canvas.text(
        "Tech Mates Racing Overlay",
        in: CGRect(x: panel.minX + 308, y: panel.maxY - 112, width: 320, height: 32),
        size: 24,
        weight: .bold
    )
    canvas.text(
        "Windows installer",
        in: CGRect(x: panel.minX + 310, y: panel.maxY - 145, width: 280, height: 22),
        size: 15,
        weight: .semibold,
        color: SplashTheme.accent
    )
    canvas.text(
        "Installing the app and Start Menu shortcut.",
        in: CGRect(x: panel.minX + 310, y: panel.maxY - 178, width: 318, height: 20),
        size: 13,
        color: SplashTheme.muted
    )
}

private func findRepositoryRoot(startingAt url: URL) -> URL? {
    var directory = url.standardizedFileURL
    while true {
        if FileManager.default.fileExists(atPath: directory.appendingPathComponent("tmrOverlay.sln").path) {
            return directory
        }

        let parent = directory.deletingLastPathComponent()
        if parent.path == directory.path {
            return nil
        }

        directory = parent
    }
}

private func renderMsiBanner(_ canvas: SplashCanvas, logo: NSImage?) {
    let bounds = CGRect(origin: .zero, size: canvas.size)
    canvas.fill(bounds, SplashTheme.background)
    canvas.fill(CGRect(x: bounds.minX, y: bounds.maxY - 4, width: bounds.width, height: 4), SplashTheme.accent)

    if let logo {
        logo.draw(in: CGRect(x: 16, y: 9, width: 76, height: 43), from: .zero, operation: .sourceOver, fraction: 1)
    }

    canvas.text(
        "Tech Mates Racing Overlay",
        in: CGRect(x: 112, y: 29, width: 260, height: 19),
        size: 15,
        weight: .bold
    )
    canvas.text(
        "Windows installer",
        in: CGRect(x: 112, y: 12, width: 190, height: 15),
        size: 10.5,
        weight: .semibold,
        color: SplashTheme.accent
    )
}

private func renderMsiLogo(_ canvas: SplashCanvas, logo: NSImage?) {
    let bounds = CGRect(origin: .zero, size: canvas.size)
    canvas.fill(bounds, SplashTheme.background)

    for x in stride(from: CGFloat(0), through: bounds.width, by: 56) {
        canvas.line(from: CGPoint(x: x, y: 0), to: CGPoint(x: x, y: bounds.height), color: NSColor(white: 1, alpha: 0.018))
    }
    for y in stride(from: CGFloat(0), through: bounds.height, by: 56) {
        canvas.line(from: CGPoint(x: 0, y: y), to: CGPoint(x: bounds.width, y: y), color: NSColor(white: 1, alpha: 0.018))
    }

    if let logo {
        logo.draw(in: CGRect(x: 54, y: 116, width: 385, height: 216), from: .zero, operation: .sourceOver, fraction: 1)
    }

    canvas.text(
        "Install the overlay app, Start Menu shortcut, and update-ready package identity.",
        in: CGRect(x: 54, y: 44, width: 370, height: 36),
        size: 13,
        color: SplashTheme.muted
    )
}

private func renderImage(
    size: CGSize,
    type: NSBitmapImageRep.FileType,
    to url: URL,
    logo: NSImage?,
    render: (SplashCanvas, NSImage?) -> Void
) throws {
    guard let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil,
        pixelsWide: Int(size.width),
        pixelsHigh: Int(size.height),
        bitsPerSample: 8,
        samplesPerPixel: 3,
        hasAlpha: false,
        isPlanar: false,
        colorSpaceName: .calibratedRGB,
        bytesPerRow: 0,
        bitsPerPixel: 24
    ) else {
        throw NSError(domain: "render_windows_installer_splash", code: 1)
    }

    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
    render(SplashCanvas(size: size), logo)
    NSGraphicsContext.restoreGraphicsState()

    guard let data = rep.representation(using: type, properties: [:]) else {
        throw NSError(domain: "render_windows_installer_splash", code: 2)
    }

    try data.write(to: url)
}

let currentDirectory = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let repositoryRoot = findRepositoryRoot(startingAt: currentDirectory) ?? currentDirectory
let logo = NSImage(contentsOf: repositoryRoot.appendingPathComponent("assets/brand/TMRLogo.png"))
let outputDirectory = CommandLine.arguments.dropFirst().first.map(URL.init(fileURLWithPath:))
    ?? repositoryRoot.appendingPathComponent("assets/brand")

do {
    try FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)
    let outputs: [(URL, CGSize, NSBitmapImageRep.FileType, (SplashCanvas, NSImage?) -> Void)] = [
        (
            outputDirectory.appendingPathComponent("TMRMsiBanner.bmp"),
            CGSize(width: 493, height: 58),
            .bmp,
            renderMsiBanner
        ),
        (
            outputDirectory.appendingPathComponent("TMRMsiLogo.bmp"),
            CGSize(width: 493, height: 312),
            .bmp,
            renderMsiLogo
        )
    ]

    for (url, size, type, render) in outputs {
        try renderImage(size: size, type: type, to: url, logo: logo, render: render)
        print("wrote \(url.path)")
    }
} catch {
    fputs("Failed to render installer artwork: \(error)\n", stderr)
    exit(1)
}
