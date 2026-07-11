using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diabolical.Models;

public enum AffixSource
{
    Base,
    Tempered,
    Transfigured,
    Implicit
}

/// <summary>
/// Parses source case-insensitively and falls back to Base — the extraction prompt's own
/// documented default when the vision model can't tell a roll's origin apart.
/// </summary>
public sealed class AffixSourceJsonConverter : JsonConverter<AffixSource>
{
    public override AffixSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Enum.TryParse<AffixSource>(value, ignoreCase: true, out var source)
            ? source
            : AffixSource.Base;
    }

    public override void Write(Utf8JsonWriter writer, AffixSource value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class ItemAffix
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    [JsonConverter(typeof(AffixSourceJsonConverter))]
    public AffixSource Source { get; set; } = AffixSource.Base;

    [JsonPropertyName("greaterAffix")]
    public bool GreaterAffix { get; set; }
}
