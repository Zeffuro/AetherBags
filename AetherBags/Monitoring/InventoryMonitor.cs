using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Scanning;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Game.NativeWrapper;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace AetherBags.Monitoring;

public static unsafe class DragDropState
{
    /// <summary>
    /// Returns true if the game's drag-drop manager is currently dragging.
    /// </summary>
    public static bool IsDragging => AtkStage.Instance()->DragDropManager.IsDragging;
}

public class InventoryMonitor : IDisposable
{

    public InventoryMonitor()
    {
        var bags = new[] { "Inventory", "InventoryLarge", "InventoryExpansion" };
        var saddle = new[] { "InventoryBuddy" };
        var retainer = new[] { "InventoryRetainer", "InventoryRetainerLarge" };

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, saddle, OnPostSetup);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, retainer, OnPostSetup);

        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, saddle, OnPreFinalize);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, retainer, OnPreFinalize);

        // PreRefresh Handlers
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, bags, InventoryPreRefreshHandler);

        // PostRequestedUpdate
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Inventory", OnInventoryUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "InventoryBuddy", OnSaddleBagUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, retainer, OnRetainerInventoryUpdate);

        // Dalamud raw event for raw inventory changes (scans once per frame)
        Services.GameInventory.InventoryChangedRaw += OnInventoryChangedRaw;

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

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        bool needsRefresh = false;
        foreach (var inventoryEventArgs in events)
        {
            if (InventoryScanner.StandardInventories.Contains((InventoryType)inventoryEventArgs.Item.ContainerType))
            {
                needsRefresh = true;
                break;
            }
        }

        if (needsRefresh)
        {
            Services.Framework.RunOnTick(() =>
            {
                if (IsInUnsafeState() || DragDropState.IsDragging) return;

                System.LootedItemsTracker.FlushPendingChanges();
                System.AddonInventoryWindow?.RefreshFromLifecycle();
                System.AddonSaddleBagWindow?.RefreshFromLifecycle();
                System.AddonRetainerWindow?.RefreshFromLifecycle();
            });
        }
    }

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

    private void OnInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        if (DragDropState.IsDragging)
            return;

        System.LootedItemsTracker.FlushPendingChanges();
        System.AddonInventoryWindow?.RefreshFromLifecycle();
    }

    private void OnSaddleBagUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        if (DragDropState.IsDragging)
            return;

        System.LootedItemsTracker.FlushPendingChanges();
        System.AddonSaddleBagWindow?.RefreshFromLifecycle();
    }

    private void OnRetainerInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        if (DragDropState.IsDragging)
            return;

        System.LootedItemsTracker.FlushPendingChanges();
        System.AddonRetainerWindow?.RefreshFromLifecycle();
    }

    public void Dispose()
    {
        Services.GameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
        Services.AddonLifecycle.UnregisterListener(OnPostSetup, OnPreFinalize, OnInventoryUpdate, OnSaddleBagUpdate, OnRetainerInventoryUpdate);
    }
}