using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using Diabolical.Models;
using Diabolical.Views;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Diabolical.Services;

/// <summary>
/// Wires the global hotkey to a drag-select overlay and captures just the selected screen
/// region as an in-memory PNG. No window-title lookups or fixed coordinates — the
/// tooltip's position varies, so the user marks the region themselves each time.
/// </summary>
public class ScreenCaptureService : IDisposable
{
    private readonly HotkeyManager _hotkeyManager;

    public event Action<byte[]>? CaptureCompleted;
    public event Action? CaptureCancelled;

    public ScreenCaptureService(HotkeyManager hotkeyManager, HotkeySettings hotkeySettings)
    {
        _hotkeyManager = hotkeyManager;
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        _hotkeyManager.Register(hotkeySettings);
    }

    private void OnHotkeyPressed() => BeginCapture();

    /// <summary>
    /// Opens the drag-select overlay. Exposed directly (not just via the hotkey) so it can
    /// be triggered from a button for manual testing. Deliberately just Show(), not
    /// Activate() — the overlay is WS_EX_NOACTIVATE (see SelectionOverlayWindow) so it never
    /// steals focus from the game, which would otherwise hide the item tooltip we're trying
    /// to capture.
    /// </summary>
    public void BeginCapture()
    {
        var overlay = new SelectionOverlayWindow();
        overlay.SelectionCompleted += (_, region) => CaptureCompleted?.Invoke(CaptureRegion(region));
        overlay.SelectionCancelled += (_, _) => CaptureCancelled?.Invoke();
        overlay.Show();
    }

    private static byte[] CaptureRegion(Int32Rect region)
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

    public void Dispose()
    {
        _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
    }
}
