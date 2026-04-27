using System;
using System.Windows;
using System.Windows.Media;

namespace TransparentCaptureApp.Utilities;

public static class WindowCoordinateUtility
{
    public static Int32Rect ToDeviceRect(Window window, FrameworkElement element)
    {
        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var topLeft = element.PointToScreen(new System.Windows.Point(0, 0));
        var width = element.ActualWidth * transform.M11;
        var height = element.ActualHeight * transform.M22;

        return new Int32Rect(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            Math.Max(1, (int)Math.Round(width)),
            Math.Max(1, (int)Math.Round(height)));
    }
}
