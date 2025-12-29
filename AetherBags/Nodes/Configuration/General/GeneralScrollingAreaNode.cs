using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Nodes.Configuration.Layout;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

public sealed class GeneralScrollingAreaNode : ScrollingAreaNode<VerticalListNode>
{
    private readonly CheckboxNode _debugCheckboxNode = null!;

    public GeneralScrollingAreaNode()
    {
        GeneralSettings config = System.Config.General;

        ContentNode.ItemSpacing = 32;

        ContentNode.AddNode(new FunctionalConfigurationNode());

        ContentNode.AddNode(new LayoutConfigurationNode());

        _debugCheckboxNode = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            IsVisible = true,
            String = "Debug Mode",
            IsChecked = config.DebugEnabled,
            OnClick = isChecked => { config.DebugEnabled = isChecked; }
        };
        ContentNode.AddNode(_debugCheckboxNode);
    }

    private void RefreshInventory() => System.AddonInventoryWindow.ManualInventoryRefresh();
}