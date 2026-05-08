// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "TmrOverlayMac",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .executable(name: "TmrOverlayMac", targets: ["TmrOverlayMac"]),
        .executable(name: "TmrOverlayMacScreenshots", targets: ["TmrOverlayMacScreenshots"])
    ],
    targets: [
        .target(
            name: "TmrOverlayMacCore",
            path: "Sources/TmrOverlayMac"
        ),
        .executableTarget(
            name: "TmrOverlayMac",
            dependencies: ["TmrOverlayMacCore"],
            path: "Sources/TmrOverlayMacRunner"
        ),
        .executableTarget(
            name: "TmrOverlayMacScreenshots",
            dependencies: ["TmrOverlayMacCore"],
            path: "Sources/TmrOverlayMacScreenshots"
        ),
        .testTarget(
            name: "TmrOverlayMacCoreTests",
            dependencies: ["TmrOverlayMacCore"]
        )
    ]
)
