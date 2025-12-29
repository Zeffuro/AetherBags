using System;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
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

public sealed class CategoryDefinitionConfigurationNode : VerticalListNode
{
    private readonly CheckboxNode _enabledCheckbox;
    private readonly TextInputNode _nameInputNode;
    private readonly TextInputNode _descriptionInputNode;
    private readonly ColorInputRow _colorInputNode;
    private readonly NumericInputNode _priorityInputNode;
    private readonly NumericInputNode _orderInputNode;

    private readonly CheckboxNode _levelEnabledCheckbox;
    private readonly NumericInputNode _levelMinNode;
    private readonly NumericInputNode _levelMaxNode;

    private readonly CheckboxNode _itemLevelEnabledCheckbox;
    private readonly NumericInputNode _itemLevelMinNode;
    private readonly NumericInputNode _itemLevelMaxNode;

    private readonly CheckboxNode _vendorPriceEnabledCheckbox;
    private readonly NumericInputNode _vendorPriceMinNode;
    private readonly NumericInputNode _vendorPriceMaxNode;

    private readonly StateFilterRowNode _untradableFilter;
    private readonly StateFilterRowNode _uniqueFilter;
    private readonly StateFilterRowNode _collectableFilter;
    private readonly StateFilterRowNode _dyeableFilter;
    private readonly StateFilterRowNode _repairableFilter;
    private readonly StateFilterRowNode _hqFilter;
    private readonly StateFilterRowNode _desynthFilter;
    private readonly StateFilterRowNode _glamourFilter;
    private readonly StateFilterRowNode _spiritbondFilter;

    private readonly UintListEditorNode _allowedItemIdsEditor;
    private readonly StringListEditorNode _allowedNamePatternsEditor;
    private readonly UintListEditorNode _allowedUiCategoriesEditor;
    private readonly RarityEditorNode _allowedRaritiesEditor;

    private bool _isInitialized;

    private static ExcelSheet<Item>? _sItemSheet;
    private static ExcelSheet<ItemUICategory>? _sUICategorySheet;

    public Action? OnLayoutChanged { get; set; }

    public Action? OnCategoryPropertyChanged { get; set; }

    private UserCategoryDefinition CategoryDefinition { get; set; }

    public CategoryDefinitionConfigurationNode(UserCategoryDefinition categoryDefinition)
    {
        CategoryDefinition = categoryDefinition;

        _sItemSheet ??= Services.DataManager.GetExcelSheet<Item>();
        _sUICategorySheet ??= Services.DataManager.GetExcelSheet<ItemUICategory>();

        FitContents = true;
        ItemSpacing = 4.0f;

        var catchAllWarningNode = new TextNode
        {
            Size = new Vector2(300, 40),
            TextFlags = TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize,
            SeString = new SeStringBuilder().Append(" Warning: No rules configured\nThis category won't match anything!").ToReadOnlySeString(),
            TextColor = ColorHelper.GetColor(17),
            LineSpacing = 20,
            IsVisible = UserCategoryMatcher.IsCatchAll(CategoryDefinition),
        };
        AddNode(catchAllWarningNode);

        AddNode(CreateSectionHeader("Basic Settings"));

        _enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(200, 20),
            String = "Enabled",
            IsChecked = CategoryDefinition.Enabled,
            OnClick = isChecked =>
            {
                CategoryDefinition.Enabled = isChecked;
                NotifyChanged();
                NotifyCategoryPropertyChanged();
            },
        };
        AddNode(_enabledCheckbox);

        AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(80, 20),
            String = "Name:"
        });
        _nameInputNode = new TextInputNode
        {
            Size = new Vector2(250, 28),
            String = CategoryDefinition.Name,
            PlaceholderString = CategoryDefinition.Name.IsNullOrWhitespace() ? "Category Name" : "",
            OnInputReceived = name =>
            {
                CategoryDefinition.Name = name.ExtractText();
                NotifyChanged();
                NotifyCategoryPropertyChanged();
            },
        };
        AddNode(_nameInputNode);

        AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(80, 20),
            String = "Description:"
        });
        _descriptionInputNode = new TextInputNode
        {
            Size = new Vector2(250, 28),
            String = CategoryDefinition.Description,
            PlaceholderString = CategoryDefinition.Description.IsNullOrWhitespace() ? "Optional description" : "",
            OnInputReceived = desc =>
            {
                CategoryDefinition.Description = desc.ExtractText();
                NotifyChanged();
            },
        };
        AddNode(_descriptionInputNode);

        _colorInputNode = new ColorInputRow
        {
            Label = "Color",
            Size = new Vector2(300, 28),
            CurrentColor = CategoryDefinition.Color,
            DefaultColor = new UserCategoryDefinition().Color,
            OnColorConfirmed = color =>
            {
                CategoryDefinition.Color = color;
                NotifyChanged();
            },
            OnColorCanceled = color =>
            {
                CategoryDefinition.Color = color;
                NotifyChanged();
            },
        };
        AddNode(_colorInputNode);

        AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(80, 20),
            String = "Priority:"
        });
        _priorityInputNode = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = 1000,
            Step = 1,
            Value = CategoryDefinition.Priority,
            OnValueUpdate = val =>
            {
                CategoryDefinition.Priority = val;
                NotifyChanged();
            },
        };
        AddNode(_priorityInputNode);

        AddNode(new LabelTextNode { TextFlags = TextFlags.AutoAdjustNodeSize, Size = new Vector2(80, 20), String = "Order:" });
        _orderInputNode = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = 9999,
            Step = 1,
            Value = CategoryDefinition.Order,
            OnValueUpdate = val =>
            {
                CategoryDefinition.Order = val;
                NotifyChanged();
                NotifyCategoryPropertyChanged();
            },
        };
        AddNode(_orderInputNode);

        AddNode(CreateSectionHeader("Range Filters"));

        (_levelEnabledCheckbox, _levelMinNode, _levelMaxNode) = CreateRangeFilter(
            "Level",
            CategoryDefinition.Rules.Level,
            0, 200,
            (enabled, min, max) =>
            {
                CategoryDefinition.Rules.Level.Enabled = enabled;
                CategoryDefinition.Rules.Level.Min = min;
                CategoryDefinition.Rules.Level.Max = max;
                NotifyChanged();
            }
        );

        (_itemLevelEnabledCheckbox, _itemLevelMinNode, _itemLevelMaxNode) = CreateRangeFilter(
            "Item Level",
            CategoryDefinition.Rules.ItemLevel,
            0, 2000,
            (enabled, min, max) =>
            {
                CategoryDefinition.Rules.ItemLevel.Enabled = enabled;
                CategoryDefinition.Rules.ItemLevel.Min = min;
                CategoryDefinition.Rules.ItemLevel.Max = max;
                NotifyChanged();
            }
        );

        (_vendorPriceEnabledCheckbox, _vendorPriceMinNode, _vendorPriceMaxNode) = CreateRangeFilterUint(
            "Vendor Price",
            CategoryDefinition.Rules.VendorPrice,
            0, 9_999_999
        );

        AddNode(CreateSectionHeader("State Filters"));

        _untradableFilter = new StateFilterRowNode("Untradable", CategoryDefinition.Rules.Untradable, NotifyChanged);
        AddNode(_untradableFilter);

        _uniqueFilter = new StateFilterRowNode("Unique", CategoryDefinition.Rules.Unique, NotifyChanged);
        AddNode(_uniqueFilter);

        _collectableFilter = new StateFilterRowNode("Collectable", CategoryDefinition.Rules.Collectable, NotifyChanged);
        AddNode(_collectableFilter);

        _dyeableFilter = new StateFilterRowNode("Dyeable", CategoryDefinition.Rules.Dyeable, NotifyChanged);
        AddNode(_dyeableFilter);

        _repairableFilter = new StateFilterRowNode("Repairable", CategoryDefinition.Rules.Repairable, NotifyChanged);
        AddNode(_repairableFilter);

        _hqFilter = new StateFilterRowNode("High Quality", CategoryDefinition.Rules.HighQuality, NotifyChanged);
        AddNode(_hqFilter);

        _desynthFilter = new StateFilterRowNode("Desynthesizable", CategoryDefinition.Rules.Desynthesizable, NotifyChanged);
        AddNode(_desynthFilter);

        _glamourFilter = new StateFilterRowNode("Glamourable", CategoryDefinition.Rules.Glamourable, NotifyChanged);
        AddNode(_glamourFilter);

        _spiritbondFilter = new StateFilterRowNode("Spiritbonded", CategoryDefinition.Rules.FullySpiritbonded, NotifyChanged);
        AddNode(_spiritbondFilter);

        AddNode(CreateSectionHeader("List Filters"));

        _allowedItemIdsEditor = new UintListEditorNode(
            "Allowed Item IDs:",
            CategoryDefinition.Rules.AllowedItemIds,
            OnListChanged,
            ResolveItemName
        );
        AddNode(_allowedItemIdsEditor);

        _allowedNamePatternsEditor = new StringListEditorNode(
            "Name Patterns (Regex):",
            CategoryDefinition.Rules.AllowedItemNamePatterns,
            OnListChanged
        );
        AddNode(_allowedNamePatternsEditor);

        _allowedUiCategoriesEditor = new UintListEditorNode(
            "UI Categories:",
            CategoryDefinition.Rules.AllowedUiCategoryIds,
            OnListChanged,
            ResolveUiCategoryName
        );
        AddNode(_allowedUiCategoriesEditor);

        _allowedRaritiesEditor = new RarityEditorNode(
            CategoryDefinition.Rules.AllowedRarities,
            NotifyChanged
        );
        AddNode(_allowedRaritiesEditor);

        _isInitialized = true;
    }

    private void OnListChanged()
    {
        NotifyChanged();
        RecalculateLayout();
        OnLayoutChanged?.Invoke();
    }

    private static string ResolveItemName(uint itemId)
    {
        try
        {
            var item = _sItemSheet?.GetRow(itemId);
            return item?.Name.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ResolveUiCategoryName(uint categoryId)
    {
        try
        {
            var category = _sUICategorySheet?.GetRow(categoryId);
            return category?.Name.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static void NotifyChanged()
    {
        System.AddonInventoryWindow.ManualInventoryRefresh();
    }

    private void NotifyCategoryPropertyChanged()
    {
        OnCategoryPropertyChanged?.Invoke();
    }

    private static LabelTextNode CreateSectionHeader(string text)
    {
        return new LabelTextNode
        {
            Size = new Vector2(300, 22),
            String = text,
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(0),
        };
    }

    private (CheckboxNode enabled, NumericInputNode min, NumericInputNode max) CreateRangeFilter(
        string label,
        RangeFilter<int> filter,
        int minBound,
        int maxBound,
        Action<bool, int, int> onUpdate)
    {
        var enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(200, 20),
            String = $"{label} Filter",
            IsChecked = filter.Enabled,
        };
        AddNode(enabledCheckbox);

        var minNode = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = minBound,
            Max = maxBound,
            Value = filter.Min,
            IsEnabled = filter.Enabled,
        };

        var maxNode = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = minBound,
            Max = maxBound,
            Value = filter.Max,
            IsEnabled = filter.Enabled,
        };

        var rangeRow = new HorizontalListNode { Size = new Vector2(300, 28), ItemSpacing = 8.0f };
        rangeRow.AddNode(new LabelTextNode { TextFlags = TextFlags.AutoAdjustNodeSize, Size = new Vector2(30, 28), String = "Min:" });
        rangeRow.AddNode(minNode);
        rangeRow.AddNode(new LabelTextNode { TextFlags = TextFlags.AutoAdjustNodeSize, Size = new Vector2(30, 28), String = "Max:" });
        rangeRow.AddNode(maxNode);
        AddNode(rangeRow);

        enabledCheckbox.OnClick = isChecked =>
        {
            minNode.IsEnabled = isChecked;
            maxNode.IsEnabled = isChecked;
            onUpdate(isChecked, minNode.Value, maxNode.Value);
        };

        minNode.OnValueUpdate = val => onUpdate(enabledCheckbox.IsChecked, val, maxNode.Value);
        maxNode.OnValueUpdate = val => onUpdate(enabledCheckbox.IsChecked, minNode.Value, val);

        return (enabledCheckbox, minNode, maxNode);
    }

    private (CheckboxNode enabled, NumericInputNode min, NumericInputNode max) CreateRangeFilterUint(
        string label,
        RangeFilter<uint> filter,
        int minBound,
        int maxBound)
    {
        var enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(200, 20),
            String = $"{label} Filter",
            IsChecked = filter.Enabled,
        };
        AddNode(enabledCheckbox);

        var minNode = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = minBound,
            Max = maxBound,
            Value = (int)filter.Min,
            IsEnabled = filter.Enabled,
        };

        var maxNode = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = minBound,
            Max = maxBound,
            Value = (int)Math.Min(filter.Max, maxBound),
            IsEnabled = filter.Enabled,
        };

        var rangeRow = new HorizontalListNode { Size = new Vector2(300, 28), ItemSpacing = 8.0f };
        rangeRow.AddNode(new LabelTextNode { TextFlags = TextFlags.AutoAdjustNodeSize, Size = new Vector2(30, 28), String = "Min:" });
        rangeRow.AddNode(minNode);
        rangeRow.AddNode(new LabelTextNode { TextFlags = TextFlags.AutoAdjustNodeSize, Size = new Vector2(30, 28), String = "Max:" });
        rangeRow.AddNode(maxNode);
        AddNode(rangeRow);

        enabledCheckbox.OnClick = isChecked =>
        {
            minNode.IsEnabled = isChecked;
            maxNode.IsEnabled = isChecked;
            CategoryDefinition.Rules.VendorPrice.Enabled = isChecked;
            NotifyChanged();
        };

        minNode.OnValueUpdate = value =>
        {
            CategoryDefinition.Rules.VendorPrice.Min = (uint)value;
            NotifyChanged();
        };

        maxNode.OnValueUpdate = value =>
        {
            CategoryDefinition.Rules.VendorPrice.Max = (uint)value;
            NotifyChanged();
        };

        return (enabledCheckbox, minNode, maxNode);
    }

    public void SetCategory(UserCategoryDefinition newCategory)
    {
        CategoryDefinition = newCategory;
        RefreshValues();
    }

    private void RefreshValues()
    {
        if (! _isInitialized) return;

        _enabledCheckbox.IsChecked = CategoryDefinition.Enabled;
        _colorInputNode.CurrentColor = CategoryDefinition.Color;
        _nameInputNode.String = CategoryDefinition.Name;
        _descriptionInputNode.String = CategoryDefinition.Description;
        _priorityInputNode.Value = CategoryDefinition.Priority;
        _orderInputNode.Value = CategoryDefinition.Order;

        RefreshRangeFilter(_levelEnabledCheckbox, _levelMinNode, _levelMaxNode, CategoryDefinition.Rules.Level);
        RefreshRangeFilter(_itemLevelEnabledCheckbox, _itemLevelMinNode, _itemLevelMaxNode, CategoryDefinition.Rules.ItemLevel);

        _vendorPriceEnabledCheckbox.IsChecked = CategoryDefinition.Rules.VendorPrice.Enabled;
        _vendorPriceMinNode.Value = (int)CategoryDefinition.Rules.VendorPrice.Min;
        _vendorPriceMaxNode.Value = (int)Math.Min(CategoryDefinition.Rules.VendorPrice.Max, int.MaxValue);
        _vendorPriceMinNode.IsEnabled = CategoryDefinition.Rules.VendorPrice.Enabled;
        _vendorPriceMaxNode.IsEnabled = CategoryDefinition.Rules.VendorPrice.Enabled;

        _untradableFilter.SetState(CategoryDefinition.Rules.Untradable);
        _uniqueFilter.SetState(CategoryDefinition.Rules.Unique);
        _collectableFilter.SetState(CategoryDefinition.Rules.Collectable);
        _dyeableFilter.SetState(CategoryDefinition.Rules.Dyeable);
        _repairableFilter.SetState(CategoryDefinition.Rules.Repairable);

        _allowedItemIdsEditor.SetList(CategoryDefinition.Rules.AllowedItemIds);
        _allowedNamePatternsEditor.SetList(CategoryDefinition.Rules.AllowedItemNamePatterns);
        _allowedUiCategoriesEditor.SetList(CategoryDefinition.Rules.AllowedUiCategoryIds);
        _allowedRaritiesEditor.SetList(CategoryDefinition.Rules.AllowedRarities);

        RecalculateLayout();
        OnLayoutChanged?.Invoke();
    }

    private static void RefreshRangeFilter(CheckboxNode enabled, NumericInputNode min, NumericInputNode max, RangeFilter<int> filter)
    {
        enabled.IsChecked = filter.Enabled;
        min.Value = filter.Min;
        max.Value = filter.Max;
        min.IsEnabled = filter.Enabled;
        max.IsEnabled = filter.Enabled;
    }
}