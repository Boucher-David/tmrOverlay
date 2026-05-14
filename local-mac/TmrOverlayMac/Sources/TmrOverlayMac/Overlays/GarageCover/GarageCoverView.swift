import AppKit

final class GarageCoverView: NSView {
    private static let telemetryFreshnessSeconds: TimeInterval = 2.5

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
        let telemetryFresh = snapshot.lastUpdatedAtUtc.map { Date().timeIntervalSince($0) <= Self.telemetryFreshnessSeconds } ?? false
        let shouldCover = !snapshot.isConnected
            || !snapshot.isCollecting
            || !telemetryFresh
            || snapshot.latestFrame == nil
            || snapshot.latestFrame?.isGarageVisible == true
        isHidden = !shouldCover
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
            defaultCoverImage = TmrBrandAssets.loadGarageCoverDefaultImage()
        }

        if let defaultCoverImage {
            defaultCoverImage.draw(in: coverRect(imageSize: defaultCoverImage.size, bounds: bounds))
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

}
