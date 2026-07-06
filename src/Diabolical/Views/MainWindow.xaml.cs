using System.IO;
using System.Windows;
using System.Windows.Controls;
using Diabolical.Models;
using Diabolical.Services;
using Microsoft.Win32;

namespace Diabolical.Views;

/// <summary>
/// Flattened, display-only view of an equipped item for the equipment DataGrid —
/// not part of the persisted schema.
/// </summary>
public sealed record EquipmentRow(string Slot, string Name, ItemRarity Rarity, ItemQuality Quality, int ItemPower);

public partial class MainWindow : Window
{
    private static readonly string ExportDirectory = Path.Combine(RepoPaths.FindRepoRoot(), "data", "exports");

    private readonly HotkeyManager? _hotkeyManager;
    private readonly ScreenCaptureService? _captureService;
    private readonly GeminiVisionService? _visionService;
    private readonly ItemDatabaseService _databaseService = new();

    private string CurrentCharacterName => CharacterComboBox.Text.Trim();

    public MainWindow()
    {
        InitializeComponent();

        RefreshCharacterList();

        try
        {
            var settings = AppSettingsLoader.Load();
            _hotkeyManager = new HotkeyManager();
            _captureService = new ScreenCaptureService(_hotkeyManager, settings.Hotkey);
            _captureService.CaptureCompleted += OnCaptureCompleted;
            _captureService.CaptureCancelled += OnCaptureCancelled;
            _visionService = new GeminiVisionService();
            StatusText.Text = $"Hotkey {settings.Hotkey.Modifiers}+{settings.Hotkey.Key} registered. Ready to capture.";
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            StatusText.Text = $"Capture unavailable: {ex.Message}";
        }
    }

    private void RefreshCharacterList()
    {
        var selected = CharacterComboBox.Text;
        CharacterComboBox.ItemsSource = _databaseService.ListCharacterNames();
        CharacterComboBox.Text = selected;
    }

    private void TestCaptureButton_Click(object sender, RoutedEventArgs e) => _captureService?.BeginCapture();

    private async void OnCaptureCompleted(byte[] imageBytes)
    {
        if (_visionService is null)
        {
            StatusText.Text = "Capture succeeded, but Gemini isn't configured — see appsettings.local.json.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentCharacterName))
        {
            StatusText.Text = "Set a character name before capturing.";
            return;
        }

        StatusText.Text = "Sending capture to Gemini...";
        var result = await _visionService.ExtractItemAsync(imageBytes);

        if (!result.Success || result.Item is null)
        {
            StatusText.Text = $"Extraction failed: {result.ErrorMessage}";
            return;
        }

        var dialog = new ReviewEditDialog(result.Item) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            StatusText.Text = "Item discarded.";
            return;
        }

        var characterName = CurrentCharacterName;
        var characterClass = string.IsNullOrWhiteSpace(ClassTextBox.Text) ? null : ClassTextBox.Text.Trim();
        await _databaseService.UpsertItemAsync(characterName, dialog.Slot, dialog.Item, characterClass);

        RefreshCharacterList();
        await RefreshEquipmentListAsync(characterName);
        StatusText.Text = $"Saved '{dialog.Item.Name}' to {characterName}'s {dialog.Slot} slot.";
    }

    private async Task RefreshEquipmentListAsync(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            EquipmentDataGrid.ItemsSource = null;
            return;
        }

        var character = await _databaseService.LoadAsync(characterName);
        EquipmentDataGrid.ItemsSource = character.Equipment
            .Select(kvp => new EquipmentRow(kvp.Key, kvp.Value.Name, kvp.Value.Rarity, kvp.Value.Quality, kvp.Value.ItemPower))
            .OrderBy(row => row.Slot, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async void RemoveEquipmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string slot })
        {
            return;
        }

        var characterName = CurrentCharacterName;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Remove the item in '{slot}' from {characterName}'s equipment? This cannot be undone.",
            "Remove Item",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await _databaseService.RemoveItemAsync(characterName, slot);
        await RefreshEquipmentListAsync(characterName);
        StatusText.Text = $"Removed item from {characterName}'s {slot} slot.";
    }

    private void OnCaptureCancelled()
    {
        StatusText.Text = "Capture cancelled.";
    }

    private async void CharacterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is not string name)
        {
            return;
        }

        var character = await _databaseService.LoadAsync(name);
        ClassTextBox.Text = character.Class;
        await RefreshEquipmentListAsync(name);
    }

    private async void SwitchCharacterButton_Click(object sender, RoutedEventArgs e)
    {
        var name = CurrentCharacterName;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Enter a character name first.";
            return;
        }

        var character = await _databaseService.LoadAsync(name);
        ClassTextBox.Text = character.Class;
        RefreshCharacterList();
        CharacterComboBox.Text = name;
        await RefreshEquipmentListAsync(name);
        StatusText.Text = $"Switched to '{name}'.";
    }

    private async void CopyJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var json = await GetCurrentCharacterJsonAsync();
        if (json is null)
        {
            return;
        }

        Clipboard.SetText(json);
        StatusText.Text = "Copied JSON to clipboard.";
    }

    private async void ExportFileButton_Click(object sender, RoutedEventArgs e)
    {
        var json = await GetCurrentCharacterJsonAsync();
        if (json is null)
        {
            return;
        }

        Directory.CreateDirectory(ExportDirectory);
        var dialog = new SaveFileDialog
        {
            FileName = $"{CurrentCharacterName}.json",
            InitialDirectory = ExportDirectory,
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await File.WriteAllTextAsync(dialog.FileName, json);
            StatusText.Text = $"Exported to {dialog.FileName}.";
        }
    }

    private async Task<string?> GetCurrentCharacterJsonAsync()
    {
        var name = CurrentCharacterName;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Select a character to export.";
            return null;
        }

        var character = await _databaseService.LoadAsync(name);
        return ItemDatabaseService.Serialize(character);
    }

    protected override void OnClosed(EventArgs e)
    {
        _captureService?.Dispose();
        _hotkeyManager?.Dispose();
        base.OnClosed(e);
    }
}
