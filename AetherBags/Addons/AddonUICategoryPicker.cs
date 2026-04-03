using AetherBags.Nodes.Configuration.Category;
using KamiToolKit.Premade.Addon.Search;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonUICategoryPicker : BaseSearchAddon<ItemUICategory, UICategoryListItemWithAddNode> {
    protected override int Comparer(ItemUICategory left, ItemUICategory right, string sort, bool rev)
        => string.CompareOrdinal(left.Name.ToString(), right.Name.ToString());

    protected override bool IsMatch(ItemUICategory item, string search)
        => item.Name.ToString().Contains(search, global::System.StringComparison.OrdinalIgnoreCase);
}