using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using Dalamud.Plugin.Ipc;

namespace AetherBags.IPC;

public record BisItemEntry(uint ItemId, Vector4 Color);

public record BisItemFilter(
    bool IncludePrereqs = true,
    bool IncludeMateria = true,
    bool IncludeCollected = false,
    bool IncludeObtainable = true,
    bool IncludeCollectedPrereqs = true
);

public class BisBuddyIPC : IDisposable
{
    private ICallGateSubscriber<bool>? _isInitialized;
    private ICallGateSubscriber<bool, bool>? _initialized;
    private ICallGateSubscriber<List<BisItemEntry>>? _getInventoryHighlightItems;
    private ICallGateSubscriber<List<BisItemEntry>, bool>? _inventoryHighlightItemsChanged;
    private ICallGateSubscriber<BisItemFilter, List<BisItemEntry>>? _getBisItemsFiltered;

    public bool IsReady { get; private set; }

    public List<BisItemEntry> CachedBisItems { get; } = new();

    public Dictionary<uint, BisItemEntry> ItemLookup { get; } = new();

    public BisItemFilter? CurrentFilter { get; private set; }

    public event Action? OnItemsRefreshed;

    public BisBuddyIPC()
    {
        try
        {
            _isInitialized = Services.PluginInterface.GetIpcSubscriber<bool>("BisBuddy.IsInitialized");
            _initialized = Services.PluginInterface.GetIpcSubscriber<bool, bool>("BisBuddy.Initialized");
            _getInventoryHighlightItems = Services.PluginInterface.GetIpcSubscriber<List<BisItemEntry>>("BisBuddy.GetInventoryHighlightItems");
            _inventoryHighlightItemsChanged = Services.PluginInterface.GetIpcSubscriber<List<BisItemEntry>, bool>("BisBuddy.InventoryHighlightItemsChanged");
            _getBisItemsFiltered = Services.PluginInterface.GetIpcSubscriber<BisItemFilter, List<BisItemEntry>>("BisBuddy.GetBisItemsFiltered");

            _initialized.Subscribe(OnBisBuddyInitialized);
            _inventoryHighlightItemsChanged.Subscribe(OnInventoryHighlightItemsChanged);

            try
            {
                IsReady = _isInitialized.InvokeFunc();
                if (IsReady) RefreshItems();
            }
            catch
            {
                IsReady = false;
            }
        }
        catch (Exception ex)
        {
            Services.Logger.DebugOnly($"BisBuddy not available: {ex.Message}");
            IsReady = false;
        }
    }

    private void OnBisBuddyInitialized(bool ready)
    {
        IsReady = ready;
        if (ready)
        {
            Services.Logger.Information("BisBuddy IPC connected");
            RefreshItems();
        }
        else
        {
            ClearHighlights();
        }
    }

    private void OnInventoryHighlightItemsChanged(List<BisItemEntry> items)
    {
        if (CurrentFilter == null)
        {
            UpdateCacheAndHighlights(items);
        }
    }

    public void RefreshItems()
    {
        if (!IsReady) return;

        try
        {
            List<BisItemEntry>? items;

            if (CurrentFilter != null)
            {
                items = _getBisItemsFiltered?.InvokeFunc(CurrentFilter);
            }
            else
            {
                items = _getInventoryHighlightItems?.InvokeFunc();
            }

            if (items != null)
            {
                UpdateCacheAndHighlights(items);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Warning($"Failed to refresh BisBuddy items: {ex.Message}");
            IsReady = false;
        }
    }

    public void SetFilter(BisItemFilter? filter)
    {
        CurrentFilter = filter;
        RefreshItems();
    }

    public void ShowAllItems()
    {
        SetFilter(new BisItemFilter(IncludeCollected: true));
    }

    public void ShowUncollectedOnly()
    {
        SetFilter(new BisItemFilter(IncludeCollected: false));
    }

    public void UseInventoryConfig()
    {
        SetFilter(null);
    }

    private void UpdateCacheAndHighlights(List<BisItemEntry> items)
    {
        CachedBisItems.Clear();
        ItemLookup.Clear();

        foreach (var item in items)
        {
            CachedBisItems.Add(item);
            ItemLookup[item.ItemId] = item;
        }

        Services.Logger.DebugOnly($"Refreshed {CachedBisItems.Count} BisBuddy items");

        ApplyHighlights();
        OnItemsRefreshed?.Invoke();
    }

    private void ApplyHighlights()
    {
        if (!System.Config.Categories.BisBuddyEnabled || CachedBisItems.Count == 0)
        {
            HighlightState.ClearLabel(HighlightSource.BiSBuddy);
        }
        else
        {
            var highlights = new Dictionary<uint, Vector4>(CachedBisItems.Count);
            foreach (var item in CachedBisItems)
            {
                highlights[item.ItemId] = item.Color;
            }
            HighlightState.SetLabelWithColors(HighlightSource.BiSBuddy, highlights);
        }

        InventoryOrchestrator.RefreshHighlights();
    }

    private void ClearHighlights()
    {
        CachedBisItems.Clear();
        ItemLookup.Clear();
        HighlightState.ClearLabel(HighlightSource.BiSBuddy);
        InventoryOrchestrator.RefreshHighlights();
    }

    public bool IsBisItem(uint itemId)
        => ItemLookup.ContainsKey(itemId);

    public BisItemEntry? GetBisItem(uint itemId)
        => ItemLookup.GetValueOrDefault(itemId);

    public Vector4? GetItemColor(uint itemId)
        => GetBisItem(itemId)?.Color;

    public void Dispose()
    {
        _initialized?.Unsubscribe(OnBisBuddyInitialized);
        _inventoryHighlightItemsChanged?.Unsubscribe(OnInventoryHighlightItemsChanged);
    }
}