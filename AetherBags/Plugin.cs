using System.Numerics;
using System.Threading.Tasks;
using AetherBags.Addons;
using AetherBags.Commands;
using AetherBags.Helpers;
using AetherBags.Hooks;
using AetherBags.Inventory.Context;
using AetherBags.IPC;
using AetherBags.IPC.AetherBagsAPI;
using AetherBags.Monitoring;
using Dalamud.Plugin;
using KamiToolKit;

namespace AetherBags;

public class Plugin : IDalamudPlugin
{
    private readonly CommandHandler _commandHandler;
    private readonly InventoryHooks _inventoryHooks;
    private readonly GlobalKeybindHandler _globalKeybindHandler;
    private readonly InventoryMonitor inventoryMonitor;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        System.Config = Util.LoadConfigOrDefault();

        BackupHelper.DoConfigBackup(pluginInterface);

        KamiToolKitLibrary.Initialize(pluginInterface);

        System.IPC = new IPCService();
        System.IPC.RefreshExternalSources();
        ItemContextMenuHandler.Initialize();
        System.LootedItemsTracker = new LootedItemsTracker();

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
            InternalName = "AetherBags_Config",
            Title = "AetherBags Config",
            Size = new Vector2(640, 512),
        };

        System.AddonChangelogWindow = new AddonChangelogWindow
        {
            InternalName = "AetherBags_Changelog",
            Title = "AetherBags Changelog",
            Size = new Vector2(640, 512),
        };

        Services.PluginInterface.UiBuilder.OpenMainUi += () => Task.Run(System.AddonConfigurationWindow.ToggleAsync);
        Services.PluginInterface.UiBuilder.OpenConfigUi += () => Task.Run(System.AddonConfigurationWindow.ToggleAsync);

        System.AetherBagsAPI = new AetherBagsIPCProvider();

        _commandHandler = new CommandHandler();

        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;

        if (Services.ClientState.IsLoggedIn) {
            Services.Framework.RunOnFrameworkThread(OnLogin);
        }

        _inventoryHooks = new InventoryHooks();
        _globalKeybindHandler = new GlobalKeybindHandler();
        System.DtrService = new DtrService();
        inventoryMonitor = new InventoryMonitor();
    }

    public void Dispose()
    {
        RegexCache.Clear();

        InventoryAddonContextMenu.Close();
        ItemContextMenuHandler.Dispose();
        _inventoryHooks.Dispose();
        _globalKeybindHandler.Dispose();
        System.DtrService.Dispose();
        inventoryMonitor.Dispose();

        System.LootedItemsTracker.Dispose();
        System.AetherBagsAPI?.Dispose();
        System.IPC.Dispose();
        HighlightState.ClearAll();

        System.AddonInventoryWindow.Dispose();
        System.AddonSaddleBagWindow.Dispose();
        System.AddonRetainerWindow.Dispose();
        System.AddonConfigurationWindow.Dispose();
        System.AddonChangelogWindow.Dispose();

        Util.SaveConfig(System.Config);
        KamiToolKitLibrary.Dispose();
    }

    private void OnLogin()
    {
        System.Config = Util.LoadConfigOrDefault();
        System.IPC.RefreshExternalSources();
        System.LootedItemsTracker.Enable();

        System.AddonInventoryWindow.DebugOpen();
        System.AddonConfigurationWindow.DebugOpen();
    }

    private void OnLogout(int type, int code)
    {
        Util.SaveConfig(System.Config);
        System.LootedItemsTracker.Disable();
        System.AddonInventoryWindow.Close();
        System.AddonSaddleBagWindow.Close();
        System.AddonRetainerWindow.Close();
        System.AddonConfigurationWindow.Close();
        System.AddonChangelogWindow.Close();
    }
}