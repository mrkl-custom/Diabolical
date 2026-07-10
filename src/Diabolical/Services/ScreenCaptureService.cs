using Diabolical.Models;
using Diabolical.Views;

namespace Diabolical.Services;

/// <summary>
/// Wires the global hotkey to a drag-select overlay and captures just the selected screen
/// region as an in-memory PNG. No window-title lookups or fixed coordinates — the
/// tooltip's position varies, so the user marks the region themselves each time.
/// </summary>
public class ScreenCaptureService
{
    public event Action<byte[]>? CaptureCompleted;
    public event Action? CaptureCancelled;

    public ScreenCaptureService(HotkeyManager hotkeyManager, HotkeySettings hotkeySettings)
    {
        hotkeyManager.Register(hotkeySettings, BeginCapture);
    }

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
        overlay.SelectionCompleted += (_, region) => CaptureCompleted?.Invoke(ScreenRegionCapture.Capture(region));
        overlay.SelectionCancelled += (_, _) => CaptureCancelled?.Invoke();
        overlay.Show();
    }
}
