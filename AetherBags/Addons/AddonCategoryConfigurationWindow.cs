using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Configuration.Category;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonCategoryConfigurationWindow : NativeAddon
{
    private const float LeftColumnWidth = 250.0f;

    private ListNode<CategoryWrapper, CategoryListItemNode>? _selectionListNode;
    private SearchInputNode? _searchInputNode;
    private TextButtonNode? _removeButton;
    private TextButtonNode? _sortDirectionNode;
    private CategoryWrapper? _selectedCategory;
    private bool _reversed;
    private VerticalLineNode? _separatorLine;
    private CategoryConfigurationNode? _configNode;
    private TextNode? _nothingSelectedTextNode;

    private List<CategoryWrapper> _categoryWrappers = new();

    private bool _suppressSelectionListRefresh;
    private bool _pendingSelectionListRefresh;
    private bool _selectionListRefreshQueued;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        _categoryWrappers = CreateCategoryWrappers();

        _searchInputNode = new SearchInputNode
        {
            Position = ContentStartPosition,
            Size = new Vector2(LeftColumnWidth, 26),
            OnInputReceived = _ => ApplyCategoryFilter(),
        };
        _searchInputNode.AttachNode(this);

        _reversed = false;
        _sortDirectionNode = new TextButtonNode
        {
            Position = ContentStartPosition + new Vector2(0, 34),
            Size = new Vector2(LeftColumnWidth, 26),
            String = "Ascending",
            OnClick = () =>
            {
                _reversed = !_reversed;
                _sortDirectionNode!.String = _reversed ? "Descending" : "Ascending";
                ApplyCategoryFilter();
            },
        };
        _sortDirectionNode.AttachNode(this);

        _selectionListNode = new ListNode<CategoryWrapper, CategoryListItemNode>
        {
            Position = ContentStartPosition + new Vector2(0, 68),
            Size = new Vector2(LeftColumnWidth, ContentSize.Y - 102),
            OptionsList = GetFilteredCategories(),
            OnItemSelected = OnOptionChanged,
            ItemSpacing = 2,
        };
        _selectionListNode.AttachNode(this);

        new TextButtonNode
        {
            Position = ContentStartPosition + new Vector2(0, ContentSize.Y - 26),
            Size = new Vector2(120, 26),
            String = "Add",
            OnClick = OnAddNewCategory,
        }.AttachNode(this);

        _removeButton = new TextButtonNode
        {
            Position = ContentStartPosition + new Vector2(130, ContentSize.Y - 26),
            Size = new Vector2(120, 26),
            String = "Remove",
            IsEnabled = false,
            OnClick = () =>
            {
                if (_selectedCategory is { } selected) OnRemoveCategory(selected);
            },
        };
        _removeButton.AttachNode(this);

        _separatorLine = new VerticalLineNode
        {
            Position = ContentStartPosition + new Vector2(250.0f + 8.0f, 0.0f),
            Size = ContentSize with { X = 4.0f },
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
            OnGeneralSettingsChanged = RebuildCategoryList,
            OnOverrideRequested = OverrideGameCategory,
        };

        _configNode.AttachNode(this);

        base.OnSetup(addon, atkValueSpan);
    }

    private static List<CategoryWrapper> CreateCategoryWrappers()
    {
        var wrappers = new List<CategoryWrapper>();
        var categoryConfig = System.Config.Categories;

        wrappers.Add(CategoryWrapper.ForGeneralSettings());

        foreach (var def in categoryConfig.UserCategories)
            wrappers.Add(CategoryWrapper.ForUserOrOverride(def));

        var overriddenByBuiltIn = new HashSet<uint>();
        foreach (var source in IPC.ExternalCategorySystem.ExternalCategoryManager.RegisteredSources)
        {
            if (!source.IsBuiltIn) continue;
            foreach (var id in source.OverriddenGameCategoryIds)
                overriddenByBuiltIn.Add(id);
        }

        foreach (var source in IPC.ExternalCategorySystem.ExternalCategoryManager.RegisteredSources)
        {
            if (source.IsBuiltIn && !categoryConfig.ShowBuiltInSourcesInConfig) continue;
            if (!source.IsBuiltIn && !categoryConfig.ShowExternalSourcesInConfig) continue;
            wrappers.Add(CategoryWrapper.ForExternalSource(source));
        }

        if (categoryConfig.ShowGameCategoriesInConfig)
        {
            var disabledGameIds = categoryConfig.DisabledGameCategoryIds;
            var sheet = IDataManager.Get().GetExcelSheet<ItemUICategory>();
            foreach (var row in sheet)
            {
                if (row.RowId == 0) continue;
                if (disabledGameIds.Contains(row.RowId)) continue;
                if (overriddenByBuiltIn.Contains(row.RowId)) continue;
                var name = row.Name.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                wrappers.Add(CategoryWrapper.ForGameCategory(row.RowId));
            }
        }

        return wrappers;
    }

    private void RebuildCategoryList()
    {
        var refreshed = CreateCategoryWrappers();
        _categoryWrappers.Clear();
        _categoryWrappers.AddRange(refreshed);
        RefreshSelectionList();
    }

    private void OnAddNewCategory()
    {
        var newCategory = new UserCategoryDefinition
        {
            Name = $"New Category {System.Config.Categories.UserCategories.Count + 1}",
            Order = System.Config.Categories.UserCategories.Count,
        };

        System.Config.Categories.UserCategories.Add(newCategory);

        var newWrapper = CategoryWrapper.ForUserOrOverride(newCategory);
        _categoryWrappers.Add(newWrapper);

        RefreshSelectionList();
        InventoryOrchestrator.RearrangeAll();
    }

    private void OverrideGameCategory(uint gameCategoryId)
    {
        if (System.Config.Categories.DisabledGameCategoryIds.Contains(gameCategoryId))
            return;

        var sourceName = CategoryWrapper.ResolveGameCategoryName(gameCategoryId);

        var existingGame = _categoryWrappers.FirstOrDefault(w =>
            w.Kind == CategoryWrapperKind.GameCategory && w.GameCategoryId == gameCategoryId);

        var newCategory = new UserCategoryDefinition
        {
            Name = sourceName,
            Order = System.Config.Categories.UserCategories.Count,
            OverrideSourceKey = CategoryOverrideKey.ForGameCategory(gameCategoryId),
            Rules = new CategoryRuleSet
            {
                AllowedUiCategoryIds = new List<uint> { gameCategoryId },
            },
        };

        System.Config.Categories.UserCategories.Add(newCategory);
        System.Config.Categories.DisabledGameCategoryIds.Add(gameCategoryId);

        var newWrapper = CategoryWrapper.ForUserOrOverride(newCategory);
        _categoryWrappers.Add(newWrapper);
        if (existingGame is not null)
            _categoryWrappers.Remove(existingGame);

        Util.SaveConfig(System.Config);
        RefreshSelectionList();
        InventoryOrchestrator.RearrangeAll();

        OnOptionChanged(newWrapper);
    }

    private void OnOptionChanged(CategoryWrapper? newOption)
    {
        if (_configNode is null) return;

        _suppressSelectionListRefresh = true;
        try
        {
            _configNode.IsVisible = newOption is not null;

            if (_nothingSelectedTextNode is not null)
                _nothingSelectedTextNode.IsVisible = newOption is null;

            _selectedCategory = newOption;
            if (_removeButton is not null)
                _removeButton.IsEnabled = newOption?.Kind is CategoryWrapperKind.User or CategoryWrapperKind.Override;
            _configNode.SelectEntry(newOption);
        }
        finally
        {
            _suppressSelectionListRefresh = false;

            if (_pendingSelectionListRefresh)
            {
                _pendingSelectionListRefresh = false;
                RefreshSelectionList();
            }
        }
    }

    private void OnRemoveCategory(CategoryWrapper categoryWrapper)
    {
        if (categoryWrapper.Kind == CategoryWrapperKind.GeneralSettings) return;
        if (categoryWrapper.Kind == CategoryWrapperKind.GameCategory) return;
        if (categoryWrapper.Kind == CategoryWrapperKind.ExternalSource) return;
        if (categoryWrapper.CategoryDefinition is null) return;

        if (categoryWrapper.Kind == CategoryWrapperKind.Override
            && CategoryOverrideKey.TryParseGameCategory(categoryWrapper.CategoryDefinition.OverrideSourceKey, out uint gameId))
        {
            System.Config.Categories.DisabledGameCategoryIds.Remove(gameId);
            _categoryWrappers.Add(CategoryWrapper.ForGameCategory(gameId));
        }

        System.Config.Categories.UserCategories.Remove(categoryWrapper.CategoryDefinition);
        _categoryWrappers.Remove(categoryWrapper);

        Util.SaveConfig(System.Config);
        RefreshSelectionList();

        if (_configNode is not null && ReferenceEquals(_selectedCategory, categoryWrapper))
        {
            OnOptionChanged(null);
        }
        InventoryOrchestrator.RearrangeAll();
    }

    private void RefreshSelectionList()
    {
        if (_suppressSelectionListRefresh)
        {
            _pendingSelectionListRefresh = true;
            return;
        }

        QueueSelectionListRefresh();
    }

    private List<CategoryWrapper> GetFilteredCategories()
    {
        var search = _searchInputNode?.String.ExtractText() ?? string.Empty;
        var filtered = _categoryWrappers.Where(category => category.GetLabel().Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        filtered.Sort((left, right) => left.Compare(right) * (_reversed ? -1 : 1));
        return filtered;
    }

    private void QueueSelectionListRefresh()
    {
        if (_selectionListRefreshQueued) return;

        _selectionListRefreshQueued = true;
        IFramework.Get().RunOnTick(() =>
        {
            _selectionListRefreshQueued = false;
            ApplyCategoryFilter();
            if (_selectionListNode is null) return;
            foreach (var row in _selectionListNode.OptionNodes) row.RefreshDisplay();
        });
    }

    private void ApplyCategoryFilter()
    {
        if (_selectionListNode is not null)
            _selectionListNode.OptionsList = GetFilteredCategories();
    }

    protected override unsafe void OnHide(AtkUnitBase* addon)
    {
        ColorInputRow.CancelActiveSession(addon);
        _configNode?.ClosePickers();
        base.OnHide(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        _selectionListRefreshQueued = false;

        _selectionListNode = null;
        _searchInputNode = null;
        _removeButton = null;
        _sortDirectionNode = null;
        _selectedCategory = null;
        _configNode = null;
        _separatorLine = null;
        _nothingSelectedTextNode = null;

        base.OnFinalize(addon);
    }
}
