using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Nodes.Configuration.Category;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Nodes;

namespace AetherBags.Addons;

public class AddonCategoryConfigurationWindow : NativeAddon
{
    private ModifyListNode<CategoryWrapper>? _selectionListNode;
    private VerticalLineNode? _separatorLine;
    private CategoryConfigurationNode? _configNode;
    private TextNode? _nothingSelectedTextNode;

    private List<CategoryWrapper> _categoryWrappers = new();

    private bool _suppressSelectionListRefresh;
    private bool _pendingSelectionListRefresh;

    protected override unsafe void OnSetup(AtkUnitBase* addon)
    {
        _categoryWrappers = CreateCategoryWrappers();

        _selectionListNode = new ModifyListNode<CategoryWrapper>
        {
            Position = ContentStartPosition,
            Size = new Vector2(250.0f, ContentSize.Y),
            SelectionOptions = _categoryWrappers,
            OnOptionChanged = OnOptionChanged,
            AddNewEntry = OnAddNewCategory,
            RemoveEntry = OnRemoveCategory,
        };
        _selectionListNode.AttachNode(this);

        _separatorLine = new VerticalLineNode
        {
            Position = ContentStartPosition + new Vector2(250.0f + 8.0f, 0.0f),
            Size = new Vector2(4.0f, ContentSize.Y),
        };
        _separatorLine.AttachNode(this);

        _nothingSelectedTextNode = new TextNode
        {
            Position = ContentStartPosition + new Vector2(250.0f + 16.0f, 0.0f),
            Size = ContentSize - new Vector2(250.0f + 16.0f, 0.0f),
            AlignmentType = AlignmentType.Center,
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
            FontSize = 14,
            LineSpacing = 22,
            FontType = FontType.Axis,
            String = "Please select a category on the left or add one.",
            TextColor = ColorHelper.GetColor(1),
        };
        _nothingSelectedTextNode.AttachNode(this);

        _configNode = new CategoryConfigurationNode
        {
            Position = ContentStartPosition + new Vector2(250.0f + 16.0f, 0.0f),
            Size = ContentSize - new Vector2(250.0f + 16.0f, 0.0f),
            IsVisible = false,
            OnCategoryChanged = RefreshSelectionList,
        };

        _configNode.AttachNode(this);
    }

    private List<CategoryWrapper> CreateCategoryWrappers()
    {
        return System.Config.Categories.UserCategories
            .Select(categoryDefinition => new CategoryWrapper(categoryDefinition))
            .ToList();
    }

    private void OnOptionChanged(CategoryWrapper?  newOption)
    {
        if (_configNode is null) return;

        _suppressSelectionListRefresh = true;
        try
        {
            _configNode.IsVisible = newOption is not null;

            if (_nothingSelectedTextNode is not null)
                _nothingSelectedTextNode.IsVisible = newOption is null;

            _configNode.ConfigurationOption = newOption;
        }
        finally
        {
            _suppressSelectionListRefresh = false;

            if (_pendingSelectionListRefresh)
            {
                _pendingSelectionListRefresh = false;
                _selectionListNode?.UpdateList();
            }
        }
    }

    private void OnAddNewCategory(ModifyListNode<CategoryWrapper> listNode)
    {
        var newCategory = new UserCategoryDefinition
        {
            Name = $"New Category {System.Config.Categories.UserCategories.Count + 1}",
            Order = System.Config.Categories.UserCategories.Count,
        };

        System.Config.Categories.UserCategories.Add(newCategory);

        var newWrapper = new CategoryWrapper(newCategory);
        _categoryWrappers.Add(newWrapper);
        listNode.AddOption(newWrapper);

        RefreshSelectionList();
        System.AddonInventoryWindow.ManualInventoryRefresh();
    }

    private void OnRemoveCategory(CategoryWrapper categoryWrapper)
    {
        if (categoryWrapper.CategoryDefinition is null) return;

        System.Config.Categories.UserCategories.Remove(categoryWrapper.CategoryDefinition);
        _categoryWrappers.Remove(categoryWrapper);

        RefreshSelectionList();

        if (_configNode is not null && ReferenceEquals(_configNode.ConfigurationOption, categoryWrapper))
        {
            OnOptionChanged(null);
        }
        System.AddonInventoryWindow.ManualInventoryRefresh();
    }

    private void RefreshSelectionList()
    {
        if (_suppressSelectionListRefresh)
        {
            _pendingSelectionListRefresh = true;
            return;
        }

        _selectionListNode?.UpdateList();
    }
}