using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

/// <summary>
/// Base class for category-like nodes that can be displayed in the inventory grid.
/// Used to allow both regular categories and special categories (like looted items) to be hoisted/pinned.
/// </summary>
public abstract class InventoryCategoryNodeBase : SimpleComponentNode
{
    /// <summary>
    /// Unique key for this category, used for sync operations.
    /// </summary>
    public abstract uint Key { get; }

    /// <summary>
    /// Whether this category should be pinned in the layout.
    /// </summary>
    public virtual bool IsPinnedInConfig => false;
}
