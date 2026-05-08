using System;
using System.Collections.Generic;
using AetherBags.IPC.ExternalCategorySystem;

namespace AetherBags.IPC.AetherBagsAPI;

public interface IAetherBagsAPI
{
    IReadOnlyList<uint> GetVisibleItemIds();
    IReadOnlyList<uint> GetItemsInCategory(uint categoryKey);
    bool IsItemVisible(uint itemId);
    string GetCurrentSearchFilter();
    bool IsInventoryOpen { get; }

    event Action<uint>? OnItemHovered;
    event Action<uint>? OnItemUnhovered;
    event Action<uint>? OnItemClicked;
    event Action<string>? OnSearchChanged;
    event Action? OnInventoryOpened;
    event Action? OnInventoryClosed;
    event Action? OnCategoriesRefreshed;
    event Action? OnConfigurationChanged;

    void RegisterSource(IExternalItemSource source);
    void UnregisterSource(string sourceName);
    IReadOnlyList<string> GetRegisteredSourceNames();

    string GetConfigurationJson();
    void SetConfigurationJson(string json);
    string GetConfigProperty(string propertyPath);
    void SetConfigProperty(string propertyPath, string jsonValue);

    bool IsVanillaInventoryBypassActive { get; }
    string AcquireVanillaInventoryBypass(string owner, int timeoutMs);
    bool ReleaseVanillaInventoryBypass(string token);
    string GetVanillaInventoryBypassStatus();
}
