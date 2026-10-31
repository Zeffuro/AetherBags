using Dalamud.Plugin.Services;
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
using AtkValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

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
    private bool _isDisposed;
    private readonly InventoryWindowSync _windowSync = new();

    public InventoryMonitor()
    {
        var bags = new[] { "Inventory", "InventoryLarge", "InventoryExpansion", "InventoryEvent" };
        var retainer = new[] { "InventoryRetainer", "InventoryRetainerLarge" };

        // PreRefresh Handlers
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PreRefresh, bags, InventoryPreRefreshHandler);

        // PostRequestedUpdate
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostRequestedUpdate, bags, OnInventoryUpdate);
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostRequestedUpdate, "InventoryBuddy", OnSaddleBagUpdate);
        IAddonLifecycle.Get().RegisterListener(AddonEvent.PostRequestedUpdate, retainer, OnRetainerInventoryUpdate);

        // Dalamud raw event for raw inventory changes (scans once per frame)
        IGameInventory.Get().InventoryChangedRaw += OnInventoryChangedRaw;

        IPluginLog.Get().Verbose("InventoryLifecycles initialized");
    }

    private bool IsInUnsafeState()
    {
        if (_isDisposed) return true;
        if (!IClientState.Get().IsLoggedIn)
            return true;

        return ICondition.Get().Any(ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51);
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

    private static readonly HashSet<InventoryType> RefreshTriggerContainers = BuildRefreshTriggerContainers();

    private static HashSet<InventoryType> BuildRefreshTriggerContainers()
    {
        var set = new HashSet<InventoryType>(InventoryScanner.StandardInventories)
        {
            InventoryType.SaddleBag1,
            InventoryType.SaddleBag2,
            InventoryType.PremiumSaddleBag1,
            InventoryType.PremiumSaddleBag2,
            InventoryType.RetainerPage1,
            InventoryType.RetainerPage2,
            InventoryType.RetainerPage3,
            InventoryType.RetainerPage4,
            InventoryType.RetainerPage5,
            InventoryType.RetainerPage6,
            InventoryType.RetainerPage7,
            InventoryType.RetainerCrystals,
            InventoryType.RetainerEquippedItems,
        };
        return set;
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        bool needsRefresh = false;
        foreach (var inventoryEventArgs in events)
        {
            if (RefreshTriggerContainers.Contains((InventoryType)inventoryEventArgs.Item.ContainerType))
            {
                needsRefresh = true;
                break;
            }
        }

        if (needsRefresh)
        {
            IFramework.Get().RunOnTick(() =>
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

        IPluginLog.Get().DebugOnly("PreRefresh event for Inventory detected");

        AtkValuePtr[] atkValues = refreshArgs.AtkValueEnumerable.ToArray();

        if (atkValues.Length < 7) return;

        AtkValue* value5 = (AtkValue*)atkValues[5].Address;
        AtkValue* value6 = (AtkValue*)atkValues[6].Address;

        if (value5->Type != AtkValueType.ManagedString || value6->Type != AtkValueType.ManagedString)
            return;

        ReadOnlySeString title = value5->String.AsReadOnlySeString();
        ReadOnlySeString upperTitle = value6->String.AsReadOnlySeString();

        System.AddonInventoryWindow.SetNotification(new InventoryNotificationInfo(title, upperTitle));

    }

    private void OnInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        if (!args.Addon.IsNull && args.Addon.IsReady)
            InventorySlotCounts.CaptureRequestedUpdate(InventorySourceType.MainBags);
        if (DragDropState.IsDragging)
            return;

        System.LootedItemsTracker.FlushPendingChanges();
        System.AddonInventoryWindow?.RefreshFromLifecycle();
    }

    private void OnSaddleBagUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        if (!args.Addon.IsNull && args.Addon.IsReady)
            InventorySlotCounts.CaptureSaddleBagUpdate();
        if (DragDropState.IsDragging)
            return;

        System.LootedItemsTracker.FlushPendingChanges();
        System.AddonSaddleBagWindow?.RefreshFromLifecycle();
    }

    private void OnRetainerInventoryUpdate(AddonEvent type, AddonArgs args)
    {
        if (IsInUnsafeState())
            return;

        if (!args.Addon.IsNull && args.Addon.IsReady)
            InventorySlotCounts.CaptureRequestedUpdate(InventorySourceType.Retainer);
        if (DragDropState.IsDragging)
            return;

        System.LootedItemsTracker.FlushPendingChanges();
        System.AddonRetainerWindow?.RefreshFromLifecycle();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _windowSync.Dispose();
        IGameInventory.Get().InventoryChangedRaw -= OnInventoryChangedRaw;
        IAddonLifecycle.Get().UnregisterListener(OnInventoryUpdate, OnSaddleBagUpdate, OnRetainerInventoryUpdate, InventoryPreRefreshHandler);
    }
}
