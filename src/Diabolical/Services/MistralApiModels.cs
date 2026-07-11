using System.Text.Json.Serialization;

namespace Diabolical.Services;

/// <summary>
/// Wire shapes for Mistral's /v1/chat/completions endpoint. Mostly OpenAI-compatible, but
/// note "image_url" is a bare string (the data URL itself), not a nested {"url": ...} object
/// like OpenAI/xAI use. Internal — callers only see MistralVisionService and the
/// Diabolical.Models types.
/// </summary>
internal class MistralChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<MistralMessage> Messages { get; set; } = new();
}

internal class MistralMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public List<MistralContentPart> Content { get; set; } = new();
}

internal class MistralContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImageUrl { get; set; }
}

internal class MistralChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<MistralChoice>? Choices { get; set; }
}

internal class MistralChoice
{
    [JsonPropertyName("message")]
    public MistralResponseMessage? Message { get; set; }
}

internal class MistralResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
