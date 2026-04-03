using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Items;
using AetherBags.IPC.ExternalCategorySystem;
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
    private const float ExpectedItemWidth = 42;
    private const float ExpectedItemHeight = 46;
    private const float HeaderHeight = 16;
    private const float MinWidth = 40;

    public override uint Key => CategoryNodeKeyBase | CategorizedInventory.Key;

    private readonly CollisionNode _hoverCollisionNode;
    private readonly TextNode _categoryNameTextNode;
    private readonly HybridDirectionalFlexNode<DragDropNode> _itemGridNode;

    private float? _fixedWidth;
    private float? _maxWidth;
    private int _hoverRefs;
    private bool _headerSuppressed;
    private bool _headerExpanded;
    private bool _collapsePending;
    private float _baseHeaderWidth = 96f;
    private string _fullHeaderText = string.Empty;

    private uint _lastCategoryKey;
    private int _lastItemCount;
    private ulong _lastItemsHash;
    private int _lastItemsPerLine;
    private bool _itemsNeedPopulation;
    private CategorizedInventory _categorizedInventory;

    public event Action<InventoryCategoryNode, bool>? HeaderHoverChanged;
    public bool NeedsItemPopulation => _itemsNeedPopulation;
    public Action? OnRefreshRequested { get; set; }
    public Action? OnDragEnd { get; set; }
    public SharedNodePool<InventoryDragDropNode>? SharedItemPool { get; set; }
    public override bool IsPinnedInConfig => CategorizedInventory.Category?.IsPinned ?? false;

    public CategorizedInventory CategorizedInventory
    {
        get => _categorizedInventory;
        set => SetCategoryData(value, _itemGridNode.ItemsPerLine);
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

    public override float? MaxWidth
    {
        get => _maxWidth;
        set => _maxWidth = value;
    }

    public InventoryCategoryNode()
    {
        _hoverCollisionNode = new CollisionNode
        {
            Size = new Vector2(240, 108),
            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.HasCollision | NodeFlags.RespondToMouse | NodeFlags.EmitsEvents,
        };
        _hoverCollisionNode.AddEvent(AtkEventType.MouseOver, BeginHeaderHover);
        _hoverCollisionNode.AddEvent(AtkEventType.MouseOut, EndHeaderHover);
        _hoverCollisionNode.AttachNode(this);

        _categoryNameTextNode = new TextNode
        {
            Size = new Vector2(96, 16),
            AlignmentType = AlignmentType.Left,
        };
        _categoryNameTextNode.TextFlags |= TextFlags.OverflowHidden | TextFlags.Ellipsis;
        _categoryNameTextNode.TextFlags &= ~(TextFlags.WordWrap | TextFlags.MultiLine);
        _categoryNameTextNode.AddNodeFlags(NodeFlags.EmitsEvents | NodeFlags.HasCollision);
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

    #region Data

    public void SetCategoryData(CategorizedInventory data, int itemsPerLine, bool deferItemCreation = false)
    {
        bool categoryChanged = data.Key != _lastCategoryKey;
        bool itemsPerLineChanged = itemsPerLine != _lastItemsPerLine;
        ulong itemsHash = ComputeItemsHash(CollectionsMarshal.AsSpan(data.Items));
        bool itemsChanged = data.Items.Count != _lastItemCount || itemsHash != _lastItemsHash;

        _lastCategoryKey = data.Key;
        _lastItemCount = data.Items.Count;
        _lastItemsHash = itemsHash;
        _lastItemsPerLine = itemsPerLine;
        _categorizedInventory = data;

        _fullHeaderText = System.Config.General.ShowCategoryItemCount
            ? $"{data.Category.Name} ({data.Items.Count})"
            : data.Category.Name;

        _categoryNameTextNode.String = _fullHeaderText;
        _categoryNameTextNode.TextColor = data.Category.Color;
        _categoryNameTextNode.TextTooltip = data.Category.Description;

        if (itemsChanged || categoryChanged)
        {
            _itemGridNode.ItemsPerLine = itemsPerLine;
            if (deferItemCreation)
                _itemsNeedPopulation = true;
            else
            {
                using (_itemGridNode.DeferRecalculateLayout()) UpdateItemGrid();
                _itemsNeedPopulation = false;
            }
        }
        else if (itemsPerLineChanged)
            _itemGridNode.ItemsPerLine = itemsPerLine;

        if (categoryChanged || itemsChanged || itemsPerLineChanged)
            RecalculateSize();
    }

    public void PopulateItems()
    {
        if (!_itemsNeedPopulation) return;
        using (_itemGridNode.DeferRecalculateLayout()) UpdateItemGrid();
        _itemsNeedPopulation = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeItemsHash(ReadOnlySpan<ItemInfo> items)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var item in items) { hash ^= item.Key; hash *= 1099511628211UL; }
        return hash;
    }

    #endregion

    #region Header Hover

    public void BeginHeaderHover()
    {
        _hoverRefs++;
        _collapsePending = false;
        if (_hoverRefs != 1) return;

        _headerExpanded = true;
        ApplyHeaderVisualState();
        HeaderHoverChanged?.Invoke(this, true);
    }

    public void EndHeaderHover()
    {
        if (_hoverRefs <= 0) return;
        _hoverRefs--;
        if (_hoverRefs != 0) return;

        _collapsePending = true;
        Services.Framework.RunOnTick(() =>
        {
            if (!_collapsePending) return;
            _collapsePending = false;
            _headerExpanded = false;
            ApplyHeaderVisualState();
            HeaderHoverChanged?.Invoke(this, false);
        });
    }

    public void SetHeaderSuppressed(bool suppressed)
    {
        if (_headerSuppressed == suppressed) return;
        _headerSuppressed = suppressed;
        ApplyHeaderVisualState();
    }

    public float GetExpandedHeaderWidth()
    {
        if (string.IsNullOrEmpty(_fullHeaderText)) return _baseHeaderWidth;
        Vector2 drawSize = _categoryNameTextNode.GetTextDrawSize(_fullHeaderText);
        return MathF.Max(_baseHeaderWidth, drawSize.X + 4f);
    }

    private void ApplyHeaderVisualState()
    {
        _categoryNameTextNode.IsVisible = !_headerSuppressed;
        if (_headerSuppressed) { _categoryNameTextNode.Position = Vector2.Zero; return; }

        var flags = _categoryNameTextNode.TextFlags & ~(TextFlags.WordWrap | TextFlags.MultiLine);

        if (_headerExpanded)
        {
            flags &= ~(TextFlags.OverflowHidden | TextFlags.Ellipsis);
            _categoryNameTextNode.TextFlags = flags;
            if (!string.IsNullOrEmpty(_fullHeaderText)) _categoryNameTextNode.String = _fullHeaderText;

            Vector2 drawSize = _categoryNameTextNode.GetTextDrawSize();
            _categoryNameTextNode.Size = _categoryNameTextNode.Size with { X = MathF.Max(_baseHeaderWidth, drawSize.X + 4f) };
            _categoryNameTextNode.Position = new Vector2(0, 1);
        }
        else
        {
            _categoryNameTextNode.Size = _categoryNameTextNode.Size with { X = _baseHeaderWidth };
            _categoryNameTextNode.Position = Vector2.Zero;
            if (!string.IsNullOrEmpty(_fullHeaderText)) _categoryNameTextNode.String = _fullHeaderText;
            _categoryNameTextNode.TextFlags = flags | TextFlags.OverflowHidden | TextFlags.Ellipsis;
        }
    }

    #endregion

    #region Layout

    public override void RecalculateSize()
    {
        int itemCount = CategorizedInventory.Items.Count;
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

    #endregion

    #region Items

    private void UpdateItemGrid()
    {
        _itemGridNode.SyncWithListDataByKey<ItemInfo, InventoryDragDropNode, ulong>(
            dataList: CategorizedInventory.Items,
            getKeyFromData: item => item.Key,
            getKeyFromNode: node => node.ItemInfo?.Key ?? 0,
            updateNode: (node, data) => { node.ItemInfo = data; ApplyItemDataToNode(node, data); },
            createNodeMethod: CreateInventoryDragDropNode,
            resetNodeForReuse: static node => node.ResetForReuse(),
            externalPool: SharedItemPool);
    }

    private unsafe InventoryDragDropNode CreateInventoryDragDropNode(ItemInfo data)
    {
        var node = new InventoryDragDropNode
        {
            Size = new Vector2(42, 46),
            IsVisible = true,
            AcceptedType = DragDropType.Item,
            IsClickable = true,
            OnDiscard = n => { if (n is InventoryDragDropNode dn) OnDiscard(n, dn.ItemInfo); },
            OnEnd = _ => OnDragEnd?.Invoke(),
            OnPayloadAccepted = (n, p) => { if (n is InventoryDragDropNode dn) OnPayloadAccepted(n, p, dn.ItemInfo); },
            OnRollOver = OnNodeRollOver,
            OnRollOut = OnNodeRollOut,
            ItemInfo = data
        };
        ApplyItemDataToNode(node, data);
        return node;
    }

    private void ApplyItemDataToNode(InventoryDragDropNode node, ItemInfo data)
    {
        var config = System.Config.General;
        InventoryItem item = data.Item;
        InventoryMappedLocation vis = data.VisualLocation;
        var visInvType = InventoryType.GetInventoryTypeFromContainerId(vis.Container);

        node.IconId = item.IconId;
        node.Alpha = data.VisualAlpha;
        node.IsDraggable = !data.IsSlotBlocked;
        node.IconNode.IconExtras.AntsNode.IsVisible = data.IsRelationshipHighlighted;

        if (data.IsRelationshipHighlighted && config.AnimationEnabled)
            node.IconNode.IconExtras.AntsNode.Timeline?.PlayAnimation(26);
        else
            node.IconNode.IconExtras.AntsNode.Timeline?.StopAnimation();

        Vector3? deco = config.UseUnifiedExternalCategories ? ExternalCategoryManager.GetItemOverlayColor(item.ItemId) : null;
        node.IconNode.AddColor = deco ?? data.HighlightOverlayColor;

        node.Payload = new DragDropPayload
        {
            Type = DragDropType.Item,
            Int1 = vis.Container,
            Int2 = vis.Slot,
            ReferenceIndex = (short)(visInvType.GetInventoryStartIndex + vis.Slot)
        };
    }

    private void OnNodeRollOver(DragDropNode n)
    {
        BeginHeaderHover();
        if (n is not InventoryDragDropNode node) return;
        n.ShowInventoryItemTooltip(node.ItemInfo.Item.Container, node.ItemInfo.Item.Slot);
    }

    private unsafe void OnNodeRollOut(DragDropNode n)
    {
        EndHeaderHover();
        ushort addonId = RaptureAtkUnitManager.Instance()->GetAddonByNode(n)->Id;
        AtkStage.Instance()->TooltipManager.HideTooltip(addonId);
    }

    public void RefreshNodeVisuals()
    {
        var config = System.Config.General;
        var nodes = _itemGridNode.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryDragDropNode itemNode || itemNode.ItemInfo == null) continue;
            var info = itemNode.ItemInfo;

            if (MathF.Abs(itemNode.Alpha - info.VisualAlpha) > 0.001f) itemNode.Alpha = info.VisualAlpha;

            Vector3? deco = config.UseUnifiedExternalCategories ? ExternalCategoryManager.GetItemOverlayColor(info.Item.ItemId) : null;
            Vector3 color = deco ?? info.HighlightOverlayColor;
            if (itemNode.IconNode.AddColor != color) itemNode.IconNode.AddColor = color;

            if (itemNode.IsDraggable != !info.IsSlotBlocked) itemNode.IsDraggable = !info.IsSlotBlocked;

            bool ants = info.IsRelationshipHighlighted;
            if (itemNode.IconNode.IconExtras.AntsNode.IsVisible != ants) itemNode.IconNode.IconExtras.AntsNode.IsVisible = ants;
            if (ants && config.AnimationEnabled) itemNode.IconNode.IconExtras.AntsNode.Timeline?.PlayAnimation(26);
            else itemNode.IconNode.IconExtras.AntsNode.Timeline?.StopAnimation();
        }
    }

    private unsafe void OnDiscard(DragDropNode node, ItemInfo item)
    {
        uint addonId = RaptureAtkUnitManager.Instance()->GetAddonByNode(node)->Id;
        AgentInventoryContext.Instance()->DiscardItem(item.Item.GetLinkedItem(), item.Item.Container, item.Item.Slot, addonId);
    }

    private void OnPayloadAccepted(DragDropNode node, DragDropPayload acceptedPayload, ItemInfo targetItemInfo)
    {
        try
        {
            var nodePayload = new DragDropPayload
            {
                Type = DragDropType.Item,
                Int1 = targetItemInfo.VisualLocation.Container,
                Int2 = targetItemInfo.VisualLocation.Slot,
                ReferenceIndex = (short)(targetItemInfo.Item.Container.GetInventoryStartIndex + targetItemInfo.VisualLocation.Slot)
            };

            if (!acceptedPayload.IsValidInventoryPayload || !nodePayload.IsValidInventoryPayload) return;

            if (acceptedPayload.IsSameBaseContainer(nodePayload))
            {
                node.IconId = targetItemInfo.IconId;
                node.Payload = nodePayload;
                return;
            }

            InventoryMoveHelper.HandleItemMovePayload(acceptedPayload, nodePayload);
            OnRefreshRequested?.Invoke();
        }
        catch (Exception ex) { Services.Logger.Error(ex, "[OnPayload] Error handling payload acceptance"); }
    }

    #endregion

    #region Reset / Pool

    public void ResetForReuse()
    {
        _lastCategoryKey = 0;
        _lastItemCount = 0;
        _lastItemsHash = 0;
        _lastItemsPerLine = 0;
        _itemsNeedPopulation = false;
        _hoverRefs = 0;
        _headerSuppressed = false;
        _headerExpanded = false;
        _collapsePending = false;
        _fullHeaderText = string.Empty;
        _fixedWidth = null;
        _maxWidth = null;

        _categoryNameTextNode.String = string.Empty;
        _categoryNameTextNode.TextTooltip = string.Empty;
        _categoryNameTextNode.IsVisible = true;
        _categoryNameTextNode.Position = Vector2.Zero;

        using (_itemGridNode.DeferRecalculateLayout())
        {
            ReturnItemsToPool();
            _itemGridNode.ClearListOnly();
        }
    }

    private void ReturnItemsToPool()
    {
        var nodes = _itemGridNode.Nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryDragDropNode itemNode) continue;
            if (SharedItemPool is not null && SharedItemPool.TryReturn(itemNode)) continue;
            try { itemNode.Dispose(); }
            catch (Exception ex) { Services.Logger.Error(ex, "[InventoryCategoryNode] Error disposing item node"); }
        }
    }

    #endregion
}
