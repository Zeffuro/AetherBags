using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using KamiToolKit.Premade.Node.Simple;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryGeneralConfigurationNode : VerticalListNode
{
    private const float TabSize = 18.0f;

    public Action? OnLayoutChanged { get; init; }

    private readonly List<IndentedConfigurationRowNode> _indentedRows = new();
    private readonly CheckboxNode _allaganToolsCheckbox;

    public CategoryGeneralConfigurationNode()
    {
        CategorySettings config = System.Config.Categories;
        config.NormalizeCategorySourceDisplayOrder();
        config.NormalizeItemSortSettings();

        FitContents = true;
        ItemSpacing = 2;

        LabelTextNode titleNode = new LabelTextNode
        {
            Size = Size with { Y = 18 },
            String = "Category Configuration",
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(0),
        };
        AddIndented(titleNode);

        CheckboxNode categoriesEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Categories Enabled",
            IsChecked = config.CategoriesEnabled,
            OnClick = isChecked =>
            {
                config.CategoriesEnabled = isChecked;
                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };
        AddIndented(categoriesEnabled, 1);

        CheckboxNode gameCategoriesEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Game Categories",
            IsChecked = config.GameCategoriesEnabled,
            TextTooltip = "Use the game's built-in item categories (e.g., Arms, Tools, Armor).",
            OnClick = isChecked =>
            {
                config.GameCategoriesEnabled = isChecked;
                RefreshInventory();
            }
        };
        AddIndented(gameCategoriesEnabled, 2);

        CheckboxNode userCategoriesEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "User Categories",
            IsChecked = config.UserCategoriesEnabled,
            TextTooltip = "Use your custom-defined categories.",
            OnClick = isChecked =>
            {
                config.UserCategoriesEnabled = isChecked;
                RefreshInventory();
            }
        };
        AddIndented(userCategoriesEnabled, 2);

        bool bisBuddyReady = System.IPC.BisBuddy?.IsReady ?? false;

        LabeledEnumDropdownNode<PluginFilterMode>? bbModeDropdown = new LabeledEnumDropdownNode<PluginFilterMode>
        {
            Size = new Vector2(500, 20),
            LabelText = "Filter Display Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            IsEnabled = config.BisBuddyEnabled && bisBuddyReady,
            Options = Enum.GetValues<PluginFilterMode>().ToList(),
            SelectedOption = config.BisBuddyMode,
            OnOptionSelected = selected =>
            {
                config.BisBuddyMode = selected;
                if (selected == PluginFilterMode.Categorize)
                    HighlightState.ClearFilter(HighlightSource.BiSBuddy);

                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };

        CheckboxNode bisBuddyEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = bisBuddyReady ? "BISBuddy" : "BISBuddy (Not Available)",
            IsChecked = config.BisBuddyEnabled,
            TextTooltip = "Allow BISBuddy to highlight items.",
            OnClick = isChecked =>
            {
                config.BisBuddyEnabled = isChecked;
                if (bbModeDropdown != null) bbModeDropdown.IsEnabled = isChecked;
                if (isChecked)
                    System.IPC.BisBuddy?.RefreshItems();
                else
                    HighlightState.ClearLabel(HighlightSource.BiSBuddy);
                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };
        AddIndented(bisBuddyEnabled, 2);
        AddIndented(bbModeDropdown, 3);

        bool allaganReady = System.IPC.AllaganTools?.IsReady ?? false;

        LabeledEnumDropdownNode<PluginFilterMode>? atModeDropdown = new LabeledEnumDropdownNode<PluginFilterMode>
        {
            Size = new Vector2(500, 20),
            LabelText = "Filter Display Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            IsEnabled = config.AllaganToolsCategoriesEnabled && allaganReady,
            Options = Enum.GetValues<PluginFilterMode>().ToList(),
            SelectedOption = config.AllaganToolsFilterMode,
            OnOptionSelected = selected =>
            {
                config.AllaganToolsFilterMode = selected;
                if (selected == PluginFilterMode.Categorize)
                {
                    HighlightState.ClearFilter(HighlightSource.AllaganTools);
                }

                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };

        _allaganToolsCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = allaganReady ?  "Allagan Tools Filters" : "Allagan Tools Filters (Not Available)",
            IsChecked = config.AllaganToolsCategoriesEnabled,
            IsEnabled = allaganReady,
            TextTooltip = allaganReady
                ? "Use search filters from Allagan Tools as categories. Items matching a filter will be grouped together."
                : "Allagan Tools is not installed or not initialized.",
            OnClick = isChecked =>
            {
                config.AllaganToolsCategoriesEnabled = isChecked;
                if (atModeDropdown != null) atModeDropdown.IsEnabled = isChecked;
                if (isChecked)
                    System.IPC?.AllaganTools?.RefreshFilters();
                else
                    HighlightState.ClearLabel(HighlightSource.AllaganTools);
                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };
        AddIndented(_allaganToolsCheckbox, 2);

        AddIndented(atModeDropdown, 3);

        CheckboxNode blankMiscCategoryName = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Show uncategorized section without a label",
            IsChecked = config.BlankMiscCategoryName,
            TextTooltip = "When items are collected into the fallback Misc section, hide the 'Misc' title and count.",
            OnClick = isChecked =>
            {
                config.BlankMiscCategoryName = isChecked;
                RefreshInventory();
            }
        };
        AddIndented(blankMiscCategoryName, 2);

        AddIndented(new ResNode { Height = 8 });

        var defaultItemSortEditor = new ItemSortCriteriaEditorNode("Default Item Sort Priority:", allowUseGlobal: false, allowCustomOrder: false);
        defaultItemSortEditor.OnChanged = () =>
        {
            config.DefaultItemSortCriteria = defaultItemSortEditor.GetCriteria();
            RefreshInventory();
        };
        defaultItemSortEditor.OnLayoutChanged = HandleLayoutChanged;
        defaultItemSortEditor.SetCriteria(config.DefaultItemSortCriteria);
        config.DefaultItemSortCriteria = defaultItemSortEditor.GetCriteria();
        AddIndented(defaultItemSortEditor, 1);

        AddIndented(new ResNode { Height = 8 });

        CategorySourceOrderEditorNode? categorySourceOrderEditor = null;
        var editor = categorySourceOrderEditor;
        categorySourceOrderEditor = new CategorySourceOrderEditorNode
        {
            OnChanged = () =>
            {
                config.CategorySourceDisplayOrder = editor!.GetOrder();
                RefreshInventory();
            }
        };
        categorySourceOrderEditor.SetOrder(config.CategorySourceDisplayOrder);
        AddIndented(categorySourceOrderEditor, 1);
    }

    private void AddIndented(NodeBase? node, int tabIndex = 0)
    {
        if (node is null) return;

        if (tabIndex <= 0)
        {
            AddNode(node);
            return;
        }

        float indent = tabIndex * TabSize;
        var row = new IndentedConfigurationRowNode(node, indent);
        row.RecalculateLayout();
        _indentedRows.Add(row);
        AddNode(row);
    }

    private void HandleLayoutChanged()
    {
        foreach (var row in _indentedRows)
            row.RecalculateLayout();

        RecalculateLayout();
        OnLayoutChanged?.Invoke();
    }

    private void RefreshInventory() => InventoryOrchestrator.RefreshAll(updateMaps: true);
}

public sealed class IndentedConfigurationRowNode : SimpleComponentNode
{
    private readonly NodeBase _content;
    private readonly float _indent;

    public IndentedConfigurationRowNode(NodeBase content, float indent)
    {
        _content = content;
        _indent = indent;

        content.AttachNode(this);
        RecalculateLayout();
    }

    public void RecalculateLayout()
    {
        if (_content is LayoutListNode layoutListNode)
            layoutListNode.RecalculateLayout();

        _content.Position = new Vector2(_indent, 0.0f);
        Size = new Vector2(_indent + _content.Width, _content.Height);
    }
}
