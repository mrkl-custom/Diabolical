using System.Text.Json.Serialization;

namespace Diabolical.Models;

/// <summary>
/// Raw shape of the Gemini extraction prompt's output. Mirrors EquipmentItem plus a
/// "slot" field that isn't part of the stored schema — slot only exists to tell the
/// merge step (and the review UI, in case Gemini guesses wrong) where to place the item.
/// </summary>
public class ParsedItemExtraction
{
    [JsonPropertyName("slot")]
    public string Slot { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    [JsonConverter(typeof(ItemRarityJsonConverter))]
    public ItemRarity Rarity { get; set; }

    [JsonPropertyName("itemPower")]
    public int ItemPower { get; set; }

    [JsonPropertyName("affixes")]
    public List<string> Affixes { get; set; } = new();

    [JsonPropertyName("aspect")]
    public string? Aspect { get; set; }

    public EquipmentItem ToEquipmentItem() => new()
    {
        Name = Name,
        Rarity = Rarity,
        ItemPower = ItemPower,
        Affixes = Affixes,
        Aspect = Aspect
    };
}
