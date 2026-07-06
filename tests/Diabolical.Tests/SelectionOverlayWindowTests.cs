using System.Windows;
using System.Windows.Media;
using Diabolical.Views;

namespace Diabolical.Tests;

public class SelectionOverlayWindowTests
{
    [Fact]
    public void ToDeviceRect_NoScaling_MapsWindowLocalToAbsoluteScreenPixels()
    {
        var selection = new Rect(new Point(100, 150), new Point(300, 400));

        var result = SelectionOverlayWindow.ToDeviceRect(
            windowLeft: 1920, // second monitor starting at x=1920
            windowTop: 0,
            selection: selection,
            transformToDevice: Matrix.Identity);

        Assert.Equal(2020, result.X);
        Assert.Equal(150, result.Y);
        Assert.Equal(200, result.Width);
        Assert.Equal(250, result.Height);
    }

    [Fact]
    public void ToDeviceRect_150PercentScaling_ScalesDipsToDevicePixels()
    {
        var selection = new Rect(new Point(0, 0), new Point(100, 200));
        var scale = new Matrix(1.5, 0, 0, 1.5, 0, 0);

        var result = SelectionOverlayWindow.ToDeviceRect(0, 0, selection, scale);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(150, result.Width);
        Assert.Equal(300, result.Height);
    }
}
