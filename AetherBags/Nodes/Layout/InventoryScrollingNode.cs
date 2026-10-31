using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;

namespace AetherBags.Nodes.Layout;

// Changing this hierarchy breaks dragging items to hotbars.
public class InventoryScrollingAreaNode<T> : SimpleComponentNode where T : NodeBase, new()
{
    public CollisionNode ScrollingCollisionNode { get; }
    public ResNode ClippingContentNode { get; }
    public T ContentNode { get; }
    public ScrollBarNode ScrollBarNode { get; }

    public unsafe InventoryScrollingAreaNode()
    {
        CollisionNode.RemoveNodeFlags(NodeFlags.Focusable);
        ScrollingCollisionNode = new CollisionNode();
        ScrollingCollisionNode.AttachNode(this);

        ClippingContentNode = new ResNode
        {
            NodeFlags = NodeFlags.Clip | NodeFlags.EmitsEvents | NodeFlags.Visible,
        };
        ClippingContentNode.AttachNode(this);

        ContentNode = new T();
        ContentNode.AttachNode(ClippingContentNode);

        ScrollBarNode = new ScrollBarNode { HideWhenDisabled = true };
        ScrollBarNode.AttachNode(this);
        ScrollBarNode.SetContentNodes(ContentNode, ScrollingCollisionNode);

        AtkResNode* clipNode = ClippingContentNode;
        AtkResNode* collisionNode = ScrollingCollisionNode;
        AtkResNode* contentNode = ContentNode;
        clipNode->AtkEventManager.RegisterEvent(AtkEventType.MouseWheel, 5, null, ScrollingCollisionNode, ScrollBarNode, false);
        collisionNode->AtkEventManager.RegisterEvent(AtkEventType.MouseWheel, 5, null, ScrollingCollisionNode, ScrollBarNode, false);
        contentNode->AtkEventManager.RegisterEvent(AtkEventType.MouseWheel, 5, null, ScrollingCollisionNode, ScrollBarNode, false);
    }

    public bool AutoHideScrollBar
    {
        get => ScrollBarNode.HideWhenDisabled;
        set => ScrollBarNode.HideWhenDisabled = value;
    }

    public required float ContentHeight
    {
        get => ContentNode.Height;
        set
        {
            ContentNode.Height = value;
            ScrollBarNode.UpdateScrollParams();
        }
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        float contentWidth = Math.Max(0.0f, Width - 16.0f);
        ContentNode.Width = contentWidth;
        ScrollingCollisionNode.Size = new Vector2(contentWidth, Height);
        ClippingContentNode.Size = new Vector2(contentWidth, Height);
        ScrollBarNode.Size = new Vector2(8.0f, Height);
        ScrollBarNode.X = Width - 8.0f;
        ScrollBarNode.UpdateScrollParams();
    }
}
