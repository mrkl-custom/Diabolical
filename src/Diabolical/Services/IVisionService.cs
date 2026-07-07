using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Extraction contract implemented by each vision provider (Gemini, Ollama, ...). Callers
/// send a cropped item-tooltip screenshot and get back a strict-JSON extraction result;
/// failures (bad HTTP status, unparseable response) are surfaced via ItemExtractionResult
/// rather than exceptions, so the capture flow can show a message instead of crashing.
/// </summary>
public interface IVisionService
{
    Task<ItemExtractionResult> ExtractItemAsync(
        byte[] imageBytes,
        string mimeType = "image/png",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight reachability check (no image, no extraction) used to drive the connection
    /// status indicator in the UI — should not count against the vision model's usage/rate limits.
    /// </summary>
    Task<VisionAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
}

public sealed record VisionAvailabilityResult(bool IsAvailable, string? Detail = null);
