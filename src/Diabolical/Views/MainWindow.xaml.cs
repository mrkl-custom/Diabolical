using System.Collections.ObjectModel;
using System.IO;
using System.Media;
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
    private readonly QuickCopyService? _quickCopyService;
    private readonly IVisionService? _visionService;
    private readonly ItemDatabaseService _databaseService = new();
    private readonly DispatcherTimer _providerStatusTimer;
    private readonly ObservableCollection<string> _statusMessages = new();
    private readonly ProviderStatusPresenter _statusPresenter;
    private bool _yoloMode;

    private string CurrentCharacterName => CharacterComboBox.Text.Trim();

    public MainWindow()
    {
        InitializeComponent();

        StatusList.ItemsSource = _statusMessages;
        _databaseService.Warning += AppendStatus;
        RefreshCharacterList();

        var visionProviderName = "";

        try
        {
            var settings = AppSettingsLoader.Load();
            _yoloMode = settings.YoloMode;
            visionProviderName = settings.VisionProvider;
            _hotkeyManager = new HotkeyManager();
            _captureService = new ScreenCaptureService(_hotkeyManager, settings.Hotkey);
            _captureService.CaptureCompleted += OnCaptureCompleted;
            _captureService.CaptureCancelled += OnCaptureCancelled;
            _captureService.ActivityChanged += SetActivity;
            _visionService = VisionServiceFactory.Create(settings);
            _quickCopyService = new QuickCopyService(_hotkeyManager, settings.QuickCopyHotkey, _visionService);
            _quickCopyService.StatusChanged += AppendStatus;
            _quickCopyService.ActivityChanged += SetActivity;
            _quickCopyService.ItemCopied += PlaySuccessSound;
            AppendStatus(
                $"Hotkey {settings.Hotkey.Modifiers}+{settings.Hotkey.Key} registered. " +
                $"Quick Copy hotkey {settings.QuickCopyHotkey.Modifiers}+{settings.QuickCopyHotkey.Key} registered. " +
                "Ready to capture.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            AppendStatus($"Capture unavailable: {ex.Message}");
        }

        _statusPresenter = new ProviderStatusPresenter(_visionService, visionProviderName);
        _statusPresenter.Changed += UpdateProviderStatusUi;

        _providerStatusTimer = new DispatcherTimer { Interval = ProviderStatusRefreshInterval };
        _providerStatusTimer.Tick += async (_, _) => await _statusPresenter.RefreshAsync();

        _ = _statusPresenter.RefreshAsync();
        _providerStatusTimer.Start();
    }

    private void UpdateProviderStatusUi()
    {
        ProviderStatusDot.Fill = _statusPresenter.IsAvailable switch
        {
            true => Brushes.LimeGreen,
            false => Brushes.Red,
            null => Brushes.Gray
        };
        ProviderStatusText.Text = _statusPresenter.StatusText;
    }

    private async void RecheckProviderButton_Click(object sender, RoutedEventArgs e) => await _statusPresenter.RefreshAsync();

    /// <summary>
    /// Layers app activity (Idle/Capturing/Processing/Error) on top of the connectivity text
    /// so the status box also doubles as a "something's happening" indicator for both the
    /// main capture flow and Quick Copy, without disturbing the periodic connectivity check.
    /// Also the single point every flow's transitions pass through (directly here for the main
    /// capture pipeline, via ActivityChanged for both services' Capturing/cancel and Quick
    /// Copy's own extract/clipboard steps), so it's where OverlayCaptureSession's reentrancy
    /// guard gets released once a flow reaches Idle/Error, letting the next hotkey press start
    /// a new capture instead of being ignored as still-in-flight.
    /// </summary>
    private void SetActivity(ActivityState state)
    {
        if (state is ActivityState.Idle or ActivityState.Error)
        {
            OverlayCaptureSession.EndCapture();
        }

        _statusPresenter.SetActivity(state);
    }

    /// <summary>Gentle success cue on a saved/copied item only — nothing on failure or cancel.</summary>
    private static void PlaySuccessSound() => SystemSounds.Asterisk.Play();

    /// <summary>
    /// Last-resort handler for exceptions that escape an `async void` handler — invoked by
    /// App.xaml.cs's DispatcherUnhandledException hook instead of letting them crash the
    /// process. Surfaces the error like any other status message rather than a silent death.
    /// </summary>
    public void ReportUnhandledException(Exception ex)
    {
        AppendStatus($"Unexpected error: {ex.Message}");
        SetActivity(ActivityState.Error);
    }

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

    private async void OnCaptureCompleted(byte[] imageBytes) => await ProcessCaptureAsync(imageBytes);

    /// <summary>
    /// The capture-completed pipeline (validate → extract → review → upsert), pulled out of
    /// the `async void` event handler so the handler itself is a trivial one-liner and any
    /// exception thrown here has a single, well-known await point rather than being buried in
    /// event-handler plumbing.
    /// </summary>
    private async Task ProcessCaptureAsync(byte[] imageBytes)
    {
        if (_visionService is null)
        {
            AppendStatus("Capture succeeded, but the vision provider isn't configured — see appsettings.local.json.");
            SetActivity(ActivityState.Idle);
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentCharacterName))
        {
            AppendStatus("Set a character name before capturing.");
            SetActivity(ActivityState.Idle);
            return;
        }

        SetActivity(ActivityState.Processing);
        AppendStatus("Sending capture to the vision model...");
        var result = await _visionService.ExtractItemAsync(imageBytes);

        if (!result.Success || result.Item is null)
        {
            AppendStatus($"Extraction failed: {result.ErrorMessage}");
            SetActivity(ActivityState.Error);
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
                SetActivity(ActivityState.Idle);
                return;
            }

            slot = dialog.Slot;
            item = dialog.Item;
        }

        var characterName = CurrentCharacterName;
        var characterClass = string.IsNullOrWhiteSpace(ClassTextBox.Text) ? null : ClassTextBox.Text.Trim();
        var character = await _databaseService.UpsertItemAsync(characterName, slot, item, characterClass);

        RefreshCharacterList();
        BindEquipmentList(character);
        AppendStatus($"Saved '{item.Name}' to {characterName}'s {slot} slot.");
        SetActivity(ActivityState.Idle);
        PlaySuccessSound();
    }

    private void BindEquipmentList(CharacterEquipment character)
    {
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

        var character = await _databaseService.RemoveItemAsync(characterName, row.Slot, row.Name);
        BindEquipmentList(character);
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
        BindEquipmentList(character);
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
        BindEquipmentList(character);
        AppendStatus($"Switched to '{name}'.");
    }

    private async void CopyJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var json = await GetCurrentCharacterJsonAsync();
        if (json is null)
        {
            return;
        }

        ClipboardHelper.SetTextWithRetry(json);
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
        _hotkeyManager?.Dispose();
        base.OnClosed(e);
    }
}
