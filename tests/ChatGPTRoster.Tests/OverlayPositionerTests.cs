using System.Windows;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class OverlayPositionerTests
{
    [TestMethod]
    public void Calculate_ReservesTitleBarMenusAndWindowControlsAtHighDpi()
    {
        var placement = OverlayPositioner.Calculate(
            new Rect(200, 100, 2400, 1500),
            new Rect(0, 0, 2880, 1620),
            dpi: 144);

        Assert.IsNotNull(placement);
        var windowRight = 2600d / 1.5;
        Assert.AreEqual(windowRight - 160 - placement.SelectorWidth, placement.SelectorLeft, 0.01);
        Assert.AreEqual(100d / 1.5 + 4, placement.SelectorTop, 0.01);
        Assert.IsTrue(placement.SelectorLeft > 200d / 1.5 + 320);
        Assert.IsTrue(placement.DropdownTop >= placement.SelectorTop + placement.SelectorHeight);
        Assert.IsTrue(placement.DropdownMaxHeight >= 220);
    }

    [TestMethod]
    public void Calculate_ClampsDropdownToNegativeMonitorWorkArea()
    {
        var placement = OverlayPositioner.Calculate(
            new Rect(-1928, -2, 1936, 1096),
            new Rect(-1920, 0, 1920, 1080),
            dpi: 96);

        Assert.IsNotNull(placement);
        Assert.IsTrue(placement.DropdownLeft >= -1920 + OverlayPositioner.WorkAreaMargin);
        Assert.IsTrue(placement.DropdownLeft + OverlayPositioner.DropdownWidth <= -OverlayPositioner.WorkAreaMargin);
    }

    [TestMethod]
    public void Calculate_HidesForWindowTooNarrowToAvoidCoveringControls()
    {
        Assert.IsNull(OverlayPositioner.Calculate(
            new Rect(0, 0, 540, 700),
            new Rect(0, 0, 1920, 1080),
            dpi: 96));
    }

    [TestMethod]
    public void Calculate_HidesWhenScaledWindowCannotFitTitleBarControls()
    {
        Assert.IsNull(OverlayPositioner.Calculate(
            new Rect(0, 0, 900, 1050),
            new Rect(0, 0, 2880, 1620),
            dpi: 144));
    }

    [TestMethod]
    public void Calculate_FollowsRightEdgeWhenResized()
    {
        var initial = OverlayPositioner.Calculate(new Rect(100, 50, 1000, 700), new Rect(0, 0, 1920, 1080), 96)!;
        var resized = OverlayPositioner.Calculate(new Rect(100, 50, 1200, 700), new Rect(0, 0, 1920, 1080), 96)!;

        Assert.AreEqual(200, resized.SelectorLeft - initial.SelectorLeft, 0.01);
        Assert.AreEqual(initial.SelectorTop, resized.SelectorTop);
    }
}
