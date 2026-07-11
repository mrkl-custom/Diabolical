using Diabolical.Models;
using Diabolical.Views;

namespace Diabolical.Services;

/// <summary>
/// Shared overlay-session mechanics for the two capture flows (main capture + Quick Copy):
/// open the drag-select overlay, wire its completed/cancelled events, fire the Capturing/Idle
/// activity transitions, and hand back the selected region as captured PNG bytes. Each caller
/// owns its own downstream pipeline (what happens once bytes exist) — this only exists to
/// avoid duplicating the overlay-wiring boilerplate between ScreenCaptureService and
/// QuickCopyService.
///
/// Also owns the reentrancy guard for both hotkeys: since both flows fire Capturing at the
/// start of Begin(), the guard is set here; it's released from MainWindow's SetActivity — the
/// single choke point every flow's terminal Idle/Error transition passes through, for both the
/// main capture flow (capture → extract → review → save) and Quick Copy (capture → extract →
/// clipboard) — so a hotkey press mid-flow is a no-op instead of stacking overlays or racing
/// two save cycles against the same character file.
/// </summary>
internal static class OverlayCaptureSession
{
    private static bool _captureInFlight;

    public static void Begin(Action<ActivityState> activityChanged, Action<byte[]> onCaptured, Action onCancelled)
    {
        if (_captureInFlight)
        {
            return;
        }

        _captureInFlight = true;

        var overlay = new SelectionOverlayWindow();
        overlay.SelectionCompleted += (_, region) => onCaptured(ScreenRegionCapture.Capture(region));
        overlay.SelectionCancelled += (_, _) =>
        {
            activityChanged(ActivityState.Idle);
            onCancelled();
        };
        activityChanged(ActivityState.Capturing);
        overlay.Show();
    }

    /// <summary>Releases the reentrancy guard once a flow reaches a terminal state.</summary>
    public static void EndCapture() => _captureInFlight = false;
}
