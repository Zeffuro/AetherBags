using System;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.NativeWrapper;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace AetherBags.AddonLifecycles;

public class InventoryLifecycles : IDisposable
{

    public InventoryLifecycles()
    {
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, ["Inventory", "InventoryLarge", "InventoryExpansion"], InventoryPreRefreshHandler);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, ["InventoryBuddy"], InventoryBuddyPreRefreshHandler);
        //Services.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, ["RetainerGrid0"], InventoryRetainerPreRefreshHandler);
        Services.Logger.Verbose("InventoryLifecycles initialized");
    }

    /*
    values[0] = OpenType
    values[1] = OpenTitleId
    values[2] = tab index
    values[3] = InventoryAddonId | (OpenerAddonId << 16)
    values[4] = focus
    values[5] = title
    values[6] = upper title
    values[7] = can use Saddlebags (Agent InventoryBuddy IsActivatable)
    */

    private unsafe void InventoryPreRefreshHandler(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonRefreshArgs refreshArgs)
            return;

        GeneralSettings config = System.Config.General;

        Services.Logger.Debug("PreRefresh event for Inventory detected");

        AtkValuePtr[] atkValues = refreshArgs.AtkValueEnumerable.ToArray();

        if (atkValues.Length < 7) return;

        AtkValue* value1 = (AtkValue*)atkValues[1].Address;
        AtkValue* value5 = (AtkValue*)atkValues[5].Address;
        AtkValue* value6 = (AtkValue*)atkValues[6].Address;

        int openTitleId = value1->Int;
        ReadOnlySeString title = value5->String.AsReadOnlySeString();
        ReadOnlySeString upperTitle = value6->String.AsReadOnlySeString();

        System.AddonInventoryWindow.SetNotification(new InventoryNotificationInfo(title, upperTitle));

        if (config.HideGameInventory) refreshArgs.AtkValueCount = 0;
        if (config.OpenWithGameInventory)
        {
            if (openTitleId == 0)
            {
                System.AddonInventoryWindow.Toggle();
            }
            else
            {
                System.AddonInventoryWindow.Open();
            }
        }
    }

    // TODO: Inventory/Retainers are not perma open, need some way to close it too.
    private void InventoryBuddyPreRefreshHandler(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonRefreshArgs refreshArgs)
            return;

        GeneralSettings config = System.Config.General;

        if (config.HideGameSaddleBags) refreshArgs.AtkValueCount = 0;
        if (config.OpenSaddleBagsWithGameInventory)
        {
            System.AddonSaddleBagWindow.Toggle();
        }
    }

    // TODO: Inventory/Retainers are not perma open, need some way to close it too.
    // TODO: Don't have the right retainer prerefresh handler yet.
    private void InventoryRetainerPreRefreshHandler(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonRefreshArgs refreshArgs)
            return;

        GeneralSettings config = System.Config.General;

        if (config.HideGameRetainer) refreshArgs.AtkValueCount = 0;
        if (config.OpenRetainerWithGameInventory)
        {
            System.AddonRetainerWindow.Toggle();
        }
    }

    public void Dispose()
    {
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, ["Inventory", "InventoryLarge", "InventoryExpansion"]);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, ["InventoryBuddy"]);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, ["RetainerGrid0"]);
    }
}