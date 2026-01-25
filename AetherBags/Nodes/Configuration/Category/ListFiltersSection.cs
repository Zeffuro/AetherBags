using System;
using System.Linq;
using AetherBags.Addons;
using AetherBags.Configuration;
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

    private AddonItemPicker? _itemPicker;
    private AddonUICategoryPicker? _categoryPicker;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _itemIdsEditor = new UintListEditorNode
        {
            Label = "Allowed Item IDs:",
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

    private void OpenItemPicker() {
        _itemPicker ??= new AddonItemPicker
        {
            Title = "Select Items to Add",
            InternalName = "Aetherbags_ItemPicker",
            SearchOptions = Services.DataManager.GetExcelSheet<Item>()
                .Where(i => i.RowId > 0 && !i.Name.IsEmpty)
                .ToList(),

            SortingOptions = ["Alphabetical", "Id"],
            ItemSpacing = 3.0f,
        };
        _itemPicker.SelectionResult = item => _itemIdsEditor?.AddValue(item.RowId);
        _itemPicker.Open();
    }

    private void OpenCategoryPicker() {
        _categoryPicker ??= new AddonUICategoryPicker {
            Title = "Select Categories to Add",
            InternalName = "Aetherbags_CategoryPicker",
            SearchOptions = Services.DataManager.GetExcelSheet<ItemUICategory>()
                .Where(i => i.RowId > 0)
                .ToList()
        };
        _categoryPicker.SelectionResult = cat => _uiCategoriesEditor?.AddValue(cat.RowId);
        _categoryPicker.Open();
    }

    public override void Refresh()
    {
        EnsureInitialized();

        _itemIdsEditor!.SetList(CategoryDefinition.Rules.AllowedItemIds);
        _namePatternsEditor!.SetList(CategoryDefinition.Rules.AllowedItemNamePatterns);
        _uiCategoriesEditor!.SetList(CategoryDefinition.Rules.AllowedUiCategoryIds);
        _raritiesEditor!.SetList(CategoryDefinition.Rules.AllowedRarities);

        RecalculateLayout();
    }
}