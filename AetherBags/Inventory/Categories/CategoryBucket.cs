using System;
using System.Collections.Generic;
using AetherBags.Configuration;
using AetherBags.Inventory.Items;

namespace AetherBags.Inventory.Categories;

public sealed class CategoryBucket
{
    public uint Key;
    public CategoryInfo Category = null!;
    public List<ItemInfo> Items = null!;
    public List<ItemInfo> FilteredItems = null!;
    public ItemSortMode ItemSortMode = ItemSortMode.UseGlobal;
    public List<uint>? CustomItemOrder;
    public bool Used;
    public bool NeedsSorting = true;
}

public sealed class ItemSortComparer : IComparer<ItemInfo>
{
    private readonly ItemSortMode _sortMode;
    private readonly Dictionary<uint, int>? _customOrder;

    public ItemSortComparer(ItemSortMode sortMode, List<uint>? customItemOrder = null)
    {
        _sortMode = sortMode == ItemSortMode.UseGlobal ? ItemSortMode.QuantityDescending : sortMode;

        if (_sortMode == ItemSortMode.CustomOrder && customItemOrder is { Count: > 0 })
        {
            _customOrder = new Dictionary<uint, int>(customItemOrder.Count);
            for (int i = 0; i < customItemOrder.Count; i++)
            {
                _customOrder.TryAdd(customItemOrder[i], i);
            }
        }
    }

    public int Compare(ItemInfo? left, ItemInfo? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return 1;
        if (right is null) return -1;

        return _sortMode switch
        {
            ItemSortMode.NameAscending => CompareNameAscending(left, right),
            ItemSortMode.RarityDescending => CompareRarity(left, right, descending: true),
            ItemSortMode.RarityAscending => CompareRarity(left, right, descending: false),
            ItemSortMode.ItemIdAscending => CompareItemId(left, right, descending: false),
            ItemSortMode.ItemIdDescending => CompareItemId(left, right, descending: true),
            ItemSortMode.CustomOrder => CompareCustomOrder(left, right),
            ItemSortMode.GameCategory => CompareGameCategory(left, right),
            _ => CompareQuantityDescending(left, right),
        };
    }

    private static int CompareQuantityDescending(ItemInfo left, ItemInfo right)
    {
        int leftCount = left.ItemCount;
        int rightCount = right.ItemCount;

        if (leftCount > rightCount) return -1;
        if (leftCount < rightCount) return 1;
        return 0;
    }

    private static int CompareNameAscending(ItemInfo left, ItemInfo right)
    {
        int name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        if (name != 0) return name;

        return CompareFallback(left, right);
    }

    private static int CompareRarity(ItemInfo left, ItemInfo right, bool descending)
    {
        int rarity = descending
            ? right.Rarity.CompareTo(left.Rarity)
            : left.Rarity.CompareTo(right.Rarity);

        if (rarity != 0) return rarity;

        return CompareFallback(left, right);
    }

    private static int CompareItemId(ItemInfo left, ItemInfo right, bool descending)
    {
        int itemId = descending
            ? right.Item.ItemId.CompareTo(left.Item.ItemId)
            : left.Item.ItemId.CompareTo(right.Item.ItemId);

        if (itemId != 0) return itemId;

        return CompareFallback(left, right);
    }

    private int CompareCustomOrder(ItemInfo left, ItemInfo right)
    {
        if (_customOrder is null)
            return CompareQuantityDescending(left, right);

        bool leftHasRank = _customOrder.TryGetValue(left.Item.ItemId, out int leftRank);
        bool rightHasRank = _customOrder.TryGetValue(right.Item.ItemId, out int rightRank);

        if (leftHasRank && rightHasRank)
        {
            int rank = leftRank.CompareTo(rightRank);
            if (rank != 0) return rank;
        }
        else if (leftHasRank)
        {
            return -1;
        }
        else if (rightHasRank)
        {
            return 1;
        }

        return CompareFallback(left, right);
    }

    private static int CompareGameCategory(ItemInfo left, ItemInfo right)
    {
        int uiCategory = left.UiCategory.RowId.CompareTo(right.UiCategory.RowId);
        if (uiCategory != 0) return uiCategory;

        return CompareFallback(left, right);
    }

    private static int CompareFallback(ItemInfo left, ItemInfo right)
    {
        int quantity = CompareQuantityDescending(left, right);
        if (quantity != 0) return quantity;

        int name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        if (name != 0) return name;

        int itemId = left.Item.ItemId.CompareTo(right.Item.ItemId);
        if (itemId != 0) return itemId;

        return left.Key.CompareTo(right.Key);
    }
}