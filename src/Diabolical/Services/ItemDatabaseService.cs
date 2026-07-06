using System.IO;
using System.Text.Json;
using Diabolical.Models;

namespace Diabolical.Services;

/// <summary>
/// Reads and writes a character's equipment JSON under data/characters/{name}.json —
/// one file per character, per CLAUDE.md's Decisions Log.
/// </summary>
public class ItemDatabaseService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _dataDirectory;

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

        await using var stream = File.OpenRead(path);
        var character = await JsonSerializer.DeserializeAsync<CharacterEquipment>(stream, SerializerOptions, cancellationToken);
        return character ?? new CharacterEquipment { Character = characterName };
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
    /// Loads the character (or starts a new one if it hasn't been saved yet), replaces a
    /// single equipment slot, and saves the whole file back — every other slot is left as-is.
    /// This is the operation the capture + review flow triggers on each save.
    /// </summary>
    public async Task<CharacterEquipment> UpsertItemAsync(
        string characterName,
        string slot,
        EquipmentItem item,
        string? characterClass = null,
        CancellationToken cancellationToken = default)
    {
        var character = await LoadAsync(characterName, cancellationToken);
        character.Character = characterName;
        if (characterClass is not null)
        {
            character.Class = characterClass;
        }

        character.Equipment[slot] = item;

        await SaveAsync(character, cancellationToken);
        return character;
    }

    private string GetFilePath(string characterName) => Path.Combine(_dataDirectory, $"{characterName}.json");

    private static string ResolveDefaultDataDirectory() =>
        Path.Combine(RepoPaths.FindRepoRoot(), "data", "characters");
}
