using System.Text.Json.Serialization;

namespace Diabolical.Models;

public class EquipmentItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    [JsonConverter(typeof(ItemRarityJsonConverter))]
    public ItemRarity Rarity { get; set; }

    [JsonPropertyName("quality")]
    [JsonConverter(typeof(ItemQualityJsonConverter))]
    public ItemQuality Quality { get; set; }

    [JsonPropertyName("itemPower")]
    public int ItemPower { get; set; }

    [JsonPropertyName("affixes")]
    public List<ItemAffix> Affixes { get; set; } = new();

    [JsonPropertyName("specialEffects")]
    public List<string> SpecialEffects { get; set; } = new();

    [JsonPropertyName("transfigured")]
    public bool Transfigured { get; set; }

    [JsonPropertyName("modifiable")]
    public bool Modifiable { get; set; } = true;
}
