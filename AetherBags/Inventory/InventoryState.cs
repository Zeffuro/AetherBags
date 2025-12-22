using AetherBags.Currency;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AetherBags.Configuration;

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

    private static readonly List<UserCategoryDefinition> UserCategoriesSortedScratch = new(capacity: 64);

    private static readonly List<uint> RemoveKeysScratch = new(capacity: 256);

    private const uint UserCategoryKeyFlag = 0x8000_0000;

    private static uint MakeUserCategoryKey(int order)
        => UserCategoryKeyFlag | (uint)(order & 0x7FFF_FFFF);

    private static bool IsUserCategoryKey(uint key)
        => (key & UserCategoryKeyFlag) != 0;

    public static bool Contains(this IReadOnlyCollection<InventoryType> inventoryTypes, GameInventoryType type)
        => inventoryTypes.Contains((InventoryType)type);

    public static void RefreshFromGame()
    {
        InventoryManager* inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            ClearAll();
            return;
        }

        var config = System.Config;

        bool userCategoriesEnabled = config.Categories.UserCategoriesEnabled;
        bool gameCategoriesEnabled = config.Categories.GameCategoriesEnabled;

        List<UserCategoryDefinition> userCategories = config.Categories.UserCategories;

        AggByItemId.Clear();

        for (int inventoryIndex = 0; inventoryIndex < BagInventories.Length; inventoryIndex++)
        {
            var container = inventoryManager->GetInventoryContainer(BagInventories[inventoryIndex]);
            if (container == null)
                continue;

            int size = container->Size;
            for (int slot = 0; slot < size; slot++)
            {
                ref var item = ref container->Items[slot];
                uint id = item.ItemId;
                if (id == 0)
                    continue;

                int quantity = item.Quantity;

                if (AggByItemId.TryGetValue(id, out AggregatedItem agg))
                {
                    agg.Total += quantity;
                    AggByItemId[id] = agg;
                }
                else
                {
                    AggByItemId.Add(id, new AggregatedItem { First = item, Total = quantity });
                }
            }
        }

        foreach (var kvp in BucketsByKey)
        {
            CategoryBucket bucket = kvp.Value;
            bucket.Used = false;
            bucket.Items.Clear();
            bucket.FilteredItems.Clear();
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
        }

        // Bucket by user category
        HashSet<uint> claimedItemIds = new(capacity: ItemInfoByItemId.Count);

        if (userCategoriesEnabled && userCategories.Count > 0)
        {
            UserCategoriesSortedScratch.Clear();
            UserCategoriesSortedScratch.AddRange(userCategories);
            UserCategoriesSortedScratch.Sort((a, b) =>
            {
                int p = a.Priority.CompareTo(b.Priority);
                if (p != 0) return p;

                int o = a.Order.CompareTo(b.Order);
                if (o != 0) return o;

                return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
            });

            for (int c = 0; c < UserCategoriesSortedScratch.Count; c++)
            {
                UserCategoryDefinition category = UserCategoriesSortedScratch[c];
                uint key = MakeUserCategoryKey(category.Order);

                if (!BucketsByKey.TryGetValue(key, out CategoryBucket? bucket))
                {
                    bucket = new CategoryBucket
                    {
                        Key = key,
                        Category = new CategoryInfo
                        {
                            Name = category.Name,
                            Description = category.Description,
                            Color = category.Color,
                        },
                        Items = new List<ItemInfo>(capacity: 16),
                        FilteredItems = new List<ItemInfo>(capacity: 16),
                        Used = true,
                    };
                    BucketsByKey.Add(key, bucket);
                }
                else
                {
                    bucket.Used = true;
                    bucket.Category.Name = category.Name;
                    bucket.Category.Description = category.Description;
                    bucket.Category.Color = category.Color;
                }

                foreach (var itemKvp in ItemInfoByItemId)
                {
                    ItemInfo item = itemKvp.Value;
                    uint itemId = item.Item.ItemId;

                    if (claimedItemIds.Contains(itemId))
                        continue;

                    if (UserCategoryMatcher.Matches(item, category))
                    {
                        bucket.Items.Add(item);
                        claimedItemIds.Add(itemId);
                    }
                }

                if (bucket.Items.Count == 0)
                    bucket.Used = false;
            }
        }

        // Game category bucket
        if (gameCategoriesEnabled)
        {
            foreach (var itemKvp in ItemInfoByItemId)
            {
                ItemInfo info = itemKvp.Value;

                if (userCategoriesEnabled && claimedItemIds.Contains(info.Item.ItemId))
                    continue;

                uint categoryKey = info.UiCategory.RowId;

                if (!BucketsByKey.TryGetValue(categoryKey, out CategoryBucket? bucket))
                {
                    bucket = new CategoryBucket
                    {
                        Key = categoryKey,
                        Category = GetCategoryInfoForKeyCached(categoryKey, info),
                        Items = new List<ItemInfo>(capacity: 16),
                        FilteredItems = new List<ItemInfo>(capacity: 16),
                        Used = true,
                    };
                    BucketsByKey.Add(categoryKey, bucket);
                }
                else
                {
                    bucket.Used = true;
                }

                bucket.Items.Add(info);
            }
        }

        // Unclaimed items
        if (!gameCategoriesEnabled)
        {
            if (!BucketsByKey.TryGetValue(0u, out CategoryBucket? miscBucket))
            {
                CategoryInfo miscInfo;
                if (ItemInfoByItemId.Count > 0)
                {
                    var sample = ItemInfoByItemId.Values.First();
                    miscInfo = GetCategoryInfoForKeyCached(0u, sample);
                }
                else
                {
                    miscInfo = new CategoryInfo { Name = "Misc", Description = "Uncategorized items" };
                }

                miscBucket = new CategoryBucket
                {
                    Key = 0u,
                    Category = miscInfo,
                    Items = new List<ItemInfo>(capacity: 16),
                    FilteredItems = new List<ItemInfo>(capacity: 16),
                    Used = true,
                };
                BucketsByKey.Add(0u, miscBucket);
            }
            else
            {
                miscBucket.Used = true;
            }

            foreach (var itemKvp in ItemInfoByItemId)
            {
                ItemInfo info = itemKvp.Value;

                if (userCategoriesEnabled && claimedItemIds.Contains(info.Item.ItemId))
                    continue;

                miscBucket.Items.Add(info);
            }

            if (miscBucket.Items.Count == 0)
                miscBucket.Used = false;
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

        SortedCategoryKeys.Sort((a, b) =>
        {
            bool au = IsUserCategoryKey(a);
            bool bu = IsUserCategoryKey(b);
            if (au != bu) return au ? -1 : 1;
            return a.CompareTo(b);
        });

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

    private const uint CurrencyIdLimitedTomestone = 0xFFFF_FFFE;
    private const uint CurrencyIdNonLimitedTomestone = 0xFFFF_FFFD;

    private static uint? GetLimitedTomestoneItemId()
        => Services.DataManager.GetExcelSheet<TomestonesItem>()
            .FirstOrDefault(t => t.Tomestones.RowId == 3)
            .Item.RowId;

    private static uint? GetNonLimitedTomestoneItemId()
        => Services.DataManager.GetExcelSheet<TomestonesItem>()
            .FirstOrDefault(t => t.Tomestones.RowId == 2)
            .Item.RowId;

    private static CurrencyItem ResolveCurrencyItemId(uint currencyId)
    {
        uint itemId = currencyId;
        bool isLimited = false;

        if (currencyId == CurrencyIdLimitedTomestone)
        {
            itemId = GetLimitedTomestoneItemId() ?? 0;
            isLimited = true;
        }

        if (currencyId == CurrencyIdNonLimitedTomestone)
        {
            itemId = GetNonLimitedTomestoneItemId() ?? 0;
        }

        return new CurrencyItem(itemId, isLimited);
    }


    public static IReadOnlyList<CurrencyInfo> GetCurrencyInfoList(uint[] currencyIds)
    {
        if (currencyIds.Length == 0) return Array.Empty<CurrencyInfo>();

        List<CurrencyInfo> currencyInfoList = new List<CurrencyInfo>(currencyIds.Length);

        for (int i = 0; i < currencyIds.Length; i++)
        {
            CurrencyItem currencyItem = ResolveCurrencyItemId(currencyIds[i]);
            if (currencyItem.ItemId == 0)
                continue;

            currencyInfoList.Add(GetCurrencyInfo(currencyItem));
        }

        return currencyInfoList;
    }

    private static CurrencyInfo GetCurrencyInfo(CurrencyItem currencyItem)
    {
        InventoryManager* inventoryManager = InventoryManager.Instance();
        var item = Services.DataManager.GetExcelSheet<Item>().GetRow(currencyItem.ItemId);

        uint amount = (uint) inventoryManager->GetInventoryItemCount(currencyItem.ItemId);
        uint maxAmount = item.StackSize;
        bool isCapped = false;
        if (currencyItem.IsLimited)
        {
            int weeklyLimit = InventoryManager.GetLimitedTomestoneWeeklyLimit();
            int weeklyAcquired = inventoryManager->GetWeeklyAcquiredTomestoneCount();
            isCapped = weeklyAcquired >= weeklyLimit;
        }

        return new CurrencyInfo
        {
            Amount = amount,
            MaxAmount = item.StackSize,
            ItemId = currencyItem.ItemId,
            IconId = item.Icon,
            LimitReached = amount >= maxAmount,
            IsCapped = isCapped
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

        public int Compare(ItemInfo? x, ItemInfo? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return 1;   // nulls last
            if (y is null) return -1;

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

    private record CurrencyItem(uint ItemId, bool IsLimited);
}
