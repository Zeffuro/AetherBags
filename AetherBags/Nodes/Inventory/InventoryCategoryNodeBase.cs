using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using System.Collections.Generic;
using System.Numerics;
using KamiToolKit.BaseTypes.ComponentNode;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Nodes.Inventory;

/// <summary>
/// Base class for category-like nodes that can be displayed in the inventory grid.
/// Used to allow both regular categories and special categories (like looted items) to be hoisted/pinned.
/// </summary>
public abstract class InventoryCategoryNodeBase : SimpleComponentNode
{
    protected InventoryCategoryNodeBase() => CollisionNode.RemoveNodeFlags(NodeFlags.Focusable);

    /// <summary>
    /// Unique key for this category, used for sync operations.
    /// </summary>
    public abstract uint Key { get; }

    /// <summary>
    /// Whether this category should be pinned in the layout.
    /// </summary>
    public virtual bool IsPinnedInConfig => false;

    public abstract float? MaxWidth { get; set; }

    public abstract void RecalculateSize();

    internal abstract void AddNavigationTargets(List<InventoryNavigationTarget> targets);
}

internal readonly record struct InventoryNavigationTarget(ComponentNode Node, Vector2 Position);
