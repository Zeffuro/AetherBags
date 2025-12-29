using AetherBags.Configuration;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Currency;
using CurrencyManager = FFXIVClientStructs.FFXIV.Client.Game.CurrencyManager;

namespace AetherBags.Inventory;

public static unsafe class InventoryState
{
    public static IReadOnlyList<InventoryType> StandardInventories => InventoryScanner.StandardInventories;

    private static readonly Dictionary<ulong, AggregatedItem> AggByKey = new(capacity: 512);
    private static readonly Dictionary<ulong, ItemInfo> ItemInfoByKey = new(capacity: 512);
    private static readonly Dictionary<uint, CategoryBucket> BucketsByKey = new(capacity: 256);
    private static readonly List<uint> SortedCategoryKeys = new(capacity: 256);
    private static readonly List<CategorizedInventory> AllCategories = new(capacity: 256);
    private static readonly List<CategorizedInventory> FilteredCategories = new(capacity: 256);
    private static readonly List<UserCategoryDefinition> UserCategoriesSortedScratch = new(capacity: 64);
    private static readonly List<ulong> RemoveKeysScratch = new(capacity:  256);
    private static readonly HashSet<ulong> ClaimedKeys = new(capacity: 512);

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
        InventoryStackMode stackMode = config.General.StackMode;
        bool userCategoriesEnabled = config.Categories.UserCategoriesEnabled;
        bool gameCategoriesEnabled = config.Categories.GameCategoriesEnabled;
        List<UserCategoryDefinition> userCategories = config.Categories.UserCategories.Where(category => category.Enabled).ToList();

        Services.Logger.DebugOnly($"RefreshFromGame StackMode={stackMode}");

        AggByKey.Clear();
        ItemInfoByKey.Clear();
        SortedCategoryKeys.Clear();
        AllCategories.Clear();
        FilteredCategories.Clear();
        ClaimedKeys.Clear();

        InventoryScanner.ScanBags(inventoryManager, stackMode, AggByKey);
        CategoryBucketManager.ResetBuckets(BucketsByKey);
        InventoryScanner.BuildItemInfos(AggByKey, ItemInfoByKey);
        InventoryContextState.RefreshMaps();
        InventoryContextState.RefreshBlockedSlots();

        if (userCategoriesEnabled && userCategories.Count > 0)
        {
            CategoryBucketManager.BucketByUserCategories(
                ItemInfoByKey,
                userCategories,
                BucketsByKey,
                ClaimedKeys,
                UserCategoriesSortedScratch);
        }

        if (gameCategoriesEnabled)
        {
            CategoryBucketManager.BucketByGameCategories(
                ItemInfoByKey,
                BucketsByKey,
                ClaimedKeys,
                userCategoriesEnabled);
        }
        else
        {
            CategoryBucketManager.BucketUnclaimedToMisc(
                ItemInfoByKey,
                BucketsByKey,
                ClaimedKeys,
                userCategoriesEnabled);
        }

        InventoryScanner.PruneStaleItemInfos(AggByKey, ItemInfoByKey, RemoveKeysScratch);
        CategoryBucketManager.SortBucketsAndBuildKeyList(BucketsByKey, SortedCategoryKeys);
        CategoryBucketManager.BuildCategorizedList(BucketsByKey, SortedCategoryKeys, AllCategories);
    }

    public static IReadOnlyList<CategorizedInventory> GetInventoryItemCategories(string filterString = "", bool invert = false)
    {
        return InventoryFilter.FilterCategories(
            AllCategories,
            BucketsByKey,
            FilteredCategories,
            filterString,
            invert);
    }

    public static InventoryStats GetInventoryStats()
    {
        int totalItems = ItemInfoByKey.Count;
        int totalQuantity = 0;

        foreach (var kvp in ItemInfoByKey)
        {
            totalQuantity += kvp.Value.ItemCount;
        }

        uint emptySlots = InventoryManager.Instance()->GetEmptySlotsInBag();
        const int totalSlots = 140;

        var categories = GetInventoryItemCategories(string.Empty);
        int categoryCount = categories.Count;

        return new InventoryStats
        {
            TotalItems = totalItems,
            TotalQuantity = totalQuantity,
            EmptySlots = (int)emptySlots,
            TotalSlots = totalSlots,
            CategoryCount = categoryCount,
        };
    }

    public static string GetEmptyItemSlotsString()
        => InventoryScanner.GetEmptyItemSlotsString();

    public static IReadOnlyList<CurrencyInfo> GetCurrencyInfoList(uint[] currencyIds)
        => CurrencyState.GetCurrencyInfoList(currencyIds);

    public static void InvalidateCurrencyCaches()
        => CurrencyState.InvalidateCaches();

    public static InventoryContainer* GetInventoryContainer(InventoryType inventoryType)
        => InventoryScanner.GetInventoryContainer(inventoryType);

    private static void ClearAll()
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