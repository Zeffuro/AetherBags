using System.Numerics;
using AetherBags.Configuration;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

public sealed class GeneralScrollingAreaNode : ConfigurationScrollingNode
{
    public GeneralScrollingAreaNode()
    {
        GeneralSettings config = System.Config.General;

        new ImportExportResetNode().AttachNode(this);

        ContentNode.ItemSpacing = 10;

        ContentNode.AddNode(new FunctionalConfigurationNode());

        ContentNode.AddNode(new DtrConfigurationNode());

        ContentNode.AddNode(new CheckboxNode
        {
            Size = new Vector2(300, 20),
            IsVisible = true,
            String = "Debug Mode",
            IsChecked = config.DebugEnabled,
            OnClick = isChecked =>
            {
                config.DebugEnabled = isChecked;
            }
        });
    }
}
