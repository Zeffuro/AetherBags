using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Currency;

public sealed class CurrencyScrollingAreaNode : ScrollingAreaNode<VerticalListNode>
{
    public CurrencyScrollingAreaNode()
    {
        ContentNode.AddNode(new CurrencyGeneralConfigurationNode
        {
            Size = Size
        });
    }
}