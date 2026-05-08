using AetherBags.Configuration;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.Keybind;

public sealed class KeybindScrollingAreaNode : ScrollingListNode
{
    public KeybindScrollingAreaNode()
    {
        ItemSpacing = 10;

        AddNode(new KeybindConfigurationNode(System.Config.Keybinds));
    }
}

internal sealed class KeybindConfigurationNode : TabbedVerticalListNode
{
    public KeybindConfigurationNode(KeybindSettings config)
    {
        ItemVerticalSpacing = 2;

        AddNode(new CategoryTextNode
        {
            Height = 18,
            String = "Keybind Configuration",
        });

        AddTab(1);

        AddNode(KeybindOptionNode.Create("Focus Search Bar", config.SearchFocusEnabled, config.SearchFocusKeybind,
            enabled => config.SearchFocusEnabled = enabled,
            kb => config.SearchFocusKeybind = kb,
            showGameConflicts: false));

        AddNode(KeybindOptionNode.Create("Open Bags + Focus Search", config.OpenBagsEnabled, config.OpenBagsKeybind,
            enabled => config.OpenBagsEnabled = enabled,
            kb => config.OpenBagsKeybind = kb));

        AddNode(KeybindOptionNode.Create("Open Saddlebags + Focus Search", config.OpenSaddlebagsEnabled, config.OpenSaddlebagsKeybind,
            enabled => config.OpenSaddlebagsEnabled = enabled,
            kb => config.OpenSaddlebagsKeybind = kb));
    }
}
