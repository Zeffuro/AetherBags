using Dalamud.Plugin.Services;
using System;
using System.Linq;
using AetherBags.Addons;
using AetherBags.Configuration;
using KamiToolKit.Enums;
using Lumina.Excel.Sheets;
using Action = System.Action;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class ListFiltersSection(Func<UserCategoryDefinition> getCategoryDefinition) : ConfigurationSection(getCategoryDefinition)
{
    public Action? OnListChanged { get; init; }

    private UintListEditorNode? _itemIdsEditor;
    private StringListEditorNode? _namePatternsEditor;
    private UintListEditorNode? _uiCategoriesEditor;
    private RarityEditorNode? _raritiesEditor;

    private bool _initialized;


    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _itemIdsEditor = new UintListEditorNode
        {
            Label = "Allowed Item IDs:",
            MaxValue = IDataManager.Get().GetExcelSheet<Item>()?.LastOrDefault().RowId ?? uint.MaxValue,
            LabelResolver = CategoryDefinitionConfigurationNode.ResolveItemName,
            OnSearchButtonClicked = OpenItemPicker,
            OnChanged = () =>
            {
                OnListChanged?.Invoke();
                RefreshLayout();
            },
        };
        AddNode(_itemIdsEditor);

        _namePatternsEditor = new StringListEditorNode
        {
            Label = "Name Patterns (Regex):",
            OnChanged = () =>
            {
                OnListChanged?.Invoke();
                RefreshLayout();
            },
        };
        AddNode(_namePatternsEditor);

        _uiCategoriesEditor = new UintListEditorNode
        {
            Label = "UI Categories:",
            MaxValue = IDataManager.Get().GetExcelSheet<ItemUICategory>()?.LastOrDefault().RowId ?? uint.MaxValue,
            LabelResolver = CategoryDefinitionConfigurationNode.ResolveUiCategoryName,
            OnSearchButtonClicked = OpenCategoryPicker,
            OnChanged = () =>
            {
                OnListChanged?.Invoke();
                RefreshLayout();
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

    private void OpenItemPicker()
    {
        System.ItemFilterPicker.OpenFor(this, item =>
        {
            if (!IsDisposed) _itemIdsEditor?.AddValue(item.RowId);
        });
    }

    private void OpenCategoryPicker()
    {
        System.UICategoryPicker.OpenFor(this, category =>
        {
            if (!IsDisposed) _uiCategoriesEditor?.AddValue(category.RowId);
        });
    }

    protected override void Dispose(bool isNativeDestructor)
    {
        System.ItemFilterPicker?.CloseFor(this);
        System.UICategoryPicker?.CloseFor(this);
        base.Dispose(isNativeDestructor);
    }

    public override void Refresh()
    {
        System.ItemFilterPicker.CloseFor(this);
        System.UICategoryPicker.CloseFor(this);
        EnsureInitialized();

        _itemIdsEditor!.SetList(CategoryDefinition.Rules.AllowedItemIds);
        _namePatternsEditor!.SetList(CategoryDefinition.Rules.AllowedItemNamePatterns);
        _uiCategoriesEditor!.SetList(CategoryDefinition.Rules.AllowedUiCategoryIds);
        _raritiesEditor!.SetList(CategoryDefinition.Rules.AllowedRarities);

        RecalculateLayout();
    }
}
