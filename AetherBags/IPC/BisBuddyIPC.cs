using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.IPC.ExternalCategorySystem;
using Dalamud.Plugin.Ipc;
using KamiToolKit.Classes;

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

    private BisBuddySource? _source;

    public void EnableExternalCategorySupport()
    {
        if (_source != null) return;

        _source = new BisBuddySource(this);
        ExternalCategoryManager.RegisterSource(_source);
    }

    public void DisableExternalCategorySupport()
    {
        if (_source == null) return;

        ExternalCategoryManager.UnregisterSource(_source.SourceName);
        _source = null;
    }

    public void Dispose()
    {
        DisableExternalCategorySupport();
        _initialized?.Unsubscribe(OnBisBuddyInitialized);
        _inventoryHighlightItemsChanged?.Unsubscribe(OnInventoryHighlightItemsChanged);
    }

    private sealed class BisBuddySource : IExternalItemSource
    {
        private readonly BisBuddyIPC _ipc;
        private int _version;

        public string SourceName => "BisBuddy";
        public string DisplayName => "Best in Slot";
        public int Priority => 100;
        public bool IsReady => _ipc.IsReady;
        public int Version => _version;
        public event Action? OnDataChanged;

        public SourceCapabilities Capabilities =>
            SourceCapabilities.Categories |
            SourceCapabilities.ItemColors |
            SourceCapabilities.SearchTags |
            SourceCapabilities.Relationships;

        public ConflictBehavior ConflictBehavior => ConflictBehavior.Replace;

        public BisBuddySource(BisBuddyIPC ipc)
        {
            _ipc = ipc;
            _ipc.OnItemsRefreshed += OnIpcRefreshed;
        }

        private void OnIpcRefreshed()
        {
            _version++;
            OnDataChanged?.Invoke();
        }

        public IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments()
        {
            var items = _ipc.ItemLookup;
            if (items.Count == 0) return null;

            var result = new Dictionary<uint, ExternalCategoryAssignment>();

            var colorGroups = new Dictionary<Vector4, List<(uint itemId, BisItemEntry entry)>>();
            foreach (var (itemId, entry) in items)
            {
                if (!colorGroups.TryGetValue(entry.Color, out var list))
                {
                    list = new List<(uint, BisItemEntry)>();
                    colorGroups[entry.Color] = list;
                }
                list.Add((itemId, entry));
            }

            uint subKey = 0;
            foreach (var (color, groupItems) in colorGroups)
            {
                uint categoryKey = CategoryBucketManager.MakeBisBuddyKey() | subKey++;

                foreach (var (itemId, entry) in groupItems)
                {
                    result[itemId] = new ExternalCategoryAssignment(
                        CategoryKey: categoryKey,
                        CategoryName: "[BiS] Gearset",
                        CategoryDescription: "Items needed for Best in Slot",
                        CategoryColor: color,
                        ItemOverlayColor: new Vector3(color.X, color.Y, color.Z),
                        SubPriority: 0
                    );
                }
            }

            return result;
        }

        public IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations()
        {
            var items = _ipc.ItemLookup;
            if (items.Count == 0) return null;

            var result = new Dictionary<uint, ItemDecoration>();
            foreach (var (itemId, entry) in items)
            {
                result[itemId] = new ItemDecoration
                {
                    OverlayColor = new Vector3(entry.Color.X, entry.Color.Y, entry.Color.Z),
                };
            }
            return result;
        }

        public IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId) => null;

        public IReadOnlyDictionary<uint, string[]>? GetSearchTags()
        {
            var items = _ipc.ItemLookup;
            if (items.Count == 0) return null;

            var result = new Dictionary<uint, string[]>();
            foreach (var itemId in items.Keys)
            {
                result[itemId] = new[] { "bis", "bestinslot", "gearset" };
            }
            return result;
        }

        public IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId)
        {
            if (!_ipc.ItemLookup.TryGetValue(itemId, out var entry)) return null;

            var sameSetItems = new List<uint>();
            foreach (var (otherId, otherEntry) in _ipc.ItemLookup)
            {
                if (otherId != itemId && otherEntry.Color == entry.Color)
                {
                    sameSetItems.Add(otherId);
                }
            }

            if (sameSetItems.Count == 0) return null;

            return new[]
            {
                new ItemRelationship(
                    Type: RelationshipType.SameSet,
                    RelatedItemIds: sameSetItems.ToArray(),
                    GroupLabel: "Same Gearset",
                    HighlightColor: new Vector3(entry.Color.X, entry.Color.Y, entry.Color.Z)
                )
            };
        }
    }
}