using System.Text.Json.Serialization;

namespace Diabolical.Models;

public class EquipmentItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("itemType")]
    public string ItemType { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    [JsonConverter(typeof(ItemRarityJsonConverter))]
    public ItemRarity Rarity { get; set; }

    [JsonPropertyName("quality")]
    [JsonConverter(typeof(ItemQualityJsonConverter))]
    public ItemQuality Quality { get; set; }

    [JsonPropertyName("itemPower")]
    public int ItemPower { get; set; }

    /// <summary>
    /// The tooltip's numeric "Quality" stat (masterworking quality) — a separate axis from
    /// the Normal/Ancestral <see cref="Quality"/> field above, which shares the same word by
    /// coincidence. Normally 0-25 (masterworking upgrade ranks), but Transfiguration can push
    /// it higher, so no upper bound is enforced here.
    /// </summary>
    [JsonPropertyName("masterworkingQuality")]
    public int MasterworkingQuality { get; set; }

    [JsonPropertyName("affixes")]
    public List<ItemAffix> Affixes { get; set; } = new();

    [JsonPropertyName("specialEffects")]
    public List<string> SpecialEffects { get; set; } = new();

    [JsonPropertyName("sockets")]
    public List<string> Sockets { get; set; } = new();

    [JsonPropertyName("transfigured")]
    public bool Transfigured { get; set; }

    [JsonPropertyName("modifiable")]
    public bool Modifiable { get; set; } = true;
}
