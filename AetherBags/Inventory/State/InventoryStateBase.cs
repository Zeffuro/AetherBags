using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Currency;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.State;

public abstract class InventoryStateBase
{
    protected readonly Dictionary<ulong, AggregatedItem> AggByKey = new(capacity: 512);
    protected readonly Dictionary<ulong, ItemInfo> ItemInfoByKey = new(capacity: 512);
    protected readonly Dictionary<uint, CategoryBucket> BucketsByKey = new(capacity: 256);
    protected readonly List<uint> SortedCategoryKeys = new(capacity: 256);
    protected readonly List<CategorizedInventory> AllCategories = new(capacity: 256);
    protected readonly List<CategorizedInventory> FilteredCategories = new(capacity: 256);
    protected readonly List<UserCategoryDefinition> UserCategoriesSortedScratch = new(capacity: 64);
    protected readonly List<ulong> RemoveKeysScratch = new(capacity:  256);
    protected readonly HashSet<ulong> ClaimedKeys = new(capacity: 512);

    public abstract InventorySourceType SourceType { get; }
    public abstract InventoryType[] Inventories { get; }

    public virtual unsafe void RefreshFromGame()
    {
        InventoryManager* inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            ClearAll();
            return;
        }

        var config = System.Config;
        InventoryStackMode stackMode = config.General.StackMode;

        AggByKey.Clear();
        ItemInfoByKey.Clear();
        SortedCategoryKeys.Clear();
        AllCategories.Clear();
        FilteredCategories.Clear();
        ClaimedKeys.Clear();

        InventoryScanner.ScanInventories(inventoryManager, stackMode, AggByKey, SourceType);
        CategoryBucketManager.ResetBuckets(BucketsByKey);
        InventoryScanner.BuildItemInfos(AggByKey, ItemInfoByKey);

        OnPostScan();

        ApplyCategories(config);

        InventoryScanner.PruneStaleItemInfos(AggByKey, ItemInfoByKey, RemoveKeysScratch);
        CategoryBucketManager.SortBucketsAndBuildKeyList(BucketsByKey, SortedCategoryKeys);
        CategoryBucketManager.BuildCategorizedList(BucketsByKey, SortedCategoryKeys, AllCategories);
    }

    protected virtual void OnPostScan()
    {
    }

    protected virtual void ApplyCategories(SystemConfiguration config)
    {
        bool categoriesEnabled = config.Categories.CategoriesEnabled;
        bool userCategoriesEnabled = config.Categories.UserCategoriesEnabled && categoriesEnabled;
        bool gameCategoriesEnabled = config.Categories.GameCategoriesEnabled && categoriesEnabled;
        bool allaganCategoriesEnabled = config.Categories.AllaganToolsCategoriesEnabled && categoriesEnabled;
        bool bisCategoriesEnabled = config.Categories.BisBuddyEnabled && categoriesEnabled;
        // TODO: Cache this when config changes
        var userCategories = config.Categories.UserCategories.Where(c => c.Enabled).ToList();

        if (userCategoriesEnabled && userCategories.Count > 0)
        {
            CategoryBucketManager.BucketByUserCategories(
                ItemInfoByKey, userCategories, BucketsByKey, ClaimedKeys, UserCategoriesSortedScratch);
        }

        if (allaganCategoriesEnabled)
        {
            if (config.Categories.AllaganToolsFilterMode == PluginFilterMode.Categorize)
            {
                CategoryBucketManager.BucketByAllaganFilters(ItemInfoByKey, BucketsByKey, ClaimedKeys, true);
                HighlightState.ClearFilter(HighlightSource.AllaganTools);
            }
            else
            {
                UpdateAllaganHighlight(HighlightState.SelectedAllaganToolsFilterKey);
            }
        }
        else
        {
            HighlightState.ClearFilter(HighlightSource.AllaganTools);
        }

        if (bisCategoriesEnabled)
        {
            if (config.Categories.BisBuddyMode == PluginFilterMode.Categorize)
            {
                CategoryBucketManager.BucketByBisBuddyItems(ItemInfoByKey, BucketsByKey, ClaimedKeys, true);
                HighlightState.ClearFilter(HighlightSource.BiSBuddy);
            }
            else
            {
                UpdateAllaganHighlight(HighlightState.SelectedBisBuddyFilterKey);
            }
        }
        else
        {
            HighlightState.ClearFilter(HighlightSource.BiSBuddy);
        }

        if (gameCategoriesEnabled)
        {
            CategoryBucketManager.BucketByGameCategories(
                ItemInfoByKey, BucketsByKey, ClaimedKeys, userCategoriesEnabled);
        }
        else
        {
            CategoryBucketManager.BucketUnclaimedToMisc(
                ItemInfoByKey, BucketsByKey, ClaimedKeys, userCategoriesEnabled);
        }
    }

    private void UpdateAllaganHighlight(string? filterKey)
    {
        if (string.IsNullOrEmpty(filterKey) || !System.IPC.AllaganTools.IsReady)
        {
            HighlightState.ClearFilter(HighlightSource.AllaganTools);
            return;
        }

        var filterItems = System.IPC.AllaganTools.GetFilterItems(filterKey);
        if (filterItems != null)
        {
            HighlightState.SetFilter(HighlightSource.AllaganTools, filterItems.Keys);
        }
        else
        {
            HighlightState.ClearFilter(HighlightSource.AllaganTools);
        }
    }

    private void UpdateBisBuddyHighlight(string? filterKey)
    {
        if (string.IsNullOrEmpty(filterKey) || !System.IPC.BisBuddy.IsReady)
        {
            HighlightState.ClearFilter(HighlightSource.BiSBuddy);
            return;
        }

        var filterItems = System.IPC.AllaganTools.GetFilterItems(filterKey);
        if (filterItems != null)
        {
            HighlightState.SetFilter(HighlightSource.BiSBuddy, filterItems.Keys);
        }
        else
        {
            HighlightState.ClearFilter(HighlightSource.BiSBuddy);
        }
    }

    public IReadOnlyList<CategorizedInventory> GetCategories(string filter = "", bool invert = false)
        => InventoryFilter.FilterCategories(AllCategories, BucketsByKey, FilteredCategories, filter, invert);

    public string GetEmptySlotsString() => InventoryScanner.GetEmptySlotsString(SourceType);

    public InventoryStats GetStats()
    {
        int totalItems = ItemInfoByKey.Count;
        int totalQuantity = 0;

        foreach (var kvp in ItemInfoByKey)
        {
            totalQuantity += kvp.Value.ItemCount;
        }

        int totalSlots = InventorySourceDefinitions.GetTotalSlots(SourceType);
        int emptySlots = InventoryScanner.GetEmptySlots(SourceType);

        var categories = GetCategories(string.Empty);
        int categoryCount = categories.Count;

        return new InventoryStats
        {
            TotalItems = totalItems,
            TotalQuantity = totalQuantity,
            EmptySlots = emptySlots,
            TotalSlots = totalSlots,
            CategoryCount = categoryCount,
        };
    }

    public static IReadOnlyList<CurrencyInfo> GetCurrencyInfoList(uint[] currencyIds)
        => CurrencyState.GetCurrencyInfoList(currencyIds);

    public static void InvalidateCurrencyCaches()
        => CurrencyState.InvalidateCaches();

    protected virtual void ClearAll()
    {
        AggByKey.Clear();
        ItemInfoByKey.Clear();

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
        ClaimedKeys.Clear();
    }
}