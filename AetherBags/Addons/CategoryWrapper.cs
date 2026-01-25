using AetherBags.Configuration;
using AetherBags.Inventory.Categories;

namespace AetherBags.Addons;

// Removed IInfoNodeData implementation
public class CategoryWrapper(UserCategoryDefinition categoryDefinition)
{
    public UserCategoryDefinition? CategoryDefinition { get; } = categoryDefinition;

    public string GetLabel() => CategoryDefinition!.Name;

    public string GetSubLabel() {
        if(UserCategoryMatcher.IsCatchAll(CategoryDefinition!)) return " No valid rules!";
        return CategoryDefinition!.Enabled ? "✓ Enabled" : " Disabled";
    }

    public uint? GetId() => null;

    public uint? GetIconId() => 0;

    public int Compare(CategoryWrapper other, string sortingMode) {
        return CategoryDefinition!.Order.CompareTo(other.CategoryDefinition!.Order);
    }
}