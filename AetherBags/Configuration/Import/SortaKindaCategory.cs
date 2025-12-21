using System.Collections.Generic;
using System.Numerics;

namespace AetherBags.Configuration.Import;

// Possible Mapping:
// Index -> Order
// Color/Id/Name
// AllowedItemNames -> AllowedItemNamePatterns
// AllowedItemTypes -> AllowedUiCategoryIds
// AllowedItemRarities -> AllowedRarities
// ItemLevelFilter / VendorPriceFilter -> RangeFilter

public sealed class SortaKindaCategory
{
    public Vector4 Color { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }

    public List<string> AllowedItemNames { get; set; } = new();
    public List<uint> AllowedItemTypes { get; set; } = new();
    public List<int> AllowedItemRarities { get; set; } = new();

    public ExternalRangeFilterDto<int> ItemLevelFilter { get; set; } = new();
    public ExternalRangeFilterDto<uint> VendorPriceFilter { get; set; } = new();

    public int Direction { get; set; }
    public int FillMode { get; set; }
    public int SortMode { get; set; }
}

public sealed class ExternalRangeFilterDto<T> where T : struct
{
    public bool Enable { get; set; }
    public string Label { get; set; } = string.Empty;
    public T MinValue { get; set; }
    public T MaxValue { get; set; }
}