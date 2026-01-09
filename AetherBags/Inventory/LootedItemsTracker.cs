using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory;

public sealed unsafe class LootedItemsTracker : IDisposable
{
    private static IReadOnlyList<InventoryType> StandardInventories => InventoryScanner.StandardInventories;

    private readonly List<LootedItemInfo> _lootedItems = new(capacity: 64);
    private bool _isEnabled;

    public event Action<IReadOnlyList<LootedItemInfo>>? OnLootedItemsChanged;

    public IReadOnlyList<LootedItemInfo> LootedItems => _lootedItems;

    public void Enable()
    {
        if (_isEnabled) return;

        _isEnabled = true;
        _lootedItems.Clear();
        Services.GameInventory.InventoryChangedRaw += OnInventoryChangedRaw;
    }

    public void Disable()
    {
        if (!_isEnabled) return;

        _isEnabled = false;
        Services.GameInventory.InventoryChangedRaw -= OnInventoryChangedRaw;
        _lootedItems.Clear();
    }

    public void Clear()
    {
        _lootedItems.Clear();
        OnLootedItemsChanged?.Invoke(_lootedItems);
    }

    public void RemoveByIndex(int index)
    {
        for (int i = 0; i < _lootedItems.Count; i++)
        {
            if (_lootedItems[i].Index == index)
            {
                _lootedItems.RemoveAt(i);
                OnLootedItemsChanged?.Invoke(_lootedItems);
                return;
            }
        }
    }

    public void Dispose()
    {
        Disable();
    }

    private void OnInventoryChangedRaw(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (!_isEnabled) return;
        if (!Services.ClientState.IsLoggedIn) return;

        bool hasChanges = false;

        foreach (var eventData in events)
        {
            if (!StandardInventories.Contains((InventoryType)eventData.Item.ContainerType)) continue;

            if (eventData is not (InventoryItemAddedArgs or InventoryItemChangedArgs)) continue;

            if (eventData is InventoryItemChangedArgs changedArgs &&
                changedArgs.OldItemState.Quantity >= changedArgs.Item.Quantity)
            {
                continue;
            }

            var inventoryItem = (InventoryItem*)eventData.Item.Address;
            var changeAmount = eventData is InventoryItemChangedArgs changed
                ? changed.Item.Quantity - changed.OldItemState.Quantity
                : eventData.Item.Quantity;

            _lootedItems.Add(new LootedItemInfo(
                _lootedItems.Count,
                *inventoryItem,
                changeAmount));

            hasChanges = true;
        }

        if (hasChanges)
        {
            OnLootedItemsChanged?.Invoke(_lootedItems);
        }
    }
}
