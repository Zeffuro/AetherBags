using System;
using AetherBags.Nodes.Configuration.Category;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonUICategoryPicker : SearchPickerAddon<ItemUICategory, UICategoryListItemWithAddNode> {
    protected override int Compare(ItemUICategory left, ItemUICategory right)
        => string.CompareOrdinal(left.Name.ToString(), right.Name.ToString());

    protected override int CompareId(ItemUICategory left, ItemUICategory right) => left.RowId.CompareTo(right.RowId);

    protected override Func<ItemUICategory, bool> CreateSearchMatch(string search)
        => item => item.Name.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);

    protected override void BindRowCallbacks()
    {
        if (ResultsListNode is null) return;
        foreach (var row in ResultsListNode.OptionNodes) row.OnAddClicked = OnAddClicked;
    }
}
