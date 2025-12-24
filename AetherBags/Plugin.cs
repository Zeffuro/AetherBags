using System;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Helpers;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using KamiToolKit;

namespace AetherBags;

public unsafe class Plugin : IDalamudPlugin
{
    private static string HelpDescription => "Opens your inventory.";
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

        Services.CommandManager.AddHandler("/aetherbags", new CommandInfo(OnCommand)
        {
            DisplayOrder = 1,
            ShowInHelp = true,
            HelpMessage = HelpDescription
        });
        Services.CommandManager.AddHandler("/ab", new CommandInfo(OnCommand)
        {
            DisplayOrder = 2,
            ShowInHelp = true,
            HelpMessage = HelpDescription
        });
        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;

        if (Services.ClientState.IsLoggedIn) {
            Services.Framework.RunOnFrameworkThread(OnLogin);
        }

        try
        {
            _moveItemSlotHook = Services.GameInteropProvider.HookFromSignature<MoveItemSlotDelegate>("E8 ?? ?? ?? ?? 48 8B 03 66 FF C5", MoveItemSlotDetour);
            _moveItemSlotHook.Enable();

            Services.Logger.Debug("MoveItemSlot hooked successfully.");
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, "Failed to hook MoveItemSlot");
        }
    }

    private unsafe delegate int MoveItemSlotDelegate(InventoryManager* inventoryManager, InventoryType srcContainer, ushort srcSlot, InventoryType dstContainer, ushort dstSlot, bool unk);

    private Hook<MoveItemSlotDelegate>? _moveItemSlotHook;

    private unsafe int MoveItemSlotDetour(InventoryManager* manager, InventoryType srcType, ushort srcSlot, InventoryType dstType, ushort dstSlot, bool unk)
    {
        InventoryItem* sourceItem = InventoryManager.Instance()->GetInventorySlot(srcType, srcSlot);
        InventoryItem* destItem = InventoryManager.Instance()->GetInventorySlot(dstType, dstSlot);
        Services.Logger.Info($"[MoveItemSlot] Moving {srcType}@{srcSlot} ID:{sourceItem->ItemId} -> {dstType}@{dstSlot} ID:{destItem->ItemId}  Unk: {unk}");

        // Call the original function
        return _moveItemSlotHook!.Original(manager, srcType, srcSlot, dstType, dstSlot, unk);
    }

    public void Dispose()
    {
        Util.SaveConfig(System.Config);

        Services.ClientState.Login -= OnLogin;
        Services.ClientState.Logout -= OnLogout;

        Services.CommandManager.RemoveHandler("/aetherbags");
        Services.CommandManager.RemoveHandler("/ab");

        System.AddonInventoryWindow.Dispose();
        System.AddonConfigurationWindow.Dispose();

        KamiToolKitLibrary.Dispose();

        _moveItemSlotHook?.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        switch (command)
        {
            case "/aetherbags":
            case "/ab":
                if(args.Length == 0)
                    System.AddonInventoryWindow.Toggle();
                if(args == "config")
                    System.AddonConfigurationWindow.Toggle();
                if (args == "import-sk")
                {
                    // Manually import from SortaKinda for testing until we have a proper config window
                    ImportExportResetHelper.TryImportSortaKindaFromClipboard(true);
                    System.AddonInventoryWindow.ManualInventoryRefresh();
                }
                break;
        }
    }

    private void OnLogin()
    {
        System.Config = Util.LoadConfigOrDefault();

        #if DEBUG
            System.AddonInventoryWindow.Toggle();
            System.AddonConfigurationWindow.Toggle();
        #endif
    }

    private void OnLogout(int type, int code)
    {
        Util.SaveConfig(System.Config);
        System.AddonInventoryWindow.Close();
    }
}