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
        await _sut.UpsertItemAsync("MyBarb", "weapon", weapon);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal(2, reloaded.Equipment.Count);
        Assert.Equal("Rage of Harrogath", Assert.Single(reloaded.Equipment["helm"]).Name);
        Assert.Equal("Windforce", Assert.Single(reloaded.Equipment["weapon"]).Name);
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
        Assert.Equal("Rage of Harrogath", Assert.Single(reloaded.Equipment["helm"]).Name);
    }

    [Fact]
    public async Task UpsertItemAsync_SecondDistinctWeapon_IsAddedAlongsideTheFirst()
    {
        var mainHand = new EquipmentItem { Name = "Windforce", Rarity = ItemRarity.Legendary, Quality = ItemQuality.Normal, ItemPower = 750 };
        await _sut.UpsertItemAsync("MyRogue", "weapon", mainHand, characterClass: "Rogue");

        var offHand = new EquipmentItem { Name = "Doombringer", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyRogue", "weapon", offHand);

        var reloaded = await _sut.LoadAsync("MyRogue");

        Assert.Equal(2, reloaded.Equipment["weapon"].Count);
        Assert.Contains(reloaded.Equipment["weapon"], i => i.Name == "Windforce");
        Assert.Contains(reloaded.Equipment["weapon"], i => i.Name == "Doombringer");
    }

    [Fact]
    public async Task UpsertItemAsync_RescanningSameNamedWeapon_UpdatesInPlaceInsteadOfAdding()
    {
        var original = new EquipmentItem { Name = "Windforce", Rarity = ItemRarity.Legendary, Quality = ItemQuality.Normal, ItemPower = 750 };
        await _sut.UpsertItemAsync("MyRogue", "weapon", original, characterClass: "Rogue");

        var retempered = new EquipmentItem { Name = "Windforce", Rarity = ItemRarity.Legendary, Quality = ItemQuality.Normal, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyRogue", "weapon", retempered);

        var reloaded = await _sut.LoadAsync("MyRogue");

        Assert.Equal(800, Assert.Single(reloaded.Equipment["weapon"]).ItemPower);
    }

    [Fact]
    public async Task UpsertItemAsync_FourDistinctWeapons_AllFitBarbarianWeaponSwapCapacity()
    {
        var names = new[] { "Weapon A", "Weapon B", "Weapon C", "Weapon D" };
        foreach (var name in names)
        {
            var weapon = new EquipmentItem { Name = name, Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 700 };
            await _sut.UpsertItemAsync("MyBarb", "weapon", weapon, characterClass: "Barbarian");
        }

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal(4, reloaded.Equipment["weapon"].Count);
        Assert.All(names, name => Assert.Contains(reloaded.Equipment["weapon"], i => i.Name == name));
    }

    [Fact]
    public async Task UpsertItemAsync_FifthDistinctWeapon_EvictsTheOldestToStayAtCapacity()
    {
        foreach (var name in new[] { "Weapon A", "Weapon B", "Weapon C", "Weapon D" })
        {
            var weapon = new EquipmentItem { Name = name, Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 700 };
            await _sut.UpsertItemAsync("MyBarb", "weapon", weapon, characterClass: "Barbarian");
        }

        var fifth = new EquipmentItem { Name = "Weapon E", Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 700 };
        await _sut.UpsertItemAsync("MyBarb", "weapon", fifth);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal(4, reloaded.Equipment["weapon"].Count);
        Assert.DoesNotContain(reloaded.Equipment["weapon"], i => i.Name == "Weapon A");
        Assert.Contains(reloaded.Equipment["weapon"], i => i.Name == "Weapon E");
    }

    [Fact]
    public async Task UpsertItemAsync_SixDistinctCharms_AllFitCharmCapacity()
    {
        var names = new[] { "Charm A", "Charm B", "Charm C", "Charm D", "Charm E", "Charm F" };
        foreach (var name in names)
        {
            var charm = new EquipmentItem { Name = name, Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 700 };
            await _sut.UpsertItemAsync("MyBarb", "charm", charm, characterClass: "Barbarian");
        }

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal(6, reloaded.Equipment["charm"].Count);
        Assert.All(names, name => Assert.Contains(reloaded.Equipment["charm"], i => i.Name == name));
    }

    [Fact]
    public async Task UpsertItemAsync_SeventhDistinctCharm_EvictsTheOldestToStayAtCapacity()
    {
        foreach (var name in new[] { "Charm A", "Charm B", "Charm C", "Charm D", "Charm E", "Charm F" })
        {
            var charm = new EquipmentItem { Name = name, Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 700 };
            await _sut.UpsertItemAsync("MyBarb", "charm", charm, characterClass: "Barbarian");
        }

        var seventh = new EquipmentItem { Name = "Charm G", Rarity = ItemRarity.Rare, Quality = ItemQuality.Normal, ItemPower = 700 };
        await _sut.UpsertItemAsync("MyBarb", "charm", seventh);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal(6, reloaded.Equipment["charm"].Count);
        Assert.DoesNotContain(reloaded.Equipment["charm"], i => i.Name == "Charm A");
        Assert.Contains(reloaded.Equipment["charm"], i => i.Name == "Charm G");
    }

    [Fact]
    public async Task UpsertItemAsync_SecondDistinctSeal_EvictsTheFirst()
    {
        var first = new EquipmentItem { Name = "Seal of the Void", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "seal", first, characterClass: "Barbarian");

        var second = new EquipmentItem { Name = "Seal of Ill Intent", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "seal", second);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Equal("Seal of Ill Intent", Assert.Single(reloaded.Equipment["seal"]).Name);
    }

    [Fact]
    public async Task RemoveItemAsync_ExistingItem_RemovesOnlyThatItem()
    {
        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        var weapon = new EquipmentItem { Name = "Windforce", Rarity = ItemRarity.Legendary, Quality = ItemQuality.Normal, ItemPower = 750 };
        await _sut.UpsertItemAsync("MyBarb", "weapon", weapon);

        var character = await _sut.RemoveItemAsync("MyBarb", "helm", "Rage of Harrogath");

        Assert.Single(character.Equipment);
        Assert.False(character.Equipment.ContainsKey("helm"));
        Assert.True(character.Equipment.ContainsKey("weapon"));

        var reloaded = await _sut.LoadAsync("MyBarb");
        Assert.Single(reloaded.Equipment);
        Assert.True(reloaded.Equipment.ContainsKey("weapon"));
    }

    [Fact]
    public async Task RemoveItemAsync_OneOfTwoWeapons_LeavesTheOtherInPlace()
    {
        var mainHand = new EquipmentItem { Name = "Windforce", Rarity = ItemRarity.Legendary, Quality = ItemQuality.Normal, ItemPower = 750 };
        await _sut.UpsertItemAsync("MyRogue", "weapon", mainHand, characterClass: "Rogue");

        var offHand = new EquipmentItem { Name = "Doombringer", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyRogue", "weapon", offHand);

        var character = await _sut.RemoveItemAsync("MyRogue", "weapon", "Windforce");

        Assert.Equal("Doombringer", Assert.Single(character.Equipment["weapon"]).Name);
    }

    [Fact]
    public async Task RemoveItemAsync_MissingItem_IsNoOp()
    {
        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "helm", helm, characterClass: "Barbarian");

        var character = await _sut.RemoveItemAsync("MyBarb", "weapon", "Windforce");

        Assert.Single(character.Equipment);
        Assert.True(character.Equipment.ContainsKey("helm"));
    }

    [Fact]
    public async Task UpsertItemAsync_DifferentlyCasedSlot_TreatedAsSameCategory()
    {
        var helm = new EquipmentItem { Name = "Rage of Harrogath", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 800 };
        await _sut.UpsertItemAsync("MyBarb", "Helm", helm, characterClass: "Barbarian");

        // If "Helm" and "HELM" were treated as distinct categories, this would add a second
        // helm alongside the first instead of respecting the capacity-1 rule for "helm".
        var replacement = new EquipmentItem { Name = "Andariel's Visage", Rarity = ItemRarity.Unique, Quality = ItemQuality.Ancestral, ItemPower = 820 };
        await _sut.UpsertItemAsync("MyBarb", "HELM", replacement);

        var reloaded = await _sut.LoadAsync("MyBarb");

        Assert.Single(reloaded.Equipment);
        Assert.Equal("Andariel's Visage", Assert.Single(reloaded.Equipment["helm"]).Name);
    }

    [Fact]
    public async Task LoadAsync_FileWithDuplicateCasedCategories_CollapsesThemIntoOne()
    {
        Directory.CreateDirectory(_dataDirectory);
        var json = """
            {
              "character": "MyBarb",
              "class": "Barbarian",
              "lastUpdated": "2026-01-01T00:00:00Z",
              "equipment": {
                "Helm": [ { "name": "Old Helm", "rarity": "Rare", "quality": "Normal", "itemPower": 500 } ],
                "helm": [ { "name": "Rage of Harrogath", "rarity": "Unique", "quality": "Ancestral", "itemPower": 800 } ]
              }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_dataDirectory, "MyBarb.json"), json);

        var character = await _sut.LoadAsync("MyBarb");

        Assert.Single(character.Equipment);
        Assert.Equal(2, character.Equipment["helm"].Count);
        Assert.Contains(character.Equipment["helm"], i => i.Name == "Old Helm");
        Assert.Contains(character.Equipment["helm"], i => i.Name == "Rage of Harrogath");
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
            Affixes = new List<ItemAffix> { new() { Text = "+40% Fury Generation" } },
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

        var helmElement = root.GetProperty("equipment").GetProperty("helm")[0];
        Assert.Equal("Rage of Harrogath", helmElement.GetProperty("name").GetString());
        Assert.Equal("Unique", helmElement.GetProperty("rarity").GetString());
        Assert.Equal("Ancestral", helmElement.GetProperty("quality").GetString());
        Assert.Equal(800, helmElement.GetProperty("itemPower").GetInt32());
        Assert.Equal("+40% Fury Generation", helmElement.GetProperty("affixes")[0].GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Array, helmElement.GetProperty("specialEffects").ValueKind);
        Assert.False(helmElement.GetProperty("transfigured").GetBoolean());
        Assert.True(helmElement.GetProperty("modifiable").GetBoolean());
    }
}
