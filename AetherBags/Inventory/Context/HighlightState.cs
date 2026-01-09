using System.Collections.Generic;
using System.Numerics;

namespace AetherBags.Inventory.Context;

public enum HighlightSource
{
    Search,
    AllaganTools,
    BiSBuddy,
}

public record HighlightEntry(uint ItemId, Vector3 Color);

public static class HighlightState
{
    private static readonly Dictionary<HighlightSource, HashSet<uint>> Filters = new();
    private static readonly Dictionary<HighlightSource, (HashSet<uint> ids, Vector3 color)> Labels = new();
    private static readonly Dictionary<HighlightSource, Dictionary<uint, HighlightEntry>> PerItemLabels = new();

    public static string? SelectedAllaganToolsFilterKey { get; set; } = string.Empty;
    public static string? SelectedBisBuddyFilterKey { get; set; } = string.Empty;

    public static bool IsFilterActive => Filters.Count > 0;

    public static void SetFilter(HighlightSource source, IEnumerable<uint> ids)
        => Filters[source] = new HashSet<uint>(ids);

    public static bool IsInActiveFilters(uint itemId)
    {
        if (Filters.Count == 0) return true;
        foreach (var filter in Filters.Values)
            if (filter.Contains(itemId)) return true;
        return false;
    }

    public static HighlightEntry? GetHighlightEntry(uint itemId)
    {
        foreach (var perItemLabel in PerItemLabels.Values)
        {
            if (perItemLabel.TryGetValue(itemId, out var entry))
                return entry;
        }

        foreach (var label in Labels.Values)
        {
            if (label.ids.Contains(itemId))
                return new HighlightEntry(itemId, label.color);
        }

        return null;
    }

    public static Vector3? GetLabelColor(uint itemId)
        => GetHighlightEntry(itemId)?.Color;

    public static void SetLabel(HighlightSource source, IEnumerable<uint> ids, Vector3 color)
    {
        PerItemLabels.Remove(source);
        Labels[source] = (new HashSet<uint>(ids), color);
    }

    public static void SetLabelWithColors(HighlightSource source, Dictionary<uint, Vector4> itemColors)
    {
        Labels.Remove(source);

        var entries = new Dictionary<uint, HighlightEntry>(itemColors.Count);
        foreach (var (itemId, color) in itemColors)
        {
            var rgb = new Vector3(
                color.X * color.W,
                color.Y * color.W,
                color.Z * color.W
            );
            entries[itemId] = new HighlightEntry(itemId, rgb);
        }

        PerItemLabels[source] = entries;
    }

    public static void SetLabelWithColors(HighlightSource source, IEnumerable<HighlightEntry> entries)
    {
        Labels.Remove(source);

        var dict = new Dictionary<uint, HighlightEntry>();
        foreach (var entry in entries)
        {
            dict[entry.ItemId] = entry;
        }

        PerItemLabels[source] = dict;
    }

    public static void SetLabelWithColors(HighlightSource source, Dictionary<uint, Vector3> itemColors)
    {
        Labels.Remove(source);

        var entries = new Dictionary<uint, HighlightEntry>(itemColors.Count);
        foreach (var (itemId, color) in itemColors)
        {
            entries[itemId] = new HighlightEntry(itemId, color);
        }

        PerItemLabels[source] = entries;
    }

    public static void ClearAll()
    {
        Filters.Clear();
        Labels.Clear();
        PerItemLabels.Clear();
        SelectedAllaganToolsFilterKey = string.Empty;
    }

    public static void ClearFilter(HighlightSource source) => Filters.Remove(source);

    public static void ClearLabel(HighlightSource source)
    {
        Labels.Remove(source);
        PerItemLabels.Remove(source);
    }
}