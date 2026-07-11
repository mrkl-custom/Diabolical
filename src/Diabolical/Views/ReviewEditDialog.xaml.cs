using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Diabolical.Models;

namespace Diabolical.Views;

/// <summary>
/// Modal confirm/edit step between a Gemini extraction and ItemDatabaseService — every
/// field (including the inferred slot) is editable since the vision model isn't trusted
/// blindly. ShowDialog() returns true only when the user picks Save.
/// </summary>
public partial class ReviewEditDialog : Window
{
    /// <summary>Known equipment categories, pre-filled into the slot ComboBox so a typo
    /// (e.g. "glvoes") doesn't silently create a junk category capped at 1 item. Still
    /// editable (IsEditable="True" in XAML) so a game patch adding a new slot doesn't
    /// require a rebuild.</summary>
    private static readonly string[] KnownSlots =
        { "helm", "chest", "gloves", "pants", "boots", "weapon", "ring", "amulet", "seal", "charm" };

    public string Slot { get; private set; } = string.Empty;
    public EquipmentItem Item { get; private set; } = new();

    private readonly ObservableCollection<AffixEditRow> _affixRows;

    public ReviewEditDialog(ParsedItemExtraction parsed)
    {
        InitializeComponent();

        RarityComboBox.ItemsSource = Enum.GetValues<ItemRarity>();
        QualityComboBox.ItemsSource = Enum.GetValues<ItemQuality>();

        SlotComboBox.ItemsSource = KnownSlots;
        SlotComboBox.Text = parsed.Slot;
        NameTextBox.Text = parsed.Name;
        ItemTypeTextBox.Text = parsed.ItemType;
        RarityComboBox.SelectedItem = parsed.Rarity;
        QualityComboBox.SelectedItem = parsed.Quality;
        ItemPowerTextBox.Text = parsed.ItemPower.ToString();
        MasterworkingQualityTextBox.Text = parsed.MasterworkingQuality.ToString();
        TransfiguredCheckBox.IsChecked = parsed.Transfigured;
        ModifiableCheckBox.IsChecked = parsed.Modifiable;
        SpecialEffectsTextBox.Text = string.Join(Environment.NewLine, parsed.SpecialEffects);
        SocketsTextBox.Text = string.Join(Environment.NewLine, parsed.Sockets);

        _affixRows = new ObservableCollection<AffixEditRow>(
            parsed.Affixes.Select(a => new AffixEditRow { Text = a.Text, GreaterAffix = a.GreaterAffix }));
        AffixesDataGrid.ItemsSource = _affixRows;
    }

    private void AddAffixButton_Click(object sender, RoutedEventArgs e)
    {
        _affixRows.Add(new AffixEditRow());
    }

    private void RemoveAffixButton_Click(object sender, RoutedEventArgs e)
    {
        if (AffixesDataGrid.SelectedItem is AffixEditRow row)
        {
            _affixRows.Remove(row);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        AffixesDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (string.IsNullOrWhiteSpace(SlotComboBox.Text))
        {
            MessageBox.Show(this, "Slot is required.", "Diabolical", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(ItemPowerTextBox.Text, out var itemPower))
        {
            MessageBox.Show(this, "Item Power must be a number.", "Diabolical", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MasterworkingQualityTextBox.Text, out var masterworkingQuality))
        {
            MessageBox.Show(this, "MW Quality must be a number.", "Diabolical", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Slot = SlotComboBox.Text.Trim();
        Item = new EquipmentItem
        {
            Name = NameTextBox.Text.Trim(),
            ItemType = ItemTypeTextBox.Text.Trim(),
            Rarity = RarityComboBox.SelectedItem is ItemRarity rarity ? rarity : ItemRarity.Unknown,
            Quality = QualityComboBox.SelectedItem is ItemQuality quality ? quality : ItemQuality.Unknown,
            ItemPower = itemPower,
            MasterworkingQuality = masterworkingQuality,
            Affixes = _affixRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .Select(row => new ItemAffix { Text = row.Text.Trim(), GreaterAffix = row.GreaterAffix })
                .ToList(),
            SpecialEffects = SpecialEffectsTextBox.Text
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList(),
            Sockets = SocketsTextBox.Text
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList(),
            Transfigured = TransfiguredCheckBox.IsChecked ?? false,
            Modifiable = ModifiableCheckBox.IsChecked ?? true
        };

        DialogResult = true;
    }

    private void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public class AffixEditRow
{
    public string Text { get; set; } = string.Empty;
    public bool GreaterAffix { get; set; }
}
