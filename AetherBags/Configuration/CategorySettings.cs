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
    public bool CrystalsEnabled { get; set; } = true;
    public bool KeyItemsEnabled { get; set; } = true;
    public List<ItemSortCriterion> DefaultItemSortCriteria { get; set; } = new();
    public bool BlankMiscCategoryName { get; set; } = false;
    public List<string> CategorySourceDisplayOrder { get; set; } = GetDefaultCategorySourceOrder();

    public HashSet<uint> DisabledGameCategoryIds { get; set; } = new();

    public List<UserCategoryDefinition> UserCategories { get; set; } = new();

    public static List<string> GetDefaultCategorySourceOrder() =>
    [
        CategorySourceIds.UserCategories,
        CategorySourceIds.BisBuddy,
        CategorySourceIds.AllaganTools,
        CategorySourceIds.GameCategories,
        CategorySourceIds.Misc,
    ];

    public void NormalizeCategorySourceDisplayOrder()
    {
        var normalized = new List<string>(8);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in CategorySourceDisplayOrder ?? [])
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (seen.Add(id)) normalized.Add(id);
        }

        foreach (var id in GetDefaultCategorySourceOrder())
        {
            if (seen.Add(id)) normalized.Add(id);
        }

        foreach (var source in IPC.ExternalCategorySystem.ExternalCategoryManager.RegisteredSources)
        {
            if (seen.Contains(source.SourceName)) continue;
            int insertAt = FindInsertPosition(normalized, source.DefaultDisplayOrder);
            normalized.Insert(insertAt, source.SourceName);
            seen.Add(source.SourceName);
        }

        CategorySourceDisplayOrder = normalized;
    }

    private static int FindInsertPosition(List<string> ordered, int newHint)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            int existingHint = HintFor(ordered[i]);
            if (newHint < existingHint) return i;
        }
        return ordered.Count;
    }

    private static int HintFor(string id)
    {
        if (CategorySourceIds.BuiltInDisplayOrderHints.TryGetValue(id, out int hint))
            return hint;

        foreach (var source in IPC.ExternalCategorySystem.ExternalCategoryManager.RegisteredSources)
        {
            if (string.Equals(source.SourceName, id, StringComparison.Ordinal))
                return source.DefaultDisplayOrder;
        }

        return 999;
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

    // JSON name kept as "ForkedFromKey" for backward-compat with existing saves.
    [JsonPropertyName("ForkedFromKey")]
    public string? OverrideSourceKey { get; set; }

    public CategoryRuleSet Rules { get; set; } = new();
}

public static class CategoryOverrideKey
{
    private const string GamePrefix = "game:";

    public static string ForGameCategory(uint id) => $"{GamePrefix}{id}";

    public static bool TryParseGameCategory(string? key, out uint id)
    {
        id = 0;
        return key is not null
            && key.StartsWith(GamePrefix, StringComparison.Ordinal)
            && uint.TryParse(key.AsSpan(GamePrefix.Length), out id);
    }
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

public static class CategorySourceIds
{
    public const string UserCategories = nameof(CategorySource.UserCategories);
    public const string BisBuddy = nameof(CategorySource.BisBuddy);
    public const string AllaganTools = nameof(CategorySource.AllaganTools);
    public const string GameCategories = nameof(CategorySource.GameCategories);
    public const string Misc = nameof(CategorySource.Misc);

    public static readonly Dictionary<string, int> BuiltInDisplayOrderHints = new(StringComparer.Ordinal)
    {
        [UserCategories] = 10,
        [BisBuddy]       = 20,
        [AllaganTools]   = 30,
        [GameCategories] = 40,
        [Misc]           = 50,
    };

    public static bool TryParseBuiltIn(string id, out CategorySource source)
        => Enum.TryParse(id, out source);
}

