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
    public List<ItemSortCriterion>? ItemSortCriteria;
    public List<uint>? CustomItemOrder;
    public bool Used;
    public bool NeedsSorting = true;
}

public sealed class ItemSortComparer : IComparer<ItemInfo>
{
    private readonly IReadOnlyList<ItemSortCriterion> _criteria;
    private readonly Dictionary<uint, int>? _customOrder;

    public ItemSortComparer(IReadOnlyList<ItemSortCriterion> criteria, List<uint>? customItemOrder = null)
    {
        _criteria = criteria.Count > 0
            ? criteria
            : CategorySettings.GetDefaultItemSortCriteria(allowUseGlobal: false);

        if (customItemOrder is { Count: > 0 } && ContainsCustomOrder(_criteria))
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

        foreach (var criterion in _criteria)
        {
            int result = CompareCriterion(left, right, criterion);
            if (result != 0)
                return result;
        }

        return CompareFallback(left, right);
    }

    private static bool ContainsCustomOrder(IReadOnlyList<ItemSortCriterion> criteria)
    {
        for (int i = 0; i < criteria.Count; i++)
        {
            if (criteria[i].Field == ItemSortField.CustomOrder)
                return true;
        }

        return false;
    }

    private int CompareCriterion(ItemInfo left, ItemInfo right, ItemSortCriterion criterion)
    {
        if (criterion.Field == ItemSortField.CustomOrder)
            return CompareCustomOrder(left, right, criterion.Direction);

        int result = criterion.Field switch
        {
            ItemSortField.Quantity => CompareQuantity(left, right),
            ItemSortField.Name => CompareName(left, right),
            ItemSortField.Rarity => left.Rarity.CompareTo(right.Rarity),
            ItemSortField.ItemId => left.Item.ItemId.CompareTo(right.Item.ItemId),
            ItemSortField.GameCategory => left.UiCategory.RowId.CompareTo(right.UiCategory.RowId),
            ItemSortField.ItemLevel => left.ItemLevel.CompareTo(right.ItemLevel),
            _ => 0,
        };

        return criterion.Direction == SortDirection.Descending ? -result : result;
    }

    private static int CompareQuantityDescending(ItemInfo left, ItemInfo right)
    {
        int quantity = CompareQuantity(left, right);
        return quantity == 0 ? 0 : -quantity;
    }

    private static int CompareQuantity(ItemInfo left, ItemInfo right)
    {
        int leftCount = left.ItemCount;
        int rightCount = right.ItemCount;

        return leftCount.CompareTo(rightCount);
    }

    private static int CompareName(ItemInfo left, ItemInfo right)
        => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

    private int CompareCustomOrder(ItemInfo left, ItemInfo right, SortDirection direction)
    {
        if (_customOrder is null)
            return 0;

        bool leftHasRank = _customOrder.TryGetValue(left.Item.ItemId, out int leftRank);
        bool rightHasRank = _customOrder.TryGetValue(right.Item.ItemId, out int rightRank);

        if (leftHasRank && rightHasRank)
        {
            int rank = leftRank.CompareTo(rightRank);
            if (direction == SortDirection.Descending)
                rank = -rank;

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

        return 0;
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