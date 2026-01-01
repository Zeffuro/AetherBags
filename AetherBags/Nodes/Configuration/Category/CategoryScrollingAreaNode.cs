using System.Numerics;
using AetherBags.Addons;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryScrollingAreaNode : ScrollingAreaNode<VerticalListNode>
{
    private AddonCategoryConfigurationWindow? _categoryConfigurationAddon;
    private readonly TextButtonNode _categoryConfigurationButtonNode;

    public CategoryScrollingAreaNode()
    {
        InitializeCategoryAddon();

        ContentNode.AddNode(new CategoryGeneralConfigurationNode());

        _categoryConfigurationButtonNode = new TextButtonNode
        {
            Size = new Vector2(300, 28),
            String = "Configure Categories",
            OnClick = () => _categoryConfigurationAddon?.Toggle(),
        };
        ContentNode.AddNode(_categoryConfigurationButtonNode);
    }

    private void InitializeCategoryAddon() {
        if (_categoryConfigurationAddon is not null) return;

        _categoryConfigurationAddon = new AddonCategoryConfigurationWindow {
            Size = new Vector2(700.0f, 500.0f),
            InternalName = "AetherBags_CategoryConfig",
            Title = "Category Configuration Window",
        };
    }
}