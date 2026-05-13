import Foundation

struct AppVersionInfo: Codable {
    let productName: String
    let version: String
    let operatingSystem: String
    let processArchitecture: String

    static let current = AppVersionInfo(
        productName: "Tech Mates Racing Overlay",
        version: "0.19.0",
        operatingSystem: ProcessInfo.processInfo.operatingSystemVersionString,
        processArchitecture: AppVersionInfo.architecture
    )

    private static var architecture: String {
        #if arch(arm64)
        "arm64"
        #elseif arch(x86_64)
        "x86_64"
        #else
        "unknown"
        #endif
    }
}
