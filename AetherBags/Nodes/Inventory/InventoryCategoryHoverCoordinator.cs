using System;
using AetherBags.Nodes.Layout;

namespace AetherBags.Nodes.Inventory;

public sealed class InventoryCategoryHoverCoordinator
{
    private InventoryCategoryNode? _active;
    private int _activeRowIndex = -1;
    private int _activeSourceIdx = -1;
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
                UnsuppressActive(grid);

                _active = source;

                if (!grid.TryGetRowIndex(source, out _activeRowIndex))
                {
                    source.SetHeaderSuppressed(false);
                    _activeSourceIdx = -1;
                    return;
                }

                var row = grid.Rows[_activeRowIndex];

                _activeSourceIdx = -1;
                for (int i = 0; i < row.Count; i++)
                {
                    if (ReferenceEquals(row[i], source))
                    {
                        _activeSourceIdx = i;
                        break;
                    }
                }

                if (_activeSourceIdx < 0)
                {
                    source.SetHeaderSuppressed(false);
                    return;
                }

                float expandedWidth = source.GetExpandedHeaderWidth();
                float textRightEdge = source.Position.X + expandedWidth;

                for (int i = _activeSourceIdx + 1; i < row.Count; i++)
                {
                    if (row[i] is not InventoryCategoryNode cat)
                        continue;

                    if (cat.Position.X >= textRightEdge)
                        break;

                    cat.SetHeaderSuppressed(true);
                }

                source.SetHeaderSuppressed(false);
                return;
            }

            if (!ReferenceEquals(_active, source))
                return;

            UnsuppressActive(grid);
            _active = null;
            _activeRowIndex = -1;
            _activeSourceIdx = -1;
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
        _activeSourceIdx = -1;
        ClearAll(grid);
    }

    private void UnsuppressActive(WrappingGridNode<InventoryCategoryNodeBase> grid)
    {
        if (_active is null || _activeSourceIdx < 0
            || _activeRowIndex < 0 || _activeRowIndex >= grid.Rows.Count)
            return;

        var row = grid.Rows[_activeRowIndex];

        for (int i = _activeSourceIdx + 1; i < row.Count; i++)
        {
            if (row[i] is InventoryCategoryNode cat)
                cat.SetHeaderSuppressed(false);
        }
    }

    private static void ClearAll(WrappingGridNode<InventoryCategoryNodeBase> grid)
    {
        foreach (var node in grid.GetNodes<InventoryCategoryNodeBase>())
        {
            if (node is InventoryCategoryNode cat)
                cat.SetHeaderSuppressed(false);
        }
    }
}
