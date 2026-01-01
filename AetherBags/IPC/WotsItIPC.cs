using System;
using Dalamud.Plugin.Ipc;

namespace AetherBags.IPC;

public class WotsItIPC : IDisposable
{
    private ICallGateSubscriber<string, string, string, uint, string>? _registerWithSearch;
    private ICallGateSubscriber<string, bool>? _invoke;
    private ICallGateSubscriber<string, bool>? _unregisterAll;

    private string? _searchGuid;

    public WotsItIPC()
    {
        try
        {
            _registerWithSearch = Services.PluginInterface.GetIpcSubscriber<string, string, string, uint, string>("FA.RegisterWithSearch");
            _unregisterAll = Services.PluginInterface.GetIpcSubscriber<string, bool>("FA.UnregisterAll");
            _invoke = Services.PluginInterface.GetIpcSubscriber<string, bool>("FA.Invoke");

            _invoke.Subscribe(OnInvoke);

            Register();
        }
        catch (Exception ex)
        {
            Services.Logger.Debug($"WotsIt not available: {ex.Message}");
        }
    }

    private void Register()
    {
        try
        {
            UnregisterAll();

            _searchGuid = _registerWithSearch?.InvokeFunc(
                Services.PluginInterface.InternalName,
                "AetherBags: Search Inventory",
                "AetherBags Search",
                66472 // Icon ID
            );
        }
        catch (Exception ex)
        {
            Services.Logger.Debug($"Failed to register with WotsIt: {ex.Message}");
        }
    }

    private void OnInvoke(string guid)
    {
        if (guid == _searchGuid)
        {
            if (! System.AddonInventoryWindow.IsOpen)
            {
                System.AddonInventoryWindow.Open();
            }
        }
    }

    private bool UnregisterAll()
    {
        try
        {
            _unregisterAll?.InvokeFunc(Services.PluginInterface.InternalName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _invoke?.Unsubscribe(OnInvoke);
        UnregisterAll();
    }
}