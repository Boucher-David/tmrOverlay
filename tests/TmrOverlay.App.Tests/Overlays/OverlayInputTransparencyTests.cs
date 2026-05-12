using TmrOverlay.App.Overlays.DesignV2;
using Xunit;

namespace TmrOverlay.App.Tests.Overlays;

public sealed class OverlayInputTransparencyTests
{
    [Fact]
    public void DesignV2InputTransparentKind_IncludesStreamChatClickThroughOverlay()
    {
        Assert.True(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.StreamChat));
    }

    [Fact]
    public void DesignV2InputTransparentKind_ExcludesDraggableDataOverlays()
    {
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.Flags));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.Standings));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.Relative));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.TrackMap));
        Assert.False(DesignV2LiveOverlayForm.IsInputTransparentKind(DesignV2LiveOverlayKind.GapToLeader));
    }
}
