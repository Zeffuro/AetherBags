using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Items;

namespace AetherBags.IPC.ExternalCategorySystem;

public static class ExternalCategoryManager
{
    private static readonly List<IExternalItemSource> Sources = new();
    private static readonly Dictionary<uint, ExternalCategoryAssignment> CategoryCache = new();
    private static readonly Dictionary<uint, ItemDecoration> DecorationCache = new();
    private static readonly Dictionary<uint, List<string>> SearchTagCache = new();
    private static int _lastCombinedVersion;

    public static IReadOnlyList<IExternalItemSource> RegisteredSources => Sources;

    public static void RegisterSource(IExternalItemSource source)
    {
        if (Sources.Any(s => s.SourceName == source.SourceName))
            return;

        Sources.Add(source);
        Sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        source.OnDataChanged += InvalidateCache;
        InvalidateCache();

        Services.Logger.Information($"Registered external category source: {source.SourceName}");
    }

    public static void UnregisterSource(string sourceName)
    {
        var source = Sources.FirstOrDefault(s => s.SourceName == sourceName);
        if (source == null) return;

        source.OnDataChanged -= InvalidateCache;
        Sources.Remove(source);
        InvalidateCache();

        Services.Logger.Information($"Unregistered external category source: {sourceName}");
    }

    public static void InvalidateCache()
    {
        _lastCombinedVersion = -1;
        CategoryCache.Clear();
        DecorationCache.Clear();
        SearchTagCache.Clear();
    }

    private static int ComputeCombinedVersion()
    {
        int version = 0;
        foreach (var source in Sources)
            version = unchecked(version * 31 + source.Version);
        return version;
    }

    public static void RebuildCacheIfNeeded()
    {
        int currentVersion = ComputeCombinedVersion();
        if (currentVersion == _lastCombinedVersion && CategoryCache.Count > 0)
            return;

        _lastCombinedVersion = currentVersion;
        CategoryCache.Clear();
        DecorationCache.Clear();
        SearchTagCache.Clear();

        foreach (var source in Sources)
        {
            if (!source.IsReady) continue;

            if (source.Capabilities.HasFlag(SourceCapabilities.Categories))
            {
                var categories = source.GetCategoryAssignments();
                if (categories != null)
                {
                    foreach (var (itemId, assignment) in categories)
                    {
                        CategoryCache.TryAdd(itemId, assignment);
                    }
                }
            }

            if (source.Capabilities.HasFlag(SourceCapabilities.ItemColors) ||
                source.Capabilities.HasFlag(SourceCapabilities.Badges))
            {
                var decorations = source.GetItemDecorations();
                if (decorations != null)
                {
                    foreach (var (itemId, decoration) in decorations)
                    {
                        if (DecorationCache.TryGetValue(itemId, out var existing))
                        {
                            DecorationCache[itemId] = MergeDecorations(existing, decoration, source.ConflictBehavior);
                        }
                        else
                        {
                            DecorationCache[itemId] = decoration;
                        }
                    }
                }
            }

            if (source.Capabilities.HasFlag(SourceCapabilities.SearchTags))
            {
                var searchTags = source.GetSearchTags();
                if (searchTags != null)
                {
                    foreach (var (itemId, tags) in searchTags)
                    {
                        if (!SearchTagCache.TryGetValue(itemId, out var existingTags))
                        {
                            existingTags = new List<string>(tags.Length);
                            SearchTagCache[itemId] = existingTags;
                        }
                        existingTags.AddRange(tags);
                    }
                }
            }
        }
    }

    private static ItemDecoration MergeDecorations(ItemDecoration existing, ItemDecoration incoming, ConflictBehavior behavior)
    {
        return behavior switch
        {
            ConflictBehavior.Replace => incoming,
            ConflictBehavior.Defer => existing,
            ConflictBehavior.Merge => new ItemDecoration
            {
                OverlayColor = incoming.OverlayColor ?? existing.OverlayColor,
                Opacity = incoming.Opacity ?? existing.Opacity,
                Badge = incoming.Badge ?? existing.Badge,
                Border = incoming.Border != BorderStyle.None ? incoming.Border : existing.Border,
                TooltipLine = CombineTooltips(existing.TooltipLine, incoming.TooltipLine),
            },
            _ => incoming
        };
    }

    private static string? CombineTooltips(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return $"{a}\n{b}";
    }

    public static void BucketItems(
        Dictionary<ulong, ItemInfo> itemInfoByKey,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        HashSet<ulong> claimedKeys)
    {
        RebuildCacheIfNeeded();

        if (CategoryCache.Count == 0) return;

        foreach (var (itemKey, item) in itemInfoByKey)
        {
            if (claimedKeys.Contains(itemKey)) continue;

            if (!CategoryCache.TryGetValue(item.Item.ItemId, out var assignment))
                continue;

            ref var bucketRef = ref CollectionsMarshal.GetValueRefOrAddDefault(bucketsByKey, assignment.CategoryKey, out bool exists);

            if (!exists)
            {
                bucketRef = new CategoryBucket
                {
                    Key = assignment.CategoryKey,
                    Category = new CategoryInfo
                    {
                        Name = assignment.CategoryName,
                        Description = assignment.CategoryDescription ?? string.Empty,
                        Color = assignment.CategoryColor,
                    },
                    Items = new List<ItemInfo>(16),
                    FilteredItems = new List<ItemInfo>(16),
                    Used = true,
                };
            }
            else
            {
                bucketRef!.Used = true;
                bucketRef.Category.Name = assignment.CategoryName;
                bucketRef.Category.Description = assignment.CategoryDescription ?? string.Empty;
                bucketRef.Category.Color = assignment.CategoryColor;
            }

            bucketRef!.Items.Add(item);
            claimedKeys.Add(itemKey);
        }
    }

    public static ItemDecoration? GetDecoration(uint itemId)
    {
        RebuildCacheIfNeeded();
        return DecorationCache.TryGetValue(itemId, out var dec) ? dec : null;
    }

    public static Vector3? GetItemOverlayColor(uint itemId)
    {
        if (CategoryCache.TryGetValue(itemId, out var assignment))
            return assignment.ItemOverlayColor;

        if (DecorationCache.TryGetValue(itemId, out var decoration))
            return decoration.OverlayColor;

        return null;
    }

    public static List<ContextMenuEntry>? GetContextMenuEntries(uint itemId)
    {
        List<ContextMenuEntry>? result = null;

        foreach (var source in Sources)
        {
            if (!source.IsReady) continue;
            if (!source.Capabilities.HasFlag(SourceCapabilities.ContextMenu)) continue;

            var entries = source.GetContextMenuEntries(itemId);
            if (entries == null || entries.Count == 0) continue;

            foreach (var entry in entries)
            {
                if (entry.IsVisible != null && !entry.IsVisible(itemId)) continue;

                result ??= new List<ContextMenuEntry>(4);
                result.Add(entry);
            }
        }

        result?.Sort((a, b) => a.Order.CompareTo(b.Order));
        return result;
    }

    public static IReadOnlyList<string>? GetSearchTags(uint itemId)
    {
        RebuildCacheIfNeeded();
        return SearchTagCache.TryGetValue(itemId, out var tags) ? tags : null;
    }

    public static bool MatchesSearchTag(uint itemId, string searchText)
    {
        RebuildCacheIfNeeded();
        if (!SearchTagCache.TryGetValue(itemId, out var tags)) return false;

        foreach (var tag in tags)
        {
            if (tag.Contains(searchText, global::System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static List<ItemRelationship>? GetItemRelationships(uint itemId)
    {
        List<ItemRelationship>? result = null;

        foreach (var source in Sources)
        {
            if (!source.IsReady) continue;
            if (!source.Capabilities.HasFlag(SourceCapabilities.Relationships)) continue;

            var relationships = source.GetItemRelationships(itemId);
            if (relationships == null || relationships.Count == 0) continue;

            result ??= new List<ItemRelationship>(4);
            result.AddRange(relationships);
        }

        return result;
    }

    public static HashSet<uint>? GetRelatedItemIds(uint itemId, RelationshipType? filterType = null)
    {
        var relationships = GetItemRelationships(itemId);
        if (relationships == null || relationships.Count == 0) return null;

        var result = new HashSet<uint>();
        foreach (var rel in relationships)
        {
            if (filterType.HasValue && rel.Type != filterType.Value) continue;

            foreach (var relatedId in rel.RelatedItemIds)
            {
                result.Add(relatedId);
            }
        }

        return result.Count > 0 ? result : null;
    }
}
