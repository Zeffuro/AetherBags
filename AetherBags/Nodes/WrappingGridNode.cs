using KamiToolKit;
using KamiToolKit.Nodes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AetherBags.Nodes;

public sealed class WrappingGridNode<T> : LayoutListNode where T : NodeBase
{
    public float HorizontalSpacing { get; set; } = 10f;
    public float VerticalSpacing { get; set; } = 10f;

    public float TopPadding { get; set; } = 0f;
    public float BottomPadding { get; set; } = 0f;

    private readonly List<List<NodeBase>> _rows = new();
    private readonly Stack<List<NodeBase>> _rowPool = new();

    private readonly Dictionary<NodeBase, int> _rowIndex = new(ReferenceEqualityComparer<NodeBase>.Instance);

    private float _requiredHeight;
    private bool _requiredHeightDirty = true;

    private readonly IReadOnlyList<IReadOnlyList<NodeBase>> _rowsView;

    public WrappingGridNode()
    {
        _rowsView = new RowsReadOnlyView(_rows);
    }

    public IReadOnlyList<IReadOnlyList<NodeBase>> Rows => _rowsView;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRowIndex(NodeBase node, out int rowIndex) => _rowIndex.TryGetValue(node, out rowIndex);

    protected override void InternalRecalculateLayout()
    {
        RecycleAllRows();
        _rowIndex.Clear();

        int count = NodeList.Count;
        if (count == 0)
        {
            _requiredHeight = 0f;
            _requiredHeightDirty = false;
            return;
        }

        _rowIndex.EnsureCapacity(count);

        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float vSpace = VerticalSpacing;
        float startX = FirstItemSpacing;

        float currentX = startX;
        float currentY = TopPadding;
        float rowHeight = 0f;

        int currentRowIndex = 0;
        List<NodeBase> currentRow = RentRowList(capacityHint: 8);

        for (int i = 0; i < count; i++)
        {
            NodeBase node = NodeList[i];

            float nodeWidth = node.Width;

            if (currentRow.Count != 0 && (currentX + nodeWidth) > availableWidth)
            {
                _rows.Add(currentRow);
                currentRowIndex++;

                currentY += rowHeight + vSpace;
                currentX = startX;
                rowHeight = 0f;

                currentRow = RentRowList(capacityHint: 8);
            }

            node.X = currentX;
            node.Y = currentY;

            AdjustNode(node);

            float nodeHeight = node.Height;
            if (nodeHeight > rowHeight) rowHeight = nodeHeight;

            currentRow.Add(node);
            _rowIndex[node] = currentRowIndex;

            currentX += nodeWidth + hSpace;
        }

        if (currentRow.Count != 0)
        {
            _rows.Add(currentRow);
        }
        else
        {
            RecycleRow(currentRow);
        }

        _requiredHeightDirty = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetRequiredHeight()
    {
        if (!_requiredHeightDirty) return _requiredHeight;

        float maxBottom = 0f;
        int count = NodeList.Count;

        for (int i = 0; i < count; i++)
        {
            NodeBase node = NodeList[i];
            float bottom = node.Y + node.Height;
            if (bottom > maxBottom) maxBottom = bottom;
        }


        maxBottom += BottomPadding;

        _requiredHeight = maxBottom;
        _requiredHeightDirty = false;
        return maxBottom;
    }

    private void RecycleAllRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            List<NodeBase> row = _rows[i];
            row.Clear();
            _rowPool.Push(row);
        }
        _rows.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<NodeBase> RentRowList(int capacityHint)
    {
        if (_rowPool.Count != 0)
        {
            List<NodeBase> row = _rowPool.Pop();
            if (row.Capacity < capacityHint) row.Capacity = capacityHint;
            return row;
        }

        return new List<NodeBase>(capacityHint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecycleRow(List<NodeBase> row)
    {
        row.Clear();
        _rowPool.Push(row);
    }

    private sealed class RowsReadOnlyView : IReadOnlyList<IReadOnlyList<NodeBase>>
    {
        private readonly List<List<NodeBase>> _rows;
        public RowsReadOnlyView(List<List<NodeBase>> rows) => _rows = rows;

        public int Count => _rows.Count;
        public IReadOnlyList<NodeBase> this[int index] => _rows[index];

        public IEnumerator<IReadOnlyList<NodeBase>> GetEnumerator()
        {
            for (int i = 0; i < _rows.Count; i++)
                yield return _rows[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private sealed class ReferenceEqualityComparer<TRef> : IEqualityComparer<TRef> where TRef : class
    {
        public static readonly ReferenceEqualityComparer<TRef> Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TRef? x, TRef? y) => ReferenceEquals(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(TRef obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
