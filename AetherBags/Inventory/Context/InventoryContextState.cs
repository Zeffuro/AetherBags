using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AetherBags.Inventory.Context;

public static unsafe class InventoryContextState
{
    private static readonly HashSet<(int page, int slot)> EligibleSlots = new();
    private static readonly HashSet<(InventoryType container, int slot)> BlockedSlots = new();

    private static readonly Dictionary<InventoryMappedLocation, InventoryMappedLocation> VisualLocationMap = new();
    private static readonly Dictionary<int, Dictionary<InventoryMappedLocation, InventoryMappedLocation>> GroupedLocationMaps = new();

    private static uint _lastContextId;

    public static uint ActiveContextId => _lastContextId;

    public static bool HasActiveContext => _lastContextId != 0;

    public static void RefreshMaps()
    {
        EligibleSlots.Clear();
        VisualLocationMap.Clear();
        GroupedLocationMaps.Clear();

        var itemOrderModule = ItemOrderModule.Instance();
        if (itemOrderModule == null) return;

        var agentInventory = AgentInventory.Instance();
        bool hasContext = agentInventory != null && agentInventory->OpenTitleId != 0;
        _lastContextId = hasContext ? agentInventory->OpenTitleId : 0;

        var invArray = hasContext ? InventoryNumberArray.Instance() : null;

        // Helper local to process any sorter
        void ProcessSorter(ItemOrderModuleSorter* sorter)
        {
            if (sorter == null) return;

            // Determine actual page size.
            // We prefer the physical container size over the sorter's 'ItemsPerPage'
            var baseInventoryType = sorter->InventoryType;
            var inventoryManager = InventoryManager.Instance();
            var container = inventoryManager != null ? inventoryManager->GetInventoryContainer(baseInventoryType) : null;

            // Fallback to sorter value if container isn't loaded, but default to 35 for main/retainer
            int itemsPerPage = baseInventoryType.UIPageSize;
            if (itemsPerPage <= 0) itemsPerPage = 35;

            var baseAgentId = (int)baseInventoryType.AgentItemContainerId;
            if (baseAgentId == 0) return;

            long count = sorter->Items.LongCount;
            for (int displayIdx = 0; displayIdx < count; displayIdx++)
            {
                var entry = sorter->Items[displayIdx].Value;
                if (entry == null) continue;

                var realContainer = (InventoryType)((int)baseInventoryType + entry->Page);
                int realSlot = entry->Slot;

                int visualPage = displayIdx / itemsPerPage;
                int visualSlot = displayIdx % itemsPerPage;
                int visualContainerId = baseAgentId + visualPage;

                var realKey = new InventoryMappedLocation((int)realContainer, realSlot);
                var visualValue = new InventoryMappedLocation(visualContainerId, visualSlot);

                VisualLocationMap[realKey] = visualValue;

                if (hasContext && invArray != null && baseInventoryType.IsMainInventory)
                {
                    var itemData = invArray->Items[displayIdx];
                    if (itemData.IconId != 0)
                    {
                        bool eligible = itemData.ItemFlags.MirageFlag == 0;
                        if (eligible)
                            EligibleSlots.Add(((int)realContainer - (int)InventoryType.Inventory1, realSlot));
                    }
                }
            }
        }

        ProcessSorter(itemOrderModule->InventorySorter);

        ProcessSorter(itemOrderModule->ArmouryMainHandSorter);
        ProcessSorter(itemOrderModule->ArmouryOffHandSorter);
        ProcessSorter(itemOrderModule->ArmouryHeadSorter);
        ProcessSorter(itemOrderModule->ArmouryBodySorter);
        ProcessSorter(itemOrderModule->ArmouryHandsSorter);
        ProcessSorter(itemOrderModule->ArmouryLegsSorter);
        ProcessSorter(itemOrderModule->ArmouryFeetSorter);
        ProcessSorter(itemOrderModule->ArmouryEarsSorter);
        ProcessSorter(itemOrderModule->ArmouryNeckSorter);
        ProcessSorter(itemOrderModule->ArmouryWristsSorter);
        ProcessSorter(itemOrderModule->ArmouryRingsSorter);
        ProcessSorter(itemOrderModule->ArmourySoulCrystalSorter);

        ProcessSorter(itemOrderModule->SaddleBagSorter);
        ProcessSorter(itemOrderModule->PremiumSaddleBagSorter);

        try
        {
            var activeRetainerSorter = itemOrderModule->GetActiveRetainerSorter();
            ProcessSorter(activeRetainerSorter);
        }
        catch
        {
            // GetActiveRetainerSorter is a member function — guard just in case
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

    public static InventoryMappedLocation GetVisualLocation(InventoryType realContainer, int slot)
    {
        var key = new InventoryMappedLocation((int)realContainer, slot);
        if (VisualLocationMap.TryGetValue(key, out var result))
            return result;

        // default fallback: use the agent container id for the real container (works for Inventory1..4, RetainerPageN, etc.)
        var defaultAgentId = (int)realContainer.AgentItemContainerId;
        if (defaultAgentId == 0)
        {
            // final fallback: Inventory1 base at 48
            defaultAgentId = 48;
        }

        return new InventoryMappedLocation(defaultAgentId, slot);
    }
}