using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Layout;

public sealed class LayoutScrollingAreaNode : ConfigurationScrollingNode
{
    private readonly LayoutConfigurationNode _layoutNode;

    public LayoutScrollingAreaNode()
    {
        ContentNode.ItemSpacing = 10;
        _layoutNode = new LayoutConfigurationNode(RefreshLayout);
        ContentNode.AddNode(_layoutNode);
        ContentNode.AddNode(new ResNode { Height = 16 });
    }

    internal void SelectSource(int source)
    {
        _layoutNode.SelectSource(source);
        ScrollToStart();
    }
}
