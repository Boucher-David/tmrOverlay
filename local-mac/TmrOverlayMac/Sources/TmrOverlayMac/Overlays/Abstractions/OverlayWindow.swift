import AppKit

final class OverlayWindow: NSWindow {
    var frameDidChange: ((NSRect) -> Void)?

    private var dragStartMouseLocation: NSPoint?
    private var dragStartWindowOrigin: NSPoint?

    override var canBecomeKey: Bool {
        true
    }

    override func mouseDown(with event: NSEvent) {
        dragStartMouseLocation = NSEvent.mouseLocation
        dragStartWindowOrigin = frame.origin
    }

    override func mouseDragged(with event: NSEvent) {
        guard let dragStartMouseLocation, let dragStartWindowOrigin else {
            return
        }

        let currentMouseLocation = NSEvent.mouseLocation
        setFrameOrigin(NSPoint(
            x: dragStartWindowOrigin.x + currentMouseLocation.x - dragStartMouseLocation.x,
            y: dragStartWindowOrigin.y + currentMouseLocation.y - dragStartMouseLocation.y
        ))
    }

    override func mouseUp(with event: NSEvent) {
        dragStartMouseLocation = nil
        dragStartWindowOrigin = nil
        frameDidChange?(frame)
    }
}
