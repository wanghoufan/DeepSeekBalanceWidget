using System.Windows;
using DeepSeekBalanceWidget.Services;
using Rect = System.Windows.Rect;

namespace DeepSeekBalanceWidget.Tests;

public sealed class EdgeAutoHideCalculatorTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(8, 300, DockEdge.Left)]
    [InlineData(700, 10, DockEdge.Top)]
    [InlineData(1692, 300, DockEdge.Right)]
    [InlineData(700, 930, DockEdge.Bottom)]
    [InlineData(100, 100, DockEdge.None)]
    public void Detect_FindsNearestDesktopEdge(
        double left, double top, DockEdge expected)
    {
        var window = new Rect(left, top, 220, 100);

        Assert.Equal(expected,
            EdgeAutoHideCalculator.Detect(window, WorkArea, 16));
    }

    [Theory]
    [InlineData(DockEdge.Left, -208, 300)]
    [InlineData(DockEdge.Top, 700, -88)]
    [InlineData(DockEdge.Right, 1908, 300)]
    [InlineData(DockEdge.Bottom, 700, 1028)]
    public void HiddenPosition_LeavesTwelvePixelRevealStrip(
        DockEdge edge, double expectedLeft, double expectedTop)
    {
        var window = new Rect(700, 300, 220, 100);

        var result = EdgeAutoHideCalculator.HiddenPosition(
            edge, window, WorkArea, 12);

        Assert.Equal(expectedLeft, result.X);
        Assert.Equal(expectedTop, result.Y);
    }

    [Fact]
    public void VisiblePosition_ClampsWindowAlongDockedEdge()
    {
        var window = new Rect(1850, -20, 220, 100);

        var result = EdgeAutoHideCalculator.VisiblePosition(
            DockEdge.Top, window, WorkArea);

        Assert.Equal(1700, result.X);
        Assert.Equal(0, result.Y);
    }
}
