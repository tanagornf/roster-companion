using System.Windows;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class OverlayPositionerTests
{
    [TestMethod]
    public void Calculate_UsesDpiIndependentOffsets()
    {
        var placement = OverlayPositioner.Calculate(
            new Rect(200, 100, 2400, 1500),
            new Rect(0, 0, 2880, 1620),
            dpi: 144);

        Assert.IsNotNull(placement);
        Assert.AreEqual(200d / 1.5 + OverlayPositioner.SelectorOffsetX, placement.SelectorLeft, 0.01);
        Assert.AreEqual(100d / 1.5 + OverlayPositioner.SelectorOffsetY, placement.SelectorTop, 0.01);
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
}
