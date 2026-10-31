using Dalamud.Plugin.Services;
using System;
using System.Linq;
using AetherBags.Addons;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class ItemOrderingSection(Func<UserCategoryDefinition> getCategoryDefinition) : ConfigurationSection(getCategoryDefinition)
{
    public Action? OnLayoutChanged { get; init; }

    private ItemSortCriteriaEditorNode? _sortCriteriaEditor;
    private UintListEditorNode? _customOrderEditor;
    private bool _initialized;

    protected override bool IsContentRowVisible(NodeBase node) =>
        node != _customOrderEditor || CategoryDefinition.ItemSortCriteria.Any(criterion => criterion.Field == ItemSortField.CustomOrder);

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var sortCriteriaEditor = new ItemSortCriteriaEditorNode("Item Sort Priority:", allowUseGlobal: true, allowCustomOrder: true);
        sortCriteriaEditor.OnLayoutChanged = RefreshOrderingLayout;
        sortCriteriaEditor.OnChanged = () =>
        {
            CategoryDefinition.ItemSortCriteria = sortCriteriaEditor.GetCriteria();
            OnValueChanged?.Invoke();
            RefreshCustomOrderVisibility();
        };
        _sortCriteriaEditor = sortCriteriaEditor;
        AddNode(_sortCriteriaEditor);

        _customOrderEditor = new UintListEditorNode
        {
            Label = "Custom Item Order:",
            MaxValue = IDataManager.Get().GetExcelSheet<Item>().LastOrDefault().RowId,
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
        System.ItemOrderPicker.OpenFor(this, item =>
        {
            if (!IsDisposed) _customOrderEditor?.AddValue(item.RowId);
        });
    }

    private void RefreshCustomOrderVisibility()
    {
        if (_customOrderEditor is not null)
            _customOrderEditor.IsVisible = CategoryDefinition.ItemSortCriteria.Any(criterion => criterion.Field == ItemSortField.CustomOrder);

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
        IFramework.Get().RunOnTick(() =>
        {
            if (IsDisposed) return;
            var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
            if (addon is not null)
                addon->UpdateCollisionNodeList(false);
        });
    }

    protected override void Dispose(bool isNativeDestructor)
    {
        System.ItemOrderPicker?.CloseFor(this);
        base.Dispose(isNativeDestructor);
    }

    public override void Refresh()
    {
        System.ItemOrderPicker.CloseFor(this);
        EnsureInitialized();

        _sortCriteriaEditor!.SetCriteria(CategoryDefinition.ItemSortCriteria);
        CategoryDefinition.ItemSortCriteria = _sortCriteriaEditor.GetCriteria();
        _customOrderEditor!.SetList(CategoryDefinition.CustomItemOrder);
        RefreshCustomOrderVisibility();
    }
}

