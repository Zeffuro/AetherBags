using AetherBags.Configuration;
using KamiToolKit.Premade;

namespace AetherBags.Addons;

public class CategoryWrapper(UserCategoryDefinition categoryDefinition) : IInfoNodeData
{
    public UserCategoryDefinition? CategoryDefinition { get; } = categoryDefinition;

    public string GetLabel() {

        return CategoryDefinition!.Name;
    }

    public string GetSubLabel() {
        return CategoryDefinition!.Enabled ? "Enabled" : "Disabled";
    }

    public uint? GetId() => null;

    public uint? GetIconId() {
        return 0;
    }

    public string? GetTexturePath()
        => null;

    public int Compare(IInfoNodeData other, string sortingMode) {
        if (other is not CategoryWrapper otherWrapper) return 0;

        return CategoryDefinition!.Order.CompareTo(otherWrapper.CategoryDefinition!.Order);
    }
}