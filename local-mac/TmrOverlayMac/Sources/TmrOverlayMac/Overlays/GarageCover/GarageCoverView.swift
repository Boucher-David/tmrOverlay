import AppKit

final class GarageCoverView: NSView {
    var imagePath = "" {
        didSet {
            if imagePath != oldValue {
                loadCoverImageIfNeeded()
                needsDisplay = true
            }
        }
    }

    private var coverImage: NSImage?
    private var defaultCoverImage: NSImage?
    private var loadedImagePath = ""
    private var loadedImageModifiedAt: Date?
    private var loadedImageSize: UInt64?

    override init(frame frameRect: NSRect = NSRect(origin: .zero, size: GarageCoverOverlayDefinition.definition.defaultSize)) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.backgroundColor = NSColor.black.cgColor
    }

    required init?(coder: NSCoder) {
        super.init(coder: coder)
        wantsLayer = true
        layer?.backgroundColor = NSColor.black.cgColor
    }

    func update(with snapshot: LiveTelemetrySnapshot) {
        isHidden = !(snapshot.isConnected && snapshot.isCollecting && snapshot.latestFrame?.isGarageVisible == true)
        loadCoverImageIfNeeded()
        needsDisplay = !isHidden
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.black.setFill()
        bounds.fill()

        if let coverImage {
            coverImage.draw(in: coverRect(imageSize: coverImage.size, bounds: bounds))
        } else {
            drawFallback()
        }

        OverlayTheme.Colors.windowBorder.setStroke()
        NSBezierPath(rect: bounds.insetBy(dx: 0.5, dy: 0.5)).stroke()
    }

    private func coverRect(imageSize: NSSize, bounds: NSRect) -> NSRect {
        guard imageSize.width > 0, imageSize.height > 0, bounds.width > 0, bounds.height > 0 else {
            return bounds
        }

        let scale = max(bounds.width / imageSize.width, bounds.height / imageSize.height)
        let width = imageSize.width * scale
        let height = imageSize.height * scale
        return NSRect(
            x: bounds.midX - width / 2,
            y: bounds.midY - height / 2,
            width: width,
            height: height
        )
    }

    private func loadCoverImageIfNeeded() {
        guard !imagePath.isEmpty else {
            coverImage = nil
            loadedImagePath = ""
            loadedImageModifiedAt = nil
            loadedImageSize = nil
            return
        }

        let url = URL(fileURLWithPath: imagePath)
        let values = try? url.resourceValues(forKeys: [.contentModificationDateKey, .fileSizeKey])
        let modifiedAt = values?.contentModificationDate
        let size = values?.fileSize.map(UInt64.init)
        guard imagePath != loadedImagePath || modifiedAt != loadedImageModifiedAt || size != loadedImageSize else {
            return
        }

        coverImage = NSImage(contentsOfFile: imagePath)
        loadedImagePath = imagePath
        loadedImageModifiedAt = modifiedAt
        loadedImageSize = size
    }

    private func drawFallback() {
        if defaultCoverImage == nil {
            defaultCoverImage = TmrBrandAssets.loadLogoImage()
        }

        if let defaultCoverImage {
            defaultCoverImage.draw(in: containRect(imageSize: defaultCoverImage.size, bounds: bounds, maxBoundsFraction: 0.58))
            return
        }

        let attributes: [NSAttributedString.Key: Any] = [
            .font: OverlayTheme.font(family: OverlayTheme.defaultFontFamily, size: max(28, bounds.height / 12), weight: .bold),
            .foregroundColor: OverlayTheme.Colors.textPrimary
        ]
        let text = "TMR" as NSString
        let size = text.size(withAttributes: attributes)
        text.draw(
            at: NSPoint(x: bounds.midX - size.width / 2, y: bounds.midY - size.height / 2),
            withAttributes: attributes
        )
    }

    private func containRect(imageSize: NSSize, bounds: NSRect, maxBoundsFraction: CGFloat) -> NSRect {
        guard imageSize.width > 0, imageSize.height > 0, bounds.width > 0, bounds.height > 0 else {
            return bounds
        }

        let targetWidth = max(1, bounds.width * maxBoundsFraction)
        let targetHeight = max(1, bounds.height * maxBoundsFraction)
        let scale = min(targetWidth / imageSize.width, targetHeight / imageSize.height)
        let width = imageSize.width * scale
        let height = imageSize.height * scale
        return NSRect(
            x: bounds.midX - width / 2,
            y: bounds.midY - height / 2,
            width: width,
            height: height
        )
    }
}
