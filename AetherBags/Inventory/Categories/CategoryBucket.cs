using System.Collections.Generic;
using AetherBags.Inventory.Items;

namespace AetherBags.Inventory.Categories;

public sealed class CategoryBucket
{
    public uint Key;
    public CategoryInfo Category = null!;
    public List<ItemInfo> Items = null!;
    public List<ItemInfo> FilteredItems = null!;
    public bool Used;
    public bool NeedsSorting = true;
}

public sealed class ItemCountDescComparer : IComparer<ItemInfo>
{
    public static readonly ItemCountDescComparer Instance = new();

    public int Compare(ItemInfo? left, ItemInfo? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return 1;
        if (right is null) return -1;

        int leftCount = left.ItemCount;
        int rightCount = right.ItemCount;

        if (leftCount > rightCount) return -1;
        if (leftCount < rightCount) return 1;
        return 0;
    }
}