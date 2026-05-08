using System;
using System.Linq;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Configuration;
using AetherBags.Nodes.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class ItemOrderingSection(Func<UserCategoryDefinition> getCategoryDefinition) : ConfigurationSection(getCategoryDefinition)
{
    public Action? OnLayoutChanged { get; init; }

    private LabeledEnumDropdownNode<ItemSortMode>? _sortModeDropdown;
    private UintListEditorNode? _customOrderEditor;
    private AddonItemPicker? _itemPicker;
    private bool _initialized;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _sortModeDropdown = new LabeledEnumDropdownNode<ItemSortMode>
        {
            Size = new Vector2(500, 20),
            LabelText = "Item Sort",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            Options = Enum.GetValues<ItemSortMode>().ToList(),
            SelectedOption = CategoryDefinition.ItemSortMode,
            OnOptionSelected = selected =>
            {
                CategoryDefinition.ItemSortMode = selected;
                OnValueChanged?.Invoke();
                RefreshCustomOrderVisibility();
            }
        };
        AddNode(_sortModeDropdown);

        _customOrderEditor = new UintListEditorNode
        {
            Label = "Custom Item Order:",
            MaxValue = Services.DataManager.GetExcelSheet<Item>().LastOrDefault().RowId,
            LabelResolver = CategoryDefinitionConfigurationNode.ResolveItemName,
            OnSearchButtonClicked = OpenItemPicker,
            OnChanged = () =>
            {
                CategoryDefinition.CustomItemOrder = _customOrderEditor?.GetList() ?? new();
                OnValueChanged?.Invoke();
                RefreshOrderingLayout();
            },
        };
        AddNode(_customOrderEditor);

        RefreshCustomOrderVisibility();
        RecalculateLayout();
    }

    private void OpenItemPicker()
    {
        _itemPicker ??= new AddonItemPicker
        {
            Title = "Select Items to Order",
            InternalName = "Aetherbags_OrderItemPicker",
            SearchOptions = Services.DataManager.GetExcelSheet<Item>()
                .Where(i => i.RowId > 0 && !i.Name.IsEmpty)
                .ToList(),
            SortingOptions = [DefaultSortOptions.Alphabetical, DefaultSortOptions.Id],
            ItemSpacing = 3.0f,
        };

        _itemPicker.SelectionResult = item => _customOrderEditor?.AddValue(item.RowId);
        ItemListItemWithAddNode.OnAddClicked = item => _customOrderEditor?.AddValue(item.RowId);
        _itemPicker.Open();
    }

    private void RefreshCustomOrderVisibility()
    {
        if (_customOrderEditor is not null)
            _customOrderEditor.IsVisible = CategoryDefinition.ItemSortMode == ItemSortMode.CustomOrder;

        RefreshOrderingLayout();
    }

    private void RefreshOrderingLayout()
    {
        CollapsibleContent.RecalculateLayout();
        RecalculateLayout();
        OnLayoutChanged?.Invoke();
        RefreshAddonCollisionList();
    }

    private unsafe void RefreshAddonCollisionList()
    {
        Services.Framework.RunOnTick(() =>
        {
            var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
            if (addon is not null)
                addon->UpdateCollisionNodeList(false);
        });
    }

    public override void Refresh()
    {
        EnsureInitialized();

        _sortModeDropdown!.SelectedOption = CategoryDefinition.ItemSortMode;
        _customOrderEditor!.SetList(CategoryDefinition.CustomItemOrder);
        RefreshCustomOrderVisibility();
    }
}

