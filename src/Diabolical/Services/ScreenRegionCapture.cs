using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Diabolical.Services;

/// <summary>
/// Grabs a screen region as an in-memory PNG. Shared by ScreenCaptureService (main
/// capture-and-save flow) and QuickCopyService (throwaway lookup flow) — both drag-select
/// via the same overlay and need identical pixel-capture logic.
/// </summary>
internal static class ScreenRegionCapture
{
    public static byte[] Capture(Int32Rect region)
    {
        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(new Point(region.X, region.Y), Point.Empty, new Size(region.Width, region.Height));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
