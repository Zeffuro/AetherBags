using Dalamud.Plugin.Services;
using System;
using System.Linq;
using AetherBags.Currency;
using AetherBags.Nodes.Configuration.Category;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonCurrencyPicker : SearchPickerAddon<Item, ItemListItemWithAddNode> {
    public AddonCurrencyPicker() {
        var allItems = IDataManager.Get().GetExcelSheet<Item>();
        var obsoleteTomes = IDataManager.Get().GetExcelSheet<TomestonesItem>()
            .Where(t => t.Tomestones.RowId == 0)
            .Select(t => t.Item.RowId).ToHashSet();

        var currentTomestones = CurrencyState.GetCurrentTomestoneIds();

        OptionsList = allItems
            .Where(i => (i.ItemUICategory.RowId == 100 || (i.RowId >= 1 && i.RowId < 100)) && !i.Name.IsEmpty)
            .Where(i => !obsoleteTomes.Contains(i.RowId))
            .Where(i => i.RowId != currentTomestones.Limited && i.RowId != currentTomestones.NonLimited)
            .ToList();
    }

    protected override Func<Item, bool> CreateSearchMatch(string search)
        => item => item.Name.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
    protected override int Compare(Item l, Item r) => string.CompareOrdinal(l.Name.ToString(), r.Name.ToString());
    protected override int CompareId(Item left, Item right) => left.RowId.CompareTo(right.RowId);

    protected override void BindRowCallbacks()
    {
        if (ResultsListNode is null) return;
        foreach (var row in ResultsListNode.OptionNodes) row.OnAddClicked = OnAddClicked;
    }
}
