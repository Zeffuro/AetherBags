using System;
using AetherBags.Inventory.Items;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

/// <summary>
/// A display-only item node for looted items. Not draggable, but shows tooltip and can be dismissed.
/// </summary>
public sealed unsafe class LootedItemDisplayNode : SimpleComponentNode
{
    private readonly IconNode _iconNode;
    private readonly TextNode _quantityTextNode;

    public Action<LootedItemDisplayNode>? OnDismiss { get; set; }

    public LootedItemDisplayNode()
    {
        Size = new Vector2(42, 46);

        _iconNode = new IconNode
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(42, 46),
        };
        _iconNode.AddEvent(AtkEventType.MouseClick, OnMouseClick);
        _iconNode.AttachNode(this);

        _quantityTextNode = new TextNode
        {
            Size = new Vector2(40.0f, 12.0f),
            Position = new Vector2(4.0f, 34.0f),
            Color = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(51),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Right,
        };
        _quantityTextNode.AttachNode(this);
    }

    public LootedItemInfo LootedItem
    {
        get;
        set
        {
            bool needsCollisionUpdate = field is null && value is not null;
            field = value;
            var item = value.Item;
            _iconNode.IconId = item.IconId;
            _iconNode.ItemTooltip = item.ItemId;
            _quantityTextNode.String = value.Quantity > 1 ? value.Quantity.ToString() : string.Empty;

            if (needsCollisionUpdate)
            {
                var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
                if (addon is not null)
                    addon->UpdateCollisionNodeList(false);
            }
        }
    } = null!;

    private void OnMouseClick(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (!atkEventData->IsLeftClick) return;
        OnDismiss?.Invoke(this);
    }
}
