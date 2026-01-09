using System.Collections.Generic;
using AetherBags.Configuration;
using AetherBags.Inventory.Items;
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
        Dictionary<ulong, AggregatedItem> aggByKey,
        InventorySourceType source)
    {
        aggByKey.Clear();

        var inventories = InventorySourceDefinitions.GetInventories(source);

        int scannedSlots = 0;
        int nonEmptySlots = 0;
        int collisions = 0;

        for (int inventoryIndex = 0; inventoryIndex < inventories.Length; inventoryIndex++)
        {
            var inventoryType = inventories[inventoryIndex];
            var container = inventoryManager->GetInventoryContainer(inventoryType);
            if (container == null)
            {
                Services.Logger.DebugOnly($"Container null:  {inventoryType}");
                continue;
            }

            int size = container->Size;
            Services.Logger.DebugOnly($"Scanning {inventoryType} Size={size}");

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

                ulong key = stackMode == InventoryStackMode.AggregateByItemId
                    ? MakeAggregatedItemKey(id, isHq)
                    : MakeNaturalSlotKey(inventoryType, slot);

                Services.Logger.DebugOnly($"Slot {inventoryType}[{slot}] ItemId={id} Qty={quantity} Key=0x{key: X16}");

                if (aggByKey.TryGetValue(key, out AggregatedItem agg))
                {
                    if (stackMode == InventoryStackMode.NaturalStacks)
                    {
                        collisions++;
                        Services.Logger.DebugOnly($"COLLISION Key=0x{key:X16}:  existing ItemId={agg.First.ItemId} new ItemId={id}");
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

        Services.Logger.DebugOnly($"ScannedSlots={scannedSlots} NonEmptySlots={nonEmptySlots} AggByKey.Count={aggByKey.Count} Collisions={collisions}");
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

        Services.Logger.DebugOnly($"ItemInfoByKey.Count={itemInfoByKey.Count}");
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
        => InventoryManager.Instance()->GetInventoryContainer(inventoryType);

    public static InventoryLocation GetFirstEmptySlot(InventorySourceType source)
    {
        var manager = InventoryManager.Instance();
        var containers = InventorySourceDefinitions.GetContainersForSource(source);

        foreach (var type in containers)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || container->Size == 0) continue;

            for (int i = 0; i < container->Size; i++)
            {
                if (container->Items[i].ItemId == 0)
                    return new InventoryLocation(type, (ushort)i);
            }
        }

        return InventoryLocation.Invalid;
    }

    public static int GetEmptySlots(InventorySourceType source) => (int)(source switch
    {
        InventorySourceType.MainBags => InventoryManager.Instance()->GetEmptySlotsInBag(),
        InventorySourceType.SaddleBag => GetEmptySlotsInContainer(InventorySourceDefinitions.SaddleBag),
        InventorySourceType.PremiumSaddleBag => GetEmptySlotsInContainer(InventorySourceDefinitions.PremiumSaddleBag),
        InventorySourceType.AllSaddleBags => GetEmptySlotsInContainer(InventorySourceDefinitions.AllSaddleBags),
        InventorySourceType.Retainer => GetEmptySlotsInContainer(InventorySourceDefinitions.Retainer),
        _ => 0u,
    });

    public static string GetEmptySlotsString(InventorySourceType source)
    {
        int total = InventorySourceDefinitions.GetTotalSlots(source);
        int empty = GetEmptySlots(source);
        int used = total - empty;
        return $"{used}/{total}";
    }

    private static uint GetEmptySlotsInContainer(InventoryType[] inventories)
    {
        uint empty = 0;
        var inventoryManager = InventoryManager.Instance();
        foreach (var inv in inventories)
        {
            var container = inventoryManager->GetInventoryContainer(inv);
            var containerSize = container->Size;

            if (container == null) continue;
            for (int i = 0; i < containerSize; i++)
            {
                if (container->Items[i]. ItemId == 0)
                    empty++;
            }
        }
        return empty;
    }
}