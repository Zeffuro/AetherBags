using System.Collections.Generic;
using System.Collections;
using System.Runtime.CompilerServices;
using System;
using KamiToolKit.BaseTypes;

namespace AetherBags.Nodes.Layout;

public sealed partial class WrappingGridNode<T> : DeferrableLayoutListNode where T : NodeBase
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
    private bool _forceFullReflow;

    private T? _hoistedNode;
    private readonly HashSet<T> _pinned = new(ReferenceEqualityComparer<T>.Instance);

    private readonly List<NodeBase> _layoutOrder = new(capacity: 256);
    private readonly List<NodeBase> _pinnedScratch = new(capacity: 64);
    private readonly List<NodeBase> _normalScratch = new(capacity: 256);
    private readonly HashSet<T> _presentScratch = new(ReferenceEqualityComparer<T>.Instance);

    public WrappingGridNode()
    {
        _rowsView = new RowsReadOnlyView(_rows);
    }

    public IReadOnlyList<IReadOnlyList<NodeBase>> Rows => _rowsView;

    public T? HoistedNode => _hoistedNode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRowIndex(NodeBase node, out int rowIndex) => _rowIndex.TryGetValue(node, out rowIndex);

    public void InvalidateLayout()
    {
        _forceFullReflow = true;
    }

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
            _forceFullReflow = false;
            RememberLayoutParams();
            return;
        }

        bool forceReflow = _forceFullReflow;
        _forceFullReflow = false;

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

            if (!forceReflow && _rows.Count != 0 && LayoutParamsMatchLast() && NodeSetMatchesExistingLayout(layoutCount))
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

        if (!forceReflow &&
            _rows.Count != 0 &&
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

        _presentScratch.Clear();
        var present = _presentScratch;

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
