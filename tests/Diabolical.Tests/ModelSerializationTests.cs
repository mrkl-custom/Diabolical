using System.Text.Json;
using System.Text.Json.Nodes;
using Diabolical.Models;

namespace Diabolical.Tests;

public class ModelSerializationTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "sample_character.json");

    [Fact]
    public void Deserialize_PopulatesExpectedFields()
    {
        var json = File.ReadAllText(FixturePath);

        var character = JsonSerializer.Deserialize<CharacterEquipment>(json);

        Assert.NotNull(character);
        Assert.Equal("MyBarb", character!.Character);
        Assert.Equal("Barbarian", character.Class);
        Assert.Equal(new DateTime(2026, 7, 6, 0, 0, 0, DateTimeKind.Utc), character.LastUpdated.ToUniversalTime());

        Assert.True(character.Equipment.ContainsKey("helm"));
        var helm = character.Equipment["helm"];
        Assert.Equal("Rage of Harrogath", helm.Name);
        Assert.Equal(ItemRarity.Unique, helm.Rarity);
        Assert.Equal(800, helm.ItemPower);
        Assert.Equal(2, helm.Affixes.Count);
        Assert.Null(helm.Aspect);

        Assert.True(character.Equipment.ContainsKey("weapon1"));
        var weapon1 = character.Equipment["weapon1"];
        Assert.Equal(ItemRarity.Legendary, weapon1.Rarity);
        Assert.Equal("Aspect of Disobedience", weapon1.Aspect);
    }

    [Fact]
    public void RoundTrip_MatchesOriginalJsonShape()
    {
        var originalJson = File.ReadAllText(FixturePath);
        var character = JsonSerializer.Deserialize<CharacterEquipment>(originalJson);

        var reserialized = JsonSerializer.Serialize(character);

        var expected = JsonNode.Parse(originalJson);
        var actual = JsonNode.Parse(reserialized);
        Assert.True(JsonNode.DeepEquals(expected, actual),
            $"Round-tripped JSON did not match schema shape.\nExpected: {expected}\nActual: {actual}");
    }

    [Fact]
    public void Rarity_UnrecognizedValue_FallsBackToUnknownInsteadOfThrowing()
    {
        const string json = """{"name":"Mystery Item","rarity":"Ancestral","itemPower":1,"affixes":[]}""";

        var item = JsonSerializer.Deserialize<EquipmentItem>(json);

        Assert.NotNull(item);
        Assert.Equal(ItemRarity.Unknown, item!.Rarity);
    }
}
