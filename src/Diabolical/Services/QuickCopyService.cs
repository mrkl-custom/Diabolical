using System.Windows;
using Diabolical.Models;
using Diabolical.Views;

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

    public QuickCopyService(HotkeyManager hotkeyManager, HotkeySettings hotkeySettings, IVisionService visionService)
    {
        _visionService = visionService;
        hotkeyManager.Register(hotkeySettings, BeginQuickCopy);
    }

    public void BeginQuickCopy()
    {
        var overlay = new SelectionOverlayWindow();
        overlay.SelectionCompleted += async (_, region) => await OnRegionSelectedAsync(region);
        overlay.SelectionCancelled += (_, _) => StatusChanged?.Invoke("Quick copy cancelled.");
        overlay.Show();
    }

    private async Task OnRegionSelectedAsync(Int32Rect region)
    {
        StatusChanged?.Invoke("Quick copy: sending capture to the vision model...");

        var imageBytes = ScreenRegionCapture.Capture(region);
        var result = await _visionService.ExtractItemAsync(imageBytes);

        if (!result.Success || result.Item is null)
        {
            StatusChanged?.Invoke($"Quick copy extraction failed: {result.ErrorMessage}");
            return;
        }

        var item = result.Item.ToEquipmentItem();
        var json = ItemDatabaseService.SerializeItem(result.Item.Slot, item);
        Clipboard.SetText(json);

        StatusChanged?.Invoke($"Quick copy: copied '{item.Name}' JSON to clipboard.");
    }
}
