using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KamiToolKit;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

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

    private float _lastAvailableWidth = float.NaN;
    private float _lastStartX = float.NaN;
    private float _lastHSpace = float.NaN;
    private float _lastVSpace = float.NaN;
    private float _lastTopPadding = float.NaN;
    private float _lastBottomPadding = float.NaN;
    private bool _lastuseCompactPacking;
    private bool _lastpreferLargestFit;
    private bool _lastuseStableInsert;
    private int _lastCompactLookahead;

    private int[] _orderScratch = Array.Empty<int>();

    public WrappingGridNode()
    {
        _rowsView = new RowsReadOnlyView(_rows);
    }

    public IReadOnlyList<IReadOnlyList<NodeBase>> Rows => _rowsView;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRowIndex(NodeBase node, out int rowIndex) => _rowIndex.TryGetValue(node, out rowIndex);

    protected override void InternalRecalculateLayout()
    {
        int count = NodeList.Count;
        if (count == 0)
        {
            RecycleAllRows();
            _rowIndex.Clear();
            _requiredHeight = 0f;
            _requiredHeightDirty = false;
            RememberLayoutParams();
            return;
        }

        if (System.Config.General.CompactPackingEnabled)
        {
            if (_rows.Count != 0 && LayoutParamsMatchLast() && NodeSetMatchesExistingLayout(count))
            {
                RepositionExistingRows();
                _requiredHeightDirty = true;
                RememberLayoutParams();
                return;
            }

            FullReflowCompact(count);
            _requiredHeightDirty = true;
            RememberLayoutParams();
            return;
        }

        if (_rows.Count != 0 && TryUpdateLayoutWithoutReflowOrTailReflow(count))
        {
            _requiredHeightDirty = true;
            RememberLayoutParams();
            return;
        }

        FullReflow(count);
        _requiredHeightDirty = true;
        RememberLayoutParams();
    }

    private bool NodeSetMatchesExistingLayout(int count)
    {
        if (_rowIndex.Count != count)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (!_rowIndex.ContainsKey(NodeList[i]))
                return false;
        }

        return true;
    }

    private bool TryUpdateLayoutWithoutReflowOrTailReflow(int count)
    {
        if (!LayoutParamsMatchLast())
            return false;

        int mismatchRow = FindFirstMismatchRow(count, out int mismatchNodeIndex);

        if (mismatchRow < 0)
        {
            RepositionExistingRows();
            return true;
        }

        TailReflowFrom(mismatchRow, mismatchNodeIndex, count);
        return true;
    }

    private int FindFirstMismatchRow(int count, out int mismatchNodeIndex)
    {
        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float startX = FirstItemSpacing;

        int rowIdx = 0;
        int nodeIdx = 0;

        while (nodeIdx < count)
        {
            if (rowIdx >= _rows.Count)
            {
                mismatchNodeIndex = nodeIdx;
                return rowIdx;
            }

            List<NodeBase> existingRow = _rows[rowIdx];
            int existingRowCount = existingRow.Count;

            if (existingRowCount == 0)
            {
                mismatchNodeIndex = nodeIdx;
                return rowIdx;
            }

            int predictedCount = 0;
            float currentX = startX;

            while (nodeIdx + predictedCount < count)
            {
                NodeBase node = NodeList[nodeIdx + predictedCount];
                float w = node.Width;

                if (predictedCount != 0 && (currentX + w) > availableWidth)
                    break;

                predictedCount++;
                currentX += w + hSpace;
            }

            if (predictedCount != existingRowCount)
            {
                mismatchNodeIndex = nodeIdx;
                return rowIdx;
            }

            for (int j = 0; j < existingRowCount; j++)
            {
                if (!ReferenceEquals(existingRow[j], NodeList[nodeIdx + j]))
                {
                    mismatchNodeIndex = nodeIdx;
                    return rowIdx;
                }
            }

            nodeIdx += existingRowCount;
            rowIdx++;
        }

        if (rowIdx < _rows.Count)
        {
            mismatchNodeIndex = nodeIdx;
            return rowIdx;
        }

        mismatchNodeIndex = -1;
        return -1;
    }

    private void RepositionExistingRows()
    {
        _rowIndex.Clear();
        _rowIndex.EnsureCapacity(NodeList.Count);

        float hSpace = HorizontalSpacing;
        float vSpace = VerticalSpacing;
        float startX = FirstItemSpacing;

        float y = TopPadding;

        for (int rowIdx = 0; rowIdx < _rows.Count; rowIdx++)
        {
            List<NodeBase> row = _rows[rowIdx];
            float x = startX;
            float rowHeight = 0f;

            for (int j = 0; j < row.Count; j++)
            {
                NodeBase node = row[j];

                node.X = x;
                node.Y = y;

                AdjustNode(node);

                float h = node.Height;
                if (h > rowHeight) rowHeight = h;

                _rowIndex[node] = rowIdx;

                x += node.Width + hSpace;
            }

            y += rowHeight + vSpace;
        }
    }

    private void TailReflowFrom(int startRowIndex, int startNodeIndex, int count)
    {
        _rowIndex.Clear();
        _rowIndex.EnsureCapacity(count);

        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float vSpace = VerticalSpacing;
        float startX = FirstItemSpacing;

        float y = TopPadding;

        if ((uint)startRowIndex > (uint)_rows.Count)
            startRowIndex = _rows.Count;

        for (int rowIdx = 0; rowIdx < startRowIndex; rowIdx++)
        {
            List<NodeBase> row = _rows[rowIdx];
            float x = startX;
            float rowHeight = 0f;

            for (int j = 0; j < row.Count; j++)
            {
                NodeBase node = row[j];

                node.X = x;
                node.Y = y;

                AdjustNode(node);

                float h = node.Height;
                if (h > rowHeight) rowHeight = h;

                _rowIndex[node] = rowIdx;

                x += node.Width + hSpace;
            }

            y += rowHeight + vSpace;
        }

        for (int i = _rows.Count - 1; i >= startRowIndex; i--)
        {
            List<NodeBase> row = _rows[i];
            row.Clear();
            _rowPool.Push(row);
            _rows.RemoveAt(i);
        }

        int currentRowIndex = startRowIndex;
        float xCursor = startX;
        float rowHeightTail = 0f;

        List<NodeBase> currentRow = RentRowList(capacityHint: 8);

        for (int i = startNodeIndex; i < count; i++)
        {
            NodeBase node = NodeList[i];
            float w = node.Width;

            if (currentRow.Count != 0 && (xCursor + w) > availableWidth)
            {
                _rows.Add(currentRow);
                currentRowIndex++;

                y += rowHeightTail + vSpace;
                xCursor = startX;
                rowHeightTail = 0f;

                currentRow = RentRowList(capacityHint: 8);
            }

            node.X = xCursor;
            node.Y = y;

            AdjustNode(node);

            float h = node.Height;
            if (h > rowHeightTail) rowHeightTail = h;

            currentRow.Add(node);
            _rowIndex[node] = currentRowIndex;

            xCursor += w + hSpace;
        }

        if (currentRow.Count != 0)
        {
            _rows.Add(currentRow);
        }
        else
        {
            RecycleRow(currentRow);
        }
    }

    private void FullReflow(int count)
    {
        RecycleAllRows();
        _rowIndex.Clear();
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
    }

    private void FullReflowCompact(int count)
    {
        RecycleAllRows();
        _rowIndex.Clear();
        _rowIndex.EnsureCapacity(count);

        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float vSpace = VerticalSpacing;
        float startX = FirstItemSpacing;

        float y = TopPadding;

        EnsureOrderScratch(count);
        for (int i = 0; i < count; i++)
            _orderScratch[i] = i;

        int lookahead = System.Config.General.CompactLookahead;
        if (lookahead < 0) lookahead = 0;

        int p = 0;
        int rowIdx = 0;

        while (p < count)
        {
            List<NodeBase> row = RentRowList(capacityHint: 8);

            float x = startX;
            float rowHeight = 0f;

            while (p < count)
            {
                int idx = _orderScratch[p];
                NodeBase node = NodeList[idx];
                float w = node.Width;

                if (row.Count == 0 || (x + w) <= availableWidth)
                {
                    node.X = x;
                    node.Y = y;

                    AdjustNode(node);

                    float h = node.Height;
                    if (h > rowHeight) rowHeight = h;

                    row.Add(node);
                    _rowIndex[node] = rowIdx;

                    x += w + hSpace;
                    p++;
                    continue;
                }

                int bestPos = -1;
                float bestWidth = 0f;

                int end = p + lookahead;
                if (end >= count) end = count - 1;

                for (int s = p + 1; s <= end; s++)
                {
                    int candIdx = _orderScratch[s];
                    NodeBase cand = NodeList[candIdx];
                    float cw = cand.Width;

                    if ((x + cw) <= availableWidth)
                    {
                        if (!System.Config.General.CompactPreferLargestFit)
                        {
                            bestPos = s;
                            break;
                        }

                        if (cw > bestWidth)
                        {
                            bestWidth = cw;
                            bestPos = s;
                        }
                    }
                }

                if (bestPos < 0)
                    break;

                if (bestPos != p)
                {
                    int chosen = _orderScratch[bestPos];

                    if (System.Config.General.CompactStableInsert)
                    {
                        Array.Copy(_orderScratch, p, _orderScratch, p + 1, bestPos - p);
                        _orderScratch[p] = chosen;
                    }
                    else
                    {
                        _orderScratch[bestPos] = _orderScratch[p];
                        _orderScratch[p] = chosen;
                    }
                }
            }

            if (row.Count == 0)
            {
                int idx = _orderScratch[p];
                NodeBase node = NodeList[idx];
                float w = node.Width;

                node.X = startX;
                node.Y = y;

                AdjustNode(node);

                rowHeight = node.Height;

                row.Add(node);
                _rowIndex[node] = rowIdx;

                p++;
            }

            _rows.Add(row);
            rowIdx++;

            y += rowHeight + vSpace;
        }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NearlyEqual(float a, float b)
    {
        float diff = MathF.Abs(a - b);
        if (diff <= 0.05f) return true;

        float max = MathF.Max(MathF.Abs(a), MathF.Abs(b));
        return diff <= max * 0.0005f;
    }

    private bool LayoutParamsMatchLast()
    {
        return
            NearlyEqual(_lastAvailableWidth, Width) &&
            NearlyEqual(_lastStartX, FirstItemSpacing) &&
            NearlyEqual(_lastHSpace, HorizontalSpacing) &&
            NearlyEqual(_lastVSpace, VerticalSpacing) &&
            NearlyEqual(_lastTopPadding, TopPadding) &&
            NearlyEqual(_lastBottomPadding, BottomPadding) &&
            _lastuseCompactPacking == System.Config.General.CompactPackingEnabled &&
            _lastpreferLargestFit == System.Config.General.CompactPreferLargestFit &&
            _lastuseStableInsert == System.Config.General.CompactStableInsert &&
            _lastCompactLookahead == System.Config.General.CompactLookahead;
    }

    private void RememberLayoutParams()
    {
        _lastAvailableWidth = Width;
        _lastStartX = FirstItemSpacing;
        _lastHSpace = HorizontalSpacing;
        _lastVSpace = VerticalSpacing;
        _lastTopPadding = TopPadding;
        _lastBottomPadding = BottomPadding;

        _lastuseCompactPacking = System.Config.General.CompactPackingEnabled;
        _lastpreferLargestFit = System.Config.General.CompactPreferLargestFit;
        _lastuseStableInsert = System.Config.General.CompactStableInsert;
        _lastCompactLookahead = System.Config.General.CompactLookahead;
    }

    private void EnsureOrderScratch(int needed)
    {
        if (_orderScratch.Length >= needed)
            return;

        int newSize = _orderScratch.Length == 0 ? 64 : _orderScratch.Length;
        while (newSize < needed) newSize *= 2;

        _orderScratch = new int[newSize];
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
