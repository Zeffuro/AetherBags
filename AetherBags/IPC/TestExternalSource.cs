using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.IPC.ExternalCategorySystem;

namespace AetherBags.IPC;

public sealed class TestExternalSource : IExternalItemSource, IDisposable
{
    private int _version;
    private bool _isEnabled;

    public string SourceName => "TestSource";
    public string DisplayName => "Test External Source";
    public int Priority => 50;
    public bool IsReady => _isEnabled;
    public int Version => _version;
    public event Action? OnDataChanged;

    public SourceCapabilities Capabilities =>
        SourceCapabilities.Categories |
        SourceCapabilities.ItemColors |
        SourceCapabilities.Badges |
        SourceCapabilities.ContextMenu |
        SourceCapabilities.SearchTags |
        SourceCapabilities.Relationships |
        SourceCapabilities.Tooltips;

    public ConflictBehavior ConflictBehavior => ConflictBehavior.Merge;

    // Test item IDs from user's inventory
    private static readonly uint[] TestCategoryAItems = { 4552, 4553, 4555, 4556, 4570 }; // Potions: Hi-Potion, Mega-Potion, Ether, Hi-Ether, Phoenix Down
    private static readonly uint[] TestCategoryBItems = { 40932, 40951, 42397, 42423, 42413 }; // Gear: Mandervillous weapons, Vanguard armor
    private static readonly uint[] TestHighlightItems = { 21800, 4868, 6141, 5729, 5734 }; // Glamour Prism, Gysahl Greens, Cordial, Dyes
    private static readonly uint[] TestBadgeGlowItems = { 4570 }; // Phoenix Down - Glow border
    private static readonly uint[] TestBadgePulseItems = { 21800 }; // Glamour Prism - Pulse border
    private static readonly uint[] TestRelationshipItems = { 42549, 42550, 42551, 42552, 42554, 42555, 42558, 42559, 42567 }; // Epochal accessories set

    public void Enable()
    {
        if (_isEnabled) return;
        _isEnabled = true;
        _version++;
        ExternalCategoryManager.RegisterSource(this);
        OnDataChanged?.Invoke();
        Services.Logger.Information("[TestSource] Enabled");
    }

    public void Disable()
    {
        if (!_isEnabled) return;
        _isEnabled = false;
        ExternalCategoryManager.UnregisterSource(SourceName);
        Services.Logger.Information("[TestSource] Disabled");
    }

    public void Refresh()
    {
        _version++;
        OnDataChanged?.Invoke();
        Services.Logger.Information("[TestSource] Refreshed");
    }

    public IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments()
    {
        if (!_isEnabled) return null;

        var result = new Dictionary<uint, ExternalCategoryAssignment>();

        // Category A - Red potions
        foreach (var itemId in TestCategoryAItems)
        {
            result[itemId] = new ExternalCategoryAssignment(
                CategoryKey: 0xFFFFA00,
                CategoryName: "[Test] Category A",
                CategoryDescription: "Test category A - Potions",
                CategoryColor: new Vector4(1.0f, 0.3f, 0.3f, 1.0f),
                ItemOverlayColor: new Vector3(0.15f, 0.0f, 0.0f),
                SubPriority: 10
            );
        }

        // Category B - Green food
        foreach (var itemId in TestCategoryBItems)
        {
            result[itemId] = new ExternalCategoryAssignment(
                CategoryKey: 0xFFFFB00,
                CategoryName: "[Test] Category B",
                CategoryDescription: "Test category B - Food",
                CategoryColor: new Vector4(0.3f, 1.0f, 0.3f, 1.0f),
                ItemOverlayColor: new Vector3(0.0f, 0.15f, 0.0f),
                SubPriority: 20
            );
        }

        return result;
    }

    public IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations()
    {
        if (!_isEnabled) return null;

        var result = new Dictionary<uint, ItemDecoration>();

        // Highlight items with overlay colors (only items NOT in badge lists)
        foreach (var itemId in TestHighlightItems)
        {
            // Skip if this item will get a badge decoration
            bool hasBadge = false;
            foreach (var badgeId in TestBadgeGlowItems)
                if (badgeId == itemId) { hasBadge = true; break; }
            if (!hasBadge)
            {
                foreach (var badgeId in TestBadgePulseItems)
                    if (badgeId == itemId) { hasBadge = true; break; }
            }

            if (!hasBadge)
            {
                result[itemId] = new ItemDecoration
                {
                    OverlayColor = new Vector3(0.0f, 0.0f, 0.2f),
                    TooltipLine = "[Test] This item is highlighted by TestSource",
                };
            }
        }

        // Badge items with glow border
        foreach (var itemId in TestBadgeGlowItems)
        {
            result[itemId] = new ItemDecoration
            {
                Badge = new BadgeInfo(
                    IconId: 60074,
                    Position: BadgePosition.TopRight,
                    TintColor: new Vector4(0.2f, 1.0f, 0.2f, 1.0f)
                ),
                Border = BorderStyle.Glow,
                OverlayColor = new Vector3(0.0f, 0.0f, 0.2f),
                TooltipLine = "[Test] Badge item (glow border)",
            };
        }

        // Badge items with pulse border
        foreach (var itemId in TestBadgePulseItems)
        {
            result[itemId] = new ItemDecoration
            {
                Badge = new BadgeInfo(
                    IconId: 60073,
                    Position: BadgePosition.TopRight,
                    TintColor: new Vector4(1.0f, 0.8f, 0.2f, 1.0f)
                ),
                Border = BorderStyle.Pulse,
                OverlayColor = new Vector3(0.0f, 0.0f, 0.2f),
                TooltipLine = "[Test] Badge item (pulse border)",
            };
        }

        return result;
    }

    public IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId)
    {
        if (!_isEnabled) return null;

        return new[]
        {
            new ContextMenuEntry(
                Label: "[Test] Log Item ID",
                IconId: 60026, // Info icon
                OnClick: ctx =>
                {
                    Services.Logger.Information($"[TestSource] Context menu clicked for item {ctx.ItemId} at [{ctx.Container}:{ctx.Slot}]");
                },
                Order: 100
            ),
            new ContextMenuEntry(
                Label: "[Test] Toggle Highlight",
                IconId: 60073, // Star icon
                OnClick: ctx =>
                {
                    Services.Logger.Information($"[TestSource] Toggle highlight for item {ctx.ItemId}");
                    Refresh();
                },
                Order: 101
            ),
        };
    }

    public IReadOnlyDictionary<uint, string[]>? GetSearchTags()
    {
        if (!_isEnabled) return null;

        var result = new Dictionary<uint, string[]>();

        foreach (var itemId in TestCategoryAItems)
        {
            result[itemId] = new[] { "test", "testa", "potion", "testsource" };
        }

        foreach (var itemId in TestCategoryBItems)
        {
            result[itemId] = new[] { "test", "testb", "food", "testsource" };
        }

        foreach (var itemId in TestHighlightItems)
        {
            if (!result.ContainsKey(itemId))
            {
                result[itemId] = new[] { "test", "highlight", "testsource" };
            }
        }

        return result;
    }

    public IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId)
    {
        if (!_isEnabled) return null;

        // Check if this item is in the relationship set
        bool isInSet = false;
        foreach (var id in TestRelationshipItems)
        {
            if (id == itemId)
            {
                isInSet = true;
                break;
            }
        }

        if (!isInSet) return null;

        // Return all other items in the set as related
        var relatedIds = new List<uint>();
        foreach (var id in TestRelationshipItems)
        {
            if (id != itemId)
            {
                relatedIds.Add(id);
            }
        }

        if (relatedIds.Count == 0) return null;

        return new[]
        {
            new ItemRelationship(
                Type: RelationshipType.SameSet,
                RelatedItemIds: relatedIds.ToArray(),
                GroupLabel: "Test Related Set",
                HighlightColor: Vector3.Zero
            )
        };
    }

    public void Dispose()
    {
        Disable();
    }
}
