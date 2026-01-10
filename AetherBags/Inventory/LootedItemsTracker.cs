using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace AetherBags.Inventory;

public sealed unsafe class LootedItemsTracker : IDisposable
{
    private static IReadOnlyList<InventoryType> StandardInventories => InventoryScanner.StandardInventories;

    private const int BatchDelayMs = 300;

    private readonly List<LootedItemInfo> _lootedItems = new(capacity: 64);
    private readonly Dictionary<(uint ItemId, bool IsHq), (InventoryItem Item, int Quantity)> _pendingChanges = new(capacity: 32);

    private bool _isEnabled;
    private long _batchStartTick;
    private bool _hasPendingRemoval;

    public event Action<IReadOnlyList<LootedItemInfo>>? OnLootedItemsChanged;

    public IReadOnlyList<LootedItemInfo> LootedItems => _lootedItems;

    public bool HasPendingChanges => _pendingChanges.Count > 0 || _hasPendingRemoval;

    public void Enable()
    {
        if (_isEnabled) return;

        _isEnabled = true;
        _lootedItems.Clear();
        _pendingChanges.Clear();
        _batchStartTick = 0;
        _hasPendingRemoval = false;
        Services.GameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
        Services.Framework.Update += OnFrameworkUpdate;
    }

    public void Disable()
    {
        if (!_isEnabled) return;

        _isEnabled = false;
        Services.GameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
        Services.Framework.Update -= OnFrameworkUpdate;
        _lootedItems.Clear();
        _pendingChanges.Clear();
        _batchStartTick = 0;
        _hasPendingRemoval = false;
    }

    public void Clear()
    {
        _lootedItems.Clear();
        _hasPendingRemoval = true;
    }

    public void RemoveByIndex(int index)
    {
        for (int i = 0; i < _lootedItems.Count; i++)
        {
            if (_lootedItems[i].Index == index)
            {
                _lootedItems.RemoveAt(i);
                _hasPendingRemoval = true;
                return;
            }
        }
    }

    public void FlushPendingChanges()
    {
        if (_pendingChanges.Count == 0 && !_hasPendingRemoval) return;

        _hasPendingRemoval = false;
        OnLootedItemsChanged?.Invoke(_lootedItems);
    }

    public void Dispose()
    {
        Disable();
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!_isEnabled) return;
        if (!Services.ClientState.IsLoggedIn) return;

        bool anyAdded = false;

        foreach (var eventData in events)
        {
            if (!StandardInventories.Contains((InventoryType)eventData.Item.ContainerType))
                continue;

            if (eventData.Item.ContainerType == GameInventoryType.DamagedGear)
                continue;

            if (eventData is not (InventoryItemAddedArgs or InventoryItemChangedArgs))
                continue;

            if (eventData is InventoryItemChangedArgs changedArgs &&
                changedArgs.OldItemState.Quantity >= changedArgs.Item.Quantity)
            {
                continue;
            }

            if (ShouldFilterItem(eventData.Item.ItemId))
                continue;

            var inventoryItem = *(InventoryItem*)eventData.Item.Address;
            var changeAmount = eventData is InventoryItemChangedArgs changed
                ? changed.Item.Quantity - changed.OldItemState.Quantity
                : eventData.Item.Quantity;

            var key = (inventoryItem.ItemId, IsHq: inventoryItem.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality));

            if (_pendingChanges.TryGetValue(key, out var existing))
            {
                _pendingChanges[key] = (inventoryItem, existing.Quantity + changeAmount);
            }
            else
            {
                _pendingChanges[key] = (inventoryItem, changeAmount);
            }

            anyAdded = true;
        }

        if (anyAdded && _batchStartTick == 0)
        {
            _batchStartTick = Environment.TickCount64;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_batchStartTick == 0)
            return;

        if (Environment.TickCount64 < _batchStartTick + BatchDelayMs)
            return;

        _batchStartTick = 0;

        if (_pendingChanges.Count == 0)
            return;

        foreach (var ((itemId, isHq), (item, quantity)) in _pendingChanges)
        {
            if (quantity <= 0)
                continue;

            _lootedItems.Add(new LootedItemInfo(
                _lootedItems.Count,
                item,
                quantity));
        }

        _pendingChanges.Clear();

        OnLootedItemsChanged?.Invoke(_lootedItems);
    }

    private static bool ShouldFilterItem(uint itemId)
    {
        if (!Services.DataManager.GetExcelSheet<Item>().TryGetRow(itemId, out var item))
            return false;

        if (item.ItemUICategory.RowId == 62)
            return true;

        return false;
    }
}
