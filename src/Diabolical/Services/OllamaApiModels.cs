using System.Text.Json.Serialization;

namespace Diabolical.Services;

/// <summary>
/// Wire shapes for Ollama's /api/generate endpoint. Internal — callers only see
/// OllamaVisionService and the Diabolical.Models types.
/// </summary>
internal class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

internal class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
}

/// <summary>Wire shape for Ollama's /api/tags endpoint (lists locally-pulled models).</summary>
internal class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaTagModel>? Models { get; set; }
}

internal class OllamaTagModel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
