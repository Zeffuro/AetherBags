using AetherBags.Nodes.Layout;

namespace AetherBags.Nodes.Inventory;

public sealed class InventoryCategoryHoverCoordinator
{
    private InventoryCategoryNode? _active;
    private int _activeRowIndex = -1;
    private bool _isProcessing;

    public void OnCategoryHoverChanged(
        WrappingGridNode<InventoryCategoryNodeBase> grid,
        InventoryCategoryNode source,
        bool hovering)
    {
        if (_isProcessing)
            return;

        try
        {
            _isProcessing = true;
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
        finally
        {
            _isProcessing = false;
        }
    }

    public void ResetAll(WrappingGridNode<InventoryCategoryNodeBase> grid)
    {
        _active = null;
        _activeRowIndex = -1;
        ClearAll(grid);
    }

    private static void ClearAll(WrappingGridNode<InventoryCategoryNodeBase> grid)
    {
        foreach (var node in grid.GetNodes<InventoryCategoryNodeBase>())
        {
            if (node is InventoryCategoryNode cat)
                cat.SetHeaderSuppressed(false);
        }
    }

    private static void SuppressAllExcept(WrappingGridNode<InventoryCategoryNodeBase> grid, InventoryCategoryNode source)
    {
        foreach (var node in grid.GetNodes<InventoryCategoryNodeBase>())
        {
            if (node is InventoryCategoryNode cat)
                cat.SetHeaderSuppressed(!ReferenceEquals(cat, source));
        }
    }
}
