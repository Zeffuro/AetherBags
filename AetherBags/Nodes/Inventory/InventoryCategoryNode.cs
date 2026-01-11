using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Items;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

public class InventoryCategoryNode : InventoryCategoryNodeBase
{
    private const uint CategoryNodeKeyBase = 0x10000000;

    public override uint Key => CategoryNodeKeyBase | CategorizedInventory.Key;
    private readonly TextNode _categoryNameTextNode;
    private readonly HybridDirectionalFlexNode<DragDropNode> _itemGridNode;

    private const float FallbackItemSize = 46;
    private const float HeaderHeight = 16;
    private const float MinWidth = 40;

    private float? _fixedWidth;
    private float? _maxWidth;
    private int _hoverRefs;
    private bool _headerSuppressed;
    private bool _headerExpanded;
    private float _baseHeaderWidth = 96f;
    private string _fullHeaderText = string.Empty;

    private uint _lastCategoryKey;
    private int _lastItemCount;
    private ulong _lastItemsHash;
    private int _lastItemsPerLine;
    private float? _lastMaxWidth;

    public event Action<InventoryCategoryNode, bool>? HeaderHoverChanged;
    public Action? OnRefreshRequested { get; set; }
    public Action? OnDragEnd { get; set; }

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

        _itemGridNode = new HybridDirectionalFlexNode<DragDropNode>
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

    private CategorizedInventory _categorizedInventory;

    public CategorizedInventory CategorizedInventory
    {
        get => _categorizedInventory;
        set => SetCategoryData(value, _itemGridNode.ItemsPerLine);
    }

    public void SetCategoryData(CategorizedInventory data, int itemsPerLine)
    {
        bool categoryChanged = data.Key != _lastCategoryKey;
        bool itemsPerLineChanged = itemsPerLine != _lastItemsPerLine;
        bool maxWidthChanged = _maxWidth != _lastMaxWidth;

        ulong itemsHash = ComputeItemsHash(CollectionsMarshal.AsSpan(data.Items));
        bool itemsChanged = data.Items.Count != _lastItemCount || itemsHash != _lastItemsHash;

        _lastCategoryKey = data.Key;
        _lastItemCount = data.Items.Count;
        _lastItemsHash = itemsHash;
        _lastItemsPerLine = itemsPerLine;
        _lastMaxWidth = _maxWidth;

        _categorizedInventory = data;

        _fullHeaderText = System.Config.General.ShowCategoryItemCount
            ? $"{data.Category.Name} ({data.Items.Count})"
            : data.Category.Name;

        _categoryNameTextNode.String = _fullHeaderText;
        _categoryNameTextNode.TextColor = data.Category.Color;
        _categoryNameTextNode.TextTooltip = data.Category.Description;

        if (itemsChanged || categoryChanged)
        {
            using (_itemGridNode.DeferRecalculateLayout())
            {
                _itemGridNode.ItemsPerLine = itemsPerLine;
                UpdateItemGrid();
            }
        }
        else if (itemsPerLineChanged)
        {
            _itemGridNode.ItemsPerLine = itemsPerLine;
        }

        if (categoryChanged || itemsChanged || itemsPerLineChanged || maxWidthChanged)
        {
            RecalculateSize();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeItemsHash(ReadOnlySpan<ItemInfo> items)
    {
        ulong hash = 14695981039346656037UL;  // FNV-1a offset basis
        foreach (var item in items)
        {
            hash ^= item.Key;
            hash *= 1099511628211UL; // FNV-1a prime
        }
        return hash;
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

    public float? MaxWidth
    {
        get => _maxWidth;
        set => _maxWidth = value;
    }

    public override bool IsPinnedInConfig => CategorizedInventory.Category?.IsPinned ?? false;

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

    public void RecalculateSize()
    {
        int itemCount = CategorizedInventory.Items.Count;

        float cellW = _itemGridNode.Nodes.Count > 0 ? _itemGridNode.Nodes[0].Width : FallbackItemSize;
        float cellH = _itemGridNode.Nodes.Count > 0 ? _itemGridNode.Nodes[0].Height : FallbackItemSize;
        float hPad = _itemGridNode.HorizontalPadding;
        float vPad = _itemGridNode.VerticalPadding;

        if (itemCount == 0)
        {
            float width = _fixedWidth ?? MinWidth;
            if (_maxWidth.HasValue) width = Math.Min(width, _maxWidth.Value);
            Size = new Vector2(width, HeaderHeight);
            _baseHeaderWidth = width;
            _itemGridNode.Position = new Vector2(0, HeaderHeight);
            _itemGridNode.Size = new Vector2(width, 0);
            ApplyHeaderVisualStateAndSize();
            return;
        }

        int itemsPerLine = Math.Max(1, _itemGridNode.ItemsPerLine);

        float minUsableWidth = cellW;
        if (_maxWidth.HasValue && _fixedWidth is null && _maxWidth.Value >= minUsableWidth)
        {
            int maxColumns = (int)MathF.Floor((_maxWidth.Value + hPad) / (cellW + hPad));
            maxColumns = Math.Max(1, maxColumns);

            float widthNeeded = maxColumns * cellW + (maxColumns - 1) * hPad;
            if (widthNeeded > _maxWidth.Value && maxColumns > 1)
                maxColumns--;

            itemsPerLine = Math.Min(itemsPerLine, maxColumns);
        }

        int rows = (itemCount + itemsPerLine - 1) / itemsPerLine;
        int actualColumns = Math.Min(itemCount, itemsPerLine);

        float calculatedWidth = _fixedWidth ?? Math.Max(MinWidth, actualColumns * cellW + (actualColumns - 1) * hPad);

        if (_maxWidth.HasValue && _fixedWidth is null && _maxWidth.Value >= minUsableWidth)
            calculatedWidth = Math.Min(calculatedWidth, _maxWidth.Value);

        float height = HeaderHeight + rows * cellH + (rows - 1) * vPad;

        Size = new Vector2(calculatedWidth, height);
        _itemGridNode.Position = new Vector2(0, HeaderHeight);
        _itemGridNode.Size = new Vector2(calculatedWidth, height - HeaderHeight);

        if (_itemGridNode.ItemsPerLine != itemsPerLine)
            _itemGridNode.ItemsPerLine = itemsPerLine;
        _baseHeaderWidth = calculatedWidth;

        ApplyHeaderVisualStateAndSize();
    }

    private void UpdateItemGrid()
    {
        _itemGridNode.SyncWithListDataByKey<ItemInfo, InventoryDragDropNode, ulong>(
            dataList: CategorizedInventory.Items,
            getKeyFromData: item => item.Key,
            getKeyFromNode: node => node.ItemInfo?.Key ?? 0,
            updateNode: UpdateInventoryDragDropNode,
            createNodeMethod: CreateInventoryDragDropNode);
    }

    private void UpdateInventoryDragDropNode(InventoryDragDropNode node, ItemInfo data)
    {
        if (node.ItemInfo?.Key == data.Key)
        {
            node.ItemInfo = data;
            node.Alpha = data.VisualAlpha;
            node.AddColor = data.HighlightOverlayColor;
            node.IsDraggable = !data.IsSlotBlocked;
            return;
        }

        InventoryItem item = data.Item;
        InventoryMappedLocation visualLocation = data.VisualLocation;

        var visualInvType = InventoryType.GetInventoryTypeFromContainerId(visualLocation.Container);
        int absoluteIndex = visualInvType.GetInventoryStartIndex + visualLocation.Slot;

        node.ItemInfo = data;
        node.IconId = item.IconId;
        node.Alpha = data.VisualAlpha;
        node.AddColor = data.HighlightOverlayColor;
        node.IsDraggable = !data.IsSlotBlocked;
        node.Payload = new DragDropPayload
        {
            Type = DragDropType.Item,
            Int1 = visualLocation.Container,
            Int2 = visualLocation.Slot,
            ReferenceIndex = (short)absoluteIndex
        };
    }

    private unsafe InventoryDragDropNode CreateInventoryDragDropNode(ItemInfo data)
    {
        InventoryItem item = data.Item;
        InventoryMappedLocation visualLocation = data.VisualLocation;

        var visualInvType = InventoryType.GetInventoryTypeFromContainerId(visualLocation.Container);
        int absoluteIndex = visualInvType.GetInventoryStartIndex + visualLocation.Slot;

        DragDropPayload nodePayload = new DragDropPayload
        {
            // Int1 is always the container ID, for Item DragDrop Int2 is only used as a fallback
            // ReferenceIndex is the absolute index that's actually used
            Type = DragDropType.Item,
            Int1 = visualLocation.Container,
            Int2 = visualLocation.Slot,
            ReferenceIndex = (short)absoluteIndex
        };

        return new InventoryDragDropNode
        {
            Size = new Vector2(42, 46),
            Alpha = data.VisualAlpha,
            AddColor = data.HighlightOverlayColor,
            IsDraggable = !data.IsSlotBlocked,
            IsVisible = true,
            IconId = item.IconId,
            AcceptedType = DragDropType.Item,
            Payload = nodePayload,
            IsClickable = true,
            OnDiscard = node => OnDiscard(node, data),
            OnEnd = _ => OnDragEnd?.Invoke(),
            OnPayloadAccepted = (node, acceptedPayload) => OnPayloadAccepted(node, acceptedPayload, data),
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

    public void RefreshNodeVisuals()
    {
        var nodes = _itemGridNode.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryDragDropNode itemNode || itemNode.ItemInfo == null)
                continue;

            var info = itemNode.ItemInfo;
            float newAlpha = info.VisualAlpha;
            Vector3 newColor = info.HighlightOverlayColor;
            bool newDraggable = !info.IsSlotBlocked;

            if (!NearlyEqual(itemNode.Alpha, newAlpha))
                itemNode.Alpha = newAlpha;

            if (itemNode.AddColor != newColor)
                itemNode.AddColor = newColor;

            if (itemNode.IsDraggable != newDraggable)
                itemNode.IsDraggable = newDraggable;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NearlyEqual(float a, float b) => MathF.Abs(a - b) < 0.001f;

    private unsafe void OnDiscard(DragDropNode node, ItemInfo item)
    {
        uint addonId = RaptureAtkUnitManager.Instance()->GetAddonByNode(node)->Id;
        AgentInventoryContext.Instance()->DiscardItem(item.Item.GetLinkedItem(), item.Item.Container, item.Item.Slot, addonId);
    }

    private void OnPayloadAccepted(DragDropNode node, DragDropPayload acceptedPayload, ItemInfo targetItemInfo)
    {
        try
        {
            // KTK clears node.Payload before invoking this, so setting it manually again
            var nodePayload = new DragDropPayload
            {
                Type = DragDropType.Item,
                Int1 = targetItemInfo.VisualLocation.Container,
                Int2 = targetItemInfo.VisualLocation.Slot,
                ReferenceIndex = (short)(targetItemInfo.Item.Container.GetInventoryStartIndex + targetItemInfo.VisualLocation.Slot)
            };

            Services.Logger.DebugOnly($"[OnPayload] ACCEPTED payload: Type={acceptedPayload.Type} Int1={acceptedPayload.Int1} Int2={acceptedPayload.Int2} Ref={acceptedPayload.ReferenceIndex}");
            Services.Logger.DebugOnly($"[OnPayload] NODE payload: Type={nodePayload.Type} Int1={nodePayload.Int1} Int2={nodePayload.Int2} Ref={nodePayload.ReferenceIndex}");

            if (!acceptedPayload.IsValidInventoryPayload || !nodePayload.IsValidInventoryPayload)
            {
                Services.Logger.Warning($"[OnPayload] Invalid payload type: Accepted={acceptedPayload.Type} Node={nodePayload.Type}");
                return;
            }

            if (acceptedPayload.IsSameBaseContainer(nodePayload))
            {
                Services.Logger.DebugOnly("[OnPayload] Source and target are in the same base container, skipping move.");
                node.IconId = targetItemInfo.IconId;
                node.Payload = nodePayload;
                return;
            }

            var sourceCopy = acceptedPayload;
            var targetCopy = nodePayload;

            InventoryMoveHelper.HandleItemMovePayload(sourceCopy, targetCopy);
            OnRefreshRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Services.Logger.Error(ex, "[OnPayload] Error handling payload acceptance");
        }
    }
}
