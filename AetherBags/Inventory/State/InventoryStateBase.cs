using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Inventory.Categories;
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
        FFXIVClientStructs.FFXIV.Client.Game.InventoryManager* inventoryManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
        if (inventoryManager == null)
        {
            ClearAll();
            return;
        }

        var config = AetherBags.System.Config;
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
        bool userCategoriesEnabled = config.Categories.UserCategoriesEnabled;
        bool gameCategoriesEnabled = config.Categories.GameCategoriesEnabled;
        var userCategories = config.Categories.UserCategories.Where(c => c.Enabled).ToList();

        if (userCategoriesEnabled && userCategories.Count > 0)
        {
            CategoryBucketManager.BucketByUserCategories(
                ItemInfoByKey, userCategories, BucketsByKey, ClaimedKeys, UserCategoriesSortedScratch);
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

    public IReadOnlyList<CategorizedInventory> GetCategories(string filter = "", bool invert = false)
        => InventoryFilter.FilterCategories(AllCategories, BucketsByKey, FilteredCategories, filter, invert);

    public string GetEmptySlotsString() => InventoryScanner.GetEmptySlotsString(SourceType);

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