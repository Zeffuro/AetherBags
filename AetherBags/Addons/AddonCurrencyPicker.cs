using System;
using System.Linq;
using AetherBags.Currency;
using KamiToolKit.Premade.ListItemNodes;
using KamiToolKit.Premade.SearchAddons;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonCurrencyPicker : BaseSearchAddon<Item, ItemListItemNode> {
    public AddonCurrencyPicker() {
        var allItems = Services.DataManager.GetExcelSheet<Item>();
        var obsoleteTomes = Services.DataManager.GetExcelSheet<TomestonesItem>()
            .Where(t => t.Tomestones.RowId == 0)
            .Select(t => t.Item.RowId).ToHashSet();

        var currentTomestones = CurrencyState.GetCurrentTomestoneIds();

        SearchOptions = allItems
            .Where(i => (i.ItemUICategory.RowId == 100 || (i.RowId >= 1 && i.RowId < 100)) && !i.Name.IsEmpty)
            .Where(i => !obsoleteTomes.Contains(i.RowId))
            .Where(i => i.RowId != currentTomestones.Limited && i.RowId != currentTomestones.NonLimited)
            .ToList();
    }

    protected override bool IsMatch(Item item, string search) => item.Name.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
    protected override int Comparer(Item l, Item r, string s, bool rev) => string.CompareOrdinal(l.Name.ToString(), r.Name.ToString());
}