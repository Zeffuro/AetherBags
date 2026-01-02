using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Currency;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.State;

public static unsafe class InventoryState
{
    private static IReadOnlyList<InventoryType> StandardInventories => InventoryScanner.StandardInventories;

    private static readonly Dictionary<ulong, AggregatedItem> AggByKey = new(capacity: 512);
    private static readonly Dictionary<ulong, ItemInfo> ItemInfoByKey = new(capacity: 512);
    private static readonly Dictionary<uint, CategoryBucket> BucketsByKey = new(capacity: 256);
    private static readonly List<uint> SortedCategoryKeys = new(capacity: 256);
    private static readonly List<CategorizedInventory> AllCategories = new(capacity: 256);
    private static readonly List<CategorizedInventory> FilteredCategories = new(capacity: 256);
    private static readonly List<UserCategoryDefinition> UserCategoriesSortedScratch = new(capacity: 64);
    private static readonly List<ulong> RemoveKeysScratch = new(capacity:  256);
    private static readonly HashSet<ulong> ClaimedKeys = new(capacity: 512);
    private static readonly List<LootedItemInfo>? LootedItems = new(capacity: 512);

    public static bool TrackLootedItems = false;

    public static bool Contains(this IReadOnlyCollection<InventoryType> inventoryTypes, GameInventoryType type)
        => inventoryTypes.Contains((InventoryType)type);

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

        uint emptySlots = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance()->GetEmptySlotsInBag();
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

    public static IReadOnlyList<CurrencyInfo> GetCurrencyInfoList(uint[] currencyIds)
        => CurrencyState.GetCurrencyInfoList(currencyIds);

    public static void InvalidateCurrencyCaches()
        => CurrencyState.InvalidateCaches();

    public static InventoryContainer* GetInventoryContainer(InventoryType inventoryType)
        => InventoryScanner.GetInventoryContainer(inventoryType);

    internal static void OnRawItemAdded(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!TrackLootedItems) return;

        bool updateRequested = false;

        foreach (var eventData in events)
        {
            if (!StandardInventories.Contains(eventData.Item.ContainerType)) continue;

            if (!Services.ClientState.IsLoggedIn) return;
            if (eventData is not (InventoryItemAddedArgs or InventoryItemChangedArgs)) return;
            if (eventData is InventoryItemChangedArgs changedArgs && changedArgs.OldItemState.Quantity >= changedArgs.Item.Quantity) return;

            var inventoryItem = (InventoryItem*)eventData.Item.Address;
            var changeAmount = eventData is InventoryItemChangedArgs changed ? changed.Item.Quantity - changed.OldItemState.Quantity : eventData.Item.Quantity;

            LootedItems?.Add(new LootedItemInfo(
                LootedItems.Count,
                *inventoryItem,
                changeAmount)
            );

            updateRequested = true;
        }

        if (updateRequested)
        {
            // System.AddonInventoryWindow?.UpdateLootedCategory(LootedItems ?? []);
        }
    }

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
        LootedItems?.Clear();
    }
}