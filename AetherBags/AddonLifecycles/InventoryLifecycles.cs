using System;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.NativeWrapper;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace AetherBags.AddonLifecycles;

public class InventoryLifecycles : IDisposable
{

    public InventoryLifecycles()
    {
        var bags = new[] { "Inventory", "InventoryLarge", "InventoryExpansion" };
        var saddle = new[] { "InventoryBuddy" };
        var retainer = new[] { "InventoryRetainer", "InventoryRetainerLarge" };

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, saddle, OnPostSetup);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, retainer, OnPostSetup);

        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, saddle, OnPreFinalize);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, retainer, OnPreFinalize);

        // PreRefresh Handlers
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, ["Inventory", "InventoryLarge", "InventoryExpansion"], InventoryPreRefreshHandler);

        // PostRequestedUpdate
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Inventory", OnInventoryUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "InventoryBuddy", OnSaddleBagUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, ["InventoryRetainer", "InventoryRetainerLarge"], OnRetainerInventoryUpdate);

        // PreShow
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreOpen, "InventoryBuddy", OnSaddleBagOpen);

        Services.Logger.Verbose("InventoryLifecycles initialized");
    }

    private void OnPreFinalize(AddonEvent type, AddonArgs args)
    {
        CloseInventories(args.AddonName);
    }

    private void OnPostSetup(AddonEvent type, AddonArgs args)
    {
        OpenInventories(args.AddonName);
    }

    private unsafe void OpenInventories(string name)
    {
        GeneralSettings config = System.Config.General;
        if (name.Contains("Retainer") && config.OpenRetainerWithGameInventory)
        {
            System.AddonRetainerWindow.Open();
            if (config.HideGameRetainer)
            {
                var addon = RaptureAtkUnitManager.Instance()->GetAddonByName("InventoryRetainer");
                if (addon != null)
                {
                    addon->IsVisible = false;
                }

                addon = RaptureAtkUnitManager.Instance()->GetAddonByName("InventoryRetainerLarge");
                if (addon != null)
                {
                    addon->IsVisible = false;
                }
            }
        }

        if (name.Contains("InventoryBuddy") && config.OpenSaddleBagsWithGameInventory)
        {
            System.AddonSaddleBagWindow.Open();
            if (config.HideGameSaddleBags)
            {
                var addon = RaptureAtkUnitManager.Instance()->GetAddonByName("InventoryBuddy");
                if (addon != null)
                {
                    addon->IsVisible = false;
                }
            }
        }
    }

    private void CloseInventories(string name)
    {
        if (name.Contains("Retainer")) System.AddonRetainerWindow.Close();
        if (name.Contains("InventoryBuddy")) System.AddonSaddleBagWindow.Close();
    }

    private static bool IsInUnsafeState()
    {
        if (!Services.ClientState.IsLoggedIn)
            return true;

        return Services.Condition.Any(ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51);
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

        if (IsInUnsafeState())
            return;

        GeneralSettings config = System.Config.General;

        Services.Logger.DebugOnly("PreRefresh event for Inventory detected");

        AtkValuePtr[] atkValues = refreshArgs.AtkValueEnumerable.ToArray();

        if (atkValues.Length < 7) return;

        AtkValue* value1 = (AtkValue*)atkValues[1].Address;
        AtkValue* value5 = (AtkValue*)atkValues[5].Address;
        AtkValue* value6 = (AtkValue*)atkValues[6].Address;

        if (value5->Type != ValueType.ManagedString || value6->Type != ValueType.ManagedString)
            return;

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

        if (IsInUnsafeState())
            return;

        GeneralSettings config = System.Config.General;

        if (config.HideGameSaddleBags) refreshArgs.AtkValueCount = 0;
        if (config.OpenSaddleBagsWithGameInventory)
        {
            System.AddonSaddleBagWindow.Toggle();
        }
    }


    private void OnInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        System.AddonInventoryWindow?.RefreshFromLifecycle();
    }

    private void OnSaddleBagUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        System.AddonSaddleBagWindow?.RefreshFromLifecycle();
    }

    private void OnRetainerInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        System.AddonRetainerWindow?.RefreshFromLifecycle();
    }

    private void OnSaddleBagOpen(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonShowArgs showArgs)
            return;
    }

    public void Dispose()
    {
        Services.AddonLifecycle.UnregisterListener(OnPostSetup, OnPreFinalize, OnInventoryUpdate, OnSaddleBagUpdate, OnRetainerInventoryUpdate, OnSaddleBagOpen);
    }
}