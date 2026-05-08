import AppKit

private var retainedDelegate: AppDelegate?

public func runTmrOverlayMac() {
    let application = NSApplication.shared
    let delegate = AppDelegate()
    retainedDelegate = delegate
    application.delegate = delegate
    application.run()
}
