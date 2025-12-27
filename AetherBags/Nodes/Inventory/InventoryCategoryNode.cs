using System;
using System.Numerics;
using AetherBags.Extensions;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

// TODO: Switch back to CS version when Dalamud Updated
using DragDropFixedNode = AetherBags.Nodes.DragDropNode;

namespace AetherBags.Nodes.Inventory;

public class InventoryCategoryNode : SimpleComponentNode
{
    private readonly TextNode _categoryNameTextNode;
    private readonly HybridDirectionalFlexNode<DragDropFixedNode> _itemGridNode;

    private const float FallbackItemSize = 46;
    private const float HeaderHeight = 16;
    private const float MinWidth = 40;

    private float? _fixedWidth;
    private int _hoverRefs;
    private bool _headerSuppressed;
    private bool _headerExpanded;
    private float _baseHeaderWidth = 96f;
    private string _fullHeaderText = string.Empty;

    public event Action<InventoryCategoryNode, bool>? HeaderHoverChanged;

    public InventoryCategoryNode()
    {
        _categoryNameTextNode = new TextNode
        {
            Size = new Vector2(96, 16),
            AlignmentType = AlignmentType.Left,
        };

        _categoryNameTextNode.AddEvent(AtkEventType.MouseOver, BeginHeaderHover);
        _categoryNameTextNode.AddEvent(AtkEventType.MouseOut, EndHeaderHover);

        _categoryNameTextNode.TextFlags |= TextFlags.OverflowHidden | TextFlags.Ellipsis;
        _categoryNameTextNode.TextFlags &= ~(TextFlags.WordWrap | TextFlags.MultiLine);

        _categoryNameTextNode.AddFlags(NodeFlags.EmitsEvents | NodeFlags.HasCollision);
        _categoryNameTextNode.AttachNode(this);

        _itemGridNode = new HybridDirectionalFlexNode<DragDropFixedNode>
        {
            Position = new Vector2(0, HeaderHeight),
            Size = new Vector2(240, 92),
            FillRowsFirst = true,
            ItemsPerLine = 10,
            HorizontalPadding = 5,
            VerticalPadding = 2,
        };

        _itemGridNode.NodeFlags |= NodeFlags.EmitsEvents;
        _itemGridNode.AttachNode(this);
    }

    public CategorizedInventory CategorizedInventory
    {
        get;
        set
        {
            field = value;

            _fullHeaderText = value.Category.Name;

            _categoryNameTextNode.String = _fullHeaderText;
            _categoryNameTextNode.TextColor = value.Category.Color;
            _categoryNameTextNode.TextTooltip = value.Category.Description;

            UpdateItemGrid();
            RecalculateSize();
        }
    }

    public int ItemsPerLine
    {
        get => _itemGridNode.ItemsPerLine;
        set
        {
            if (_itemGridNode.ItemsPerLine == value) return;
            _itemGridNode.ItemsPerLine = value;
            RecalculateSize();
        }
    }

    public float? FixedWidth
    {
        get => _fixedWidth;
        set
        {
            if (_fixedWidth.Equals(value)) return;
            _fixedWidth = value;
            RecalculateSize();
        }
    }

    public void BeginHeaderHover()
    {
        _hoverRefs++;
        if (_hoverRefs != 1) return;

        _headerExpanded = true;
        ApplyHeaderVisualStateAndSize();
        HeaderHoverChanged?.Invoke(this, true);
    }

    public void EndHeaderHover()
    {
        if (_hoverRefs <= 0) return;

        _hoverRefs--;
        if (_hoverRefs != 0) return;

        _headerExpanded = false;
        ApplyHeaderVisualStateAndSize();
        HeaderHoverChanged?.Invoke(this, false);
    }

    public void SetHeaderSuppressed(bool suppressed)
    {
        if (_headerSuppressed == suppressed) return;
        _headerSuppressed = suppressed;
        ApplyHeaderVisualStateAndSize();
    }

    private void ApplyHeaderVisualStateAndSize()
    {
        _categoryNameTextNode.IsVisible = ! _headerSuppressed;
        if (_headerSuppressed)
            return;

        var flags = _categoryNameTextNode.TextFlags;
        flags &= ~(TextFlags.WordWrap | TextFlags.MultiLine);

        if (_headerExpanded)
        {
            flags &= ~(TextFlags.OverflowHidden | TextFlags.Ellipsis);
            _categoryNameTextNode.TextFlags = flags;

            if (! string.IsNullOrEmpty(_fullHeaderText))
                _categoryNameTextNode.String = _fullHeaderText;

            Vector2 drawSize = _categoryNameTextNode.GetTextDrawSize();
            float expandedWidth = MathF.Max(_baseHeaderWidth, drawSize.X + 4f);
            _categoryNameTextNode.Size = _categoryNameTextNode.Size with { X = expandedWidth };
        }
        else
        {
            _categoryNameTextNode.Size = _categoryNameTextNode.Size with { X = _baseHeaderWidth };

            if (!string.IsNullOrEmpty(_fullHeaderText))
                _categoryNameTextNode.String = _fullHeaderText;

            flags |= TextFlags.OverflowHidden | TextFlags.Ellipsis;
            _categoryNameTextNode.TextFlags = flags;
        }
    }

    private void RecalculateSize()
    {
        int itemCount = CategorizedInventory.Items.Count;

        if (itemCount == 0)
        {
            float width = _fixedWidth ?? MinWidth;
            Size = new Vector2(width, HeaderHeight);
            _baseHeaderWidth = width;
            _itemGridNode.Position = new Vector2(0, HeaderHeight);
            _itemGridNode.Size = new Vector2(width, 0);
            ApplyHeaderVisualStateAndSize();
            return;
        }

        int itemsPerLine = Math.Max(1, _itemGridNode.ItemsPerLine);
        int rows = (itemCount + itemsPerLine - 1) / itemsPerLine;
        int actualColumns = Math.Min(itemCount, itemsPerLine);

        float cellW = _itemGridNode.Nodes.Count > 0 ? _itemGridNode.Nodes[0].Width : FallbackItemSize;
        float cellH = _itemGridNode.Nodes.Count > 0 ? _itemGridNode.Nodes[0].Height : FallbackItemSize;

        float hPad = _itemGridNode.HorizontalPadding;
        float vPad = _itemGridNode.VerticalPadding;

        float calculatedWidth = _fixedWidth ?? Math.Max(MinWidth, actualColumns * cellW + (actualColumns - 1) * hPad);
        float height = HeaderHeight + rows * cellH + (rows - 1) * vPad;

        Size = new Vector2(calculatedWidth, height);
        _itemGridNode.Position = new Vector2(0, HeaderHeight);
        _itemGridNode.Size = new Vector2(calculatedWidth, height - HeaderHeight);
        _baseHeaderWidth = calculatedWidth;

        ApplyHeaderVisualStateAndSize();
    }

    private void UpdateItemGrid()
    {
        _itemGridNode.SyncWithListData(
            CategorizedInventory.Items,
            node => node.ItemInfo,
            CreateInventoryDragDropNode);
    }

    private unsafe InventoryDragDropNode CreateInventoryDragDropNode(ItemInfo data)
    {
        InventoryItem item = data.Item;

        return new InventoryDragDropNode
        {
            Size = new Vector2(42, 46),
            IsVisible = true,
            IconId = item.IconId,
            AcceptedType = DragDropType.Item,
            IsDraggable = true,
            Payload = new DragDropPayload
            {
                Type = DragDropType.Inventory_Item,
                Int1 = (int)item.Container,
                Int2 = item.Slot,
            },
            IsClickable = true,
            OnEnd = _ => System.AddonInventoryWindow.ManualInventoryRefresh(),
            OnPayloadAccepted = (node, payload) => OnPayloadAccepted(node, payload, data),
            OnRollOver = node =>
            {
                BeginHeaderHover();
                node.ShowInventoryItemTooltip(item.Container, item.Slot);
            },
            OnRollOut = node =>
            {
                EndHeaderHover();

                ushort addonId = RaptureAtkUnitManager.Instance()->GetAddonByNode(node)->Id;
                AtkStage.Instance()->TooltipManager.HideTooltip(addonId);
            },
            ItemInfo = data
        };
    }

    private void OnPayloadAccepted(DragDropNode _, DragDropPayload payload, ItemInfo targetItemInfo)
    {
        Services.Logger.Debug($"[OnPayload] Received payload of type {payload.Type}, Int1={payload.Int1}, Int2={payload.Int2}, RefIndex={payload.ReferenceIndex}, Text={payload.Text}");
        if (!payload.IsValidInventoryPayload)
            return;

        InventoryLocation sourceLocation = payload.InventoryLocation;

        if (!sourceLocation.IsValid)
        {
            Services.Logger.Warning($"[OnPayload] Could not resolve source from payload");
            return;
        }

        InventoryLocation targetLocation = new InventoryLocation(targetItemInfo.Item.Container, (ushort)targetItemInfo.Item.Slot);

        Services.Logger.Debug($"[OnPayload] Moving {sourceLocation.ToString()} -> {targetLocation.ToString()}");

        InventoryMoveHelper.MoveItem(sourceLocation.Container, sourceLocation.Slot, targetLocation.Container, targetLocation.Slot);
    }
}