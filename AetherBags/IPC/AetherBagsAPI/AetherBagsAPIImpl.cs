using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AetherBags.Helpers;
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
    public event Action? OnConfigurationChanged;

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

    public string GetConfigurationJson()
    {
        return System.Config != null ? JsonSerializer.Serialize(System.Config) : "{}";
    }

    public void SetConfigurationJson(string json)
    {
        try
        {
            var config = JsonSerializer.Deserialize<Configuration.SystemConfiguration>(json);
            if (config != null)
            {
                config.EnsureInitialized();
                System.Config = config;
                Util.SaveConfig(System.Config);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error($"Failed to set configuration from JSON: {ex}");
        }
    }

    public string GetConfigProperty(string propertyPath)
    {
        try
        {
            var parts = propertyPath.Split('.');
            object? current = System.Config;

            foreach (var part in parts)
            {
                if (current == null) return "null";
                var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) throw new Exception($"Property '{part}' not found on {current.GetType().Name}");
                current = prop.GetValue(current);
            }

            return JsonSerializer.Serialize(current);
        }
        catch (Exception ex)
        {
            Services.Logger.Error($"Failed to get config property '{propertyPath}': {ex}");
            return "null";
        }
    }

    public void SetConfigProperty(string propertyPath, string jsonValue)
    {
        try
        {
            var parts = propertyPath.Split('.');
            object? current = System.Config;
            PropertyInfo? lastProp = null;
            object? parent = null;

            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null) throw new Exception($"Cannot traverse null object at property '{parts[i - 1]}'");
                lastProp = current.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (lastProp == null) throw new Exception($"Property '{parts[i]}' not found on {current.GetType().Name}");

                if (i < parts.Length - 1)
                {
                    parent = current;
                    current = lastProp.GetValue(current);
                }
                else
                {
                    parent = current;
                }
            }

            if (lastProp != null && parent != null)
            {
                var value = JsonSerializer.Deserialize(jsonValue, lastProp.PropertyType);
                lastProp.SetValue(parent, value);
                Util.SaveConfig(System.Config);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error($"Failed to set config property '{propertyPath}': {ex}");
        }
    }

    public void RaiseItemHovered(uint itemId) => OnItemHovered?.Invoke(itemId);
    public void RaiseItemUnhovered(uint itemId) => OnItemUnhovered?.Invoke(itemId);
    public void RaiseItemClicked(uint itemId) => OnItemClicked?.Invoke(itemId);
    public void RaiseSearchChanged(string search) => OnSearchChanged?.Invoke(search);
    public void RaiseInventoryOpened() => OnInventoryOpened?.Invoke();
    public void RaiseInventoryClosed() => OnInventoryClosed?.Invoke();
    public void RaiseCategoriesRefreshed() => OnCategoriesRefreshed?.Invoke();
    public void RaiseConfigurationChanged() => OnConfigurationChanged?.Invoke();
}
