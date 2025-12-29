using System;
using AetherBags.Addons;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public class CategoryConfigurationNode :  ConfigNode<CategoryWrapper>
{
    private readonly ScrollingAreaNode<VerticalListNode> _categoryList;
    private CategoryDefinitionConfigurationNode? _activeNode;

    public Action? OnCategoryChanged { get; set; }

    public CategoryConfigurationNode()
    {
        _categoryList = new ScrollingAreaNode<VerticalListNode>
        {
            ContentHeight = 100.0f,
            AutoHideScrollBar = true,
        };
        _categoryList.ContentNode.FitContents = true;
        _categoryList.AttachNode(this);
    }

    protected override void OptionChanged(CategoryWrapper? option)
    {
        if (option?.CategoryDefinition is null)
        {
            _categoryList.IsVisible = false;
            return;
        }

        _categoryList.IsVisible = true;

        if (_activeNode is null)
        {
            _activeNode = new CategoryDefinitionConfigurationNode(option.CategoryDefinition)
            {
                Size = _categoryList.ContentNode.Size,
                OnLayoutChanged = UpdateScrollHeight,
                OnCategoryPropertyChanged = OnCategoryChanged,
            };
            _categoryList.ContentNode.AddNode(_activeNode);
        }
        else
        {
            _activeNode.SetCategory(option.CategoryDefinition);
        }

        UpdateScrollHeight();
    }

    private void UpdateScrollHeight()
    {
        _categoryList.ContentNode.RecalculateLayout();
        _categoryList.ContentHeight = _categoryList.ContentNode.Height;
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _categoryList.Size = Size;
        _categoryList.ContentNode.Width = Width;

        foreach (var node in _categoryList.ContentNode.GetNodes<CategoryDefinitionConfigurationNode>())
        {
            node.Width = Width;
        }

        UpdateScrollHeight();
    }
}