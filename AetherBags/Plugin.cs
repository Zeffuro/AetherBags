using Dalamud.Plugin.Services;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AetherBags.Addons;
using AetherBags.Commands;
using AetherBags.Helpers;
using AetherBags.Hooks;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using AetherBags.IPC;
using AetherBags.IPC.AetherBagsAPI;
using AetherBags.Monitoring;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Configuration.Keybind;
using Dalamud.IoC;
using Dalamud.Plugin;
using ThreadSafety = Dalamud.Utility.ThreadSafety;
using KamiToolKit;
using KamiToolKit.BaseTypes;
using Lumina.Excel.Sheets;

namespace AetherBags;

public class Plugin : IAsyncDalamudPlugin
{
    private CommandHandler? _commandHandler;
    private InventoryHooks? _inventoryHooks;
    private GlobalKeybindHandler? _globalKeybindHandler;
    private InventoryMonitor? inventoryMonitor;
    private bool _isDisposed;
    private bool _loadStarted;
    private bool _toolkitInitializationStarted;

    internal static bool IsUnloading { get; private set; }

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; set; } = null!;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _loadStarted = true;
        IsUnloading = false;

        try
        {
            System.Config = Util.LoadConfigOrDefault();
            BackupHelper.DoConfigBackup(PluginInterface);

            _toolkitInitializationStarted = true;
            await KamiToolKitLibrary.InitializeAsync(PluginInterface);
            cancellationToken.ThrowIfCancellationRequested();
            await IFramework.Get().RunOnFrameworkThread(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Initialize();
            });
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    private void Initialize()
    {
        InventoryOrchestrator.Enable();
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

        System.CategoryConfigurationWindow = new AddonCategoryConfigurationWindow
        {
            InternalName = "AetherBags_CategoryConfig",
            Title = "Category Configuration Window",
            Size = new Vector2(700, 500),
        };

        var items = IDataManager.Get().GetExcelSheet<Item>()
            .Where(item => item.RowId > 0 && !item.Name.IsEmpty).ToList();
        System.ItemFilterPicker = new AddonItemPicker
        {
            InternalName = "Aetherbags_ItemPicker",
            Title = "Select Items to Add",
            OptionsList = items,
        };
        System.ItemOrderPicker = new AddonItemPicker
        {
            InternalName = "Aetherbags_OrderItemPicker",
            Title = "Select Items to Order",
            OptionsList = items,
        };
        System.CurrencyPicker = new AddonCurrencyPicker
        {
            InternalName = "AetherBags_CurrencyPicker",
            Title = "Select Currency to Add",
        };
        System.UICategoryPicker = new AddonUICategoryPicker
        {
            InternalName = "Aetherbags_CategoryPicker",
            Title = "Select Categories to Add",
            OptionsList = IDataManager.Get().GetExcelSheet<ItemUICategory>()
                .Where(category => category.RowId > 0).ToList(),
        };

        PluginInterface.UiBuilder.OpenMainUi += ToggleConfiguration;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfiguration;

        System.AetherBagsAPI = new AetherBagsIPCProvider();

        _commandHandler = new CommandHandler();

        IClientState.Get().Login += OnLogin;
        IClientState.Get().Logout += OnLogout;

        if (IClientState.Get().IsLoggedIn) {
            OnLogin();
        }

        _inventoryHooks = new InventoryHooks();
        _globalKeybindHandler = new GlobalKeybindHandler();
        System.DtrService = new DtrService();
        inventoryMonitor = new InventoryMonitor();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsUnloading = true;
        InventoryOrchestrator.Disable();
        if (!_loadStarted) return;

        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfiguration;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfiguration;
        IClientState.Get().Login -= OnLogin;
        IClientState.Get().Logout -= OnLogout;

        await IFramework.Get().RunOnFrameworkThread(() =>
        {
            _commandHandler?.Dispose();
            inventoryMonitor?.Dispose();
            _globalKeybindHandler?.Dispose();
            _inventoryHooks?.Dispose();
            System.DtrService?.Dispose();
            InventoryAddonContextMenu.Close();
            ItemContextMenuHandler.Dispose();
            System.AetherBagsAPI?.Dispose();
            System.IPC?.Dispose();
        }).ConfigureAwait(false);

        if (_toolkitInitializationStarted)
        {
            if (ThreadSafety.IsMainThread)
                await Task.Run(DisposeAddonsAsync).ConfigureAwait(false);
            else
                await DisposeAddonsAsync().ConfigureAwait(false);
        }

        await IFramework.Get().RunOnFrameworkThread(() =>
        {
            System.LootedItemsTracker?.Dispose();
            HighlightState.ClearAll();
            RegexCache.Clear();
            if (System.Config is not null) Util.SaveConfig(System.Config);

        }).ConfigureAwait(false);

        if (_toolkitInitializationStarted)
            await KamiToolKitLibrary.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task DisposeAddonsAsync()
    {
        await ColorInputRow.DisposeSharedColorPickerAsync().ConfigureAwait(false);
        await KeybindOptionNode.DisposeDialogsAsync().ConfigureAwait(false);
        await DisposeAddonAsync(System.ItemFilterPicker).ConfigureAwait(false);
        await DisposeAddonAsync(System.ItemOrderPicker).ConfigureAwait(false);
        await DisposeAddonAsync(System.CurrencyPicker).ConfigureAwait(false);
        await DisposeAddonAsync(System.UICategoryPicker).ConfigureAwait(false);
        await DisposeAddonAsync(System.CategoryConfigurationWindow).ConfigureAwait(false);
        await DisposeAddonAsync(System.AddonConfigurationWindow).ConfigureAwait(false);
        await DisposeAddonAsync(System.AddonChangelogWindow).ConfigureAwait(false);
        await DisposeAddonAsync(System.AddonInventoryWindow).ConfigureAwait(false);
        await DisposeAddonAsync(System.AddonSaddleBagWindow).ConfigureAwait(false);
        await DisposeAddonAsync(System.AddonRetainerWindow).ConfigureAwait(false);
    }

    private static async Task DisposeAddonAsync(NativeAddon? addon)
    {
        if (addon is not null) await addon.DisposeAsync().ConfigureAwait(false);
    }

    private void ToggleConfiguration()
    {
        if (!_isDisposed) System.AddonConfigurationWindow.Toggle();
    }

    private void OnLogin()
    {
        if (_isDisposed) return;

        System.Config = Util.LoadConfigOrDefault();
        System.IPC.RefreshExternalSources();
        System.LootedItemsTracker.Enable();

        System.AddonInventoryWindow.DebugOpen();
        System.AddonConfigurationWindow.DebugOpen();
    }

    private void OnLogout(int type, int code)
    {
        if (_isDisposed) return;

        Util.SaveConfig(System.Config);
        System.LootedItemsTracker.Disable();
        System.AddonInventoryWindow.Close();
        System.AddonSaddleBagWindow.Close();
        System.AddonRetainerWindow.Close();
        System.AddonConfigurationWindow.Close();
        System.AddonChangelogWindow.Close();
    }
}
