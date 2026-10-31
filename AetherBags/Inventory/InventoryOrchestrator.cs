using Dalamud.Plugin.Services;
using System.Collections.Generic;
using AetherBags.Addons;
using AetherBags.Inventory.Context;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags.Inventory;

public static unsafe class InventoryOrchestrator
{
    private static readonly InventoryNotificationState NotificationState = new();
    private static bool _isRefreshing;
    private static bool _isEnabled = true;

    public static void Enable() => _isEnabled = true;
    public static void Disable() => _isEnabled = false;

    public static void RearrangeAll() => RefreshAll(updateMaps: true, rearrange: true);

    public static void RefreshAll(bool updateMaps = true) => RefreshAll(updateMaps, rearrange: false);

    private static void RefreshAll(bool updateMaps, bool rearrange)
    {
        if (!_isEnabled || _isRefreshing)
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

            IFramework.Get().RunOnTick(() =>
            {
                if (!_isEnabled) return;
                if (notification != null && System.AddonInventoryWindow.IsOpen)
                    System.AddonInventoryWindow.SetNotification(notification);

                foreach (var window in GetAllWindows())
                {
                    if (rearrange && window is InventoryAddonBase addon) addon.ResetLayoutForConfig();
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
        if (!_isEnabled || !HasAnyWindowOpen())
            return;

        IFramework.Get().RunOnTick(() =>
        {
            if (!_isEnabled) return;
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
