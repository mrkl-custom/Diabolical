using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Constructs the active IVisionService from AppSettings.VisionProvider. Manual selection,
/// no automatic fallback between providers — the only place that needs a new case when a
/// future vision provider is added.
/// </summary>
public static class VisionServiceFactory
{
    public static IVisionService Create(AppSettings settings)
    {
        return settings.VisionProvider switch
        {
            "Gemini" => new GeminiVisionService(),
            "Ollama" => new OllamaVisionService(),
            _ => throw new InvalidOperationException(
                $"Unknown VisionProvider '{settings.VisionProvider}'. Expected 'Gemini' or 'Ollama'.")
        };
    }
}
