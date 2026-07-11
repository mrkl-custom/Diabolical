using System.Text.Json.Serialization;

namespace Diabolical.Services;

/// <summary>
/// Wire shapes for xAI's OpenAI-compatible /v1/chat/completions endpoint. Internal — callers
/// only see GrokVisionService and the Diabolical.Models types.
/// </summary>
internal class GrokChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<GrokMessage> Messages { get; set; } = new();
}

internal class GrokMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public List<GrokContentPart> Content { get; set; } = new();
}

internal class GrokContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GrokImageUrl? ImageUrl { get; set; }
}

internal class GrokImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

internal class GrokChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<GrokChoice>? Choices { get; set; }
}

internal class GrokChoice
{
    [JsonPropertyName("message")]
    public GrokResponseMessage? Message { get; set; }
}

internal class GrokResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
