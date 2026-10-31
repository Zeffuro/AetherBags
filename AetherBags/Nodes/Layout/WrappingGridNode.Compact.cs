using System;
using System.Collections.Generic;
using KamiToolKit.BaseTypes;

namespace AetherBags.Nodes.Layout;

public sealed partial class WrappingGridNode<T> where T : NodeBase
{
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

}
