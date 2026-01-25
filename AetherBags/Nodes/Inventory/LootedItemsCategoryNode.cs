using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using AetherBags.Inventory.Items;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

/// <summary>
/// A special category node for displaying recently looted items.
/// Items are not draggable but can be dismissed individually or cleared entirely.
/// </summary>
public class LootedItemsCategoryNode : InventoryCategoryNodeBase
{
    private const uint LootedCategoryKey = 0x20000001;

    public override uint Key => LootedCategoryKey;
    private readonly TextNode _headerTextNode;
    private readonly CircleButtonNode _clearButton;
    private readonly HybridDirectionalFlexNode<LootedItemDisplayNode> _itemGridNode;

    private const float HeaderHeight = 20;
    private const float ClearButtonSize = 20;
    private const float MinWidth = 100;

    private IReadOnlyList<LootedItemInfo> _lootedItems = Array.Empty<LootedItemInfo>();

    private int _lastItemCount;
    private long _lastItemsHash;

    private int _hoverRefs;
    private bool _headerExpanded;
    private float _baseHeaderWidth = 96f;
    private string _fullHeaderText = "Recently Looted";

    public event Action<LootedItemsCategoryNode, bool>? HeaderHoverChanged;
    public Action<int>? OnDismissItem { get; set; }
    public Action? OnClearAll { get; set; }

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

    public bool HasItems => _lootedItems.Count > 0;

    public LootedItemsCategoryNode()
    {
        _headerTextNode = new TextNode
        {
            Position = Vector2.Zero,
            Size = new Vector2(96, HeaderHeight),
            AlignmentType = AlignmentType.Left,
            String = "Recently Looted",
            TextFlags = TextFlags.OverflowHidden | TextFlags.Ellipsis,
            TextColor = ColorHelper.GetColor(26), // Gold-ish color
        };

        _headerTextNode.AddEvent(AtkEventType.MouseOver, BeginHeaderHover);
        _headerTextNode.AddEvent(AtkEventType.MouseOut, EndHeaderHover);

        _headerTextNode.TextFlags |= TextFlags.OverflowHidden | TextFlags.Ellipsis;
        _headerTextNode.TextFlags &= ~(TextFlags.WordWrap | TextFlags.MultiLine);

        _headerTextNode.AddNodeFlags(NodeFlags.EmitsEvents | NodeFlags.HasCollision);
        _headerTextNode.AttachNode(this);

        _clearButton = new CircleButtonNode
        {
            Size = new Vector2(ClearButtonSize),
            Icon = ButtonIcon.CrossSmall,
            OnClick = () => OnClearAll?.Invoke(),
        };
        _clearButton.AttachNode(this);

        _itemGridNode = new HybridDirectionalFlexNode<LootedItemDisplayNode>
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

        RecalculateSize();
    }

    public void UpdateLootedItems(IReadOnlyList<LootedItemInfo> lootedItems)
    {
        long newHash = ComputeItemsHash(lootedItems);
        bool itemsChanged = lootedItems.Count != _lastItemCount || newHash != _lastItemsHash;

        _lastItemCount = lootedItems.Count;
        _lastItemsHash = newHash;
        _lootedItems = lootedItems;

        UpdateHeaderText();

        if (itemsChanged)
        {
            using (_itemGridNode.DeferRecalculateLayout())
            {
                SyncItemGrid();
            }
            RecalculateSize();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ComputeItemsHash(IReadOnlyList<LootedItemInfo> items)
    {
        unchecked
        {
            long hash = unchecked((long)14695981039346656037UL);
            for (int i = 0; i < items.Count; i++)
            {
                hash ^= items[i].Index;
                hash *= 1099511628211L;
                hash ^= items[i].Item.ItemId;
                hash *= 1099511628211L;
            }
            return hash;
        }
    }

    private void UpdateHeaderText()
    {
        _fullHeaderText = _lootedItems.Count > 0
            ? $"Recently Looted ({_lootedItems.Count})"
            : "Recently Looted";

        _headerTextNode.String = _fullHeaderText;
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

    private void ApplyHeaderVisualStateAndSize()
    {
        var flags = _headerTextNode.TextFlags;
        flags &= ~(TextFlags.WordWrap | TextFlags.MultiLine);

        if (_headerExpanded)
        {
            flags &= ~(TextFlags.OverflowHidden | TextFlags.Ellipsis);
            _headerTextNode.TextFlags = flags;

            if (!string.IsNullOrEmpty(_fullHeaderText))
                _headerTextNode.String = _fullHeaderText;

            Vector2 drawSize = _headerTextNode.GetTextDrawSize();
            float expandedWidth = MathF.Max(_baseHeaderWidth, drawSize.X + 4f);
            _headerTextNode.Size = _headerTextNode.Size with { X = expandedWidth };
        }
        else
        {
            _headerTextNode.Size = _headerTextNode.Size with { X = _baseHeaderWidth };

            if (!string.IsNullOrEmpty(_fullHeaderText))
                _headerTextNode.String = _fullHeaderText;

            flags |= TextFlags.OverflowHidden | TextFlags.Ellipsis;
            _headerTextNode.TextFlags = flags;
        }
    }

    private void SyncItemGrid()
    {
        _itemGridNode.SyncWithListDataByKey(
            dataList: _lootedItems,
            getKeyFromData: item => item.Index,
            getKeyFromNode: node => node.LootedItem?.Index ?? -1,
            updateNode: UpdateLootedItemNode,
            createNodeMethod: CreateLootedItemNode);
    }

    private static void UpdateLootedItemNode(LootedItemDisplayNode node, LootedItemInfo data)
    {
        node.LootedItem = data;
    }

    private LootedItemDisplayNode CreateLootedItemNode(LootedItemInfo lootedItem)
    {
        return new LootedItemDisplayNode
        {
            OnDismiss = OnItemDismissed,
            LootedItem = lootedItem,
        };
    }

    private void OnItemDismissed(LootedItemDisplayNode node)
    {
        if(node.LootedItem is null) return;
        int index = node.LootedItem.Index;
        OnDismissItem?.Invoke(index);
    }

    private void RecalculateSize()
    {
        int itemCount = _lootedItems.Count;

        if (itemCount == 0)
        {
            float width = MinWidth;
            Size = new Vector2(width, HeaderHeight);
            _baseHeaderWidth = width - ClearButtonSize - 4;
            _headerTextNode.Size = new Vector2(_baseHeaderWidth, HeaderHeight);
            _clearButton.Position = new Vector2(width - ClearButtonSize, (HeaderHeight - ClearButtonSize) / 2);
            _clearButton.IsVisible = false;
            _itemGridNode.Position = new Vector2(0, HeaderHeight);
            _itemGridNode.Size = new Vector2(width, 0);
            ApplyHeaderVisualStateAndSize();
            return;
        }

        int itemsPerLine = Math.Max(1, _itemGridNode.ItemsPerLine);
        int rows = (itemCount + itemsPerLine - 1) / itemsPerLine;
        int actualColumns = Math.Min(itemCount, itemsPerLine);

        const float cellW = 42f;
        const float cellH = 46f;

        float hPad = _itemGridNode.HorizontalPadding;
        float vPad = _itemGridNode.VerticalPadding;

        float calculatedWidth = Math.Max(MinWidth, actualColumns * cellW + (actualColumns - 1) * hPad);
        float gridHeight = rows * cellH + (rows - 1) * vPad;
        float totalHeight = HeaderHeight + gridHeight;

        Size = new Vector2(calculatedWidth, totalHeight);
        _baseHeaderWidth = calculatedWidth - ClearButtonSize - 4;
        _headerTextNode.Size = new Vector2(_baseHeaderWidth, HeaderHeight);
        _clearButton.Position = new Vector2(calculatedWidth - ClearButtonSize, (HeaderHeight - ClearButtonSize) / 2);
        _clearButton.IsVisible = true;
        _itemGridNode.Position = new Vector2(0, HeaderHeight);
        _itemGridNode.Size = new Vector2(calculatedWidth, gridHeight);
        ApplyHeaderVisualStateAndSize();
    }
}
