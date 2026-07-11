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

    public EquipmentItem ToEquipmentItem() => new()
    {
        Name = Name,
        ItemType = ItemType,
        Rarity = Rarity,
        Quality = Quality,
        ItemPower = ItemPower,
        MasterworkingQuality = MasterworkingQuality,
        Affixes = Affixes,
        SpecialEffects = SpecialEffects,
        Sockets = Sockets,
        Transfigured = Transfigured,
        Modifiable = Modifiable
    };
}
