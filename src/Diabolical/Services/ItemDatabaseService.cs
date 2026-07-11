using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Reads and writes a character's equipment JSON under data/characters/{name}.json —
/// one file per character, per CLAUDE.md's Decisions Log.
/// </summary>
public class ItemDatabaseService
{
    /// <summary>
    /// Max equipped items per category. Everything not listed here is capped at a single
    /// entry (this covers "seal", which is always 1). "ring" is always 2. "weapon" is 4, not
    /// 2 — Barbarian's weapon-swap mechanic means up to two full one-hand/one-hand sets (4
    /// weapons) can be equipped at once, and a tooltip alone can't tell us which set/hand an
    /// item occupies, so we don't try to model slots more precisely than "how many of this
    /// category can exist" — items within a multi-item category are matched by name instead.
    /// "charm" is capped at 6, the game's hard maximum regardless of how many are currently
    /// unlocked by the equipped seal — that's live game state, not something the JSON tracks.
    /// </summary>
    private static readonly Dictionary<string, int> CategoryCapacities =
        new(StringComparer.OrdinalIgnoreCase) { ["weapon"] = 4, ["ring"] = 2, ["charm"] = 6 };
    private const int DefaultCategoryCapacity = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _dataDirectory;

    /// <summary>Fires when a character file couldn't be read as-is (e.g. corrupted JSON), so
    /// callers can surface a status message instead of the load silently starting over.</summary>
    public event Action<string>? Warning;

    public ItemDatabaseService() : this(ResolveDefaultDataDirectory())
    {
    }

    public ItemDatabaseService(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    public async Task<CharacterEquipment> LoadAsync(string characterName, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(characterName);
        if (!File.Exists(path))
        {
            return new CharacterEquipment { Character = characterName };
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var character = await JsonSerializer.DeserializeAsync<CharacterEquipment>(stream, SerializerOptions, cancellationToken);
            if (character is null)
            {
                return new CharacterEquipment { Character = characterName };
            }

            character.Equipment = NormalizeSlotKeys(character.Equipment);
            return character;
        }
        catch (JsonException ex)
        {
            Warning?.Invoke($"'{characterName}'s save file is corrupted ({ex.Message}) — starting a fresh one. The old file was left on disk.");
            return new CharacterEquipment { Character = characterName };
        }
    }

    public async Task SaveAsync(CharacterEquipment character, CancellationToken cancellationToken = default)
    {
        character.LastUpdated = DateTime.UtcNow;

        Directory.CreateDirectory(_dataDirectory);
        var path = GetFilePath(character.Character);
        var tempPath = path + ".tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, character, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Loads the character (or starts a new one if it hasn't been saved yet), merges an item
    /// into an equipment category, and saves the whole file back — every other category is
    /// left as-is. This is the operation the capture + review flow triggers on each save.
    /// An item with the same name already in that category is replaced in place (e.g.
    /// re-scanning after tempering); otherwise it's added, up to the category's capacity (see
    /// CategoryCapacities). Beyond capacity, the oldest entry is evicted to make room, since
    /// a re-scan has no way to say which existing item it's replacing.
    /// </summary>
    public async Task<CharacterEquipment> UpsertItemAsync(
        string characterName,
        string slot,
        EquipmentItem item,
        string? characterClass = null,
        CancellationToken cancellationToken = default)
    {
        slot = slot.ToLowerInvariant();
        var character = await LoadAsync(characterName, cancellationToken);
        character.Character = characterName;
        if (characterClass is not null)
        {
            character.Class = characterClass;
        }

        if (!character.Equipment.TryGetValue(slot, out var items))
        {
            items = new List<EquipmentItem>();
            character.Equipment[slot] = items;
        }

        var existingIndex = items.FindIndex(i => string.Equals(i.Name, item.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            items[existingIndex] = item;
        }
        else
        {
            items.Add(item);
            var capacity = CategoryCapacities.GetValueOrDefault(slot, DefaultCategoryCapacity);
            while (items.Count > capacity)
            {
                items.RemoveAt(0);
            }
        }

        await SaveAsync(character, cancellationToken);
        return character;
    }

    /// <summary>
    /// Loads the character, removes a single named item from an equipment category if
    /// present (dropping the category entirely once it's empty), and saves the whole file
    /// back — every other item is left as-is. Counterpart to UpsertItemAsync for the
    /// equipment list's remove action.
    /// </summary>
    public async Task<CharacterEquipment> RemoveItemAsync(
        string characterName,
        string slot,
        string itemName,
        CancellationToken cancellationToken = default)
    {
        slot = slot.ToLowerInvariant();
        var character = await LoadAsync(characterName, cancellationToken);
        character.Character = characterName;

        if (character.Equipment.TryGetValue(slot, out var items))
        {
            items.RemoveAll(i => string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));
            if (items.Count == 0)
            {
                character.Equipment.Remove(slot);
            }
        }

        await SaveAsync(character, cancellationToken);
        return character;
    }

    /// <summary>
    /// Character names with a saved file, for populating the character switcher —
    /// derived from filenames rather than tracked separately.
    /// </summary>
    public IReadOnlyList<string> ListCharacterNames()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(_dataDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Serializes a character with the same formatting used on disk, so the export
    /// flow (clipboard/file) matches what SaveAsync would have written.
    /// </summary>
    public static string Serialize(CharacterEquipment character) =>
        JsonSerializer.Serialize(character, SerializerOptions);

    /// <summary>
    /// Serializes a single equipped item standalone (for the item details popup's "Copy
    /// JSON" action), with its equipment-category slot inlined as a leading property — the
    /// item alone has no category signal, and that's the point of copying it in the first
    /// place (as context for an AI assistant).
    /// </summary>
    public static string SerializeItem(string slot, EquipmentItem item)
    {
        var itemNode = JsonSerializer.SerializeToNode(item, SerializerOptions)!.AsObject();
        var output = new JsonObject { ["slot"] = slot };
        foreach (var property in itemNode)
        {
            output[property.Key] = property.Value?.DeepClone();
        }

        return output.ToJsonString(SerializerOptions);
    }

    /// <summary>
    /// System.Text.Json assigns a fresh, default-comparer Dictionary through the Equipment
    /// property setter on deserialize, discarding the OrdinalIgnoreCase comparer set by its
    /// field initializer. Rebuilds it case-insensitively here, merging any categories that
    /// only differ by case (e.g. a file saved before slots were normalized to lowercase) so
    /// they collapse back into a single category instead of silently duplicating.
    /// </summary>
    private static Dictionary<string, List<EquipmentItem>> NormalizeSlotKeys(Dictionary<string, List<EquipmentItem>> equipment)
    {
        var normalized = new Dictionary<string, List<EquipmentItem>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slot, items) in equipment)
        {
            if (normalized.TryGetValue(slot, out var existing))
            {
                existing.AddRange(items);
            }
            else
            {
                normalized[slot] = items;
            }
        }

        return normalized;
    }

    private string GetFilePath(string characterName) => Path.Combine(_dataDirectory, $"{characterName}.json");

    private static string ResolveDefaultDataDirectory() =>
        Path.Combine(RepoPaths.FindRepoRoot(), "data", "characters");
}
