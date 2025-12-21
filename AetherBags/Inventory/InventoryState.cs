using AetherBags.Currency;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AetherBags.Inventory;

public static unsafe class InventoryState
{
    public static readonly InventoryType[] StandardInventories =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.EquippedItems,
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryWaist,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.Currency,
        InventoryType.Crystals,
        InventoryType.ArmorySoulCrystal,
    ];

    private static readonly InventoryType[] BagInventories =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    private static readonly Dictionary<uint, CategoryInfo> CategoryInfoCache = new(capacity: 256);

    private static readonly Dictionary<uint, AggregatedItem> AggByItemId = new(capacity: 512);
    private static readonly Dictionary<uint, ItemInfo> ItemInfoByItemId = new(capacity: 512);

    private static readonly Dictionary<uint, CategoryBucket> BucketsByKey = new(capacity: 256);
    private static readonly List<uint> SortedCategoryKeys = new(capacity: 256);

    private static readonly List<CategorizedInventory> AllCategories = new(capacity: 256);

    private static readonly List<CategorizedInventory> FilteredCategories = new(capacity: 256);

    private static readonly List<uint> RemoveKeysScratch = new(capacity: 256);

    public static bool Contains(this IReadOnlyCollection<InventoryType> inventoryTypes, GameInventoryType type)
        => inventoryTypes.Contains((InventoryType)type);

    public static void RefreshFromGame()
    {
        InventoryManager* mgr = InventoryManager.Instance();
        if (mgr == null)
        {
            ClearAll();
            return;
        }

        AggByItemId.Clear();

        for (int invIndex = 0; invIndex < BagInventories.Length; invIndex++)
        {
            var container = mgr->GetInventoryContainer(BagInventories[invIndex]);
            if (container == null)
                continue;

            int size = container->Size;
            for (int slot = 0; slot < size; slot++)
            {
                ref var item = ref container->Items[slot];
                uint id = item.ItemId;
                if (id == 0)
                    continue;

                int qty = item.Quantity;

                if (AggByItemId.TryGetValue(id, out AggregatedItem agg))
                {
                    agg.Total += qty;
                    AggByItemId[id] = agg;
                }
                else
                {
                    AggByItemId.Add(id, new AggregatedItem { First = item, Total = qty });
                }
            }
        }

        foreach (var kvp in BucketsByKey)
        {
            CategoryBucket b = kvp.Value;
            b.Used = false;
            b.Items.Clear();
            b.FilteredItems.Clear();
        }

        foreach (var kvp in AggByItemId)
        {
            uint itemId = kvp.Key;
            AggregatedItem agg = kvp.Value;

            if (!ItemInfoByItemId.TryGetValue(itemId, out ItemInfo? info))
            {
                info = new ItemInfo
                {
                    Item = agg.First,
                    ItemCount = agg.Total,
                };
                ItemInfoByItemId.Add(itemId, info);
            }
            else
            {
                info.Item = agg.First;
                info.ItemCount = agg.Total;
            }

            uint catKey = info.UiCategory.RowId;

            if (!BucketsByKey.TryGetValue(catKey, out CategoryBucket? bucket))
            {
                bucket = new CategoryBucket
                {
                    Key = catKey,
                    Category = GetCategoryInfoForKeyCached(catKey, info),
                    Items = new List<ItemInfo>(capacity: 16),
                    FilteredItems = new List<ItemInfo>(capacity: 16),
                    Used = true,
                };
                BucketsByKey.Add(catKey, bucket);
            }
            else
            {
                bucket.Used = true;
            }

            bucket.Items.Add(info);
        }

        if (ItemInfoByItemId.Count != AggByItemId.Count)
        {
            RemoveKeysScratch.Clear();

            foreach (var kvp in ItemInfoByItemId)
            {
                uint itemId = kvp.Key;
                if (!AggByItemId.ContainsKey(itemId))
                    RemoveKeysScratch.Add(itemId);
            }

            for (int i = 0; i < RemoveKeysScratch.Count; i++)
                ItemInfoByItemId.Remove(RemoveKeysScratch[i]);
        }

        SortedCategoryKeys.Clear();

        foreach (var kvp in BucketsByKey)
        {
            CategoryBucket bucket = kvp.Value;
            if (!bucket.Used)
                continue;

            bucket.Items.Sort(ItemCountDescComparer.Instance);
            SortedCategoryKeys.Add(bucket.Key);
        }

        SortedCategoryKeys.Sort();

        AllCategories.Clear();
        AllCategories.Capacity = Math.Max(AllCategories.Capacity, SortedCategoryKeys.Count);

        for (int i = 0; i < SortedCategoryKeys.Count; i++)
        {
            uint key = SortedCategoryKeys[i];
            CategoryBucket bucket = BucketsByKey[key];
            AllCategories.Add(new CategorizedInventory(bucket.Key, bucket.Category, bucket.Items));
        }
    }

    public static IReadOnlyList<CategorizedInventory> GetInventoryItemCategories(string filterString = "", bool invert = false)
    {
        if (string.IsNullOrEmpty(filterString))
            return AllCategories;

        Regex? re = null;
        bool regexValid = true;

        try
        {
            re = new Regex(filterString, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch
        {
            regexValid = false;
        }

        FilteredCategories.Clear();

        for (int i = 0; i < AllCategories.Count; i++)
        {
            CategorizedInventory cat = AllCategories[i];
            CategoryBucket bucket = BucketsByKey[cat.Key];

            var filtered = bucket.FilteredItems;
            filtered.Clear();

            var src = bucket.Items;
            for (int j = 0; j < src.Count; j++)
            {
                ItemInfo info = src[j];

                bool isMatch;
                if (regexValid)
                {
                    isMatch = info.IsRegexMatch(re!);
                }
                else
                {
                    isMatch = info.Name.Contains(filterString, StringComparison.OrdinalIgnoreCase)
                              || info.DescriptionContains(filterString);
                }

                if (isMatch != invert)
                    filtered.Add(info);
            }

            if (filtered.Count != 0)
                FilteredCategories.Add(new CategorizedInventory(bucket.Key, bucket.Category, filtered));
        }

        return FilteredCategories;
    }

    public static string GetEmptyItemSlotsString()
    {
        uint empty = InventoryManager.Instance()->GetEmptySlotsInBag();
        uint used = 140 - empty;
        return $"{used}/140";
    }

    public static CurrencyInfo GetCurrencyInfo(uint itemId)
    {
        return new CurrencyInfo
        {
            Amount = InventoryManager.Instance()->GetInventoryItemCount(1),
            ItemId = itemId,
            IconId = Services.DataManager.GetExcelSheet<Item>().GetRow(itemId).Icon
        };
    }

    private static void ClearAll()
    {
        AggByItemId.Clear();
        ItemInfoByItemId.Clear();

        foreach (var kvp in BucketsByKey)
        {
            kvp.Value.Items.Clear();
            kvp.Value.FilteredItems.Clear();
            kvp.Value.Used = false;
        }

        SortedCategoryKeys.Clear();
        AllCategories.Clear();
        FilteredCategories.Clear();
        RemoveKeysScratch.Clear();
    }

    private static CategoryInfo GetCategoryInfoForKeyCached(uint key, ItemInfo sample)
    {
        if (CategoryInfoCache.TryGetValue(key, out var cached))
            return cached;

        CategoryInfo info = GetCategoryInfoForKeySlow(key, sample);
        CategoryInfoCache[key] = info;
        return info;
    }

    private static CategoryInfo GetCategoryInfoForKeySlow(uint key, ItemInfo sample)
    {
        if (key == 0)
        {
            return new CategoryInfo
            {
                Name = "Misc",
                Description = "Uncategorized items",
            };
        }

        var uiCat = sample.UiCategory.Value;
        string? name = uiCat.Name.ToString();

        if (string.IsNullOrWhiteSpace(name))
            name = $"Category\\ {key}";

        return new CategoryInfo
        {
            Name = name,
        };
    }

    private struct AggregatedItem
    {
        public InventoryItem First;
        public int Total;
    }

    private sealed class ItemCountDescComparer : IComparer<ItemInfo>
    {
        public static readonly ItemCountDescComparer Instance = new();

        public int Compare(ItemInfo x, ItemInfo y)
        {
            int a = x.ItemCount;
            int b = y.ItemCount;
            if (a > b) return -1;
            if (a < b) return 1;
            return 0;
        }
    }

    private sealed class CategoryBucket
    {
        public uint Key;
        public CategoryInfo Category = null!;
        public List<ItemInfo> Items = null!;
        public List<ItemInfo> FilteredItems = null!;
        public bool Used;
    }
}
