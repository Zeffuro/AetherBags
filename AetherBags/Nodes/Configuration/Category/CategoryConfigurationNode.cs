using System;
using AetherBags.Addons;
using KamiToolKit.Premade.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public class CategoryConfigurationNode : ConfigNode<CategoryWrapper>
{
    private CategoryDefinitionConfigurationNode? _activeNode;

    public Action? OnCategoryChanged { get; set; }

    public CategoryConfigurationNode()
    {
    }

    protected override void OptionChanged(CategoryWrapper? option)
    {
        if (option?.CategoryDefinition is null)
        {
            if (_activeNode is not null)
            {
                _activeNode.IsVisible = false;
            }
            return;
        }

        if (_activeNode is null)
        {
            _activeNode = new CategoryDefinitionConfigurationNode
            {
                OnLayoutChanged = RecalculateLayout,
                OnCategoryPropertyChanged = OnCategoryChanged,
            };
            _activeNode.AttachNode(this);
        }

        _activeNode.IsVisible = true;
        _activeNode.Size = Size;
        _activeNode.SetCategory(option.CategoryDefinition);
    }

    private void RecalculateLayout()
    {
        // Trigger parent layout update if needed
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        if (_activeNode is not null)
        {
            _activeNode.Size = Size;
        }
    }
}