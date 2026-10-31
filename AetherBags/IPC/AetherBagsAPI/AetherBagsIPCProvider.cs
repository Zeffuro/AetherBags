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
    private readonly ICallGateProvider<string, int, string> _acquireVanillaInventoryBypass;
    private readonly ICallGateProvider<string, bool> _releaseVanillaInventoryBypass;
    private readonly ICallGateProvider<bool> _isVanillaInventoryBypassActive;
    private readonly ICallGateProvider<string> _getVanillaInventoryBypassStatus;

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

        _isInventoryOpen = Plugin.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}IsInventoryOpen");
        _getVisibleItemIds = Plugin.PluginInterface.GetIpcProvider<List<uint>>($"{IpcPrefix}GetVisibleItemIds");
        _getItemsInCategory = Plugin.PluginInterface.GetIpcProvider<uint, List<uint>>($"{IpcPrefix}GetItemsInCategory");
        _isItemVisible = Plugin.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}IsItemVisible");
        _getSearchFilter = Plugin.PluginInterface.GetIpcProvider<string>($"{IpcPrefix}GetSearchFilter");
        _getRegisteredSources = Plugin.PluginInterface.GetIpcProvider<List<string>>($"{IpcPrefix}GetRegisteredSources");
        _getConfigurationJson = Plugin.PluginInterface.GetIpcProvider<string>($"{IpcPrefix}GetConfigurationJson");
        _setConfigurationJson = Plugin.PluginInterface.GetIpcProvider<string, bool>($"{IpcPrefix}SetConfigurationJson");
        _getConfigProperty = Plugin.PluginInterface.GetIpcProvider<string, string>($"{IpcPrefix}GetConfigProperty");
        _setConfigProperty = Plugin.PluginInterface.GetIpcProvider<string, string, bool>($"{IpcPrefix}SetConfigProperty");

        _acquireVanillaInventoryBypass = Plugin.PluginInterface.GetIpcProvider<string, int, string>($"{IpcPrefix}AcquireVanillaInventoryBypass");
        _releaseVanillaInventoryBypass = Plugin.PluginInterface.GetIpcProvider<string, bool>($"{IpcPrefix}ReleaseVanillaInventoryBypass");
        _isVanillaInventoryBypassActive = Plugin.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}IsVanillaInventoryBypassActive");
        _getVanillaInventoryBypassStatus = Plugin.PluginInterface.GetIpcProvider<string>($"{IpcPrefix}GetVanillaInventoryBypassStatus");

        _onItemHovered = Plugin.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemHovered");
        _onItemUnhovered = Plugin.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemUnhovered");
        _onItemClicked = Plugin.PluginInterface.GetIpcProvider<uint, bool>($"{IpcPrefix}OnItemClicked");
        _onSearchChanged = Plugin.PluginInterface.GetIpcProvider<string, bool>($"{IpcPrefix}OnSearchChanged");
        _onInventoryOpened = Plugin.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnInventoryOpened");
        _onInventoryClosed = Plugin.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnInventoryClosed");
        _onCategoriesRefreshed = Plugin.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnCategoriesRefreshed");
        _onConfigurationChanged = Plugin.PluginInterface.GetIpcProvider<bool>($"{IpcPrefix}OnConfigurationChanged");

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
        _acquireVanillaInventoryBypass.RegisterFunc((owner, timeoutMs) => _api.AcquireVanillaInventoryBypass(owner, timeoutMs));
        _releaseVanillaInventoryBypass.RegisterFunc(token => _api.ReleaseVanillaInventoryBypass(token));
        _isVanillaInventoryBypassActive.RegisterFunc(() => _api.IsVanillaInventoryBypassActive);
        _getVanillaInventoryBypassStatus.RegisterFunc(() => _api.GetVanillaInventoryBypassStatus());
    }

    private void SubscribeEvents()
    {
        _api.OnItemHovered += OnItemHovered;
        _api.OnItemUnhovered += OnItemUnhovered;
        _api.OnItemClicked += OnItemClicked;
        _api.OnSearchChanged += OnSearchChanged;
        _api.OnInventoryOpened += OnInventoryOpened;
        _api.OnInventoryClosed += OnInventoryClosed;
        _api.OnCategoriesRefreshed += OnCategoriesRefreshed;
        _api.OnConfigurationChanged += OnConfigurationChanged;
    }

    private void OnItemHovered(uint itemId) => _onItemHovered.SendMessage(itemId);
    private void OnItemUnhovered(uint itemId) => _onItemUnhovered.SendMessage(itemId);
    private void OnItemClicked(uint itemId) => _onItemClicked.SendMessage(itemId);
    private void OnSearchChanged(string search) => _onSearchChanged.SendMessage(search);
    private void OnInventoryOpened() => _onInventoryOpened.SendMessage();
    private void OnInventoryClosed() => _onInventoryClosed.SendMessage();
    private void OnCategoriesRefreshed() => _onCategoriesRefreshed.SendMessage();
    private void OnConfigurationChanged() => _onConfigurationChanged.SendMessage();

    public void Dispose()
    {
        _api.OnItemHovered -= OnItemHovered;
        _api.OnItemUnhovered -= OnItemUnhovered;
        _api.OnItemClicked -= OnItemClicked;
        _api.OnSearchChanged -= OnSearchChanged;
        _api.OnInventoryOpened -= OnInventoryOpened;
        _api.OnInventoryClosed -= OnInventoryClosed;
        _api.OnCategoriesRefreshed -= OnCategoriesRefreshed;
        _api.OnConfigurationChanged -= OnConfigurationChanged;

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
        _acquireVanillaInventoryBypass.UnregisterFunc();
        _releaseVanillaInventoryBypass.UnregisterFunc();
        _isVanillaInventoryBypassActive.UnregisterFunc();
        _getVanillaInventoryBypassStatus.UnregisterFunc();
    }
}
