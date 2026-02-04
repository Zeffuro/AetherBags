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

namespace AetherBags.Monitoring;

public sealed unsafe class LootedItemsTracker : IDisposable
{
    private static IReadOnlyList<InventoryType> StandardInventories => InventoryScanner.StandardInventories;

    private const int BatchDelayMs = 300;

    private readonly List<LootedItemInfo> _lootedItems = new(capacity: 64);
    private readonly Dictionary<(uint ItemId, bool IsHq), (InventoryItem Item, int Quantity)> _pendingChanges = new(capacity: 32);

    private static HashSet<uint>? _filteredCategoryItems;

    private bool _isEnabled;
    private long _batchStartTick;
    private bool _hasPendingRemoval;
    private int _nextIndex;

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
        _nextIndex = 0;
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
        _nextIndex = 0;
    }

    public void Clear()
    {
        _lootedItems.Clear();
        _hasPendingRemoval = true;
        _nextIndex = 0;
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

        ProcessPendingChanges();

        _hasPendingRemoval = false;
        OnLootedItemsChanged?.Invoke(_lootedItems);
    }

    public void Dispose()
    {
        Disable();
    }

    private void ProcessPendingChanges()
    {
        if (_pendingChanges.Count == 0) return;

        foreach (var ((itemId, isHq), (item, delta)) in _pendingChanges)
        {
            int existingIndex = FindExistingItemIndex(itemId, isHq);

            if (existingIndex >= 0)
            {
                var current = _lootedItems[existingIndex];
                int newQty = current.Quantity + delta;

                if (newQty <= 0)
                    _lootedItems.RemoveAt(existingIndex);
                else
                    _lootedItems[existingIndex] = current with { Quantity = newQty };
            }
            else if (delta > 0)
            {
                _lootedItems.Add(new LootedItemInfo(_nextIndex++, item, delta));
            }
        }

        _pendingChanges.Clear();
    }

    private int FindExistingItemIndex(uint itemId, bool isHq)
    {
        for (int i = 0; i < _lootedItems.Count; i++)
        {
            var info = _lootedItems[i];
            if (info.Item.ItemId == itemId &&
                info.Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) == isHq)
            {
                return i;
            }
        }
        return -1;
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!_isEnabled || !Services.ClientState.IsLoggedIn) return;

        bool anyChanged = false;

        foreach (var eventData in events)
        {
            if (!StandardInventories.Contains((InventoryType)eventData.Item.ContainerType))
                continue;

            if (eventData.Item.ContainerType == GameInventoryType.DamagedGear)
                continue;

            int changeAmount = eventData switch
            {
                InventoryItemAddedArgs added => added.Item.Quantity,
                InventoryItemRemovedArgs removed => -removed.Item.Quantity,
                InventoryItemChangedArgs changed => changed.Item.Quantity - changed.OldItemState.Quantity,
                _ => 0
            };

            if (changeAmount == 0) continue;

            if (ShouldFilterItem(eventData.Item.ItemId))
                continue;

            uint itemId = eventData.Item.ItemId;
            bool isHq = eventData.Item.IsHq;
            var key = (itemId, isHq);

            if (_pendingChanges.TryGetValue(key, out var existing))
            {
                InventoryItem itemStruct = existing.Item;
                if (changeAmount > 0 && itemStruct.ItemId == 0)
                {
                    itemStruct = *(InventoryItem*)eventData.Item.Address;
                }
                _pendingChanges[key] = (itemStruct, existing.Quantity + changeAmount);
            }
            else
            {
                InventoryItem itemStruct = default;
                if (changeAmount > 0)
                {
                    itemStruct = *(InventoryItem*)eventData.Item.Address;
                }

                _pendingChanges[key] = (itemStruct, changeAmount);
            }

            anyChanged = true;
        }

        if (anyChanged && _batchStartTick == 0)
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

        FlushPendingChanges();
    }

    private static bool ShouldFilterItem(uint itemId)
    {
        if (_filteredCategoryItems == null)
        {
            _filteredCategoryItems = new HashSet<uint>();
            var sheet = Services.DataManager.GetExcelSheet<Item>();
            foreach (var row in sheet)
            {
                if (row.ItemUICategory.RowId == 62)
                    _filteredCategoryItems.Add(row.RowId);
            }
            Services.Logger.DebugOnly($"[LootedItemsTracker] Built filter cache with {_filteredCategoryItems.Count} items");
        }

        return _filteredCategoryItems.Contains(itemId);
    }
}
