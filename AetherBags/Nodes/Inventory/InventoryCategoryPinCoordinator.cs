using AetherBags.Nodes.Layout;

namespace AetherBags.Nodes.Inventory;

public sealed class InventoryCategoryPinCoordinator
{
    public bool ApplyPinnedStates(WrappingGridNode<InventoryCategoryNodeBase> grid)
    {
        bool changed = false;

        using (grid.DeferRecalculateLayout())
        {
            foreach (var node in grid.GetNodes<InventoryCategoryNodeBase>())
            {
                bool shouldBePinned = node.IsPinnedInConfig;

                bool isPinned = grid.IsPinned(node);

                if (shouldBePinned)
                {
                    if (!isPinned)
                    {
                        grid.PinNode(node);
                        changed = true;
                    }
                }
                else
                {
                    if (isPinned)
                    {
                        grid.UnpinNode(node);
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    public bool PrunePinnedNotInGrid(WrappingGridNode<InventoryCategoryNodeBase> grid)
    {
        return false;
    }
}