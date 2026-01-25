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

    public List<UserCategoryDefinition> UserCategories { get; set; } = new();
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

    public CategoryRuleSet Rules { get; set; } = new();
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