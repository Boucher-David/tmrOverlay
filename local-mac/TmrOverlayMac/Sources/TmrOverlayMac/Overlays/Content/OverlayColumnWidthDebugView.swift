import AppKit

final class OverlayColumnWidthDebugView: NSView, NSTextFieldDelegate {
    static let preferredSize = NSSize(width: 500, height: 760)

    private enum WidthBinding {
        case overlay(OverlayDefinition)
        case column(OverlayContentDefinition, OverlayContentColumnDefinition)
    }

    private var settings: ApplicationSettings
    private let overlayDefinitions: [OverlayDefinition]
    private let onOverlayWidthChanged: (OverlayDefinition, Int) -> Void
    private let onColumnWidthChanged: (OverlayContentDefinition, OverlayContentColumnDefinition, Int) -> Void
    private var bindings: [String: WidthBinding] = [:]
    private var fields: [String: NSTextField] = [:]
    private let scrollView = NSScrollView()
    private let documentView = NSView()

    init(
        settings: ApplicationSettings,
        overlayDefinitions: [OverlayDefinition],
        onOverlayWidthChanged: @escaping (OverlayDefinition, Int) -> Void,
        onColumnWidthChanged: @escaping (OverlayContentDefinition, OverlayContentColumnDefinition, Int) -> Void
    ) {
        self.settings = settings
        self.overlayDefinitions = overlayDefinitions
        self.onOverlayWidthChanged = onOverlayWidthChanged
        self.onColumnWidthChanged = onColumnWidthChanged
        super.init(frame: NSRect(origin: .zero, size: Self.preferredSize))
        wantsLayer = true
        layer?.backgroundColor = NSColor(red: 0.055, green: 0.070, blue: 0.082, alpha: 1).cgColor
        buildControls()
    }

    required init?(coder: NSCoder) {
        nil
    }

    override var isFlipped: Bool {
        true
    }

    override func layout() {
        super.layout()
        scrollView.frame = bounds
    }

    func applySettings(_ settings: ApplicationSettings) {
        self.settings = settings
        for (key, field) in fields {
            guard field.currentEditor() == nil,
                  let binding = bindings[key] else {
                continue
            }

            field.stringValue = String(currentWidth(for: binding))
        }
    }

    private func buildControls() {
        subviews.forEach { $0.removeFromSuperview() }
        documentView.subviews.forEach { $0.removeFromSuperview() }
        bindings.removeAll()
        fields.removeAll()

        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.borderType = .noBorder
        scrollView.drawsBackground = false
        scrollView.frame = bounds
        addSubview(scrollView)

        var y: CGFloat = 16
        documentView.addSubview(label("Temporary width editor", frame: NSRect(x: 18, y: y, width: 290, height: 22), size: 14, weight: .bold))
        y += 24
        documentView.addSubview(label("Any positive px value is accepted; production ranges are reference only.", frame: NSRect(x: 18, y: y, width: 430, height: 18), size: 11, color: mutedColor))
        y += 36

        y = addOverlaySection(y: y)
        y = addColumnSection(title: "Standings Columns", definition: OverlayContentColumns.standings, y: y + 18)
        y = addColumnSection(title: "Relative Columns", definition: OverlayContentColumns.relative, y: y + 18)

        documentView.frame = NSRect(x: 0, y: 0, width: bounds.width - 18, height: y + 18)
        scrollView.documentView = documentView
    }

    private func addOverlaySection(y startY: CGFloat) -> CGFloat {
        var y = startY
        documentView.addSubview(label("Overlay Widths", frame: NSRect(x: 18, y: y, width: 180, height: 20), size: 12, weight: .bold))
        y += 28
        documentView.addSubview(header("Overlay", x: 28, y: y, width: 190))
        documentView.addSubview(header("Default", x: 230, y: y, width: 62, alignment: .right))
        documentView.addSubview(header("Width", x: 306, y: y, width: 72, alignment: .right))
        documentView.addSubview(header("Reference", x: 392, y: y, width: 64, alignment: .right))
        y += 22

        for definition in overlayDefinitions {
            let current = overlaySettings(for: definition)
            let row = rowView(y: y)
            row.addSubview(label(definition.displayName, frame: NSRect(x: 10, y: 6, width: 190, height: 18), size: 12))
            row.addSubview(label(String(Int(definition.defaultSize.width.rounded())), frame: NSRect(x: 206, y: 6, width: 58, height: 18), size: 12, color: mutedColor, alignment: .right))

            let key = "overlay|\(definition.id)"
            let field = widthField(key: key, value: Int(current.width.rounded()), x: 282)
            bindings[key] = .overlay(definition)
            fields[key] = field
            row.addSubview(field)

            row.addSubview(label(
                "scale",
                frame: NSRect(x: 374, y: 6, width: 56, height: 18),
                size: 12,
                color: mutedColor,
                alignment: .right
            ))
            documentView.addSubview(row)
            y += 36
        }

        return y
    }

    private func addColumnSection(title: String, definition: OverlayContentDefinition, y startY: CGFloat) -> CGFloat {
        var y = startY
        guard !definition.columns.isEmpty else {
            return y
        }

        documentView.addSubview(label(title, frame: NSRect(x: 18, y: y, width: 180, height: 20), size: 12, weight: .bold))
        y += 28
        documentView.addSubview(header("Column", x: 28, y: y, width: 190))
        documentView.addSubview(header("Default", x: 230, y: y, width: 62, alignment: .right))
        documentView.addSubview(header("Width", x: 306, y: y, width: 72, alignment: .right))
        documentView.addSubview(header("Reference", x: 392, y: y, width: 64, alignment: .right))
        y += 22

        let states = OverlayContentColumns.columnStates(
            for: definition,
            settings: overlaySettings(for: definition)
        )
        for state in states.sorted(by: { $0.definition.defaultOrder < $1.definition.defaultOrder }) {
            let row = rowView(y: y)
            row.addSubview(label(state.definition.label, frame: NSRect(x: 10, y: 6, width: 190, height: 18), size: 12))
            row.addSubview(label(String(state.definition.defaultWidth), frame: NSRect(x: 206, y: 6, width: 58, height: 18), size: 12, color: mutedColor, alignment: .right))

            let key = "column|\(definition.overlayId)|\(state.definition.id)"
            let field = widthField(key: key, value: state.width, x: 282)
            bindings[key] = .column(definition, state.definition)
            fields[key] = field
            row.addSubview(field)

            row.addSubview(label(
                "\(state.definition.minimumWidth)-\(state.definition.maximumWidth)",
                frame: NSRect(x: 362, y: 6, width: 76, height: 18),
                size: 12,
                color: mutedColor,
                alignment: .right
            ))
            documentView.addSubview(row)
            y += 36
        }

        return y
    }

    private func rowView(y: CGFloat) -> NSView {
        let row = NSView(frame: NSRect(x: 18, y: y, width: Self.preferredSize.width - 54, height: 30))
        row.wantsLayer = true
        row.layer?.cornerRadius = 5
        row.layer?.backgroundColor = NSColor(red: 0.086, green: 0.108, blue: 0.126, alpha: 1).cgColor
        return row
    }

    private func widthField(key: String, value: Int, x: CGFloat) -> NSTextField {
        let field = NSTextField(string: String(max(1, value)))
        field.frame = NSRect(x: x, y: 2, width: 72, height: 26)
        field.identifier = NSUserInterfaceItemIdentifier(key)
        field.alignment = .right
        field.font = NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .regular)
        field.textColor = textColor
        field.backgroundColor = NSColor(red: 0.040, green: 0.052, blue: 0.064, alpha: 1)
        field.isBordered = true
        field.bezelStyle = .roundedBezel
        field.delegate = self
        field.target = self
        field.action = #selector(widthFieldCommitted(_:))
        return field
    }

    private func header(_ text: String, x: CGFloat, y: CGFloat, width: CGFloat, alignment: NSTextAlignment = .left) -> NSTextField {
        label(text, frame: NSRect(x: x, y: y, width: width, height: 16), size: 10, color: mutedColor, alignment: alignment)
    }

    private func label(
        _ text: String,
        frame: NSRect,
        size: CGFloat,
        weight: NSFont.Weight = .regular,
        color: NSColor? = nil,
        alignment: NSTextAlignment = .left
    ) -> NSTextField {
        let field = NSTextField(labelWithString: text)
        field.frame = frame
        field.font = OverlayTheme.font(family: OverlayTheme.defaultFontFamily, size: size, weight: weight)
        field.textColor = color ?? textColor
        field.alignment = alignment
        return field
    }

    private func overlaySettings(for definition: OverlayDefinition) -> OverlaySettings {
        var copy = settings
        return copy.overlay(
            id: definition.id,
            defaultSize: definition.defaultSize
        )
    }

    private func overlaySettings(for definition: OverlayContentDefinition) -> OverlaySettings {
        var copy = settings
        return copy.overlay(
            id: definition.overlayId,
            defaultSize: .zero
        )
    }

    private func currentWidth(for binding: WidthBinding) -> Int {
        switch binding {
        case let .overlay(definition):
            Int(overlaySettings(for: definition).width.rounded())
        case let .column(definition, column):
            OverlayContentColumns
                .columnStates(for: definition, settings: overlaySettings(for: definition))
                .first { $0.definition.id == column.id }?
                .width ?? column.defaultWidth
        }
    }

    @objc private func widthFieldCommitted(_ sender: NSTextField) {
        applyWidth(sender, commit: true)
    }

    func controlTextDidChange(_ obj: Notification) {
        guard let field = obj.object as? NSTextField else {
            return
        }
        applyWidth(field, commit: false)
    }

    func controlTextDidEndEditing(_ obj: Notification) {
        guard let field = obj.object as? NSTextField else {
            return
        }
        applyWidth(field, commit: true)
    }

    private func applyWidth(_ field: NSTextField, commit: Bool) {
        guard let key = field.identifier?.rawValue,
              let binding = bindings[key],
              let parsed = Int(field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)) else {
            return
        }

        let width = max(1, parsed)
        if commit {
            field.stringValue = String(width)
        } else if parsed < 1 {
            return
        }

        switch binding {
        case let .overlay(definition):
            onOverlayWidthChanged(definition, width)
        case let .column(definition, column):
            onColumnWidthChanged(definition, column, width)
        }
    }

    private var textColor: NSColor {
        NSColor(red: 0.86, green: 0.90, blue: 0.93, alpha: 1)
    }

    private var mutedColor: NSColor {
        NSColor(red: 0.56, green: 0.64, blue: 0.69, alpha: 1)
    }
}
