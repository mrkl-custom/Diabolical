using System.Text.Json.Serialization;

namespace Diabolical.Models;

public class ItemAffix
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("greaterAffix")]
    public bool GreaterAffix { get; set; }
}
