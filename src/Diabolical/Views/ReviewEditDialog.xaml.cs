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
    public string Slot { get; private set; } = string.Empty;
    public EquipmentItem Item { get; private set; } = new();

    private readonly ObservableCollection<AffixEditRow> _affixRows;

    public ReviewEditDialog(ParsedItemExtraction parsed)
    {
        InitializeComponent();

        RarityComboBox.ItemsSource = Enum.GetValues<ItemRarity>();
        QualityComboBox.ItemsSource = Enum.GetValues<ItemQuality>();
        AffixSourceColumn.ItemsSource = Enum.GetValues<AffixSource>();

        SlotTextBox.Text = parsed.Slot;
        NameTextBox.Text = parsed.Name;
        RarityComboBox.SelectedItem = parsed.Rarity;
        QualityComboBox.SelectedItem = parsed.Quality;
        ItemPowerTextBox.Text = parsed.ItemPower.ToString();
        TransfiguredCheckBox.IsChecked = parsed.Transfigured;
        ModifiableCheckBox.IsChecked = parsed.Modifiable;
        SpecialEffectsTextBox.Text = string.Join(Environment.NewLine, parsed.SpecialEffects);

        _affixRows = new ObservableCollection<AffixEditRow>(
            parsed.Affixes.Select(a => new AffixEditRow { Text = a.Text, Source = a.Source }));
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

        if (string.IsNullOrWhiteSpace(SlotTextBox.Text))
        {
            MessageBox.Show(this, "Slot is required.", "Diabolical", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(ItemPowerTextBox.Text, out var itemPower))
        {
            MessageBox.Show(this, "Item Power must be a number.", "Diabolical", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Slot = SlotTextBox.Text.Trim();
        Item = new EquipmentItem
        {
            Name = NameTextBox.Text.Trim(),
            Rarity = RarityComboBox.SelectedItem is ItemRarity rarity ? rarity : ItemRarity.Unknown,
            Quality = QualityComboBox.SelectedItem is ItemQuality quality ? quality : ItemQuality.Unknown,
            ItemPower = itemPower,
            Affixes = _affixRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .Select(row => new ItemAffix { Text = row.Text.Trim(), Source = row.Source })
                .ToList(),
            SpecialEffects = SpecialEffectsTextBox.Text
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
    public AffixSource Source { get; set; } = AffixSource.Base;
}
