using AetherBags.Nodes.Configuration.Currency;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration;

public sealed class CurrencyScrollingAreaNode : ScrollingAreaNode<VerticalListNode>
{
    public CurrencyScrollingAreaNode()
    {
        ContentNode.AddNode(new CurrencyConfigurationNode
        {
            Size = Size
        });
    }
}