using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Diabolical.Models;
using Diabolical.Services;
using Microsoft.Win32;

namespace Diabolical.Views;

/// <summary>
/// Flattened, display-only view of an equipped item for the equipment DataGrid —
/// not part of the persisted schema.
/// </summary>
public sealed record EquipmentRow(string Slot, string Name, ItemRarity Rarity, ItemQuality Quality, int ItemPower, EquipmentItem Item);

public partial class MainWindow : Window
{
    private static readonly string ExportDirectory = Path.Combine(RepoPaths.FindRepoRoot(), "data", "exports");

    private static readonly TimeSpan ProviderStatusRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly HotkeyManager? _hotkeyManager;
    private readonly ScreenCaptureService? _captureService;
    private readonly IVisionService? _visionService;
    private readonly ItemDatabaseService _databaseService = new();
    private readonly DispatcherTimer _providerStatusTimer;
    private readonly ObservableCollection<string> _statusMessages = new();
    private string _visionProviderName = "";
    private bool _yoloMode;

    private string CurrentCharacterName => CharacterComboBox.Text.Trim();

    public MainWindow()
    {
        InitializeComponent();

        StatusList.ItemsSource = _statusMessages;
        RefreshCharacterList();

        _providerStatusTimer = new DispatcherTimer { Interval = ProviderStatusRefreshInterval };
        _providerStatusTimer.Tick += async (_, _) => await RefreshProviderStatusAsync();

        try
        {
            var settings = AppSettingsLoader.Load();
            _yoloMode = settings.YoloMode;
            _visionProviderName = settings.VisionProvider;
            _hotkeyManager = new HotkeyManager();
            _captureService = new ScreenCaptureService(_hotkeyManager, settings.Hotkey);
            _captureService.CaptureCompleted += OnCaptureCompleted;
            _captureService.CaptureCancelled += OnCaptureCancelled;
            _visionService = VisionServiceFactory.Create(settings);
            AppendStatus($"Hotkey {settings.Hotkey.Modifiers}+{settings.Hotkey.Key} registered. Ready to capture.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            AppendStatus($"Capture unavailable: {ex.Message}");
        }

        _ = RefreshProviderStatusAsync();
        _providerStatusTimer.Start();
    }

    private async Task RefreshProviderStatusAsync()
    {
        if (_visionService is null)
        {
            ProviderStatusDot.Fill = Brushes.Gray;
            ProviderStatusText.Text = "No vision provider configured.";
            return;
        }

        ProviderStatusDot.Fill = Brushes.Gray;
        ProviderStatusText.Text = $"{_visionProviderName}: checking...";

        var result = await _visionService.CheckAvailabilityAsync();

        ProviderStatusDot.Fill = result.IsAvailable ? Brushes.LimeGreen : Brushes.Red;
        ProviderStatusText.Text = result.IsAvailable
            ? $"{_visionProviderName}: connected"
            : $"{_visionProviderName}: unreachable{(result.Detail is null ? "" : $" — {result.Detail}")}";
    }

    private async void RecheckProviderButton_Click(object sender, RoutedEventArgs e) => await RefreshProviderStatusAsync();

    /// <summary>Appends a message to the status list and scrolls it into view.</summary>
    private void AppendStatus(string message)
    {
        _statusMessages.Add(message);
        StatusList.ScrollIntoView(message);
    }

    private void RefreshCharacterList()
    {
        var selected = CharacterComboBox.Text;
        CharacterComboBox.ItemsSource = _databaseService.ListCharacterNames();
        CharacterComboBox.Text = selected;
    }

    private async void OnCaptureCompleted(byte[] imageBytes)
    {
        if (_visionService is null)
        {
            AppendStatus("Capture succeeded, but the vision provider isn't configured — see appsettings.local.json.");
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentCharacterName))
        {
            AppendStatus("Set a character name before capturing.");
            return;
        }

        AppendStatus("Sending capture to the vision model...");
        var result = await _visionService.ExtractItemAsync(imageBytes);

        if (!result.Success || result.Item is null)
        {
            AppendStatus($"Extraction failed: {result.ErrorMessage}");
            return;
        }

        string slot;
        EquipmentItem item;

        if (_yoloMode)
        {
            slot = result.Item.Slot;
            item = result.Item.ToEquipmentItem();
        }
        else
        {
            var dialog = new ReviewEditDialog(result.Item) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                AppendStatus("Item discarded.");
                return;
            }

            slot = dialog.Slot;
            item = dialog.Item;
        }

        var characterName = CurrentCharacterName;
        var characterClass = string.IsNullOrWhiteSpace(ClassTextBox.Text) ? null : ClassTextBox.Text.Trim();
        await _databaseService.UpsertItemAsync(characterName, slot, item, characterClass);

        RefreshCharacterList();
        await RefreshEquipmentListAsync(characterName);
        AppendStatus($"Saved '{item.Name}' to {characterName}'s {slot} slot.");
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
            .SelectMany(kvp => kvp.Value.Select(item => new EquipmentRow(kvp.Key, item.Name, item.Rarity, item.Quality, item.ItemPower, item)))
            .OrderBy(row => row.Slot, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ViewEquipmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: EquipmentRow row })
        {
            return;
        }

        var dialog = new ItemDetailsDialog(row.Slot, row.Item) { Owner = this };
        dialog.ShowDialog();
    }

    private async void RemoveEquipmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: EquipmentRow row })
        {
            return;
        }

        var characterName = CurrentCharacterName;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return;
        }

        if (!_yoloMode)
        {
            var confirm = MessageBox.Show(
                this,
                $"Remove '{row.Name}' from {characterName}'s {row.Slot} slot? This cannot be undone.",
                "Remove Item",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await _databaseService.RemoveItemAsync(characterName, row.Slot, row.Name);
        await RefreshEquipmentListAsync(characterName);
        AppendStatus($"Removed '{row.Name}' from {characterName}'s {row.Slot} slot.");
    }

    private void OnCaptureCancelled()
    {
        AppendStatus("Capture cancelled.");
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
            AppendStatus("Enter a character name first.");
            return;
        }

        var character = await _databaseService.LoadAsync(name);
        ClassTextBox.Text = character.Class;
        RefreshCharacterList();
        CharacterComboBox.Text = name;
        await RefreshEquipmentListAsync(name);
        AppendStatus($"Switched to '{name}'.");
    }

    private async void CopyJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var json = await GetCurrentCharacterJsonAsync();
        if (json is null)
        {
            return;
        }

        Clipboard.SetText(json);
        AppendStatus("Copied JSON to clipboard.");
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
            AppendStatus($"Exported to {dialog.FileName}.");
        }
    }

    private async Task<string?> GetCurrentCharacterJsonAsync()
    {
        var name = CurrentCharacterName;
        if (string.IsNullOrWhiteSpace(name))
        {
            AppendStatus("Select a character to export.");
            return null;
        }

        var character = await _databaseService.LoadAsync(name);
        return ItemDatabaseService.Serialize(character);
    }

    protected override void OnClosed(EventArgs e)
    {
        _providerStatusTimer.Stop();
        _captureService?.Dispose();
        _hotkeyManager?.Dispose();
        base.OnClosed(e);
    }
}
