using System;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBags.IPC.ExternalCategorySystem;

public interface IExternalItemSource
{
    string SourceName { get; }
    string DisplayName { get; }
    int Priority { get; }
    bool IsReady { get; }

    int Version { get; }
    event Action? OnDataChanged;

    SourceCapabilities Capabilities { get; }
    ConflictBehavior ConflictBehavior { get; }

    IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments();
    IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations();
    IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId);
    IReadOnlyDictionary<uint, string[]>? GetSearchTags();
    IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId);
}

[Flags]
public enum SourceCapabilities
{
    None = 0,
    Categories = 1,
    ItemColors = 2,
    Badges = 4,
    ContextMenu = 8,
    SearchTags = 16,
    Relationships = 32,
    Tooltips = 64
}

public enum ConflictBehavior
{
    Replace,
    Merge,
    Defer
}

public readonly record struct ExternalCategoryAssignment(
    uint CategoryKey,
    string CategoryName,
    string? CategoryDescription,
    Vector4 CategoryColor,
    Vector3? ItemOverlayColor,
    int SubPriority
);

public record struct ItemDecoration
{
    public Vector3? OverlayColor { get; init; }
    public float? Opacity { get; init; }
    public BadgeInfo? Badge { get; init; }
    public BorderStyle Border { get; init; }
    public string? TooltipLine { get; init; }
}

public record struct BadgeInfo(
    uint IconId,
    BadgePosition Position,
    Vector4? TintColor
);

public enum BadgePosition { TopLeft, TopRight, BottomLeft, BottomRight }
public enum BorderStyle { None, Solid, Glow, Pulse }

public record struct ContextMenuEntry(
    string Label,
    uint? IconId,
    Action<ContextMenuContext> OnClick,
    int Order,
    Func<uint, bool>? IsVisible = null
);

public record struct ContextMenuContext(
    uint ItemId,
    int Container,
    int Slot
);

public record struct ItemRelationship(
    RelationshipType Type,
    uint[] RelatedItemIds,
    string? GroupLabel,
    Vector3? HighlightColor
);

public enum RelationshipType
{
    SameSet,
    Upgrades,
    UpgradedFrom,
    CraftedFrom,
    CraftsInto,
    Alternative
}
