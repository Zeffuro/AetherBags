using System.Numerics;
using AetherBags.Inventory;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
// TODO: Switch back to CS version when Dalamud Updated
using DragDropFixedNode = AetherBags.Nodes.DragDropNode;

namespace AetherBags.Nodes.Inventory;

public class InventoryDragDropNode : DragDropFixedNode
{
    private readonly TextNode _quantityTextNode;
    public unsafe InventoryDragDropNode()
    {
        _quantityTextNode = new TextNode {
            Size = new Vector2(40.0f, 12.0f),
            Position = new Vector2(4.0f, 34.0f),
            NodeFlags = NodeFlags.Enabled | NodeFlags.EmitsEvents,
            Color = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(51),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Right,
        };
        _quantityTextNode.AttachNode(this);
        CollisionNode.AddEvent(AtkEventType.MouseDown, OnItemMouseDown);
        CollisionNode.AddEvent(AtkEventType.MouseClick, OnItemClicked);
    }

    public required ItemInfo ItemInfo
    {
        get;
        set
        {
            field = value;
            _quantityTextNode.String = value.ItemCount.ToString();
        }
    }

    private unsafe void OnItemMouseDown(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
        InventoryItem item = ItemInfo.Item;
        if (Services.KeyState[VirtualKey.SHIFT] && atkEventData->IsLeftClick && System.Config.General.LinkItemEnabled)
        {
            AgentChatLog.Instance()->LinkItem(item.ItemId);
            return;
        }

        if (!atkEventData->IsRightClick) return;

        AgentInventoryContext* context = AgentInventoryContext.Instance();
        context->OpenForItemSlot(item.Container, item.Slot, 0, context->AddonId);
    }

    private unsafe void OnItemClicked(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (Services.KeyState[VirtualKey.SHIFT] && System.Config.General.LinkItemEnabled) return;
        InventoryItem item = ItemInfo.Item;
        if (!atkEventData->IsLeftClick) return;
        item.UseItem();
    }
}