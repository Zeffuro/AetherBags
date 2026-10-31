using Dalamud.Plugin.Services;
using System;
using Dalamud.Plugin.Ipc;

namespace AetherBags.IPC;

public class WotsItIPC : IDisposable
{
    private bool _isDisposed;
    private ICallGateSubscriber<string, string, string, uint, string>? _registerWithSearch;
    private ICallGateSubscriber<string, bool>? _invoke;
    private ICallGateSubscriber<string, bool>? _unregisterAll;

    private string? _searchGuid;

    public WotsItIPC()
    {
        try
        {
            _registerWithSearch = Plugin.PluginInterface.GetIpcSubscriber<string, string, string, uint, string>("FA.RegisterWithSearch");
            _unregisterAll = Plugin.PluginInterface.GetIpcSubscriber<string, bool>("FA.UnregisterAll");
            _invoke = Plugin.PluginInterface.GetIpcSubscriber<string, bool>("FA.Invoke");

            _invoke.Subscribe(OnInvoke);

            Register();
        }
        catch (Exception ex)
        {
            IPluginLog.Get().DebugOnly($"WotsIt not available: {ex.Message}");
        }
    }

    private void Register()
    {
        try
        {
            UnregisterAll();

            _searchGuid = _registerWithSearch?.InvokeFunc(
                Plugin.PluginInterface.InternalName,
                "AetherBags: Search Inventory",
                "AetherBags Search",
                66472 // Icon ID
            );
        }
        catch (Exception ex)
        {
            IPluginLog.Get().DebugOnly($"Failed to register with WotsIt: {ex.Message}");
        }
    }

    private void OnInvoke(string guid)
    {
        if (!_isDisposed && guid == _searchGuid)
        {
            IFramework.Get().RunOnFrameworkThread(() =>
            {
                if (!_isDisposed && !System.AddonInventoryWindow.IsOpen)
                    System.AddonInventoryWindow.Open();
            });
        }
    }

    private bool UnregisterAll()
    {
        try
        {
            _unregisterAll?.InvokeFunc(Plugin.PluginInterface.InternalName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _invoke?.Unsubscribe(OnInvoke);
        UnregisterAll();
    }
}
