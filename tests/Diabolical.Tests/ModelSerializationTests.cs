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
        var helm = Assert.Single(character.Equipment["helm"]);
        Assert.Equal("Rage of Harrogath", helm.Name);
        Assert.Equal(ItemRarity.Unique, helm.Rarity);
        Assert.Equal(ItemQuality.Ancestral, helm.Quality);
        Assert.Equal(800, helm.ItemPower);
        Assert.Equal(2, helm.Affixes.Count);
        Assert.All(helm.Affixes, a => Assert.Equal(AffixSource.Base, a.Source));
        Assert.Single(helm.SpecialEffects);
        Assert.False(helm.Transfigured);
        Assert.True(helm.Modifiable);

        Assert.True(character.Equipment.ContainsKey("weapon"));
        var weapon = Assert.Single(character.Equipment["weapon"]);
        Assert.Equal(ItemRarity.Legendary, weapon.Rarity);
        Assert.Equal(ItemQuality.Normal, weapon.Quality);
        Assert.Equal(AffixSource.Tempered, weapon.Affixes[1].Source);
        Assert.Equal("Aspect of Disobedience", Assert.Single(weapon.SpecialEffects));
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
        const string json = """{"name":"Mystery Item","rarity":"Nonsense","quality":"Normal","itemPower":1,"affixes":[]}""";

        var item = JsonSerializer.Deserialize<EquipmentItem>(json);

        Assert.NotNull(item);
        Assert.Equal(ItemRarity.Unknown, item!.Rarity);
    }

    [Fact]
    public void AffixSource_UnrecognizedValue_FallsBackToBaseInsteadOfThrowing()
    {
        const string json = """{"text":"+100 Strength","source":"Nonsense"}""";

        var affix = JsonSerializer.Deserialize<ItemAffix>(json);

        Assert.NotNull(affix);
        Assert.Equal(AffixSource.Base, affix!.Source);
    }
}
