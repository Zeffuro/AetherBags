using System;
using System.Text.RegularExpressions;
using AetherBags.Nodes.Configuration.Category;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonItemPicker : SearchPickerAddon<Item, ItemListItemWithAddNode>
{
    protected override int Compare(Item left, Item right)
        => string.CompareOrdinal(left.Name.ToString(), right.Name.ToString());

    protected override int CompareId(Item left, Item right) => left.RowId.CompareTo(right.RowId);

    protected override Func<Item, bool> CreateSearchMatch(string search)
    {
        var includeDescription = search.StartsWith('$');
        if (includeDescription) search = search[1..];
        Regex regex;
        try
        {
            regex = new Regex(search, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            regex = new Regex(Regex.Escape(search), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }

        return item => regex.IsMatch(item.RowId.ToString())
                       || regex.IsMatch(item.Name.ToString())
                       || (includeDescription && regex.IsMatch(item.Description.ToString()))
                       || regex.IsMatch(item.LevelEquip.ToString())
                       || regex.IsMatch(item.LevelItem.RowId.ToString())
                       || regex.IsMatch(item.ClassJobCategory.ValueNullable?.Name.ToString() ?? string.Empty)
                       || regex.IsMatch(item.ItemUICategory.ValueNullable?.Name.ToString() ?? string.Empty);
    }

    protected override void BindRowCallbacks()
    {
        if (ResultsListNode is null) return;
        foreach (var row in ResultsListNode.OptionNodes) row.OnAddClicked = OnAddClicked;
    }
}
