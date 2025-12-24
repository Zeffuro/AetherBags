using AetherBags.Nodes.Layout;

namespace AetherBags.Nodes.Inventory;

public sealed class InventoryCategoryHoverCoordinator
{
    private InventoryCategoryNode? _active;
    private int _activeRowIndex = -1;

    public void OnCategoryHoverChanged(
        WrappingGridNode<InventoryCategoryNode> grid,
        InventoryCategoryNode source,
        bool hovering)
    {
        grid.RecalculateLayout();

        if (hovering)
        {
            _active = source;

            if (!grid.TryGetRowIndex(source, out _activeRowIndex))
            {
                SuppressAllExcept(grid, source);
                source.SetHeaderSuppressed(false);
                return;
            }

            ClearAll(grid);

            var row = grid.Rows[_activeRowIndex];
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i] is InventoryCategoryNode cat && !ReferenceEquals(cat, source))
                    cat.SetHeaderSuppressed(true);
            }

            source.SetHeaderSuppressed(false);
            return;
        }

        if (!ReferenceEquals(_active, source))
            return;

        _active = null;

        if (_activeRowIndex >= 0 && _activeRowIndex < grid.Rows.Count)
        {
            var row = grid.Rows[_activeRowIndex];
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i] is InventoryCategoryNode cat)
                    cat.SetHeaderSuppressed(false);
            }
        }
        else
        {
            ClearAll(grid);
        }

        _activeRowIndex = -1;
    }

    public void ResetAll(WrappingGridNode<InventoryCategoryNode> grid)
    {
        _active = null;
        _activeRowIndex = -1;
        ClearAll(grid);
    }

    private static void ClearAll(WrappingGridNode<InventoryCategoryNode> grid)
    {
        foreach (var cat in grid.GetNodes<InventoryCategoryNode>())
            cat.SetHeaderSuppressed(false);
    }

    private static void SuppressAllExcept(WrappingGridNode<InventoryCategoryNode> grid, InventoryCategoryNode source)
    {
        foreach (var cat in grid.GetNodes<InventoryCategoryNode>())
            cat.SetHeaderSuppressed(!ReferenceEquals(cat, source));
    }
}
