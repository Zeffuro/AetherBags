using System;
using System.Numerics;
using AetherBags.Inventory.Items;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

/// <summary>
/// A display-only item node for looted items. Not draggable, but shows tooltip and can be dismissed.
/// </summary>
public unsafe class LootedItemDisplayNode : SimpleComponentNode
{
    private readonly IconNode _iconNode;
    private readonly TextNode _quantityTextNode;
    private readonly ResNode _collisionNode;

    public Action<LootedItemDisplayNode>? OnDismiss { get; set; }

    public LootedItemDisplayNode()
    {
        Size = new Vector2(42, 46);

        _iconNode = new IconNode
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(42, 46),
            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
        };
        _iconNode.AttachNode(this);

        _quantityTextNode = new TextNode
        {
            Size = new Vector2(40.0f, 12.0f),
            Position = new Vector2(4.0f, 34.0f),
            NodeFlags = NodeFlags.Enabled | NodeFlags.EmitsEvents,
            Color = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(51),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Right,
        };
        _quantityTextNode.AttachNode(this);

        _collisionNode = new ResNode
        {
            Size = new Vector2(42, 46),
            NodeFlags = NodeFlags.Enabled | NodeFlags.EmitsEvents | NodeFlags.HasCollision,
        };
        _collisionNode.AddEvent(AtkEventType.MouseOver, OnMouseOver);
        _collisionNode.AddEvent(AtkEventType.MouseOut, OnMouseOut);
        _collisionNode.AddEvent(AtkEventType.MouseClick, OnMouseClick);
        _collisionNode.AttachNode(this);
    }

    public LootedItemInfo LootedItem { get; private set; } = null!;

    public void SetLootedItem(LootedItemInfo lootedItem)
    {
        LootedItem = lootedItem;
        var item = lootedItem.Item;
        _iconNode.IconId = item.IconId;
        _quantityTextNode.String = lootedItem.Quantity > 1 ? lootedItem.Quantity.ToString() : string.Empty;
    }

    private void OnMouseOver(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        var item = LootedItem.Item;
        _collisionNode.ShowInventoryItemTooltip(item.Container, item.Slot);
    }

    private void OnMouseOut(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        ushort addonId = RaptureAtkUnitManager.Instance()->GetAddonByNode(_collisionNode)->Id;
        AtkStage.Instance()->TooltipManager.HideTooltip(addonId);
    }

    private void OnMouseClick(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (!atkEventData->IsLeftClick) return;
        OnDismiss?.Invoke(this);
    }
}
