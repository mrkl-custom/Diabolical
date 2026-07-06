using System.Text.Json;
using System.Text.Json.Serialization;

namespace Diabolical.Models;

public enum ItemRarity
{
    Common,
    Magic,
    Rare,
    Legendary,
    Unique,
    Mythic,
    Unknown
}

/// <summary>
/// Parses rarity case-insensitively and falls back to Unknown instead of throwing,
/// so an unexpected value from the vision LLM doesn't blow up the whole parse
/// before the user reaches the review/edit screen.
/// </summary>
public sealed class ItemRarityJsonConverter : JsonConverter<ItemRarity>
{
    public override ItemRarity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Enum.TryParse<ItemRarity>(value, ignoreCase: true, out var rarity)
            ? rarity
            : ItemRarity.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, ItemRarity value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
