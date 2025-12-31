using AetherBags.Inventory.Context;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags.Inventory;

public static unsafe class InventoryOrchestrator
{
    private static readonly InventoryNotificationState NotificationState = new();

    public static void RefreshAll(bool updateMaps = true)
    {
        // 1. Update the mapping data (Context menus / Visual slots)
        if (updateMaps)
        {
            InventoryContextState.RefreshMaps();
            InventoryContextState.RefreshBlockedSlots();
        }

        // 2. Fetch the current context (Are we selling? Trading? Talking to a retainer?)
        var agent = AgentInventory.Instance();
        var contextId = agent != null ? agent->OpenTitleId : 0;
        var notification = NotificationState.GetNotificationInfo(contextId);

        // 3. Trigger UI refreshes
        Services.Framework.RunOnTick(() =>
        {
            if (System.AddonInventoryWindow.IsOpen)
            {
                System.AddonInventoryWindow.SetNotification(notification!);
                System.AddonInventoryWindow.ManualRefresh();
            }

            if (System.AddonSaddleBagWindow.IsOpen)
                System.AddonSaddleBagWindow.ManualRefresh();

            if (System.AddonRetainerWindow.IsOpen)
                System.AddonRetainerWindow.ManualRefresh();
        });
    }
}