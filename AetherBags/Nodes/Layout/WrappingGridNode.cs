using KamiToolKit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AetherBags.Nodes.Layout;

public sealed class WrappingGridNode<T> : DeferrableLayoutListNode where T : NodeBase
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
    private bool _lastUseCompactPacking;
    private bool _lastPreferLargestFit;
    private bool _lastUseStableInsert;
    private int _lastCompactLookahead;

    private int[] _orderScratch = Array.Empty<int>();

    private T? _hoistedNode;
    private readonly HashSet<T> _pinned = new(ReferenceEqualityComparer<T>.Instance);

    private readonly List<NodeBase> _layoutOrder = new(capacity: 256);
    private readonly List<NodeBase> _pinnedScratch = new(capacity: 64);
    private readonly List<NodeBase> _normalScratch = new(capacity: 256);

    public WrappingGridNode()
    {
        _rowsView = new RowsReadOnlyView(_rows);
    }

    public IReadOnlyList<IReadOnlyList<NodeBase>> Rows => _rowsView;

    public T? HoistedNode => _hoistedNode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRowIndex(NodeBase node, out int rowIndex) => _rowIndex.TryGetValue(node, out rowIndex);

    public void SetHoistedNode(T? node)
    {
        if (ReferenceEquals(_hoistedNode, node))
            return;

        _hoistedNode = node;

        if (node is not null)
        {
            if (!NodeList.Contains(node))
                AddNode(node);
        }

        RecalculateLayout();
    }

    public bool PinNode(T node)
    {
        if (_pinned.Add(node))
        {
            RecalculateLayout();
            return true;
        }
        return false;
    }

    public bool UnpinNode(T node)
    {
        if (_pinned.Remove(node))
        {
            RecalculateLayout();
            return true;
        }
        return false;
    }

    public void ClearPinned()
    {
        if (_pinned.Count == 0) return;
        _pinned.Clear();
        RecalculateLayout();
    }

    public bool IsPinned(T node) => _pinned.Contains(node);

    protected override void InternalRecalculateLayout()
    {
        int layoutCount = BuildLayoutOrder(out int hoistedCount, out int pinnedCount);

        if (layoutCount == 0)
        {
            RecycleAllRows();
            _rowIndex.Clear();
            _requiredHeight = 0f;
            _requiredHeightDirty = false;
            RememberLayoutParams();
            return;
        }

        bool hasSpecials = hoistedCount != 0 || pinnedCount != 0;
        bool compactEnabled = System.Config.General.CompactPackingEnabled;

        if (compactEnabled)
        {
            if (hasSpecials)
            {
                FullReflowCompactSections(layoutCount, hoistedCount, pinnedCount);
                _requiredHeightDirty = true;
                RememberLayoutParams();
                return;
            }

            if (_rows.Count != 0 && LayoutParamsMatchLast() && NodeSetMatchesExistingLayout(layoutCount))
            {
                RepositionExistingRows();
                _requiredHeightDirty = true;
                RememberLayoutParams();
                return;
            }

            FullReflowCompact(layoutCount);
            _requiredHeightDirty = true;
            RememberLayoutParams();
            return;
        }

        if (_rows.Count != 0 &&
            NodeSetMatchesExistingLayout(layoutCount) &&
            TryUpdateLayoutWithoutReflowOrTailReflow(layoutCount, hoistedCount, pinnedCount))
        {
            _requiredHeightDirty = true;
            RememberLayoutParams();
            return;
        }

        FullReflowOrdered(layoutCount, hoistedCount, pinnedCount);
        _requiredHeightDirty = true;
        RememberLayoutParams();
    }

    private int BuildLayoutOrder(out int hoistedCount, out int pinnedCount)
    {
        _layoutOrder.Clear();
        _pinnedScratch.Clear();
        _normalScratch.Clear();

        int nodeCount = NodeList.Count;
        if (nodeCount == 0)
        {
            _hoistedNode = null;
            if (_pinned.Count != 0) _pinned.Clear();

            hoistedCount = 0;
            pinnedCount = 0;
            return 0;
        }

        var present = new HashSet<T>(ReferenceEqualityComparer<T>.Instance);

        bool hoistedPresent = false;
        T? hoisted = _hoistedNode;

        for (int i = 0; i < nodeCount; i++)
        {
            if (NodeList[i] is not T node)
                continue;

            present.Add(node);

            if (hoisted != null && ReferenceEquals(node, hoisted))
            {
                hoistedPresent = true;
                continue;
            }

            if (_pinned.Contains(node))
                _pinnedScratch.Add(node);
            else
                _normalScratch.Add(node);
        }

        if (_pinned.Count != 0)
            _pinned.RemoveWhere(n => !present.Contains(n));

        if (hoisted != null && !hoistedPresent)
            _hoistedNode = null;

        if (hoistedPresent && hoisted != null)
            _layoutOrder.Add(hoisted);

        for (int i = 0; i < _pinnedScratch.Count; i++)
            _layoutOrder.Add(_pinnedScratch[i]);

        for (int i = 0; i < _normalScratch.Count; i++)
            _layoutOrder.Add(_normalScratch[i]);

        hoistedCount = (hoistedPresent && hoisted != null) ? 1 : 0;
        pinnedCount = _pinnedScratch.Count;
        return _layoutOrder.Count;
    }


    private bool NodeSetMatchesExistingLayout(int layoutCount)
    {
        if (_rowIndex.Count != layoutCount)
            return false;

        for (int i = 0; i < layoutCount; i++)
        {
            if (!_rowIndex.ContainsKey(_layoutOrder[i]))
                return false;
        }

        return true;
    }

    private bool TryUpdateLayoutWithoutReflowOrTailReflow(int layoutCount, int hoistedCount, int pinnedCount)
    {
        if (!LayoutParamsMatchLast())
            return false;

        int mismatchRow = FindFirstMismatchRow(layoutCount, hoistedCount, pinnedCount, out int mismatchNodeIndex);

        if (mismatchRow < 0)
        {
            RepositionExistingRows();
            return true;
        }

        TailReflowFrom(mismatchRow, mismatchNodeIndex, layoutCount, hoistedCount, pinnedCount);
        return true;
    }

    private int FindFirstMismatchRow(int layoutCount, int hoistedCount, int pinnedCount, out int mismatchNodeIndex)
    {
        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float startX = FirstItemSpacing;

        int normalStart = hoistedCount + pinnedCount;

        int rowIdx = 0;
        int nodeIdx = 0;

        while (nodeIdx < layoutCount)
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

            int predictedCount;

            if (hoistedCount != 0 && nodeIdx == 0)
            {
                predictedCount = 1;
            }
            else
            {
                int sectionEnd = nodeIdx < normalStart ? normalStart : layoutCount;

                predictedCount = 0;
                float currentX = startX;

                while (nodeIdx + predictedCount < sectionEnd)
                {
                    NodeBase node = _layoutOrder[nodeIdx + predictedCount];
                    float w = node.Width;

                    if (predictedCount != 0 && (currentX + w) > availableWidth)
                        break;

                    predictedCount++;
                    currentX += w + hSpace;
                }

                if (predictedCount == 0 && nodeIdx < sectionEnd)
                    predictedCount = 1;
            }

            if (predictedCount != existingRowCount)
            {
                mismatchNodeIndex = nodeIdx;
                return rowIdx;
            }

            for (int j = 0; j < existingRowCount; j++)
            {
                if (!ReferenceEquals(existingRow[j], _layoutOrder[nodeIdx + j]))
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
        _rowIndex.EnsureCapacity(_layoutOrder.Count);

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

    private void TailReflowFrom(int startRowIndex, int startNodeIndex, int layoutCount, int hoistedCount, int pinnedCount)
    {
        _rowIndex.Clear();
        _rowIndex.EnsureCapacity(layoutCount);

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

        int normalStart = hoistedCount + pinnedCount;

        int rowIndex = startRowIndex;
        int idx = startNodeIndex;

        while (idx < layoutCount)
        {
            List<NodeBase> row = RentRowList(capacityHint: 8);

            float x = startX;
            float rowHeight = 0f;

            if (hoistedCount != 0 && idx == 0)
            {
                NodeBase node = _layoutOrder[0];

                node.X = x;
                node.Y = y;

                AdjustNode(node);

                rowHeight = node.Height;
                row.Add(node);
                _rowIndex[node] = rowIndex;

                idx = 1;
            }
            else
            {
                int sectionEnd = idx < normalStart ? normalStart : layoutCount;

                while (idx < sectionEnd)
                {
                    NodeBase node = _layoutOrder[idx];
                    float w = node.Width;

                    if (row.Count != 0 && (x + w) > availableWidth)
                        break;

                    node.X = x;
                    node.Y = y;

                    AdjustNode(node);

                    float h = node.Height;
                    if (h > rowHeight) rowHeight = h;

                    row.Add(node);
                    _rowIndex[node] = rowIndex;

                    x += w + hSpace;
                    idx++;
                }

                if (row.Count == 0 && idx < sectionEnd)
                {
                    NodeBase node = _layoutOrder[idx];

                    node.X = startX;
                    node.Y = y;

                    AdjustNode(node);

                    rowHeight = node.Height;

                    row.Add(node);
                    _rowIndex[node] = rowIndex;

                    idx++;
                }
            }

            if (row.Count != 0)
            {
                _rows.Add(row);
                rowIndex++;
                y += rowHeight + vSpace;
            }
            else
            {
                RecycleRow(row);
                break;
            }
        }
    }

    private void FullReflowOrdered(int layoutCount, int hoistedCount, int pinnedCount)
    {
        RecycleAllRows();
        _rowIndex.Clear();
        _rowIndex.EnsureCapacity(layoutCount);

        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float vSpace = VerticalSpacing;
        float startX = FirstItemSpacing;

        float y = TopPadding;

        int normalStart = hoistedCount + pinnedCount;

        int rowIdx = 0;
        int idx = 0;

        while (idx < layoutCount)
        {
            List<NodeBase> row = RentRowList(capacityHint: 8);

            float x = startX;
            float rowHeight = 0f;

            if (hoistedCount != 0 && idx == 0)
            {
                NodeBase node = _layoutOrder[0];

                node.X = x;
                node.Y = y;

                AdjustNode(node);

                rowHeight = node.Height;

                row.Add(node);
                _rowIndex[node] = rowIdx;

                idx = 1;
            }
            else
            {
                int sectionEnd = idx < normalStart ? normalStart : layoutCount;

                while (idx < sectionEnd)
                {
                    NodeBase node = _layoutOrder[idx];
                    float w = node.Width;

                    if (row.Count != 0 && (x + w) > availableWidth)
                        break;

                    node.X = x;
                    node.Y = y;

                    AdjustNode(node);

                    float h = node.Height;
                    if (h > rowHeight) rowHeight = h;

                    row.Add(node);
                    _rowIndex[node] = rowIdx;

                    x += w + hSpace;
                    idx++;
                }

                if (row.Count == 0 && idx < sectionEnd)
                {
                    NodeBase node = _layoutOrder[idx];

                    node.X = startX;
                    node.Y = y;

                    AdjustNode(node);

                    rowHeight = node.Height;

                    row.Add(node);
                    _rowIndex[node] = rowIdx;

                    idx++;
                }
            }

            if (row.Count != 0)
            {
                _rows.Add(row);
                rowIdx++;
                y += rowHeight + vSpace;
            }
            else
            {
                RecycleRow(row);
                break;
            }
        }
    }

    private void FullReflowCompactSections(int layoutCount, int hoistedCount, int pinnedCount)
    {
        RecycleAllRows();
        _rowIndex.Clear();
        _rowIndex.EnsureCapacity(layoutCount);

        float vSpace = VerticalSpacing;
        float y = TopPadding;

        int rowIdx = 0;
        int idx = 0;

        if (hoistedCount != 0)
        {
            NodeBase node = _layoutOrder[0];
            List<NodeBase> row = RentRowList(capacityHint: 1);

            node.X = FirstItemSpacing;
            node.Y = y;

            AdjustNode(node);

            row.Add(node);
            _rowIndex[node] = rowIdx;

            _rows.Add(row);

            y += node.Height + vSpace;
            rowIdx++;
            idx = 1;
        }

        int pinnedStart = idx;
        int pinnedEnd = pinnedStart + pinnedCount;
        if (pinnedCount > 0)
        {
            PackSectionCompact(pinnedStart, pinnedEnd, ref y, ref rowIdx);
            idx = pinnedEnd;
        }

        if (idx < layoutCount)
        {
            PackSectionCompact(idx, layoutCount, ref y, ref rowIdx);
        }
    }

    private void PackSectionCompact(int startIndex, int endIndex, ref float y, ref int rowIdx)
    {
        int sectionCount = endIndex - startIndex;
        if (sectionCount <= 0)
            return;

        float availableWidth = Width;
        float hSpace = HorizontalSpacing;
        float vSpace = VerticalSpacing;
        float startX = FirstItemSpacing;

        EnsureOrderScratch(sectionCount);
        for (int i = 0; i < sectionCount; i++)
            _orderScratch[i] = i;

        int lookahead = System.Config.General.CompactLookahead;
        if (lookahead < 0) lookahead = 0;

        int p = 0;

        while (p < sectionCount)
        {
            List<NodeBase> row = RentRowList(capacityHint: 8);

            float x = startX;
            float rowHeight = 0f;

            while (p < sectionCount)
            {
                int localIdx = _orderScratch[p];
                NodeBase node = _layoutOrder[startIndex + localIdx];
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
                if (end >= sectionCount) end = sectionCount - 1;

                for (int s = p + 1; s <= end; s++)
                {
                    int candLocalIdx = _orderScratch[s];
                    NodeBase cand = _layoutOrder[startIndex + candLocalIdx];
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
                int localIdx = _orderScratch[p];
                NodeBase node = _layoutOrder[startIndex + localIdx];

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
                NodeBase node = _layoutOrder[idx];
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
                    NodeBase cand = _layoutOrder[candIdx];
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
                NodeBase node = _layoutOrder[idx];

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
        int count = _layoutOrder.Count;

        for (int i = 0; i < count; i++)
        {
            NodeBase node = _layoutOrder[i];
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
            _lastUseCompactPacking == System.Config.General.CompactPackingEnabled &&
            _lastPreferLargestFit == System.Config.General.CompactPreferLargestFit &&
            _lastUseStableInsert == System.Config.General.CompactStableInsert &&
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

        _lastUseCompactPacking = System.Config.General.CompactPackingEnabled;
        _lastPreferLargestFit = System.Config.General.CompactPreferLargestFit;
        _lastUseStableInsert = System.Config.General.CompactStableInsert;
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
