using System.IO;
using System.Net.Http;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Constructs the active IVisionService from AppSettings.VisionProvider. Manual selection,
/// no automatic fallback between providers — the only place that needs a new case when a
/// future vision provider is added. Owns the shared HttpClient/prompt-file loading so
/// AppSettings and the prompt file are each read from disk exactly once at startup, instead
/// of the constructed service re-reading them itself.
/// </summary>
public static class VisionServiceFactory
{
    private const string DefaultPromptRelativePath = "Prompts/item_extraction_prompt.txt";

    public static IVisionService Create(AppSettings settings)
    {
        var prompt = LoadDefaultPrompt();

        return settings.VisionProvider switch
        {
            "Gemini" => new GeminiVisionService(new HttpClient(), settings.Gemini, prompt),
            // Local inference is legitimately slow (large vision models on modest hardware can
            // take minutes), so Ollama gets a longer timeout than HttpClient's 100s default.
            "Ollama" => new OllamaVisionService(new HttpClient { Timeout = TimeSpan.FromMinutes(5) }, settings.Ollama, prompt),
            _ => throw new InvalidOperationException(
                $"Unknown VisionProvider '{settings.VisionProvider}'. Expected 'Gemini' or 'Ollama'.")
        };
    }

    private static string LoadDefaultPrompt() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, DefaultPromptRelativePath));
}
