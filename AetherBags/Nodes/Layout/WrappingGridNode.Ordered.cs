using System;
using System.Collections.Generic;
using KamiToolKit.BaseTypes;

namespace AetherBags.Nodes.Layout;

public sealed partial class WrappingGridNode<T> where T : NodeBase
{
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

}
