using System.Collections.Generic;
using AetherBags.Addons;
using AetherBags.Inventory.Context;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags.Inventory;

public static unsafe class InventoryOrchestrator
{
    private static readonly InventoryNotificationState NotificationState = new();
    private static bool _isRefreshing;

    public static void RefreshAll(bool updateMaps = true)
    {
        if (_isRefreshing)
            return;

        try
        {
            _isRefreshing = true;

            if (updateMaps)
            {
                InventoryContextState.RefreshMaps();
                InventoryContextState.RefreshBlockedSlots();
            }

            if (!HasAnyWindowOpen())
                return;

            var agent = AgentInventory.Instance();
            var contextId = agent != null ? agent->OpenTitleId : 0;
            var notification = NotificationState.GetNotificationInfo(contextId);

            Services.Framework.RunOnTick(() =>
            {
                if (notification != null && System.AddonInventoryWindow.IsOpen)
                    System.AddonInventoryWindow.SetNotification(notification);

                foreach (var window in GetAllWindows())
                {
                    window.ManualRefresh();
                }
            });
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public static void CloseAll()
    {
        foreach (var window in GetAllWindows())
        {
            window.Close();
        }
    }

    public static void RefreshHighlights()
    {
        if (!HasAnyWindowOpen())
            return;

        Services.Framework.RunOnTick(() =>
        {
            foreach (var window in GetAllWindows())
            {
                window.ItemRefresh();
            }
        });
    }

    private static bool HasAnyWindowOpen()
    {
        foreach (var window in GetAllWindows())
        {
            if (window.IsOpen)
                return true;
        }
        return false;
    }

    private static IEnumerable<IInventoryWindow> GetAllWindows()
    {
        if (System.AddonInventoryWindow != null)
            yield return System.AddonInventoryWindow;
        if (System.AddonSaddleBagWindow != null)
            yield return System.AddonSaddleBagWindow;
        if (System.AddonRetainerWindow != null)
            yield return System.AddonRetainerWindow;
    }
}