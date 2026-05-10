using System;
using AetherBags.Addons;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.Category;

public class CategoryConfigurationNode : ConfigNode<CategoryWrapper>
{
    private CategoryDefinitionConfigurationNode? _activeNode;
    private ReadOnlyCategoryDisplayNode? _readOnlyNode;

    public Action? OnCategoryChanged { get; set; }
    public Action<uint>? OnOverrideRequested { get; set; }

    public CategoryConfigurationNode()
    {
    }

    protected override void OptionChanged(CategoryWrapper? option)
    {
        if (option is null)
        {
            HideAll();
            return;
        }

        if (option.Kind == CategoryWrapperKind.GameCategory)
        {
            EnsureReadOnlyNode();
            HideEditor();
            _readOnlyNode!.IsVisible = true;
            _readOnlyNode.Size = Size;
            _readOnlyNode.SetWrapper(option);
            return;
        }

        if (option.CategoryDefinition is null)
        {
            HideAll();
            return;
        }

        EnsureEditor();
        HideReadOnly();
        _activeNode!.IsVisible = true;
        _activeNode.Size = Size;
        _activeNode.SetCategory(option.CategoryDefinition);
    }

    private void EnsureEditor()
    {
        if (_activeNode is not null) return;
        _activeNode = new CategoryDefinitionConfigurationNode
        {
            OnLayoutChanged = RecalculateLayout,
            OnCategoryPropertyChanged = OnCategoryChanged,
            OnCategoryImported = OnCategoryChanged,
        };
        _activeNode.AttachNode(this);
    }

    private void EnsureReadOnlyNode()
    {
        if (_readOnlyNode is not null) return;
        _readOnlyNode = new ReadOnlyCategoryDisplayNode
        {
            OnOverrideRequested = id => OnOverrideRequested?.Invoke(id),
        };
        _readOnlyNode.AttachNode(this);
    }

    private void HideAll()
    {
        HideEditor();
        HideReadOnly();
    }

    private void HideEditor()
    {
        if (_activeNode is not null) _activeNode.IsVisible = false;
    }

    private void HideReadOnly()
    {
        if (_readOnlyNode is not null) _readOnlyNode.IsVisible = false;
    }

    private void RecalculateLayout()
    {
        // Trigger parent layout update if needed
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        if (_activeNode is not null) _activeNode.Size = Size;
        if (_readOnlyNode is not null) _readOnlyNode.Size = Size;
    }
}
