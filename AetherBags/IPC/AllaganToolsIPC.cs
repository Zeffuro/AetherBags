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

public class AllaganToolsIPC : IDisposable
{
    private ICallGateSubscriber<bool>? _isInitialized;
    private ICallGateSubscriber<bool, bool>? _initialized;
    private ICallGateSubscriber<string, Dictionary<uint, uint>>? _getFilterItems;
    private ICallGateSubscriber<Dictionary<string, string>>? _getSearchFilters;
    private ICallGateSubscriber<string, bool>? _enableUiFilter;
    private ICallGateSubscriber<string, bool>? _toggleUiFilter;

    public bool IsReady { get; private set; }

    /// <summary>
    /// Cached filter items. Key = filterKey, Value = (ItemId -> Quantity).
    /// </summary>
    public Dictionary<string, Dictionary<uint, uint>> CachedFilterItems { get; } = new();

    /// <summary>
    /// Cached search filters. Key -> Name.
    /// </summary>
    public Dictionary<string, string> CachedSearchFilters { get; } = new();

    /// <summary>
    /// Quick lookup:  ItemId -> List of filter keys that contain this item.
    /// </summary>
    public Dictionary<uint, List<string>> ItemToFilters { get; } = new();

    public event Action? OnInitialized;
    public event Action? OnFiltersRefreshed;

    public AllaganToolsIPC()
    {
        try
        {
            _isInitialized = Services.PluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
            _initialized = Services.PluginInterface.GetIpcSubscriber<bool, bool>("AllaganTools.Initialized");
            _getFilterItems = Services.PluginInterface.GetIpcSubscriber<string, Dictionary<uint, uint>>("AllaganTools.GetFilterItems");
            _getSearchFilters = Services.PluginInterface.GetIpcSubscriber<Dictionary<string, string>>("AllaganTools.GetSearchFilters");
            _enableUiFilter = Services.PluginInterface.GetIpcSubscriber<string, bool>("AllaganTools.EnableUiFilter");
            _toggleUiFilter = Services.PluginInterface.GetIpcSubscriber<string, bool>("AllaganTools.ToggleUiFilter");

            _initialized.Subscribe(OnAllaganInitialized);

            try
            {
                IsReady = _isInitialized.InvokeFunc();
                if (IsReady)
                {
                    RefreshFilters();
                }
            }
            catch
            {
                IsReady = false;
            }
        }
        catch (Exception ex)
        {
            Services.Logger.DebugOnly($"Allagan Tools not available: {ex.Message}");
            IsReady = false;
        }
    }

    private void OnAllaganInitialized(bool initialized)
    {
        IsReady = initialized;
        if (initialized)
        {
            Services.Logger.Information("Allagan Tools IPC connected");
            RefreshFilters();
            OnInitialized?.Invoke();
        }
    }

    /// <summary>
    /// Refreshes all cached filter data from Allagan Tools.
    /// Call this when you need updated filter information.
    /// </summary>
    public void RefreshFilters()
    {
        if (!IsReady) return;

        try
        {
            CachedSearchFilters.Clear();
            CachedFilterItems.Clear();
            ItemToFilters.Clear();

            var filters = _getSearchFilters?.InvokeFunc();
            if (filters == null) return;

            foreach (var (key, name) in filters)
            {
                CachedSearchFilters[key] = name;

                var items = _getFilterItems?.InvokeFunc(key);
                if (items != null && items.Count > 0)
                {
                    CachedFilterItems[key] = items;

                    // Build reverse lookup
                    foreach (var itemId in items.Keys)
                    {
                        if (!ItemToFilters.TryGetValue(itemId, out var filterList))
                        {
                            filterList = new List<string>(capacity: 4);
                            ItemToFilters[itemId] = filterList;
                        }
                        filterList.Add(key);
                    }
                }
            }

            Services.Logger.DebugOnly($"Refreshed {CachedSearchFilters.Count} Allagan Tools filters, {ItemToFilters.Count} unique items");
            OnFiltersRefreshed?.Invoke();
        }
        catch (Exception ex)
        {
            Services.Logger.Warning($"Failed to refresh Allagan Tools filters: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if an item is in any Allagan Tools filter.
    /// </summary>
    public bool IsItemInAnyFilter(uint itemId)
        => ItemToFilters.ContainsKey(itemId);

    /// <summary>
    /// Gets all filter keys that contain this item.
    /// </summary>
    public IReadOnlyList<string>? GetFiltersForItem(uint itemId)
        => ItemToFilters.TryGetValue(itemId, out var list) ? list : null;

    /// <summary>
    /// Gets items from a specific filter.  Returns ItemId -> Quantity.
    /// </summary>
    public Dictionary<uint, uint>? GetFilterItems(string filterKey)
    {
        // Try cache first
        if (CachedFilterItems.TryGetValue(filterKey, out var cached))
            return cached;

        if (!IsReady) return null;

        try
        {
            return _getFilterItems?.InvokeFunc(filterKey);
        }
        catch (Exception ex)
        {
            Services.Logger.Warning($"GetFilterItems failed:  {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all available search filters.  Returns Key -> Name.
    /// </summary>
    public Dictionary<string, string>? GetSearchFilters()
    {
        if (CachedSearchFilters.Count > 0)
            return CachedSearchFilters;

        if (!IsReady) return null;

        try
        {
            return _getSearchFilters?.InvokeFunc();
        }
        catch (Exception ex)
        {
            Services.Logger.Warning($"GetSearchFilters failed: {ex.Message}");
            return null;
        }
    }

    public void SelectFilter(string filterKey)
    {
        HighlightState.SelectedAllaganToolsFilterKey = filterKey;
        InventoryOrchestrator.RefreshHighlights();
    }

    private AllaganToolsSource? _source;

    public void EnableExternalCategorySupport()
    {
        if (_source != null) return;

        _source = new AllaganToolsSource(this);
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
        _initialized?.Unsubscribe(OnAllaganInitialized);
    }

    private sealed class AllaganToolsSource : IExternalItemSource
    {
        private readonly AllaganToolsIPC _ipc;
        private int _version;

        public string SourceName => "AllaganTools";
        public string DisplayName => "Allagan Tools";
        public int Priority => 50;
        public bool IsReady => _ipc.IsReady;
        public int Version => _version;
        public event Action? OnDataChanged;

        public SourceCapabilities Capabilities =>
            SourceCapabilities.Categories |
            SourceCapabilities.SearchTags;

        public ConflictBehavior ConflictBehavior => ConflictBehavior.Defer;

        public AllaganToolsSource(AllaganToolsIPC ipc)
        {
            _ipc = ipc;
            _ipc.OnFiltersRefreshed += OnIpcRefreshed;
        }

        private void OnIpcRefreshed()
        {
            _version++;
            OnDataChanged?.Invoke();
        }

        public IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments()
        {
            if (_ipc.CachedFilterItems.Count == 0) return null;

            var result = new Dictionary<uint, ExternalCategoryAssignment>();
            int filterIndex = 0;

            foreach (var (filterKey, filterName) in _ipc.CachedSearchFilters)
            {
                if (!_ipc.CachedFilterItems.TryGetValue(filterKey, out var itemIds))
                {
                    filterIndex++;
                    continue;
                }

                uint categoryKey = CategoryBucketManager.MakeAllaganFilterKey(filterIndex);

                foreach (var itemId in itemIds.Keys)
                {
                    result.TryAdd(itemId, new ExternalCategoryAssignment(
                        CategoryKey: categoryKey,
                        CategoryName: $"[AT] {filterName}",
                        CategoryDescription: $"Allagan Tools filter: {filterName}",
                        CategoryColor: ColorHelper.GetColor(32),
                        ItemOverlayColor: null,
                        SubPriority: filterIndex
                    ));
                }

                filterIndex++;
            }

            return result;
        }

        public IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations() => null;

        public IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId) => null;

        public IReadOnlyDictionary<uint, string[]>? GetSearchTags()
        {
            if (_ipc.ItemToFilters.Count == 0) return null;

            var result = new Dictionary<uint, string[]>();
            foreach (var (itemId, filterKeys) in _ipc.ItemToFilters)
            {
                var tags = new List<string>(filterKeys.Count + 1) { "at", "allagantools" };
                foreach (var key in filterKeys)
                {
                    if (_ipc.CachedSearchFilters.TryGetValue(key, out var name))
                    {
                        tags.Add(name.ToLowerInvariant());
                    }
                }
                result[itemId] = tags.ToArray();
            }
            return result;
        }

        public IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId) => null;
    }
}