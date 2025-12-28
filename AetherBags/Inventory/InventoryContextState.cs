using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AetherBags.Inventory;

public static unsafe class InventoryContextState
{
    private static readonly HashSet<(int page, int slot)> EligibleSlots = new();
    private static readonly HashSet<(InventoryType container, int slot)> BlockedSlots = new();
    private static readonly Dictionary<InventoryMappedLocation, InventoryMappedLocation> VisualLocationMap = new();
    private static uint _lastContextId;

    public static void RefreshMaps()
    {
        EligibleSlots. Clear();
        VisualLocationMap. Clear();

        var sorter = ItemOrderModule.Instance()->InventorySorter;
        if (sorter == null) return;

        var agentInventory = AgentInventory.Instance();
        bool hasContext = agentInventory != null && agentInventory->OpenTitleId != 0;
        _lastContextId = hasContext ? agentInventory->OpenTitleId : 0;

        var invArray = hasContext ? InventoryNumberArray.Instance() : null;

        int itemsPerPage = sorter->ItemsPerPage;

        for (int displayIdx = 0; displayIdx < 140; displayIdx++)
        {
            var entry = sorter->Items[displayIdx]. Value;
            if (entry == null) continue;

            int realPage = entry->Page;
            int realSlot = entry->Slot;

            int visualPage = displayIdx / itemsPerPage;
            int visualSlot = displayIdx % itemsPerPage;
            int visualContainerId = 48 + visualPage;

            VisualLocationMap[new InventoryMappedLocation(realPage, realSlot)] = new InventoryMappedLocation(visualContainerId, visualSlot);

            if (hasContext && invArray != null)
            {
                var itemData = invArray->Items[displayIdx];
                if (itemData. IconId == 0) continue;

                bool eligible = itemData.ItemFlags.MirageFlag == 0;
                if (eligible)
                {
                    EligibleSlots.Add((realPage, realSlot));
                }
            }
        }
    }

    public static void RefreshBlockedSlots()
    {
        BlockedSlots.Clear();

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return;

        var blockedContainer = inventoryManager->GetInventoryContainer(InventoryType.BlockedItems);
        if (blockedContainer == null) return;

        for (int i = 0; i < blockedContainer->Size; i++)
        {
            ref var item = ref blockedContainer->Items[i];
            if (item.ItemId == 0) continue;

            BlockedSlots.Add((item.Container, item.Slot));
        }
    }

    public static bool IsEligible(int page, int slot)
        => EligibleSlots.Contains((page, slot));

    public static bool IsSlotBlocked(InventoryType container, int slot)
        => BlockedSlots.Contains((container, slot));

    public static bool HasActiveContext
        => _lastContextId != 0;

    public static InventoryMappedLocation GetVisualLocation(int page, int slot)
        => VisualLocationMap.TryGetValue(new InventoryMappedLocation(page, slot), out var result) ? result : new InventoryMappedLocation(48 + page, slot);
}