using System;
using System.Collections.Generic;
using System.Numerics;
using KamiToolKit.BaseTypes;

namespace AetherBags.Nodes.Layout;

public sealed partial class WrappingGridNode<T> where T : NodeBase
{
    private readonly Dictionary<NodeBase, Vector2> _heldPositions = new(ReferenceEqualityComparer<NodeBase>.Instance);
    private readonly List<NodeBase> _heldOrder = [];
    private float _heldWidth;

    public bool HoldPositions
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            _heldPositions.Clear();
            InvalidateLayout();
        }
    }

    public bool SeparateRows { get; set; }

    public void ResetHeldPositions()
    {
        _heldPositions.Clear();
        InvalidateLayout();
    }

    private void CaptureHeldLayout()
    {
        if (!HoldPositions) return;
        _heldPositions.Clear();
        foreach (var node in _layoutOrder) _heldPositions[node] = node.Position;
        _heldWidth = Width;
    }

    private bool TryRecalculateHeldLayout()
    {
        if (!HoldPositions || _heldPositions.Count == 0 || !NearlyEqual(_heldWidth, Width)) return false;
        int count = BuildLayoutOrder(out _, out _);
        float bottom = TopPadding;
        foreach (var node in _layoutOrder)
        {
            if (!_heldPositions.TryGetValue(node, out var position)) continue;
            node.Position = position;
            bottom = Math.Max(bottom, node.Y + node.Height + VerticalSpacing);
        }
        if (_hoistedNode != null && _heldPositions.ContainsKey(_hoistedNode))
        {
            bool overlaps = false;
            float end = TopPadding;
            foreach (var node in _layoutOrder)
            {
                if (ReferenceEquals(node, _hoistedNode) || !_heldPositions.ContainsKey(node)) continue;
                end = Math.Max(end, node.Y + node.Height + VerticalSpacing);
                if (node.X < _hoistedNode.X + _hoistedNode.Width && node.X + node.Width > _hoistedNode.X
                    && node.Y < _hoistedNode.Y + _hoistedNode.Height && node.Y + node.Height > _hoistedNode.Y)
                    overlaps = true;
            }
            if (overlaps)
            {
                _hoistedNode.Y = end;
                _heldPositions[_hoistedNode] = _hoistedNode.Position;
                bottom = Math.Max(end + _hoistedNode.Height + VerticalSpacing, bottom);
            }
        }

        float x = FirstItemSpacing;
        float y = bottom;
        float rowHeight = 0;
        foreach (var node in _layoutOrder)
        {
            if (_heldPositions.ContainsKey(node)) continue;
            if (x > FirstItemSpacing && (SeparateRows || x + node.Width > Width))
            {
                x = FirstItemSpacing;
                y += rowHeight + VerticalSpacing;
                rowHeight = 0;
            }
            node.Position = new Vector2(x, y);
            _heldPositions[node] = node.Position;
            rowHeight = Math.Max(rowHeight, node.Height);
            x += node.Width + HorizontalSpacing;
        }

        _heldOrder.Clear();
        _heldOrder.AddRange(_layoutOrder);
        _heldOrder.Sort(static (a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        RecycleAllRows();
        _rowIndex.Clear();
        List<NodeBase>? row = null;
        float lastY = float.NaN;
        foreach (var node in _heldOrder)
        {
            if (row == null || !NearlyEqual(lastY, node.Y))
            {
                row = RentRowList(8);
                _rows.Add(row);
                lastY = node.Y;
            }
            row.Add(node);
            _rowIndex[node] = _rows.Count - 1;
            AdjustNode(node);
        }
        if (_heldPositions.Count != count)
        {
            foreach (var node in new List<NodeBase>(_heldPositions.Keys))
                if (!_rowIndex.ContainsKey(node)) _heldPositions.Remove(node);
        }
        _requiredHeightDirty = true;
        _forceFullReflow = false;
        RememberLayoutParams();
        return true;
    }

    private void RecalculateSeparateRows(int count)
    {
        RecycleAllRows();
        _rowIndex.Clear();
        float y = TopPadding;
        for (int i = 0; i < count; i++)
        {
            var node = _layoutOrder[i];
            node.Position = new Vector2(FirstItemSpacing, y);
            var row = RentRowList(1);
            row.Add(node);
            _rows.Add(row);
            _rowIndex[node] = i;
            AdjustNode(node);
            y += node.Height + VerticalSpacing;
        }
        _forceFullReflow = false;
        _requiredHeightDirty = true;
        RememberLayoutParams();
    }
}
