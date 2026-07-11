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
    /// Keyed by equipment category (e.g. "helm", "weapon", "ring"). Values are lists rather
    /// than single items because a tooltip screenshot alone never reveals which physical
    /// slot an item occupies (e.g. weapon1 vs weapon2) — only how many of that category can
    /// be equipped. Single-instance categories (helm, chest, ...) hold at most one entry;
    /// "weapon" and "ring" can hold up to two. See ItemDatabaseService for the merge rules.
    /// </summary>
    [JsonPropertyName("equipment")]
    public Dictionary<string, List<EquipmentItem>> Equipment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
