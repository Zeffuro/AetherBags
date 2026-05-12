using System;
using AetherBags.Inventory.Items;

namespace AetherBags.Tags;

public readonly record struct DtrTagContext(InventoryStats Main, InventoryStats Saddle, InventoryStats Total)
{
    public static DtrTagContext Create()
    {
        var main = System.AddonInventoryWindow.GetStats();
        var saddle = System.AddonSaddleBagWindow.GetStats();
        return new DtrTagContext(main, saddle, main);
    }

    public bool TryGetValue(string key, string subKey, out object value)
    {
        var metric = key;
        var source = Total;

        var separatorIndex = key.IndexOf('.');
        if (separatorIndex >= 0)
        {
            source = ResolveSource(key[..separatorIndex]);
            metric = key[(separatorIndex + 1)..];
        }
        else if (!string.IsNullOrEmpty(subKey) && IsSourceName(subKey))
        {
            source = ResolveSource(subKey);
        }

        return TryResolveMetric(source, metric, out value);
    }

    private InventoryStats ResolveSource(string source) => source.ToLowerInvariant() switch
    {
        "main" => Main,
        "saddle" or "saddles" or "saddlebags" => Saddle,
        _ => Total,
    };

    private static bool IsSourceName(string value) => value.Equals("main", StringComparison.OrdinalIgnoreCase)
        || value.Equals("saddle", StringComparison.OrdinalIgnoreCase)
        || value.Equals("saddles", StringComparison.OrdinalIgnoreCase)
        || value.Equals("saddlebags", StringComparison.OrdinalIgnoreCase)
        || value.Equals("total", StringComparison.OrdinalIgnoreCase)
        || value.Equals("all", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveMetric(InventoryStats stats, string metric, out object value)
    {
        switch (metric.ToLowerInvariant())
        {
            case "used" or "usedslots":
                value = stats.UsedSlots;
                return true;
            case "total" or "totalslots" or "slots":
                value = stats.TotalSlots;
                return true;
            case "free" or "empty" or "emptyslots":
                value = stats.EmptySlots;
                return true;
            case "percent" or "usage" or "usagepercent":
                value = stats.UsagePercent;
                return true;
            case "items" or "totalitems":
                value = stats.TotalItems;
                return true;
            case "quantity" or "totalquantity":
                value = stats.TotalQuantity;
                return true;
            case "categories" or "categorycount":
                value = stats.CategoryCount;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
