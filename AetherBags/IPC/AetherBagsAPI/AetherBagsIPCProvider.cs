using System;
using System.Collections.Generic;
using Dalamud.Plugin.Ipc;

namespace AetherBags.IPC.AetherBagsAPI;

public class AetherBagsIPCProvider : IDisposable
{
    private const string IpcPrefix = "AetherBags.";

    private readonly AetherBagsAPIImpl _api;

    private readonly ICallGateProvider<bool> _isInventoryOpen;
    private readonly ICallGateProvider<List<uint>> _getVisibleItemIds;
    private readonly ICallGateProvider<uint, List<uint>> _getItemsInCategory;
    private readonly ICallGateProvider<uint, bool> _isItemVisible;
    private readonly ICallGateProvider<string> _getSearchFilter;
    private readonly ICallGateProvider<List<string>> _getRegisteredSources;

    private readonly ICallGateProvider<uint, bool> _onItemHovered;
    private readonly ICallGateProvider<uint, bool> _onItemUnhovered;
    private readonly ICallGateProvider<uint, bool> _onItemClicked;
    private readonly ICallGateProvider<string, bool> _onSearchChanged;
    private readonly ICallGateProvider<bool> _onInventoryOpened;
    private readonly ICallGateProvider<bool> _onInventoryClosed;
    private readonly ICallGateProvider<bool> _onCategoriesRefreshed;

    public AetherBagsAPIImpl API => _api;

    public AetherBagsIPCProvider()
    {
        _api = new AetherBagsAPIImpl();

        _isInventoryOpen = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}IsInventoryOpen");
        _getVisibleItemIds = Services.PluginInterface.GetIpcProvider<List<uint>>($"{IpcPrefix}GetVisibleItemIds");
        _getItemsInCategory = Services.PluginInterface.GetIpcProvider<uint, List<uint>>($"{IpcPrefix}GetItemsInCategory");
        _isItemVisible = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}IsItemVisible");
        _getSearchFilter = Services.PluginInterface.GetIpcProvider<string>($"{IpcPrefix}GetSearchFilter");
        _getRegisteredSources = Services.PluginInterface.GetIpcProvider<List<string>>($"{IpcPrefix}GetRegisteredSources");

        _onItemHovered = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemHovered");
        _onItemUnhovered = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemUnhovered");
        _onItemClicked = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemClicked");
        _onSearchChanged = Services.PluginInterface.GetIpcProvider<string, bool>($"{IpcPrefix}OnSearchChanged");
        _onInventoryOpened = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnInventoryOpened");
        _onInventoryClosed = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnInventoryClosed");
        _onCategoriesRefreshed = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnCategoriesRefreshed");

        RegisterFunctions();
        SubscribeEvents();
    }

    private void RegisterFunctions()
    {
        _isInventoryOpen.RegisterFunc(() => _api.IsInventoryOpen);
        _getVisibleItemIds.RegisterFunc(() => new List<uint>(_api.GetVisibleItemIds()));
        _getItemsInCategory.RegisterFunc(key => new List<uint>(_api.GetItemsInCategory(key)));
        _isItemVisible.RegisterFunc(itemId => _api.IsItemVisible(itemId));
        _getSearchFilter.RegisterFunc(() => _api.GetCurrentSearchFilter());
        _getRegisteredSources.RegisterFunc(() => new List<string>(_api.GetRegisteredSourceNames()));
    }

    private void SubscribeEvents()
    {
        _api.OnItemHovered += itemId => _onItemHovered.SendMessage(itemId);
        _api.OnItemUnhovered += itemId => _onItemUnhovered.SendMessage(itemId);
        _api.OnItemClicked += itemId => _onItemClicked.SendMessage(itemId);
        _api.OnSearchChanged += search => _onSearchChanged.SendMessage(search);
        _api.OnInventoryOpened += () => _onInventoryOpened.SendMessage();
        _api.OnInventoryClosed += () => _onInventoryClosed.SendMessage();
        _api.OnCategoriesRefreshed += () => _onCategoriesRefreshed.SendMessage();
    }

    public void Dispose()
    {
        _isInventoryOpen.UnregisterFunc();
        _getVisibleItemIds.UnregisterFunc();
        _getItemsInCategory.UnregisterFunc();
        _isItemVisible.UnregisterFunc();
        _getSearchFilter.UnregisterFunc();
        _getRegisteredSources.UnregisterFunc();
    }
}
