using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Nodes.Configuration.Category;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public class AddonCategoryConfigurationWindow : NativeAddon
{
    private const float LeftColumnWidth = 250.0f;

    private ModifyListNode<CategoryWrapper, CategoryListItemNode>? _selectionListNode;
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

        _selectionListNode = new ModifyListNode<CategoryWrapper, CategoryListItemNode>
        {
            Position = ContentStartPosition,
            Size = ContentSize with { X = LeftColumnWidth },
            Options = _categoryWrappers,
            SelectionChanged = OnOptionChanged,
            AddNewEntry = OnAddNewCategory,
            RemoveEntry = OnRemoveCategory,
            SortOptions = [ DefaultSortOptions.Alphabetical ],
            ItemComparer = (left, right, mode) => left.Compare(right),
            IsSearchMatch = (data, search) => data.GetLabel().Contains(search, StringComparison.OrdinalIgnoreCase)
        };
        _selectionListNode.AttachNode(this);

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
            var sheet = Services.DataManager.GetExcelSheet<ItemUICategory>();
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
        InventoryOrchestrator.RefreshAll(updateMaps: true);
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
        InventoryOrchestrator.RefreshAll(updateMaps: true);

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

            _configNode.ConfigurationOption = newOption;
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

        if (_configNode is not null && ReferenceEquals(_configNode.ConfigurationOption, categoryWrapper))
        {
            OnOptionChanged(null);
        }
        InventoryOrchestrator.RefreshAll(updateMaps: true);
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

    private void QueueSelectionListRefresh()
    {
        if (_selectionListRefreshQueued) return;

        _selectionListRefreshQueued = true;
        Services.Framework.RunOnTick(() =>
        {
            _selectionListRefreshQueued = false;
            _selectionListNode?.RefreshList();
        });
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        _selectionListRefreshQueued = false;

        _selectionListNode = null;
        _configNode = null;
        _separatorLine = null;
        _nothingSelectedTextNode = null;

        base.OnFinalize(addon);
    }
}