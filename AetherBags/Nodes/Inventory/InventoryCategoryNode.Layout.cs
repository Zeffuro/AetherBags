using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory.Layout;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;

namespace AetherBags.Nodes.Inventory;

public partial class InventoryCategoryNode
{
    private int _layoutSlotCount;
    internal ulong LayoutKey { get; private set; }

    internal void SetLayoutData(InventoryLayoutBlock block, bool showEmptySlots, bool deferItems)
    {
        LayoutKey = block.Key;
        _layoutSlotCount = block.SlotCount;
        _itemGridNode.ItemSlots = block.ItemSlots;
        _itemGridNode.ShowEmptySlots = showEmptySlots;
        SetCategoryData(block.Inventory, block.Columns, deferItems);
        RecalculateSize();
        _itemGridNode.RecalculateLayout();
        _itemGridNode.UpdateEmptySlots(_layoutSlotCount);
    }

    public override void RecalculateSize()
    {
        int itemCount = Math.Max(CategorizedInventory.Items.Count, _layoutSlotCount);
        float cellW = ExpectedItemWidth, cellH = ExpectedItemHeight;
        float hPad = _itemGridNode.HorizontalPadding, vPad = _itemGridNode.VerticalPadding;

        if (itemCount == 0)
        {
            float width = _fixedWidth ?? MinWidth;
            if (_maxWidth.HasValue) width = Math.Min(width, _maxWidth.Value);
            Size = new Vector2(width, HeaderHeight);
            _hoverCollisionNode.Size = Size;
            _baseHeaderWidth = width;
            _itemGridNode.Position = new Vector2(0, HeaderHeight);
            _itemGridNode.Size = new Vector2(width, 0);
            ApplyHeaderVisualState();
            return;
        }

        int itemsPerLine = Math.Max(1, _itemGridNode.ItemsPerLine);
        if (_maxWidth.HasValue && _fixedWidth is null && _maxWidth.Value >= cellW)
        {
            int maxColumns = Math.Max(1, (int)MathF.Floor((_maxWidth.Value + hPad) / (cellW + hPad)));
            if (maxColumns * cellW + (maxColumns - 1) * hPad > _maxWidth.Value && maxColumns > 1) maxColumns--;
            itemsPerLine = Math.Min(itemsPerLine, maxColumns);
        }

        int rows = (itemCount + itemsPerLine - 1) / itemsPerLine;
        int actualColumns = Math.Min(itemCount, itemsPerLine);
        float calculatedWidth = _fixedWidth ?? Math.Max(MinWidth, actualColumns * cellW + (actualColumns - 1) * hPad);
        if (_maxWidth.HasValue && _fixedWidth is null && _maxWidth.Value >= cellW)
            calculatedWidth = Math.Min(calculatedWidth, _maxWidth.Value);

        float height = HeaderHeight + rows * cellH + (rows - 1) * vPad;
        Size = new Vector2(calculatedWidth, height);
        _hoverCollisionNode.Size = Size;
        _itemGridNode.Position = new Vector2(0, HeaderHeight);
        _itemGridNode.Size = new Vector2(calculatedWidth, height - HeaderHeight);
        if (_itemGridNode.ItemsPerLine != itemsPerLine) _itemGridNode.ItemsPerLine = itemsPerLine;
        _baseHeaderWidth = calculatedWidth;
        ApplyHeaderVisualState();
    }

    private sealed class InventoryItemGridNode : HybridDirectionalFlexNode<DragDropNode>
    {
        private readonly List<SimpleImageNode> _emptySlots = [];
        private readonly HashSet<int> _occupied = [];
        public IReadOnlyDictionary<ulong, int>? ItemSlots { get; set; }
        public bool ShowEmptySlots { get; set; }

        protected override void AdjustNode(NodeBase node)
        {
            if (node is InventoryDragDropNode item && ItemSlots != null
                && ItemSlots.TryGetValue(item.ItemInfo.Key, out int slot))
                node.Position = GetSlotPosition(slot);
        }

        private Vector2 GetSlotPosition(int slot)
        {
            int columns = Math.Max(1, ItemsPerLine);
            return new(slot % columns * (ExpectedItemWidth + HorizontalPadding),
                slot / columns * (ExpectedItemHeight + VerticalPadding));
        }

        public void UpdateEmptySlots(int count)
        {
            _occupied.Clear();
            int index = 0;
            foreach (var node in Nodes)
            {
                if (node is not InventoryDragDropNode item) continue;
                int slot = ItemSlots != null && ItemSlots.TryGetValue(item.ItemInfo.Key, out int reserved) ? reserved : index;
                _occupied.Add(slot);
                index++;
            }
            int visible = 0;
            if (ShowEmptySlots)
            {
                for (int slot = 0; slot < count; slot++)
                {
                    if (_occupied.Contains(slot)) continue;
                    if (visible == _emptySlots.Count)
                    {
                        var image = new SimpleImageNode
                        {
                            Size = new Vector2(44),
                            TextureCoordinates = Vector2.Zero,
                            TextureSize = new Vector2(44),
                            WrapMode = WrapMode.Tile,
                            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
                        };
                        image.LoadTexture("ui/uld/DragTargetA.tex", false);
                        image.AttachNode(this, NodePosition.AsFirstChild);
                        _emptySlots.Add(image);
                    }
                    var frame = _emptySlots[visible++];
                    frame.Position = GetSlotPosition(slot);
                    frame.IsVisible = true;
                }
            }
            for (int i = visible; i < _emptySlots.Count; i++) _emptySlots[i].IsVisible = false;
        }
    }
}
