using System.Collections.Generic;
using System.Numerics;

namespace AetherBags.Configuration.Import;

public sealed class SortaKindaImportFile
{
    public List<SortaKindaCategory> Rules { get; set; } = new();

    public object? MainInventory { get; set; }
}

public sealed class SortaKindaCategory
{
    public Vector4 Color { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }

    public List<string> AllowedItemNames { get; set; } = new();

    public List<AllowedNameRegexDto> AllowedNameRegexes { get; set; } = new();

    // Common
    public List<uint> AllowedItemTypes { get; set; } = new();
    public List<int> AllowedItemRarities { get; set; } = new();

    public ExternalRangeFilterDto<int>? LevelFilter { get; set; }
    public ExternalRangeFilterDto<int> ItemLevelFilter { get; set; } = new();
    public ExternalRangeFilterDto<uint> VendorPriceFilter { get; set; } = new();

    public ExternalStateFilterDto? UntradableFilter { get; set; }
    public ExternalStateFilterDto? UniqueFilter { get; set; }
    public ExternalStateFilterDto? CollectableFilter { get; set; }
    public ExternalStateFilterDto? DyeableFilter { get; set; }
    public ExternalStateFilterDto? RepairableFilter { get; set; }

    public int Direction { get; set; }
    public int FillMode { get; set; }
    public int SortMode { get; set; }
    public bool InclusiveAnd { get; set; }
}

public sealed class AllowedNameRegexDto
{
    public string Text { get; set; } = string.Empty;
}

public sealed class ExternalStateFilterDto
{
    public int State { get; set; }
    public int Filter { get; set; }
}

public sealed class ExternalRangeFilterDto<T> where T : struct
{
    public bool Enable { get; set; }
    public string Label { get; set; } = string.Empty;
    public T MinValue { get; set; }
    public T MaxValue { get; set; }
}