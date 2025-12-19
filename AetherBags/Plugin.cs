using Dalamud.Plugin;
using Dalamud.Game.Command;

namespace AetherBags;

public class Plugin : IDalamudPlugin
{
    public const string CommandName = "/aetherbags";

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = ""
        });
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
    }