using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

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

    // Slot hint for first appearance in the user's display order; built-ins use 10/20/30/40/50.
    int DefaultDisplayOrder => 100;

    // Sources that ship with the plugin. Surfaced in the config list but cannot be removed.
    bool IsBuiltIn => false;

    // Game UI category IDs this source supersedes — matching rows are hidden from the config list.
    IReadOnlyList<uint> OverriddenGameCategoryIds => Array.Empty<uint>();

    IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments();
    IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations();
    IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId);
    IReadOnlyDictionary<uint, string[]>? GetSearchTags();
    IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId);
}

public interface IInventoryTypeProvidingSource : IExternalItemSource
{
    IReadOnlyList<InventoryType> AdditionalInventoryTypes { get; }

    IReadOnlyList<InventoryType> AdditionalInventoryTypesFor(InventorySourceType sourceType)
        => sourceType == InventorySourceType.MainBags
            ? AdditionalInventoryTypes
            : Array.Empty<InventoryType>();

    bool LocksDragOut => true;
    bool LocksDragIn => true;

    DragDropType GetDragDropTypeFor(InventoryType container) => DragDropType.Item;

    // Send drops with InventoryType.Invalid + slot 0xFFFF so the game picks the slot by item type
    // (RetainerCrystals deposits work this way; each slot is fixed to one element).
    bool AutoRoutesDeposits(InventoryType container) => false;
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
    int SubPriority,
    bool IsPinned = false
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
