using System.Text.Json;
using Diabolical.Models;
using Diabolical.Services;

namespace Diabolical.Tests;

public class ItemDatabaseServiceTests : IDisposable
{
    private readonly string _dataDirectory;
    private readonly ItemDatabaseService _sut;

    public ItemDatabaseServiceTests()
    {
        _dataDirectory = Path.Combine(Path.GetTempPath(), "DiabolicalTests_" + Guid.NewGuid());
        _sut = new ItemDatabaseService(_dataDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_NoExistingFile_ReturnsNewEquipmentForCharacter()
    {
        var character = await _sut.LoadAsync("NewBarb");

        Assert.Equal("NewBarb", character.Character);
        Assert.Empty(character.Equipment);
    }

    [Fact]
    public async Task UpsertItemAsync_CreatesFileAndStampsLastUpdated()
    {
        var before = DateTime.UtcNow;

        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        var character = await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        Assert.True(File.Exists(Path.Combine(_dataDirectory, "MyBarb.json")));
        Assert.Equal("Barbarian", character.Class);
        Assert.InRange(character.LastUpdated, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task UpsertItemAsync_ExistingCharacter_MergesWithoutClobberingOtherSlots()
    {
        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        var weapon = new EquipmentItem
        {
            Name = "Windforce",
            Rarity = ItemRarity.Legendary,
            Quality = ItemQuality.Normal,
            ItemPower = 750,
            SpecialEffects = new List<string> { "Aspect of Disobedience" }
        };
        await _sut.UpsertItemAsync("MyBarb", "weapon1", weapon);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal(2, reloaded.Equipment.Count);
        Assert.Equal("Rage of Harrogath", reloaded.Equipment["helm"].Name);
        Assert.Equal("Windforce", reloaded.Equipment["weapon1"].Name);
        Assert.Equal("Barbarian", reloaded.Class);
    }

    [Fact]
    public async Task UpsertItemAsync_UpdatingExistingSlot_ReplacesOnlyThatSlot()
    {
        var oldHelm = new EquipmentItem { Name = "Old Helm", Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 500 };
        await _sut.UpsertItemAsync("MyBarb", "helm", oldHelm, characterClass: "Barbarian");

        var newHelm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "helm", newHelm);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Single(reloaded.Equipment);
        Assert.Equal("Rage of Harrogath", reloaded.Equipment["helm"].Name);
    }

    [Fact]
    public async Task RemoveItemAsync_ExistingSlot_RemovesOnlyThatSlot()
    {
        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        var weapon = new EquipmentItem { Name = "Windforce", Rarity = ItemRarity.Legendary, Quality = ItemQuality.Normal, ItemPower = 750 };
        await _sut.UpsertItemAsync("MyBarb", "weapon1", weapon);

        var character = await _sut.RemoveItemAsync("MyBarb", "helm");

        Assert.Single(character.Equipment);
        Assert.False(character.Equipment.ContainsKey("helm"));
        Assert.True(character.Equipment.ContainsKey("weapon1"));

        var reloaded = await _sut.LoadAsync("MyBarb");
        Assert.Single(reloaded.Equipment);
        Assert.True(reloaded.Equipment.ContainsKey("weapon1"));
    }

    [Fact]
    public async Task RemoveItemAsync_MissingSlot_IsNoOp()
    {
        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        var character = await _sut.RemoveItemAsync("MyBarb", "weapon1");

        Assert.Single(character.Equipment);
        Assert.True(character.Equipment.ContainsKey("helm"));
    }

    [Fact]
    public async Task RoundTrip_WrittenFileMatchesSchemaShape()
    {
        var helm = new EquipmentItem
        {
            Name = "Rage of Harrogath",
            Rarity = ItemRarity.Unique,
            Quality = ItemQuality.Ancestral,
            ItemPower = 800,
            Affixes = new List<ItemAffix> { new() { Text = "+40% Fury Generation", Source = AffixSource.Base } },
            SpecialEffects = new List<string>(),
            Transfigured = false,
            Modifiable = true
        };

        await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        var json = await File.ReadAllTextAsync(Path.Combine(_dataDirectory, "MyBarb.json"));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("MyBarb", root.GetProperty("character").GetString());
        Assert.Equal("Barbarian", root.GetProperty("class").GetString());
        Assert.True(root.TryGetProperty("lastUpdated", out _));

        var helmElement = root.GetProperty("equipment").GetProperty("helm");
        Assert.Equal("Rage of Harrogath", helmElement.GetProperty("name").GetString());
        Assert.Equal("Unique", helmElement.GetProperty("rarity").GetString());
        Assert.Equal("Ancestral", helmElement.GetProperty("quality").GetString());
        Assert.Equal(800, helmElement.GetProperty("itemPower").GetInt32());
        Assert.Equal("Base", helmElement.GetProperty("affixes")[0].GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Array, helmElement.GetProperty("specialEffects").ValueKind);
        Assert.False(helmElement.GetProperty("transfigured").GetBoolean());
        Assert.True(helmElement.GetProperty("modifiable").GetBoolean());
    }
}
