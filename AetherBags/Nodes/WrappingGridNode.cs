using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KamiToolKit;
using KamiToolKit. Nodes;

namespace AetherBags.Nodes
{
    public sealed class WrappingGridNode<T> : LayoutListNode where T : NodeBase
    {

        public float HorizontalSpacing { get; set; } = 10f;
        public float VerticalSpacing { get; set; } = 10f;

        private readonly List<List<NodeBase>> _rows = new();
        private readonly Stack<List<NodeBase>> _rowPool = new();

        private float _requiredHeight;
        private bool _requiredHeightDirty = true;

        protected override void InternalRecalculateLayout()
        {
            RecycleAllRows();

            int count = NodeList.Count;
            if (count == 0)
            {
                _requiredHeight = 0f;
                _requiredHeightDirty = false;
                return;
            }

            float availableWidth = Width;
            float hSpace = HorizontalSpacing;
            float vSpace = VerticalSpacing;
            float startX = FirstItemSpacing;

            float currentX = startX;
            float currentY = 0f;
            float rowHeight = 0f;

            List<NodeBase> currentRow = RentRowList(capacityHint: 8);

            for (int i = 0; i < count; i++)
            {
                NodeBase node = NodeList[i];

                float nodeWidth = node.Width;

                if (currentRow.Count != 0 && (currentX + nodeWidth) > availableWidth)
                {
                    _rows.Add(currentRow);

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
    }
}