using System;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Inventory;
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
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, ["Inventory", "InventoryLarge", "InventoryExpansion"], PreRefreshHandler);
        Services.Logger.Verbose("InventoryLifecycles initialized");
    }

    private unsafe void PreRefreshHandler(AddonEvent type, AddonArgs args)
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

    public void Dispose()
    {
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, ["Inventory", "InventoryLarge", "InventoryExpansion"]);
    }
}