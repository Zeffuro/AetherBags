using System.Numerics;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Currency;

public sealed class CurrencyScrollingAreaNode : ConfigurationScrollingNode
{
    public CurrencyScrollingAreaNode()
    {
        ContentNode.AddNode(new CurrencyGeneralConfigurationNode
        {
            Width = 600,
            OnLayoutChanged = RefreshLayout
        });
    }
}