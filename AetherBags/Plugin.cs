using System.Numerics;
using AetherBags.AddonLifecycles;
using AetherBags.Addons;
using AetherBags.Commands;
using AetherBags.Helpers;
using AetherBags.Hooks;
using AetherBags.Inventory;
using Dalamud.Plugin;
using KamiToolKit;

namespace AetherBags;

public unsafe class Plugin : IDalamudPlugin
{
    private static string HelpDescription => "Opens your inventory.";

    private readonly CommandHandler _commandHandler;
    private readonly InventoryHooks _inventoryHooks;
    private readonly InventoryLifecycles _inventoryLifecycles;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        BackupHelper.DoConfigBackup(pluginInterface);

        KamiToolKitLibrary.Initialize(pluginInterface);

        System.Config = Util.LoadConfigOrDefault();

        System.AddonInventoryWindow = new AddonInventoryWindow
        {
            InternalName = "AetherBags",
            Title = "AetherBags",
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

        Services.GameInventory.InventoryChanged += InventoryState.OnRawItemAdded;

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
        Util.SaveConfig(System.Config);

        Services.GameInventory.InventoryChanged -= InventoryState.OnRawItemAdded;

        Services.ClientState.Login -= OnLogin;
        Services.ClientState.Logout -= OnLogout;

        _commandHandler.Dispose();

        System.AddonInventoryWindow.Dispose();
        System.AddonConfigurationWindow.Dispose();

        KamiToolKitLibrary.Dispose();

        _inventoryHooks.Dispose();
        _inventoryLifecycles.Dispose();
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
        System.AddonConfigurationWindow.Close();
    }
}