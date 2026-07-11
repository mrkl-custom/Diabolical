using System.Linq;
using System.Windows;
using Diabolical.Models;
using Diabolical.Services;

namespace Diabolical.Views;

/// <summary>
/// Read-only popup showing a single equipped item's full stats, opened from the equipment
/// list's "View" button. "Copy JSON" serializes just this item (with its slot inlined) via
/// ItemDatabaseService.SerializeItem, for handing a single piece of gear to an AI assistant
/// without exporting the whole character.
/// </summary>
public partial class ItemDetailsDialog : Window
{
    private readonly string _slot;
    private readonly EquipmentItem _item;

    public ItemDetailsDialog(string slot, EquipmentItem item)
    {
        InitializeComponent();

        _slot = slot;
        _item = item;

        SlotText.Text = slot;
        NameText.Text = item.Name;
        RarityText.Text = item.Rarity.ToString();
        QualityText.Text = item.Quality.ToString();
        ItemPowerText.Text = item.ItemPower.ToString();
        FlagsText.Text = string.Join(" · ", new[]
            {
                item.Transfigured ? "Transfigured" : null,
                item.Modifiable ? "Modifiable" : "Not Modifiable"
            }.Where(flag => flag is not null));

        AffixesDataGrid.ItemsSource = item.Affixes;
        SpecialEffectsTextBox.Text = string.Join(Environment.NewLine, item.SpecialEffects);
    }

    private void CopyJsonButton_Click(object sender, RoutedEventArgs e)
    {
        ClipboardHelper.SetTextWithRetry(ItemDatabaseService.SerializeItem(_slot, _item));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
