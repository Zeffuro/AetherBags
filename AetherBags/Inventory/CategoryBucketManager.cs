using AetherBags.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherBags.Inventory;

public static class CategoryBucketManager
{
    private const uint UserCategoryKeyFlag = 0x8000_0000;

    private static readonly Dictionary<uint, CategoryInfo> CategoryInfoCache = new(capacity: 256);

    public static uint MakeUserCategoryKey(int order)
        => UserCategoryKeyFlag | (uint)(order & 0x7FFF_FFFF);

    public static bool IsUserCategoryKey(uint key)
        => (key & UserCategoryKeyFlag) != 0;

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
        sortedScratch.Sort((left, right) =>
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0) return priority;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0) return order;

            return string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < sortedScratch.Count; i++)
        {
            UserCategoryDefinition category = sortedScratch[i];

            if (!category.Enabled)
                continue;

            if (UserCategoryMatcher.IsCatchAll(category))
                continue;

            uint bucketKey = MakeUserCategoryKey(category.Order);

            if (!bucketsByKey.TryGetValue(bucketKey, out CategoryBucket? bucket))
            {
                bucket = new CategoryBucket
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
                bucketsByKey.Add(bucketKey, bucket);
            }
            else
            {
                bucket.Used = true;
                bucket.Category.Name = category.Name;
                bucket.Category.Description = category.Description;
                bucket.Category.Color = category.Color;
                bucket.Category.IsPinned = category.Pinned;
            }

            foreach (var itemKvp in itemInfoByKey)
            {
                ulong itemKey = itemKvp.Key;
                ItemInfo item = itemKvp.Value;

                if (claimedKeys.Contains(itemKey))
                    continue;

                if (UserCategoryMatcher.Matches(item, category))
                {
                    bucket.Items.Add(item);
                    claimedKeys.Add(itemKey);
                }
            }

            if (bucket.Items.Count == 0)
                bucket.Used = false;
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

            if (!bucketsByKey.TryGetValue(categoryKey, out CategoryBucket? bucket))
            {
                bucket = new CategoryBucket
                {
                    Key = categoryKey,
                    Category = GetCategoryInfoCached(categoryKey, info),
                    Items = new List<ItemInfo>(capacity: 16),
                    FilteredItems = new List<ItemInfo>(capacity: 16),
                    Used = true,
                };
                bucketsByKey.Add(categoryKey, bucket);
            }
            else
            {
                bucket.Used = true;
            }

            bucket.Items.Add(info);
        }
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
                var sample = itemInfoByKey.Values.First();
                miscInfo = GetCategoryInfoCached(0u, sample);
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

            bucket.Items.Sort(ItemCountDescComparer.Instance);
            sortedCategoryKeys.Add(bucket.Key);
        }

        sortedCategoryKeys.Sort((left, right) =>
        {
            bool leftCategory = IsUserCategoryKey(left);
            bool rightCategory = IsUserCategoryKey(right);
            if (leftCategory != rightCategory) return leftCategory ? -1 : 1;
            return left.CompareTo(right);
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

public sealed class CategoryBucket
{
    public uint Key;
    public CategoryInfo Category = null!;
    public List<ItemInfo> Items = null!;
    public List<ItemInfo> FilteredItems = null!;
    public bool Used;
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