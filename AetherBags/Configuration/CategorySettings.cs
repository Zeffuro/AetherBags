using System;
using System.Collections.Generic;
using System.Numerics;
using KamiToolKit.Classes;

namespace AetherBags.Configuration;

public class CategorySettings
{
    public bool GameCategoriesEnabled { get; set; } = true;
    public bool UserCategoriesEnabled { get; set; } = true;

    public List<UserCategoryDefinition> UserCategories { get; set; } = new();
}

public class UserCategoryDefinition
{
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

    public RangeFilter<int> ItemLevel { get; set; } = new() { Enabled = false, Min = 0, Max = 2000 };
    public RangeFilter<uint> VendorPrice { get; set; } = new() { Enabled = false, Min = 0, Max = 9_999_999 };
}

public class RangeFilter<T> where T : struct, IComparable<T>
{
    public bool Enabled { get; set; }
    public T Min { get; set; }
    public T Max { get; set; }
}