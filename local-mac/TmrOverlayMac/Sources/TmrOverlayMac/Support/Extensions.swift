import AppKit
import Foundation

extension Date {
    var unixTimeMilliseconds: Int64 {
        Int64((timeIntervalSince1970 * 1000.0).rounded())
    }
}

extension Data {
    mutating func appendInt32LE(_ value: Int32) {
        var littleEndian = value.littleEndian
        Swift.withUnsafeBytes(of: &littleEndian) { bytes in
            append(contentsOf: bytes)
        }
    }

    mutating func appendInt64LE(_ value: Int64) {
        var littleEndian = value.littleEndian
        Swift.withUnsafeBytes(of: &littleEndian) { bytes in
            append(contentsOf: bytes)
        }
    }

    mutating func appendDoubleLE(_ value: Double) {
        var littleEndian = value.bitPattern.littleEndian
        Swift.withUnsafeBytes(of: &littleEndian) { bytes in
            append(contentsOf: bytes)
        }
    }

    mutating func replaceInt32LE(_ value: Int32, at offset: Int) {
        let littleEndian = value.littleEndian
        replaceValue(littleEndian, at: offset)
    }

    mutating func replaceDoubleLE(_ value: Double, at offset: Int) {
        let littleEndian = value.bitPattern.littleEndian
        replaceValue(littleEndian, at: offset)
    }

    private mutating func replaceValue<T>(_ value: T, at offset: Int) {
        var mutableValue = value
        Swift.withUnsafeBytes(of: &mutableValue) { bytes in
            replaceSubrange(offset..<(offset + bytes.count), with: bytes)
        }
    }
}

extension NSColor {
    convenience init(red255 red: CGFloat, green: CGFloat, blue: CGFloat, alpha: CGFloat = 1) {
        self.init(
            calibratedRed: red / 255.0,
            green: green / 255.0,
            blue: blue / 255.0,
            alpha: alpha
        )
    }
}

extension NSLock {
    func withLock<T>(_ action: () -> T) -> T {
        lock()
        defer { unlock() }
        return action()
    }
}

extension String {
    func slug() -> String {
        var output = ""
        var previousWasSeparator = false

        for character in lowercased() {
            if character.isLetter || character.isNumber {
                output.append(character)
                previousWasSeparator = false
                continue
            }

            if !previousWasSeparator {
                output.append("-")
                previousWasSeparator = true
            }
        }

        return output.trimmingCharacters(in: CharacterSet(charactersIn: "-"))
    }
}
