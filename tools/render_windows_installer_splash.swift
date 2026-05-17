import AppKit
import Foundation

private enum SplashTheme {
    static let background = NSColor(calibratedRed: 3 / 255, green: 11 / 255, blue: 24 / 255, alpha: 1)
    static let panel = NSColor(calibratedRed: 9 / 255, green: 18 / 255, blue: 34 / 255, alpha: 1)
    static let darkPanel = NSColor(calibratedRed: 8 / 255, green: 10 / 255, blue: 28 / 255, alpha: 1)
    static let border = NSColor(calibratedRed: 32 / 255, green: 54 / 255, blue: 84 / 255, alpha: 1)
    static let text = NSColor(calibratedRed: 255 / 255, green: 247 / 255, blue: 255 / 255, alpha: 1)
    static let muted = NSColor(calibratedRed: 185 / 255, green: 217 / 255, blue: 255 / 255, alpha: 1)
    static let dim = NSColor(calibratedRed: 82 / 255, green: 112 / 255, blue: 148 / 255, alpha: 1)
    static let accent = NSColor(calibratedRed: 0 / 255, green: 232 / 255, blue: 255 / 255, alpha: 1)
    static let accentDark = NSColor(calibratedRed: 0 / 255, green: 160 / 255, blue: 190 / 255, alpha: 1)
    static let magenta = NSColor(calibratedRed: 255 / 255, green: 42 / 255, blue: 167 / 255, alpha: 1)

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
        canvas.line(from: CGPoint(x: x, y: 0), to: CGPoint(x: x, y: bounds.height), color: NSColor(calibratedRed: 0 / 255, green: 232 / 255, blue: 255 / 255, alpha: 0.12))
    }
    for y in stride(from: CGFloat(0), through: bounds.height, by: 64) {
        canvas.line(from: CGPoint(x: 0, y: y), to: CGPoint(x: bounds.width, y: y), color: NSColor(calibratedRed: 255 / 255, green: 42 / 255, blue: 167 / 255, alpha: 0.10))
    }

    let panel = bounds.insetBy(dx: 34, dy: 32)
    canvas.fill(panel, SplashTheme.panel, radius: 12)
    canvas.stroke(panel, SplashTheme.border, width: 1, radius: 12)
    canvas.fill(CGRect(x: panel.minX, y: panel.maxY - 6, width: panel.width, height: 3), SplashTheme.magenta, radius: 12)
    canvas.fill(CGRect(x: panel.minX, y: panel.maxY - 3, width: panel.width, height: 2), SplashTheme.accent, radius: 12)

    if let logo {
        logo.draw(in: CGRect(x: panel.minX + 50, y: panel.maxY - 150, width: 205, height: 115), from: .zero, operation: .sourceOver, fraction: 1)
    }

    canvas.text(
        "Tech Mates Racing Overlay",
        in: CGRect(x: panel.minX + 292, y: panel.maxY - 110, width: 266, height: 32),
        size: 20,
        weight: .bold
    )
    canvas.text(
        "Windows installer",
        in: CGRect(x: panel.minX + 294, y: panel.maxY - 143, width: 230, height: 22),
        size: 15,
        weight: .semibold,
        color: SplashTheme.accent
    )
    canvas.text(
        "Desktop and Start Menu shortcuts.",
        in: CGRect(x: panel.minX + 294, y: panel.maxY - 176, width: 254, height: 20),
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
    canvas.fill(bounds, SplashTheme.darkPanel)
    canvas.fill(CGRect(x: bounds.minX, y: bounds.maxY - 5, width: bounds.width, height: 3), SplashTheme.magenta)
    canvas.fill(CGRect(x: bounds.minX, y: bounds.maxY - 2, width: bounds.width, height: 2), SplashTheme.accent)

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
        canvas.line(from: CGPoint(x: x, y: 0), to: CGPoint(x: x, y: bounds.height), color: NSColor(calibratedRed: 0 / 255, green: 232 / 255, blue: 255 / 255, alpha: 0.12))
    }
    for y in stride(from: CGFloat(0), through: bounds.height, by: 56) {
        canvas.line(from: CGPoint(x: 0, y: y), to: CGPoint(x: bounds.width, y: y), color: NSColor(calibratedRed: 255 / 255, green: 42 / 255, blue: 167 / 255, alpha: 0.10))
    }

    let brandBlock = CGRect(x: 32, y: 48, width: bounds.width - 64, height: 222)
    canvas.fill(brandBlock, SplashTheme.panel, radius: 14)
    canvas.stroke(brandBlock, SplashTheme.border, width: 1, radius: 14)
    canvas.fill(CGRect(x: brandBlock.minX, y: brandBlock.maxY - 7, width: brandBlock.width, height: 4), SplashTheme.magenta, radius: 14)
    canvas.fill(CGRect(x: brandBlock.minX, y: brandBlock.maxY - 3, width: brandBlock.width, height: 3), SplashTheme.accent, radius: 14)

    if let logo {
        logo.draw(in: CGRect(x: 111, y: 122, width: 270, height: 152), from: .zero, operation: .sourceOver, fraction: 1)
    }

    canvas.text(
        "Desktop and Start Menu shortcuts included.",
        in: CGRect(x: 60, y: 72, width: 378, height: 42),
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
        samplesPerPixel: 4,
        hasAlpha: true,
        isPlanar: false,
        colorSpaceName: .deviceRGB,
        bytesPerRow: 0,
        bitsPerPixel: 0
    ) else {
        throw NSError(domain: "render_windows_installer_splash", code: 1)
    }

    NSGraphicsContext.saveGraphicsState()
    let graphicsContext = NSGraphicsContext(bitmapImageRep: rep)!
    NSGraphicsContext.current = graphicsContext
    graphicsContext.cgContext.clear(CGRect(origin: .zero, size: size))
    render(SplashCanvas(size: size), logo)
    NSGraphicsContext.restoreGraphicsState()

    if type == .bmp {
        try writeBmp24(rep, to: url)
        return
    }

    guard let data = rep.representation(using: type, properties: [:]) else {
        throw NSError(domain: "render_windows_installer_splash", code: 2)
    }

    try data.write(to: url)
}

private func writeBmp24(_ rep: NSBitmapImageRep, to url: URL) throws {
    let width = rep.pixelsWide
    let height = rep.pixelsHigh
    let rowStride = ((width * 3 + 3) / 4) * 4
    let imageSize = rowStride * height
    let fileSize = 54 + imageSize
    var data = Data(capacity: fileSize)

    appendUInt16LE(0x4D42, to: &data)
    appendUInt32LE(UInt32(fileSize), to: &data)
    appendUInt16LE(0, to: &data)
    appendUInt16LE(0, to: &data)
    appendUInt32LE(54, to: &data)
    appendUInt32LE(40, to: &data)
    appendInt32LE(Int32(width), to: &data)
    appendInt32LE(-Int32(height), to: &data)
    appendUInt16LE(1, to: &data)
    appendUInt16LE(24, to: &data)
    appendUInt32LE(0, to: &data)
    appendUInt32LE(UInt32(imageSize), to: &data)
    appendInt32LE(0, to: &data)
    appendInt32LE(0, to: &data)
    appendUInt32LE(0, to: &data)
    appendUInt32LE(0, to: &data)

    for y in 0..<height {
        var row = Data(capacity: rowStride)
        for x in 0..<width {
            let color = rep.colorAt(x: x, y: y)?.usingColorSpace(.deviceRGB)
                ?? NSColor.black
            row.append(UInt8(clamping: Int((color.blueComponent * 255).rounded())))
            row.append(UInt8(clamping: Int((color.greenComponent * 255).rounded())))
            row.append(UInt8(clamping: Int((color.redComponent * 255).rounded())))
        }

        while row.count < rowStride {
            row.append(0)
        }
        data.append(row)
    }

    try data.write(to: url)
}

private func appendUInt16LE(_ value: UInt16, to data: inout Data) {
    var littleEndian = value.littleEndian
    withUnsafeBytes(of: &littleEndian) { bytes in
        data.append(contentsOf: bytes)
    }
}

private func appendUInt32LE(_ value: UInt32, to data: inout Data) {
    var littleEndian = value.littleEndian
    withUnsafeBytes(of: &littleEndian) { bytes in
        data.append(contentsOf: bytes)
    }
}

private func appendInt32LE(_ value: Int32, to data: inout Data) {
    var littleEndian = value.littleEndian
    withUnsafeBytes(of: &littleEndian) { bytes in
        data.append(contentsOf: bytes)
    }
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
            outputDirectory.appendingPathComponent("TMRInstallerSplash.png"),
            CGSize(width: 640, height: 400),
            .png,
            renderSplash
        ),
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
