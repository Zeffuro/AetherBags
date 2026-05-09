using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Text.Json.Serialization;
using KamiToolKit.Classes;

namespace AetherBags.Configuration;

public class CategorySettings
{
    public bool CategoriesEnabled { get; set; } = true;
    public bool GameCategoriesEnabled { get; set; } = true;
    public bool UserCategoriesEnabled { get; set; } = true;
    public bool BisBuddyEnabled { get; set; } = true;
    public PluginFilterMode BisBuddyMode { get; set; } = PluginFilterMode.Highlight;
    public bool AllaganToolsCategoriesEnabled { get; set; } = false;
    public PluginFilterMode AllaganToolsFilterMode { get; set; } = PluginFilterMode.Highlight;
    public List<ItemSortCriterion> DefaultItemSortCriteria { get; set; } = new();
    public bool BlankMiscCategoryName { get; set; } = false;
    public List<CategorySource> CategorySourceDisplayOrder { get; set; } = GetDefaultCategorySourceOrder();

    public List<UserCategoryDefinition> UserCategories { get; set; } = new();

    public static List<CategorySource> GetDefaultCategorySourceOrder() =>
    [
        CategorySource.UserCategories,
        CategorySource.BisBuddy,
        CategorySource.AllaganTools,
        CategorySource.GameCategories,
        CategorySource.Misc,
    ];

    public void NormalizeCategorySourceDisplayOrder()
    {
        var normalized = new List<CategorySource>(GetDefaultCategorySourceOrder().Count);

        foreach (var source in CategorySourceDisplayOrder ?? [])
        {
            if (Enum.IsDefined(source) && !normalized.Contains(source))
                normalized.Add(source);
        }

        foreach (var source in GetDefaultCategorySourceOrder())
        {
            if (!normalized.Contains(source))
                normalized.Add(source);
        }

        CategorySourceDisplayOrder = normalized;
    }

    public void NormalizeItemSortSettings()
    {
        DefaultItemSortCriteria = NormalizeItemSortCriteria(DefaultItemSortCriteria, allowUseGlobal: false);

        foreach (var category in UserCategories ?? [])
        {
            category.CustomItemOrder ??= new();
            category.ItemSortCriteria = NormalizeItemSortCriteria(category.ItemSortCriteria, allowUseGlobal: true);
        }
    }

    public static List<ItemSortCriterion> NormalizeItemSortCriteria(List<ItemSortCriterion>? criteria, bool allowUseGlobal)
    {
        var normalized = new List<ItemSortCriterion>();
        var seenFields = new HashSet<ItemSortField>();

        foreach (var criterion in criteria ?? [])
        {
            if (!Enum.IsDefined(criterion.Field) || !Enum.IsDefined(criterion.Direction))
                continue;

            if (criterion.Field == ItemSortField.UseGlobal)
            {
                if (allowUseGlobal)
                    return [new ItemSortCriterion { Field = ItemSortField.UseGlobal, Direction = SortDirection.Ascending }];

                continue;
            }

            if (seenFields.Add(criterion.Field))
            {
                normalized.Add(new ItemSortCriterion
                {
                    Field = criterion.Field,
                    Direction = criterion.Direction,
                });
            }
        }

        return normalized.Count > 0
            ? normalized
            : GetDefaultItemSortCriteria(allowUseGlobal);
    }

    public static List<ItemSortCriterion> GetDefaultItemSortCriteria(bool allowUseGlobal) => allowUseGlobal
        ? [new ItemSortCriterion { Field = ItemSortField.UseGlobal, Direction = SortDirection.Ascending }]
        : [new ItemSortCriterion { Field = ItemSortField.Quantity, Direction = SortDirection.Descending }];
}

public class UserCategoryDefinition
{
    public bool Enabled { get; set; } = true;
    public bool Pinned { get; set; } = false;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Category";
    public string Description { get; set; } = string.Empty;

    public int Order { get; set; }
    public int Priority { get; set; } = 100;
    public Vector4 Color { get; set; } = ColorHelper.GetColor(50);
    public List<ItemSortCriterion> ItemSortCriteria { get; set; } = new();
    public List<uint> CustomItemOrder { get; set; } = new();

    public CategoryRuleSet Rules { get; set; } = new();
}

public class ItemSortCriterion
{
    public ItemSortField Field { get; set; } = ItemSortField.Quantity;
    public SortDirection Direction { get; set; } = SortDirection.Descending;
}

public enum ItemSortField
{
    [Description("Use Global Default")]
    UseGlobal = 0,

    [Description("Quantity")]
    Quantity = 1,

    [Description("Name")]
    Name = 2,

    [Description("Rarity")]
    Rarity = 3,

    [Description("Item ID")]
    ItemId = 4,

    [Description("Custom Item Order")]
    CustomOrder = 5,

    [Description("Game Category")]
    GameCategory = 6,

    [Description("Item Level")]
    ItemLevel = 7,
}

public enum SortDirection
{
    [Description("Ascending")]
    Ascending = 0,

    [Description("Descending")]
    Descending = 1,
}

public class CategoryRuleSet
{
    public List<uint> AllowedItemIds { get; set; } = new();
    public List<string> AllowedItemNamePatterns { get; set; } = new();
    public List<uint> AllowedUiCategoryIds { get; set; } = new();
    public List<int> AllowedRarities { get; set; } = new();

    public RangeFilter<int> Level { get; set; } = new() { Enabled = false, Min = 0, Max = 200 };
    public RangeFilter<int> ItemLevel { get; set; } = new() { Enabled = false, Min = 0, Max = 2000 };
    public RangeFilter<uint> VendorPrice { get; set; } = new() { Enabled = false, Min = 0, Max = 9_999_999 };
    public StateFilter Untradable { get; set; } = new();
    public StateFilter Unique { get; set; } = new();
    public StateFilter Collectable { get; set; } = new();
    public StateFilter Dyeable { get; set; } = new();
    public StateFilter Repairable { get; set; } = new();
    public StateFilter HighQuality { get; set; } = new();
    public StateFilter Desynthesizable { get; set; } = new();
    public StateFilter Glamourable { get; set; } = new();
    public StateFilter FullySpiritbonded { get; set; } = new();
}

public class RangeFilter<T> where T : struct, IComparable<T>
{
    public bool Enabled { get; set; }
    public T Min { get; set; }
    public T Max { get; set; }
}

public class StateFilter
{
    public int State { get; set; } = 0;
    public int Filter { get; set; } = 0;

    [JsonIgnore]
    public ToggleFilterState ToggleState
    {
        get => Enum.IsDefined(typeof(ToggleFilterState), State) ? (ToggleFilterState)State : ToggleFilterState.Ignored;
        set => State = (int)value;
    }
}

public enum ToggleFilterState
{
    Ignored = 0,
    Allow = 1,
    Disallow = 2,
}

public enum PluginFilterMode
{
    [Description("Create New Categories")]
    Categorize = 0,

    [Description("Apply Highlight Only")]
    Highlight = 1,
}

public enum CategorySource
{
    [Description("User Categories")]
    UserCategories = 0,

    [Description("BISBuddy")]
    BisBuddy = 1,

    [Description("Allagan Tools")]
    AllaganTools = 2,

    [Description("Game Categories")]
    GameCategories = 3,

    [Description("Misc")]
    Misc = 4,
}

