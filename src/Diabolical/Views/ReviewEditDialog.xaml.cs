using System.Windows;
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

    public ReviewEditDialog(ParsedItemExtraction parsed)
    {
        InitializeComponent();

        RarityComboBox.ItemsSource = Enum.GetValues<ItemRarity>();

        SlotTextBox.Text = parsed.Slot;
        NameTextBox.Text = parsed.Name;
        RarityComboBox.SelectedItem = parsed.Rarity;
        ItemPowerTextBox.Text = parsed.ItemPower.ToString();
        AspectTextBox.Text = parsed.Aspect;
        AffixesTextBox.Text = string.Join(Environment.NewLine, parsed.Affixes);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
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
            ItemPower = itemPower,
            Affixes = AffixesTextBox.Text
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList(),
            Aspect = string.IsNullOrWhiteSpace(AspectTextBox.Text) ? null : AspectTextBox.Text.Trim()
        };

        DialogResult = true;
    }

    private void DiscardButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
