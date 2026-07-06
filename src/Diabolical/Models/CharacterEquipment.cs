using System.Text.Json.Serialization;

namespace Diabolical.Models;

public class CharacterEquipment
{
    [JsonPropertyName("character")]
    public string Character { get; set; } = string.Empty;

    [JsonPropertyName("class")]
    public string Class { get; set; } = string.Empty;

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Keyed by equipment slot (e.g. "helm", "weapon1") to match the JSON schema —
    /// slots aren't a fixed set (dual-wield, rings, etc.), so a dictionary serializes
    /// cleanly without a property per possible slot.
    /// </summary>
    [JsonPropertyName("equipment")]
    public Dictionary<string, EquipmentItem> Equipment { get; set; } = new();
}
