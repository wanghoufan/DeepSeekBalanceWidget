using System;
using System.Windows;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace DeepSeekBalanceWidget.Services;

public enum DockEdge
{
    None,
    Left,
    Top,
    Right,
    Bottom
}

public static class EdgeAutoHideCalculator
{
    public static DockEdge Detect(Rect window, Rect workArea, double threshold)
    {
        var candidates = new[]
        {
            (Edge: DockEdge.Left, Distance: Math.Abs(window.Left - workArea.Left)),
            (Edge: DockEdge.Top, Distance: Math.Abs(window.Top - workArea.Top)),
            (Edge: DockEdge.Right, Distance: Math.Abs(window.Right - workArea.Right)),
            (Edge: DockEdge.Bottom, Distance: Math.Abs(window.Bottom - workArea.Bottom))
        };

        DockEdge nearest = DockEdge.None;
        double nearestDistance = threshold + 1;
        foreach (var candidate in candidates)
        {
            if (candidate.Distance <= threshold && candidate.Distance < nearestDistance)
            {
                nearest = candidate.Edge;
                nearestDistance = candidate.Distance;
            }
        }
        return nearest;
    }

    public static Point VisiblePosition(
        DockEdge edge, Rect window, Rect workArea)
    {
        double left = Math.Clamp(window.Left, workArea.Left,
            Math.Max(workArea.Left, workArea.Right - window.Width));
        double top = Math.Clamp(window.Top, workArea.Top,
            Math.Max(workArea.Top, workArea.Bottom - window.Height));

        return edge switch
        {
            DockEdge.Left => new Point(workArea.Left, top),
            DockEdge.Top => new Point(left, workArea.Top),
            DockEdge.Right => new Point(workArea.Right - window.Width, top),
            DockEdge.Bottom => new Point(left, workArea.Bottom - window.Height),
            _ => new Point(left, top)
        };
    }

    public static Point HiddenPosition(
        DockEdge edge, Rect window, Rect workArea, double revealThickness)
    {
        var visible = VisiblePosition(edge, window, workArea);
        return edge switch
        {
            DockEdge.Left => new Point(workArea.Left - window.Width + revealThickness, visible.Y),
            DockEdge.Top => new Point(visible.X, workArea.Top - window.Height + revealThickness),
            DockEdge.Right => new Point(workArea.Right - revealThickness, visible.Y),
            DockEdge.Bottom => new Point(visible.X, workArea.Bottom - revealThickness),
            _ => visible
        };
    }
}
