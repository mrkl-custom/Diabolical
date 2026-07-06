namespace Diabolical.Models;

public class CharacterEquipment
{
    public string Character { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, EquipmentItem> Equipment { get; set; } = new();
}
