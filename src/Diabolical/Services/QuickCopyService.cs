using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Independent of ScreenCaptureService/ItemDatabaseService by design (see CLAUDE.md's
/// Decisions Log) — a throwaway single-item lookup for pasting into an AI assistant, not
/// part of gear tracking. Drag-selects with the same overlay, sends the crop to the
/// configured vision provider, and copies the extracted item straight to the clipboard.
/// No Review/Edit dialog, no character context, nothing saved to data/characters/.
/// </summary>
public class QuickCopyService
{
    private readonly IVisionService _visionService;

    /// <summary>Fires with a human-readable status line, mirroring the main capture flow's status messages.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Idle → Capturing (overlay open) → Processing (sent to vision model) → Idle/Error.</summary>
    public event Action<ActivityState>? ActivityChanged;

    /// <summary>Fires once the extracted item's JSON has actually landed on the clipboard, for a success cue.</summary>
    public event Action? ItemCopied;

    public QuickCopyService(HotkeyManager hotkeyManager, HotkeySettings hotkeySettings, IVisionService visionService)
    {
        _visionService = visionService;
        hotkeyManager.Register(hotkeySettings, BeginQuickCopy);
    }

    public void BeginQuickCopy()
    {
        OverlayCaptureSession.Begin(
            activityChanged: state => ActivityChanged?.Invoke(state),
            onCaptured: bytes => _ = OnCapturedAsync(bytes),
            onCancelled: () => StatusChanged?.Invoke("Quick copy cancelled."));
    }

    private async Task OnCapturedAsync(byte[] imageBytes)
    {
        ActivityChanged?.Invoke(ActivityState.Processing);
        StatusChanged?.Invoke("Quick copy: sending capture to the vision model...");

        var result = await _visionService.ExtractItemAsync(imageBytes);

        if (!result.Success || result.Item is null)
        {
            StatusChanged?.Invoke($"Quick copy extraction failed: {result.ErrorMessage}");
            ActivityChanged?.Invoke(ActivityState.Error);
            return;
        }

        var item = result.Item.ToEquipmentItem();
        var json = ItemDatabaseService.SerializeItem(result.Item.Slot, item);
        ClipboardHelper.SetTextWithRetry(json);

        StatusChanged?.Invoke($"Quick copy: copied '{item.Name}' JSON to clipboard.");
        ActivityChanged?.Invoke(ActivityState.Idle);
        ItemCopied?.Invoke();
    }
}
