using System;
using System.ComponentModel;

namespace AetherBags.Configuration;

public sealed class InventoryWindowSizingSettings
{
    public InventoryWindowSizingMode Mode { get; set; } = InventoryWindowSizingMode.Automatic;
    public int FixedWidth { get; set; } = 620;
    public int FixedHeight { get; set; } = 600;
    public int MinWidth { get; set; } = 620;
    public int MaxWidth { get; set; } = 800;
    public int MinHeight { get; set; } = 200;
    public int MaxHeight { get; set; } = 1000;
}

public enum InventoryWindowSizingMode : byte
{
    [Description("Auto-fit")]
    Automatic = 0,

    [Description("Fixed")]
    Fixed = 1,

    [Description("Min / Max")]
    CustomBounds = 2,
}

public readonly record struct InventoryWindowSizingLimits(int SafeMinWidth, int DefaultMaxWidth, int SafeMinHeight, int DefaultMaxHeight)
{
    public static InventoryWindowSizingLimits Inventory => new(620, 800, 200, 1000);
    public static InventoryWindowSizingLimits SaddleBag => new(500, 600, 200, 1000);
    public static InventoryWindowSizingLimits Retainer => new(500, 700, 200, 1000);
}

public static class InventoryWindowSizingDefaults
{
    public const int MaxConfigurableWidth = 2000;
    public const int MaxConfigurableHeight = 2000;

    public static InventoryWindowSizingSettings Create(InventoryWindowSizingLimits limits)
        => new()
        {
            FixedWidth = limits.DefaultMaxWidth,
            FixedHeight = Math.Clamp(600, limits.SafeMinHeight, limits.DefaultMaxHeight),
            MinWidth = limits.SafeMinWidth,
            MaxWidth = limits.DefaultMaxWidth,
            MinHeight = limits.SafeMinHeight,
            MaxHeight = limits.DefaultMaxHeight,
        };

    public static void Normalize(InventoryWindowSizingSettings settings, InventoryWindowSizingLimits limits)
    {
        int safeMinWidth = Math.Clamp(limits.SafeMinWidth, 1, MaxConfigurableWidth);
        int safeMinHeight = Math.Clamp(limits.SafeMinHeight, 1, MaxConfigurableHeight);
        int defaultMaxWidth = Math.Clamp(limits.DefaultMaxWidth, safeMinWidth, MaxConfigurableWidth);
        int defaultMaxHeight = Math.Clamp(limits.DefaultMaxHeight, safeMinHeight, MaxConfigurableHeight);

        settings.FixedWidth = Math.Clamp(settings.FixedWidth, safeMinWidth, MaxConfigurableWidth);
        settings.FixedHeight = Math.Clamp(settings.FixedHeight, safeMinHeight, MaxConfigurableHeight);
        settings.MinWidth = Math.Clamp(settings.MinWidth, safeMinWidth, MaxConfigurableWidth);
        settings.MinHeight = Math.Clamp(settings.MinHeight, safeMinHeight, MaxConfigurableHeight);
        settings.MaxWidth = Math.Clamp(settings.MaxWidth <= 0 ? defaultMaxWidth : settings.MaxWidth, settings.MinWidth, MaxConfigurableWidth);
        settings.MaxHeight = Math.Clamp(settings.MaxHeight <= 0 ? defaultMaxHeight : settings.MaxHeight, settings.MinHeight, MaxConfigurableHeight);
    }
}
