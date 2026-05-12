using System.Text.Json;
using TmrOverlay.App.Diagnostics;
using Xunit;

namespace TmrOverlay.App.Tests.Diagnostics;

public sealed class WindowsTopLevelWindowDiagnosticsContractTests
{
    [Fact]
    public void SnapshotContract_SerializesTopMostForWindowsAndForegroundHistory()
    {
        var snapshot = new TopLevelWindowDiagnosticsSnapshot(
            CapturedAtUtc: DateTimeOffset.Parse("2026-05-12T12:00:00Z"),
            Available: true,
            UnavailableReason: null,
            Error: null,
            ProcessId: 123,
            ForegroundWindow: new TopLevelWindowSnapshot(
                ZOrderIndex: 0,
                Hwnd: "0x0000000000000001",
                OwnerHwnd: null,
                ProcessId: 10,
                ProcessName: "TmrOverlay",
                Title: "TmrOverlay",
                ClassName: "WindowsForms10.Window.8.app.0",
                Visible: true,
                Minimized: false,
                TopMost: true,
                Style: "0x0000000000000000",
                ExStyle: "0x0000000000000008",
                Bounds: new TopLevelWindowBounds(1, 2, 3, 4)),
            ForegroundHistory:
            [
                new ForegroundWindowChangeSnapshot(
                    AtUtc: DateTimeOffset.Parse("2026-05-12T11:59:59Z"),
                    Hwnd: "0x0000000000000002",
                    ProcessId: 20,
                    ProcessName: "iRacingSim64DX11",
                    Title: "iRacing.com Simulator",
                    ClassName: "IRacingSimWnd",
                    TopMost: false,
                    ZOrderIndex: 3)
            ],
            WindowCount: 1,
            Windows:
            [
                new TopLevelWindowSnapshot(
                    ZOrderIndex: 0,
                    Hwnd: "0x0000000000000001",
                    OwnerHwnd: null,
                    ProcessId: 10,
                    ProcessName: "TmrOverlay",
                    Title: "Stream Chat",
                    ClassName: "WindowsForms10.Window.8.app.0",
                    Visible: true,
                    Minimized: false,
                    TopMost: true,
                    Style: "0x0000000000000000",
                    ExStyle: "0x0000000000000008",
                    Bounds: new TopLevelWindowBounds(1, 2, 3, 4))
            ]);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Contains("\"topMost\":true", json);
        Assert.Contains("\"topMost\":false", json);
        Assert.Contains("\"zOrderIndex\":0", json);
    }
}
