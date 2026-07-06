namespace Diabolical.Models;

public class EquipmentItem
{
    public string Name { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public int ItemPower { get; set; }
    public List<string> Affixes { get; set; } = new();
    public string? Aspect { get; set; }
}
