using System.Text.Json.Serialization;

namespace Diabolical.Models;

public class EquipmentItem
{
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
}
