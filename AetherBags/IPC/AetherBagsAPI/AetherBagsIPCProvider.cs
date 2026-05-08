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
    private readonly ICallGateProvider<string> _getConfigurationJson;
    private readonly ICallGateProvider<string, bool> _setConfigurationJson;
    private readonly ICallGateProvider<string, string> _getConfigProperty;
    private readonly ICallGateProvider<string, string, bool> _setConfigProperty;

    private readonly ICallGateProvider<uint, bool> _onItemHovered;
    private readonly ICallGateProvider<uint, bool> _onItemUnhovered;
    private readonly ICallGateProvider<uint, bool> _onItemClicked;
    private readonly ICallGateProvider<string, bool> _onSearchChanged;
    private readonly ICallGateProvider<bool> _onInventoryOpened;
    private readonly ICallGateProvider<bool> _onInventoryClosed;
    private readonly ICallGateProvider<bool> _onCategoriesRefreshed;
    private readonly ICallGateProvider<bool> _onConfigurationChanged;

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
        _getConfigurationJson = Services.PluginInterface.GetIpcProvider<string>($"{IpcPrefix}GetConfigurationJson");
        _setConfigurationJson = Services.PluginInterface.GetIpcProvider<string, bool>($"{IpcPrefix}SetConfigurationJson");
        _getConfigProperty = Services.PluginInterface.GetIpcProvider<string, string>($"{IpcPrefix}GetConfigProperty");
        _setConfigProperty = Services.PluginInterface.GetIpcProvider<string, string, bool>($"{IpcPrefix}SetConfigProperty");

        _onItemHovered = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemHovered");
        _onItemUnhovered = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemUnhovered");
        _onItemClicked = Services.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemClicked");
        _onSearchChanged = Services.PluginInterface.GetIpcProvider<string, bool>($"{IpcPrefix}OnSearchChanged");
        _onInventoryOpened = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnInventoryOpened");
        _onInventoryClosed = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnInventoryClosed");
        _onCategoriesRefreshed = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnCategoriesRefreshed");
        _onConfigurationChanged = Services.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnConfigurationChanged");

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
        _getConfigurationJson.RegisterFunc(() => _api.GetConfigurationJson());
        _setConfigurationJson.RegisterAction(json => _api.SetConfigurationJson(json));
        _getConfigProperty.RegisterFunc(path => _api.GetConfigProperty(path));
        _setConfigProperty.RegisterAction((path, jsonValue) => _api.SetConfigProperty(path, jsonValue));
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
        _api.OnConfigurationChanged += () => _onConfigurationChanged.SendMessage();
    }

    public void Dispose()
    {
        _isInventoryOpen.UnregisterFunc();
        _getVisibleItemIds.UnregisterFunc();
        _getItemsInCategory.UnregisterFunc();
        _isItemVisible.UnregisterFunc();
        _getSearchFilter.UnregisterFunc();
        _getRegisteredSources.UnregisterFunc();
        _getConfigurationJson.UnregisterFunc();
        _setConfigurationJson.UnregisterAction();
        _getConfigProperty.UnregisterFunc();
        _setConfigProperty.UnregisterAction();
    }
}
