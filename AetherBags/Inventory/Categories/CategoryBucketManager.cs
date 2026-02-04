using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AetherBags.Configuration;
using AetherBags.Inventory.Items;
using KamiToolKit.Classes;

namespace AetherBags.Inventory.Categories;

public static class CategoryBucketManager
{
    private const uint UserCategoryKeyFlag = 0x8000_0000;

    private static readonly Dictionary<uint, CategoryInfo> CategoryInfoCache = new(capacity: 256);

    public static uint MakeUserCategoryKey(int order)
        => UserCategoryKeyFlag | (uint)(order & 0x7FFF_FFFF);

    public static bool IsUserCategoryKey(uint key)
        => (key & UserCategoryKeyFlag) != 0;

    private const uint AllaganFilterKeyFlag = 0x4000_0000;

    private const uint BisBuddyKeyFlag = 0x2000_0000;

    public static uint MakeAllaganFilterKey(int index)
        => AllaganFilterKeyFlag | (uint)(index & 0x3FFF_FFFF);

    public static uint MakeBisBuddyKey()
        => BisBuddyKeyFlag;

    public static bool IsBisBuddyKey(uint key)
        => (key & BisBuddyKeyFlag) != 0
           && (key & AllaganFilterKeyFlag) == 0
           && (key & UserCategoryKeyFlag) == 0;

    public static bool IsAllaganFilterKey(uint key)
        => (key & AllaganFilterKeyFlag) != 0 && (key & UserCategoryKeyFlag) == 0;


    /// <summary>
    /// Resets all buckets for a new refresh cycle.
    /// </summary>
    public static void ResetBuckets(Dictionary<uint, CategoryBucket> bucketsByKey)
    {
        foreach (var kvp in bucketsByKey)
        {
            CategoryBucket bucket = kvp.Value;
            bucket.Used = false;
            bucket.Items.Clear();
            bucket.FilteredItems.Clear();
            bucket.NeedsSorting = true;
        }
    }

    public static void BucketByUserCategories(
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        List<UserCategoryDefinition> userCategories,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        HashSet<ulong> claimedKeys,
        List<UserCategoryDefinition> sortedScratch)
    {
        sortedScratch.Clear();
        sortedScratch.AddRange(userCategories);
        sortedScratch.Sort(UserCategoryComparer.Instance);

        var activeBuckets = new (uint key, CategoryBucket bucket, UserCategoryDefinition def)[sortedScratch.Count];
        int activeCount = 0;

        for (int i = 0; i < sortedScratch.Count; i++)
        {
            UserCategoryDefinition category = sortedScratch[i];

            if (!category.Enabled || UserCategoryMatcher.IsCatchAll(category))
                continue;

            uint bucketKey = MakeUserCategoryKey(category.Order);
            ref var bucketRef = ref CollectionsMarshal.GetValueRefOrAddDefault(bucketsByKey, bucketKey, out bool exists);

            if (!exists)
            {
                bucketRef = new CategoryBucket
                {
                    Key = bucketKey,
                    Category = new CategoryInfo
                    {
                        Name = category.Name,
                        Description = category.Description,
                        Color = category.Color,
                        IsPinned = category.Pinned,
                    },
                    Items = new List<ItemInfo>(capacity: 16),
                    FilteredItems = new List<ItemInfo>(capacity: 16),
                    Used = true,
                };
            }
            else
            {
                bucketRef!.Used = true;
                bucketRef.Category.Name = category.Name;
                bucketRef.Category.Description = category.Description;
                bucketRef.Category.Color = category.Color;
                bucketRef.Category.IsPinned = category.Pinned;
            }

            activeBuckets[activeCount++] = (bucketKey, bucketRef!, category);
        }

        foreach (var itemKvp in itemInfoByKey)
        {
            ulong itemKey = itemKvp.Key;
            if (claimedKeys.Contains(itemKey))
                continue;

            ItemInfo item = itemKvp.Value;

            for (int i = 0; i < activeCount; i++)
            {
                ref var entry = ref activeBuckets[i];
                if (UserCategoryMatcher.Matches(item, entry.def))
                {
                    entry.bucket.Items.Add(item);
                    claimedKeys.Add(itemKey);
                    break;
                }
            }
        }

        for (int i = 0; i < activeCount; i++)
        {
            ref var entry = ref activeBuckets[i];
            if (entry.bucket.Items.Count == 0)
                entry.bucket.Used = false;
        }
    }

    private sealed class UserCategoryComparer : IComparer<UserCategoryDefinition>
    {
        public static readonly UserCategoryComparer Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(UserCategoryDefinition? left, UserCategoryDefinition? right)
        {
            if (left is null || right is null) return 0;

            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0) return priority;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0) return order;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void BucketByGameCategories(
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        HashSet<ulong> claimedKeys,
        bool userCategoriesEnabled)
    {
        foreach (var itemKvp in itemInfoByKey)
        {
            ulong itemKey = itemKvp.Key;
            ItemInfo info = itemKvp.Value;

            if (userCategoriesEnabled && claimedKeys.Contains(itemKey))
                continue;

            uint categoryKey = info.UiCategory.RowId;

            ref var bucketRef = ref CollectionsMarshal.GetValueRefOrAddDefault(bucketsByKey, categoryKey, out bool exists);

            if (!exists)
            {
                bucketRef = new CategoryBucket
                {
                    Key = categoryKey,
                    Category = GetCategoryInfoCached(categoryKey, info),
                    Items = new List<ItemInfo>(capacity: 16),
                    FilteredItems = new List<ItemInfo>(capacity: 16),
                    Used = true,
                };
            }
            else
            {
                bucketRef!.Used = true;
            }

            bucketRef!.Items.Add(info);
        }
    }

    public static void BucketByAllaganFilters(
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        HashSet<ulong> claimedKeys,
        bool allaganCategoriesEnabled)
    {
        if (!allaganCategoriesEnabled) return;
        if (!System.IPC.AllaganTools.IsReady) return;

        var filters = System.IPC.AllaganTools.CachedSearchFilters;
        var itemToFilters = System.IPC.AllaganTools.ItemToFilters;

        if (filters.Count == 0 || itemToFilters.Count == 0) return;

        var filterKeyToIndex = new Dictionary<string, int>(filters.Count);
        int index = 0;
        foreach (var filterKey in filters.Keys)
        {
            filterKeyToIndex[filterKey] = index++;
        }

        index = 0;
        foreach (var (filterKey, filterName) in filters)
        {
            uint bucketKey = MakeAllaganFilterKey(index);
            ref var bucketRef = ref CollectionsMarshal.GetValueRefOrAddDefault(bucketsByKey, bucketKey, out bool exists);

            if (!exists)
            {
                bucketRef = new CategoryBucket
                {
                    Key = bucketKey,
                    Category = new CategoryInfo
                    {
                        Name = $"[AT] {filterName}",
                        Description = $"Allagan Tools filter: {filterName}",
                        Color = ColorHelper.GetColor(32),
                    },
                    Items = new List<ItemInfo>(capacity: 16),
                    FilteredItems = new List<ItemInfo>(capacity: 16),
                    Used = true,
                };
            }
            else
            {
                bucketRef!.Used = true;
                bucketRef.Category.Name = $"[AT] {filterName}";
            }

            index++;
        }

        foreach (var itemKvp in itemInfoByKey)
        {
            ulong itemKey = itemKvp.Key;
            if (claimedKeys.Contains(itemKey))
                continue;

            ItemInfo item = itemKvp.Value;

            if (!itemToFilters.TryGetValue(item.Item.ItemId, out var filterKeys))
                continue;

            if (filterKeys.Count > 0 && filterKeyToIndex.TryGetValue(filterKeys[0], out int filterIndex))
            {
                uint bucketKey = MakeAllaganFilterKey(filterIndex);
                if (bucketsByKey.TryGetValue(bucketKey, out var bucket))
                {
                    bucket.Items.Add(item);
                    claimedKeys.Add(itemKey);
                }
            }
        }

        index = 0;
        foreach (var _ in filters)
        {
            uint bucketKey = MakeAllaganFilterKey(index++);
            if (bucketsByKey.TryGetValue(bucketKey, out var bucket) && bucket.Items.Count == 0)
                bucket.Used = false;
        }
    }

    public static void BucketByBisBuddyItems(
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        HashSet<ulong> claimedKeys,
        bool bisCategoriesEnabled)
    {
        if (!bisCategoriesEnabled) return;
        if (!System.IPC.BisBuddy.IsReady) return;

        var bisItems = System.IPC.BisBuddy.ItemLookup;
        if (bisItems.Count == 0) return;

        uint bucketKey = MakeBisBuddyKey();

        ref var bucketRef = ref CollectionsMarshal.GetValueRefOrAddDefault(bucketsByKey, bucketKey, out bool exists);

        if (!exists)
        {
            bucketRef = new CategoryBucket
            {
                Key = bucketKey,
                Category = new CategoryInfo
                {
                    Name = "[BiS] Best in Slot",
                    Description = "Items needed for your BiS gearsets",
                    Color = ColorHelper.GetColor(50),
                },
                Items = new List<ItemInfo>(capacity: 16),
                FilteredItems = new List<ItemInfo>(capacity: 16),
                Used = true,
            };
        }
        else
        {
            bucketRef!.Used = true;
        }

        var bucket = bucketRef!;

        foreach (var itemKvp in itemInfoByKey)
        {
            ulong itemKey = itemKvp.Key;
            if (claimedKeys.Contains(itemKey))
                continue;

            ItemInfo item = itemKvp.Value;

            if (bisItems.ContainsKey(item.Item.ItemId))
            {
                bucket.Items.Add(item);
                claimedKeys.Add(itemKey);
            }
        }

        if (bucket.Items.Count == 0)
            bucket.Used = false;
    }

    public static void BucketUnclaimedToMisc(
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        HashSet<ulong> claimedKeys,
        bool userCategoriesEnabled)
    {
        if (!bucketsByKey.TryGetValue(0u, out CategoryBucket? miscBucket))
        {
            CategoryInfo miscInfo;
            if (itemInfoByKey.Count > 0)
            {
                using var enumerator = itemInfoByKey.Values.GetEnumerator();
                enumerator.MoveNext();
                miscInfo = GetCategoryInfoCached(0u, enumerator.Current);
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
            bucketsByKey.Add(0u, miscBucket);
        }
        else
        {
            miscBucket.Used = true;
        }

        foreach (var itemKvp in itemInfoByKey)
        {
            ulong itemKey = itemKvp.Key;
            ItemInfo info = itemKvp.Value;

            if (userCategoriesEnabled && claimedKeys.Contains(itemKey))
                continue;

            miscBucket.Items.Add(info);
        }

        if (miscBucket.Items.Count == 0)
            miscBucket.Used = false;
    }

    public static void SortBucketsAndBuildKeyList(
        Dictionary<uint, CategoryBucket> bucketsByKey,
        List<uint> sortedCategoryKeys)
    {
        sortedCategoryKeys.Clear();

        foreach (var kvp in bucketsByKey)
        {
            CategoryBucket bucket = kvp.Value;
            if (!bucket.Used)
                continue;

            // TODO: Make configurable
            // Only sort if items changed
            if (bucket.NeedsSorting)
            {
                bucket.Items.Sort(ItemCountDescComparer.Instance);
                bucket.NeedsSorting = false;
            }
            sortedCategoryKeys.Add(bucket.Key);
        }

        // TODO: Make sortable by user
        sortedCategoryKeys.Sort((left, right) =>
        {
            int GetPriority(uint key)
            {
                if (IsUserCategoryKey(key)) return 1;
                if (IsBisBuddyKey(key)) return 2;
                if (IsAllaganFilterKey(key)) return 3;
                if (key == 0) return 99;
                return 10;
            }

            int leftPrio = GetPriority(left);
            int rightPrio = GetPriority(right);

            return leftPrio != rightPrio ? leftPrio.CompareTo(rightPrio) : left.CompareTo(right);
        });
    }

    public static void BuildCategorizedList(
        Dictionary<uint, CategoryBucket> bucketsByKey,
        List<uint> sortedCategoryKeys,
        List<CategorizedInventory> allCategories)
    {
        allCategories.Clear();
        allCategories.Capacity = Math.Max(allCategories.Capacity, sortedCategoryKeys.Count);

        for (int i = 0; i < sortedCategoryKeys.Count; i++)
        {
            uint key = sortedCategoryKeys[i];
            CategoryBucket bucket = bucketsByKey[key];
            allCategories.Add(new CategorizedInventory(bucket.Key, bucket.Category, bucket.Items));
        }

        int displayed = 0;
        for (int i = 0; i < allCategories.Count; i++)
            displayed += allCategories[i].Items.Count;

        Services.Logger.DebugOnly($"AllCategories={allCategories.Count} DisplayedItemsTotal={displayed}");
    }

    private static CategoryInfo GetCategoryInfoCached(uint key, ItemInfo sample)
    {
        if (CategoryInfoCache.TryGetValue(key, out var cached))
            return cached;

        CategoryInfo info = GetCategoryInfoSlow(key, sample);
        CategoryInfoCache[key] = info;
        return info;
    }

    private static CategoryInfo GetCategoryInfoSlow(uint key, ItemInfo sample)
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
        string name = uiCat.Name.ToString();

        if (string.IsNullOrWhiteSpace(name))
            name = $"Category {key}";

        return new CategoryInfo
        {
            Name = name,
        };
    }
}