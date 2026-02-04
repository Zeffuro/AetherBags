using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.IPC.ExternalCategorySystem;

namespace AetherBags.IPC.AetherBagsAPI;

public class AetherBagsAPIImpl : IAetherBagsAPI
{
    public event Action<uint>? OnItemHovered;
    public event Action<uint>? OnItemUnhovered;
    public event Action<uint>? OnItemClicked;
    public event Action<string>? OnSearchChanged;
    public event Action? OnInventoryOpened;
    public event Action? OnInventoryClosed;
    public event Action? OnCategoriesRefreshed;

    public bool IsInventoryOpen => System.AddonInventoryWindow?.IsOpen ?? false;

    public IReadOnlyList<uint> GetVisibleItemIds()
    {
        var window = System.AddonInventoryWindow;
        if (window == null || !window.IsOpen) return Array.Empty<uint>();

        var categories = window.GetVisibleCategories();
        if (categories == null) return Array.Empty<uint>();

        var result = new List<uint>();
        foreach (var category in categories)
        {
            foreach (var item in category.Items)
            {
                result.Add(item.Item.ItemId);
            }
        }
        return result;
    }

    public IReadOnlyList<uint> GetItemsInCategory(uint categoryKey)
    {
        var window = System.AddonInventoryWindow;
        if (window == null || !window.IsOpen) return Array.Empty<uint>();

        var categories = window.GetVisibleCategories();
        if (categories == null) return Array.Empty<uint>();

        var category = categories.FirstOrDefault(c => c.Key == categoryKey);
        if (category.Items == null) return Array.Empty<uint>();

        return category.Items.Select(i => i.Item.ItemId).ToList();
    }

    public bool IsItemVisible(uint itemId)
    {
        var window = System.AddonInventoryWindow;
        if (window == null || !window.IsOpen) return false;

        var categories = window.GetVisibleCategories();
        if (categories == null) return false;

        foreach (var category in categories)
        {
            if (category.Items.Any(i => i.Item.ItemId == itemId))
                return true;
        }
        return false;
    }

    public string GetCurrentSearchFilter()
    {
        return System.AddonInventoryWindow?.GetSearchText() ?? string.Empty;
    }

    public void RegisterSource(IExternalItemSource source)
    {
        ExternalCategoryManager.RegisterSource(source);
    }

    public void UnregisterSource(string sourceName)
    {
        ExternalCategoryManager.UnregisterSource(sourceName);
    }

    public IReadOnlyList<string> GetRegisteredSourceNames()
    {
        return ExternalCategoryManager.RegisteredSources.Select(s => s.SourceName).ToList();
    }

    public void RaiseItemHovered(uint itemId) => OnItemHovered?.Invoke(itemId);
    public void RaiseItemUnhovered(uint itemId) => OnItemUnhovered?.Invoke(itemId);
    public void RaiseItemClicked(uint itemId) => OnItemClicked?.Invoke(itemId);
    public void RaiseSearchChanged(string search) => OnSearchChanged?.Invoke(search);
    public void RaiseInventoryOpened() => OnInventoryOpened?.Invoke();
    public void RaiseInventoryClosed() => OnInventoryClosed?.Invoke();
    public void RaiseCategoriesRefreshed() => OnCategoriesRefreshed?.Invoke();
}
