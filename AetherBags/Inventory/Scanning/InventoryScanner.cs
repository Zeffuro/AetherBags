using Dalamud.Plugin.Services;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Inventory.Items;
using AetherBags.IPC.ExternalCategorySystem;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.Scanning;

public static unsafe class InventoryScanner
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
        InventoryType.KeyItems,
        InventoryType.ArmorySoulCrystal,
    ];

    private const ulong AggregatedKeyTag = 1UL << 63;

    public static ulong MakeAggregatedItemKey(uint itemId, bool isHighQuality)
        => AggregatedKeyTag | ((ulong)itemId << 1) | (isHighQuality ? 1UL : 0UL);

    public static ulong MakeNaturalSlotKey(InventoryType container, int slot)
        => ((ulong)(uint)container << 32) | (uint)slot;

    public static void ScanInventories(
        InventoryManager* inventoryManager,
        InventoryStackMode stackMode,
        bool aggregateUnstackable,
        Dictionary<ulong, AggregatedItem> aggByKey,
        InventorySourceType source)
    {
        aggByKey.Clear();
        if (inventoryManager == null) return;

        InventoryType[] inventories = InventorySourceDefinitions.GetInventories(source);

        var extras = ExternalCategoryManager.GetActiveInventoryTypes(source);
        if (extras.Count > 0)
        {
            var combined = new HashSet<InventoryType>(inventories);
            foreach (var t in extras) combined.Add(t);
            inventories = combined.ToArray();
        }

        int scannedSlots = 0;
        int nonEmptySlots = 0;
        int collisions = 0;

        for (int inventoryIndex = 0; inventoryIndex < inventories.Length; inventoryIndex++)
        {
            var inventoryType = inventories[inventoryIndex];
            var container = inventoryManager->GetInventoryContainer(inventoryType);
            if (!IsContainerReady(container))
            {
                IPluginLog.Get().DebugOnly($"Container unavailable: {inventoryType}");
                continue;
            }

            int size = container->Size;
            IPluginLog.Get().DebugOnly($"Scanning {inventoryType} Size={size}");

            for (int slot = 0; slot < size; slot++)
            {
                scannedSlots++;

                ref var item = ref container->Items[slot];
                uint id = item.ItemId;
                if (id == 0)
                    continue;

                nonEmptySlots++;

                int quantity = item.Quantity;
                bool isHq = (item.Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                bool isCollectable = (item.Flags & InventoryItem.ItemFlags.Collectable) != 0;

                ulong key = !isCollectable && stackMode == InventoryStackMode.AggregateByItemId && (aggregateUnstackable || ItemInfo.IsAggregatable(id))
                    ? MakeAggregatedItemKey(id, isHq)
                    : MakeNaturalSlotKey(inventoryType, slot);

                IPluginLog.Get().DebugOnly($"Slot {inventoryType}[{slot}] ItemId={id} Qty={quantity} Key=0x{key: X16}");

                if (aggByKey.TryGetValue(key, out AggregatedItem agg))
                {
                    if (stackMode == InventoryStackMode.NaturalStacks)
                    {
                        collisions++;
                        IPluginLog.Get().DebugOnly($"COLLISION Key=0x{key:X16}:  existing ItemId={agg.First.ItemId} new ItemId={id}");
                    }

                    agg.Total += quantity;
                    aggByKey[key] = agg;
                }
                else
                {
                    aggByKey.Add(key, new AggregatedItem { First = item, Total = quantity });
                }
            }
        }

        IPluginLog.Get().DebugOnly($"ScannedSlots={scannedSlots} NonEmptySlots={nonEmptySlots} AggByKey.Count={aggByKey.Count} Collisions={collisions}");
    }

    public static void BuildItemInfos(
        Dictionary<ulong, AggregatedItem> aggByKey,
        Dictionary<ulong, ItemInfo> itemInfoByKey)
    {
        foreach (var kvp in aggByKey)
        {
            ulong key = kvp.Key;
            AggregatedItem agg = kvp.Value;

            if (!itemInfoByKey.TryGetValue(key, out ItemInfo? info))
            {
                info = new ItemInfo
                {
                    Key = key,
                    Item = agg.First,
                    ItemCount = agg.Total,
                };
                itemInfoByKey.Add(key, info);
            }
            else
            {
                info.Item = agg.First;
                info.ItemCount = agg.Total;
            }
        }

        IPluginLog.Get().DebugOnly($"ItemInfoByKey.Count={itemInfoByKey.Count}");
    }

    public static void PruneStaleItemInfos(
        Dictionary<ulong, AggregatedItem> aggByKey,
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        List<ulong> removeKeysScratch)
    {
        if (itemInfoByKey.Count == aggByKey.Count)
            return;

        removeKeysScratch.Clear();

        foreach (var kvp in itemInfoByKey)
        {
            ulong key = kvp.Key;
            if (!aggByKey.ContainsKey(key))
                removeKeysScratch.Add(key);
        }

        for (int i = 0; i < removeKeysScratch.Count; i++)
            itemInfoByKey.Remove(removeKeysScratch[i]);
    }

    public static InventoryContainer* GetInventoryContainer(InventoryType inventoryType)
    {
        var manager = InventoryManager.Instance();
        return manager == null ? null : manager->GetInventoryContainer(inventoryType);
    }

    public static InventoryLocation GetFirstEmptySlot(InventorySourceType source)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return InventoryLocation.Invalid;
        var containers = InventorySourceDefinitions.GetContainersForSource(source);

        foreach (var type in containers)
        {
            var container = manager->GetInventoryContainer(type);
            if (!IsContainerReady(container)) continue;

            for (int i = 0; i < container->Size; i++)
            {
                if (container->Items[i].ItemId == 0)
                    return new InventoryLocation(type, (ushort)i);
            }
        }

        return InventoryLocation.Invalid;
    }

    public static string GetEmptySlotsString(InventorySourceType source)
    {
        return InventorySlotCounts.TryGet(source, out var total, out var empty)
            ? $"{total - empty}/{total}"
            : $"—/{InventorySourceDefinitions.GetTotalSlots(source)}";
    }

    public static bool TryGetSlotCounts(InventorySourceType source, out int total, out int empty)
    {
        total = 0;
        empty = 0;
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return false;

        foreach (var inv in InventorySourceDefinitions.GetContainersForSource(source))
        {
            var container = inventoryManager->GetInventoryContainer(inv);
            if (!IsContainerReady(container))
            {
                total = 0;
                empty = 0;
                return false;
            }

            total += container->Size;
            for (int i = 0; i < container->Size; i++)
            {
                if (container->Items[i].ItemId == 0)
                    empty++;
            }
        }
        return true;
    }

    private static bool IsContainerReady(InventoryContainer* container)
        => container != null && container->IsLoaded && container->Items != null && container->Size > 0;
}
