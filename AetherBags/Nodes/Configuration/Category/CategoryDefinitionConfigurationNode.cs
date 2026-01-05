using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Nodes.Color;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text;
using Action = System.Action;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryDefinitionConfigurationNode : SimpleComponentNode
{
    private static ExcelSheet<Item>? ItemSheet => Services.DataManager.GetExcelSheet<Item>();
    private static ExcelSheet<ItemUICategory>? UICategorySheet => Services.DataManager.GetExcelSheet<ItemUICategory>();

    public Action? OnLayoutChanged { get; init; }
    public Action? OnCategoryPropertyChanged { get; init; }

    private UserCategoryDefinition _categoryDefinition = new();

    private readonly ScrollingAreaNode<TreeListNode> _scrollingArea;
    private readonly BasicSettingsSection _basicSettings;
    private readonly RangeFiltersSection _rangeFilters;
    private readonly StateFiltersSection _stateFilters;
    private readonly ListFiltersSection _listFilters;

    public CategoryDefinitionConfigurationNode()
    {
        _scrollingArea = new ScrollingAreaNode<TreeListNode>
        {
            ContentHeight = 100.0f,
            AutoHideScrollBar = true,
        };
        _scrollingArea.AttachNode(this);

        _scrollingArea.ContentNode.OnLayoutUpdate = newHeight =>
        {
            _scrollingArea.ContentHeight = newHeight;
        };

        _scrollingArea.ContentNode.CategoryVerticalSpacing = 4.0f;

        var treeListNode = _scrollingArea.ContentAreaNode;

        _basicSettings = new BasicSettingsSection(() => _categoryDefinition)
        {
            String = "Basic Settings",
            IsCollapsed = false,
            OnPropertyChanged = () =>
            {
                NotifyChanged();
                NotifyCategoryPropertyChanged();
            },
            OnValueChanged = NotifyChanged,
        };
        _basicSettings.OnToggle = _ => HandleLayoutChange();
        treeListNode.AddCategoryNode(_basicSettings);

        _rangeFilters = new RangeFiltersSection(() => _categoryDefinition)
        {
            String = "Range Filters",
            IsCollapsed = true,
            OnValueChanged = NotifyChanged,
        };
        _rangeFilters.OnToggle = _ => HandleLayoutChange();
        treeListNode.AddCategoryNode(_rangeFilters);

        _stateFilters = new StateFiltersSection(() => _categoryDefinition)
        {
            String = "State Filters",
            IsCollapsed = true,
            OnValueChanged = NotifyChanged,
        };
        _stateFilters.OnToggle = _ => HandleLayoutChange();
        treeListNode.AddCategoryNode(_stateFilters);

        _listFilters = new ListFiltersSection(() => _categoryDefinition)
        {
            String = "List Filters",
            IsCollapsed = true,
            OnValueChanged = NotifyChanged,
            OnListChanged = HandleListChanged,
        };
        _listFilters.OnToggle = _ => HandleLayoutChange();
        treeListNode.AddCategoryNode(_listFilters);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        _scrollingArea.Size = Size;

        foreach (var categoryNode in _scrollingArea.ContentNode.CategoryNodes)
        {
            categoryNode.Width = Width - 16.0f;
        }

        _scrollingArea.ContentNode.RefreshLayout();
    }

    public void SetCategory(UserCategoryDefinition newCategory)
    {
        _categoryDefinition = newCategory;
        RefreshAllValues();
    }

    private void RefreshAllValues()
    {
        _basicSettings.Refresh();
        _rangeFilters.Refresh();
        _stateFilters.Refresh();
        _listFilters.Refresh();

        HandleLayoutChange();
    }

    private void HandleListChanged()
    {
        NotifyChanged();
        HandleLayoutChange();
    }

    private void HandleLayoutChange()
    {
        _scrollingArea.ContentNode.RefreshLayout();
        OnLayoutChanged?.Invoke();
    }

    private static void NotifyChanged() => InventoryOrchestrator.RefreshAll(updateMaps: true);

    private void NotifyCategoryPropertyChanged() => OnCategoryPropertyChanged?.Invoke();

    public static string ResolveItemName(uint itemId) => ItemSheet?.GetRow(itemId).Name.ToString() ?? "Unknown";

    public static string ResolveUiCategoryName(uint categoryId) => UICategorySheet?.GetRow(categoryId).Name.ToString() ?? "Unknown";
}

public abstract class ConfigurationSection : TreeListCategoryNode
{
    private readonly Func<UserCategoryDefinition> _getCategoryDefinition;

    public Action? OnValueChanged { get; init; }

    protected UserCategoryDefinition CategoryDefinition => _getCategoryDefinition();

    protected ConfigurationSection(Func<UserCategoryDefinition> getCategoryDefinition)
    {
        _getCategoryDefinition = getCategoryDefinition;
        VerticalPadding = 4.0f;
    }

    protected static LabelTextNode CreateLabel(string text) => new()
    {
        TextFlags = TextFlags.AutoAdjustNodeSize,
        Size = new Vector2(80, 20),
        String = text,
    };
}

public sealed class BasicSettingsSection : ConfigurationSection
{
    public Action? OnPropertyChanged { get; init; }

    private CheckboxNode? _enabledCheckbox;
    private CheckboxNode? _pinnedCheckbox;
    private TextInputNode? _nameInput;
    private TextInputNode? _descriptionInput;
    private ColorInputRow? _colorInput;
    private NumericInputNode? _priorityInput;
    private NumericInputNode? _orderInput;

    private bool _initialized;

    public BasicSettingsSection(Func<UserCategoryDefinition> getCategoryDefinition)
        : base(getCategoryDefinition)
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 20),
            String = "Enabled",
            OnClick = isChecked =>
            {
                CategoryDefinition.Enabled = isChecked;
                OnPropertyChanged?.Invoke();
            },
        };
        AddNode(_enabledCheckbox);

        _pinnedCheckbox = new CheckboxNode
        {
            Size = new Vector2(Width, 20),
            String = "Pinned",
            OnClick = isChecked =>
            {
                CategoryDefinition.Pinned = isChecked;
                OnPropertyChanged?.Invoke();
            },
        };
        AddNode(_pinnedCheckbox);

        AddNode(CreateLabel("Name: "));
        _nameInput = new TextInputNode
        {
            Size = new Vector2(250, 28),
            PlaceholderString = "Category Name",
            OnInputReceived = input =>
            {
                CategoryDefinition.Name = input.ExtractText();
                OnPropertyChanged?.Invoke();
            },
        };
        AddNode(_nameInput);

        AddNode(CreateLabel("Description:"));
        _descriptionInput = new TextInputNode
        {
            Size = new Vector2(250, 28),
            PlaceholderString = "Optional description",
            OnInputReceived = input =>
            {
                CategoryDefinition.Description = input.ExtractText();
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_descriptionInput);

        _colorInput = new ColorInputRow
        {
            Label = "Color",
            Size = new Vector2(300, 28),
            CurrentColor = new UserCategoryDefinition().Color,
            DefaultColor = new UserCategoryDefinition().Color,
            OnColorConfirmed = c => { CategoryDefinition.Color = c; OnValueChanged?.Invoke(); },
            OnColorCanceled = c => { CategoryDefinition.Color = c; OnValueChanged?.Invoke(); },
            OnColorPreviewed = c => { CategoryDefinition.Color = c; OnValueChanged?.Invoke(); },
        };
        AddNode(_colorInput);

        AddNode(CreateLabel("Priority:"));
        _priorityInput = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = 1000,
            Step = 1,
            OnValueUpdate = val =>
            {
                CategoryDefinition.Priority = val;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_priorityInput);

        AddNode(CreateLabel("Order: "));
        _orderInput = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = 9999,
            Step = 1,
            OnValueUpdate = val =>
            {
                CategoryDefinition.Order = val;
                OnPropertyChanged?.Invoke();
            },
        };
        AddNode(_orderInput);

        RecalculateLayout();
    }

    public void Refresh()
    {
        EnsureInitialized();

        _enabledCheckbox!.IsChecked = CategoryDefinition.Enabled;
        _pinnedCheckbox!.IsChecked = CategoryDefinition.Pinned;
        _nameInput!.String = CategoryDefinition.Name;
        _nameInput.PlaceholderString = CategoryDefinition.Name.IsNullOrWhitespace() ? "Category Name" : "";
        _descriptionInput!.String = CategoryDefinition.Description;
        _descriptionInput.PlaceholderString = CategoryDefinition.Description.IsNullOrWhitespace() ? "Optional description" : "";
        _colorInput!.CurrentColor = CategoryDefinition.Color;
        _priorityInput!.Value = CategoryDefinition.Priority;
        _orderInput!.Value = CategoryDefinition.Order;

        RecalculateLayout();
        ParentTreeListNode?.RefreshLayout();
    }
}

public sealed class RangeFiltersSection : ConfigurationSection
{
    private RangeFilterRow? _levelFilter;
    private RangeFilterRow? _itemLevelFilter;
    private RangeFilterRowUint? _vendorPriceFilter;

    private bool _initialized;

    public RangeFiltersSection(Func<UserCategoryDefinition> getCategoryDefinition)
        : base(getCategoryDefinition)
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _levelFilter = new RangeFilterRow
        {
            Label = "Level",
            MinBound = 0,
            MaxBound = 200,
            OnFilterChanged = (enabled, min, max) =>
            {
                CategoryDefinition.Rules.Level.Enabled = enabled;
                CategoryDefinition.Rules.Level.Min = min;
                CategoryDefinition.Rules.Level.Max = max;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_levelFilter);

        _itemLevelFilter = new RangeFilterRow
        {
            Label = "Item Level",
            MinBound = 0,
            MaxBound = 2000,
            OnFilterChanged = (enabled, min, max) =>
            {
                CategoryDefinition.Rules.ItemLevel.Enabled = enabled;
                CategoryDefinition.Rules.ItemLevel.Min = min;
                CategoryDefinition.Rules.ItemLevel.Max = max;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_itemLevelFilter);

        _vendorPriceFilter = new RangeFilterRowUint
        {
            Label = "Vendor Price",
            MinBound = 0,
            MaxBound = 9_999_999,
            OnFilterChanged = (enabled, min, max) =>
            {
                CategoryDefinition.Rules.VendorPrice.Enabled = enabled;
                CategoryDefinition.Rules.VendorPrice.Min = min;
                CategoryDefinition.Rules.VendorPrice.Max = max;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_vendorPriceFilter);

        RecalculateLayout();
    }

    public void Refresh()
    {
        EnsureInitialized();

        _levelFilter!.SetFilter(CategoryDefinition.Rules.Level);
        _itemLevelFilter!.SetFilter(CategoryDefinition.Rules.ItemLevel);
        _vendorPriceFilter!.SetFilter(CategoryDefinition.Rules.VendorPrice);

        RecalculateLayout();
        ParentTreeListNode?.RefreshLayout();
    }
}

public sealed class StateFiltersSection : ConfigurationSection
{
    private readonly List<(StateFilterRowNode Node, Func<UserCategoryDefinition, StateFilter> GetFilter)> _filters = [];
    private bool _initialized;

    public StateFiltersSection(Func<UserCategoryDefinition> getCategoryDefinition)
        : base(getCategoryDefinition)
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        AddFilter("Untradable", def => def.Rules.Untradable);
        AddFilter("Unique", def => def.Rules.Unique);
        AddFilter("Collectable", def => def.Rules.Collectable);
        AddFilter("Dyeable", def => def.Rules.Dyeable);
        AddFilter("Repairable", def => def.Rules.Repairable);
        AddFilter("High Quality", def => def.Rules.HighQuality);
        AddFilter("Desynthesizable", def => def.Rules.Desynthesizable);
        AddFilter("Glamourable", def => def.Rules.Glamourable);
        AddFilter("Spiritbonded", def => def.Rules.FullySpiritbonded);

        RecalculateLayout();
    }

    private void AddFilter(string label, Func<UserCategoryDefinition, StateFilter> getFilter)
    {
        var node = new StateFilterRowNode(label, new StateFilter(), () => OnValueChanged?.Invoke());
        _filters.Add((node, getFilter));
        AddNode(node);
    }

    public void Refresh()
    {
        EnsureInitialized();

        foreach (var (node, getFilter) in _filters)
        {
            node.SetState(getFilter(CategoryDefinition));
        }

        RecalculateLayout();
        ParentTreeListNode?.RefreshLayout();
    }
}

public sealed class ListFiltersSection : ConfigurationSection
{
    public Action? OnListChanged { get; init; }

    private UintListEditorNode? _itemIdsEditor;
    private StringListEditorNode? _namePatternsEditor;
    private UintListEditorNode? _uiCategoriesEditor;
    private RarityEditorNode? _raritiesEditor;

    private bool _initialized;

    public ListFiltersSection(Func<UserCategoryDefinition> getCategoryDefinition)
        : base(getCategoryDefinition)
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _itemIdsEditor = new UintListEditorNode
        {
            Label = "Allowed Item IDs:",
            LabelResolver = CategoryDefinitionConfigurationNode.ResolveItemName,
            OnChanged = () =>
            {
                OnListChanged?.Invoke();
                RecalculateLayout();
                ParentTreeListNode?.RefreshLayout();
            },
        };
        AddNode(_itemIdsEditor);

        _namePatternsEditor = new StringListEditorNode
        {
            Label = "Name Patterns (Regex):",
            OnChanged = () =>
            {
                OnListChanged?.Invoke();
                RecalculateLayout();
                ParentTreeListNode?.RefreshLayout();
            },
        };
        AddNode(_namePatternsEditor);

        _uiCategoriesEditor = new UintListEditorNode
        {
            Label = "UI Categories:",
            LabelResolver = CategoryDefinitionConfigurationNode.ResolveUiCategoryName,
            OnChanged = () =>
            {
                OnListChanged?.Invoke();
                RecalculateLayout();
                ParentTreeListNode?.RefreshLayout();
            },
        };
        AddNode(_uiCategoriesEditor);

        _raritiesEditor = new RarityEditorNode
        {
            OnChanged = () => OnValueChanged?.Invoke(),
        };
        AddNode(_raritiesEditor);

        RecalculateLayout();
    }

    public void Refresh()
    {
        EnsureInitialized();

        _itemIdsEditor!.SetList(CategoryDefinition.Rules.AllowedItemIds);
        _namePatternsEditor!.SetList(CategoryDefinition.Rules.AllowedItemNamePatterns);
        _uiCategoriesEditor!.SetList(CategoryDefinition.Rules.AllowedUiCategoryIds);
        _raritiesEditor!.SetList(CategoryDefinition.Rules.AllowedRarities);

        RecalculateLayout();
        ParentTreeListNode?.RefreshLayout();
    }
}