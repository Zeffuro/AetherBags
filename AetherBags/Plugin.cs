using System.Numerics;
using AetherBags.Addons;
using AetherBags.Configuration;
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

        System.Config = Util.LoadConfigOrDefault();

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
        Services.ClientState.Logout += OnLogout;

        if (Services.ClientState.IsLoggedIn) {
            Services.Framework.RunOnFrameworkThread(OnLogin);
        }
    }

    public void Dispose()
    {
        Util.SaveConfig(System.Config);

        Services.ClientState.Login -= OnLogin;
        Services.ClientState.Logout -= OnLogout;

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

    private void OnLogin()
    {
        System.Config = Util.LoadConfigOrDefault();

        #if DEBUG
            System.AddonInventoryWindow.Toggle();
        #endif
    }

    private void OnLogout(int type, int code)
    {
        Util.SaveConfig(System.Config);
        System.AddonInventoryWindow.Close();
    }
}