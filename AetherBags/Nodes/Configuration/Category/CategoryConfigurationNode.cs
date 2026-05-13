using System;
using AetherBags.Addons;
using AetherBags.Configuration;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.Category;

public class CategoryConfigurationNode : ConfigNode<CategoryWrapper>
{
    private CategoryDefinitionConfigurationNode? _activeNode;
    private ReadOnlyCategoryDisplayNode? _readOnlyNode;
    private BuiltInSourceEditorNode? _builtInEditorNode;
    private CategoryGeneralSettingsEditorNode? _generalSettingsNode;

    public Action? OnCategoryChanged { get; set; }
    public Action? OnGeneralSettingsChanged { get; set; }
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

        if (option.Kind == CategoryWrapperKind.GeneralSettings)
        {
            EnsureGeneralSettingsNode();
            HideEditor();
            HideReadOnly();
            HideBuiltInEditor();
            _generalSettingsNode!.IsVisible = true;
            _generalSettingsNode.Size = Size;
            _generalSettingsNode.Refresh();
            return;
        }

        if (option.Kind == CategoryWrapperKind.ExternalSource && option.Source is not null)
        {
            EnsureBuiltInEditor();
            HideEditor();
            HideReadOnly();
            HideGeneralSettings();
            _builtInEditorNode!.IsVisible = true;
            _builtInEditorNode.Size = Size;

            var overrides = System.Config.Categories.BuiltInSourceOverrides;
            if (!overrides.TryGetValue(option.Source.SourceName, out var prefs))
            {
                prefs = new BuiltInSourceOverride();
                overrides[option.Source.SourceName] = prefs;
            }
            _builtInEditorNode.SetSource(option.Source, prefs);
            return;
        }

        if (option.Kind == CategoryWrapperKind.GameCategory)
        {
            EnsureReadOnlyNode();
            HideEditor();
            HideBuiltInEditor();
            HideGeneralSettings();
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
        HideBuiltInEditor();
        HideGeneralSettings();
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

    private void EnsureBuiltInEditor()
    {
        if (_builtInEditorNode is not null) return;
        _builtInEditorNode = new BuiltInSourceEditorNode
        {
            OnLayoutChanged = RecalculateLayout,
        };
        _builtInEditorNode.AttachNode(this);
    }

    private void EnsureGeneralSettingsNode()
    {
        if (_generalSettingsNode is not null) return;
        _generalSettingsNode = new CategoryGeneralSettingsEditorNode
        {
            OnSettingsChanged = () => OnGeneralSettingsChanged?.Invoke(),
        };
        _generalSettingsNode.AttachNode(this);
    }

    private void HideAll()
    {
        HideEditor();
        HideReadOnly();
        HideBuiltInEditor();
        HideGeneralSettings();
    }

    private void HideGeneralSettings()
    {
        if (_generalSettingsNode is not null) _generalSettingsNode.IsVisible = false;
    }

    private void HideEditor()
    {
        if (_activeNode is not null) _activeNode.IsVisible = false;
    }

    private void HideReadOnly()
    {
        if (_readOnlyNode is not null) _readOnlyNode.IsVisible = false;
    }

    private void HideBuiltInEditor()
    {
        if (_builtInEditorNode is not null) _builtInEditorNode.IsVisible = false;
    }

    private void RecalculateLayout()
    {
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        if (_activeNode is not null) _activeNode.Size = Size;
        if (_readOnlyNode is not null) _readOnlyNode.Size = Size;
        if (_builtInEditorNode is not null) _builtInEditorNode.Size = Size;
        if (_generalSettingsNode is not null) _generalSettingsNode.Size = Size;
    }
}
