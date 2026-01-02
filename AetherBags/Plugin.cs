using System.Numerics;
using AetherBags.AddonLifecycles;
using AetherBags.Addons;
using AetherBags.Commands;
using AetherBags.Helpers;
using AetherBags.Hooks;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.State;
using AetherBags.IPC;
using Dalamud.Game.Gui;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit;

namespace AetherBags;

public unsafe class Plugin : IDalamudPlugin
{
    private readonly CommandHandler _commandHandler;
    private readonly InventoryHooks _inventoryHooks;
    private readonly InventoryLifecycles _inventoryLifecycles;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        System.Config = Util.LoadConfigOrDefault();

        BackupHelper.DoConfigBackup(pluginInterface);

        KamiToolKitLibrary.Initialize(pluginInterface);

        System.IPC = new IPCService();

        System.AddonInventoryWindow = new AddonInventoryWindow
        {
            InternalName = "AetherBags_MainBags",
            Title = "AetherBags",
            Size = new Vector2(750, 750),
        };

        System.AddonSaddleBagWindow = new AddonSaddleBagWindow
        {
            InternalName = "AetherBags_SaddleBag",
            Title = "AetherSaddlebag",
            Size = new Vector2(750, 750),
        };

        System.AddonRetainerWindow = new AddonRetainerWindow
        {
            InternalName = "AetherBags_Retainer",
            Title = "AetherRetainerbag",
            Size = new Vector2(750, 750),
        };

        System.AddonConfigurationWindow = new AddonConfigurationWindow
        {
            InternalName = "AetherBags Config",
            Title = "AetherBags Config",
            Size = new Vector2(640, 512),
        };

        Services.PluginInterface.UiBuilder.OpenMainUi += System.AddonInventoryWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenConfigUi += System.AddonConfigurationWindow.Toggle;

        _commandHandler = new CommandHandler();

        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;

        if (Services.ClientState.IsLoggedIn) {
            Services.Framework.RunOnFrameworkThread(OnLogin);
        }

        _inventoryHooks = new InventoryHooks();
        _inventoryLifecycles = new InventoryLifecycles();
    }

    public void Dispose()
    {
        InventoryAddonContextMenu.Close();
        _inventoryHooks.Dispose();
        _inventoryLifecycles.Dispose();

        System.IPC.Dispose();
        HighlightState.ClearAll();

        System.AddonInventoryWindow.Dispose();
        System.AddonSaddleBagWindow.Dispose();
        System.AddonRetainerWindow.Dispose();
        System.AddonConfigurationWindow.Dispose();

        Util.SaveConfig(System.Config);
        KamiToolKitLibrary.Dispose();
    }

    private void OnLogin()
    {
        System.Config = Util.LoadConfigOrDefault();
        InventoryState.TrackLootedItems = true;

#if DEBUG
        System.AddonInventoryWindow.Toggle();
        System.AddonConfigurationWindow.Toggle();
#endif
    }

    private void OnLogout(int type, int code)
    {
        Util.SaveConfig(System.Config);
        InventoryState.TrackLootedItems = false;
        System.AddonInventoryWindow.Close();
        System.AddonSaddleBagWindow.Close();
        System.AddonRetainerWindow.Close();
        System.AddonConfigurationWindow.Close();
    }
}