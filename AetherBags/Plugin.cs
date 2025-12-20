using System.Numerics;
using AetherBags.Addons;
using AetherBags.Helpers;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using KamiToolKit;

namespace AetherBags;

public class Plugin : IDalamudPlugin
{
    private static string HelpDescription => "Opens your inventory.";
    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        BackupHelper.DoConfigBackup(pluginInterface);

        KamiToolKitLibrary.Initialize(pluginInterface);

        System.AddonInventoryWindow = new AddonInventoryWindow
        {
            InternalName = "AetherBags",
            Title = "AetherBags",
            Size = new Vector2(750, 750),
        };

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

        if (Services.ClientState.IsLoggedIn) {
            Services.Framework.RunOnFrameworkThread(OnLogin);
        }
    }

    public void Dispose()
    {
        Services.ClientState.Login -= OnLogin;

        Services.CommandManager.RemoveHandler("/aetherbags");
        Services.CommandManager.RemoveHandler("/ab");

        System.AddonInventoryWindow.Dispose();

        KamiToolKitLibrary.Dispose();
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
                    System.AddonInventoryWindow.Toggle();
                break;
        }
    }

    private void OnLogin() {
        #if DEBUG
            System.AddonInventoryWindow.Toggle();
        #endif
    }
}