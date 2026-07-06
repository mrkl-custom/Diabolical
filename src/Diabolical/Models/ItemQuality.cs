using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diabolical.Models;

public enum ItemQuality
{
    Normal,
    Ancestral,
    Unknown
}

/// <summary>
/// Parses quality case-insensitively and falls back to Unknown instead of throwing,
/// mirroring ItemRarityJsonConverter's leniency toward unexpected vision-model output.
/// </summary>
public sealed class ItemQualityJsonConverter : JsonConverter<ItemQuality>
{
    public override ItemQuality Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Enum.TryParse<ItemQuality>(value, ignoreCase: true, out var quality)
            ? quality
            : ItemQuality.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, ItemQuality value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
