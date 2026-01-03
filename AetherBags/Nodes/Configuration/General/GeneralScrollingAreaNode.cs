using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Nodes.Configuration.Layout;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

public sealed class GeneralScrollingAreaNode : ScrollingListNode
{
    public GeneralScrollingAreaNode()
    {
        GeneralSettings config = System.Config.General;

        new ImportExportResetNode().AttachNode(this);

        ItemSpacing = 10;

        AddNode(new FunctionalConfigurationNode());

        AddNode(new LayoutConfigurationNode());

        AddNode(new CheckboxNode
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