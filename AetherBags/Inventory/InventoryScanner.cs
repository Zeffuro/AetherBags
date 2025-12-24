using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;

namespace AetherBags.Inventory;

public static unsafe class InventoryScanner
{
    private static readonly InventoryType[] BagInventories =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

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

    public static void ScanBags(
        InventoryManager* inventoryManager,
        InventoryStackMode stackMode,
        Dictionary<ulong, AggregatedItem> aggByKey)
    {
        aggByKey.Clear();

        int scannedSlots = 0;
        int nonEmptySlots = 0;
        int collisions = 0;

        for (int inventoryIndex = 0; inventoryIndex < BagInventories.Length; inventoryIndex++)
        {
            var inventoryType = BagInventories[inventoryIndex];
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
                    ?  MakeAggregatedItemKey(id, isHq)
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

            if (!itemInfoByKey.TryGetValue(key, out ItemInfo?  info))
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

    public static string GetEmptyItemSlotsString()
    {
        uint empty = InventoryManager.Instance()->GetEmptySlotsInBag();
        uint used = 140 - empty;
        return $"{used}/140";
    }
}

public struct AggregatedItem
{
    public InventoryItem First;
    public int Total;
}