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
    public const float MaxContentHeight = short.MaxValue;
    public bool IsContentLimited { get; private set; }

    public CollisionNode ScrollingCollisionNode { get; }
    public ResNode ClippingContentNode { get; }
    public T ContentNode { get; }
    public ScrollBarNode ScrollBarNode { get; }

    public unsafe InventoryScrollingAreaNode()
    {
        CollisionNode.RemoveNodeFlags(NodeFlags.Focusable);
        ScrollingCollisionNode = new CollisionNode
        {
            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.HasCollision | NodeFlags.RespondToMouse | NodeFlags.EmitsEvents,
        };
        ScrollingCollisionNode.AttachNode(this);

        ClippingContentNode = new ResNode
        {
            NodeFlags = NodeFlags.Clip | NodeFlags.EmitsEvents | NodeFlags.Visible,
        };
        ClippingContentNode.AttachNode(this);

        ContentNode = new T();
        ContentNode.AttachNode(ClippingContentNode);

        ScrollBarNode = new ScrollBarNode { HideWhenDisabled = true, IsAcceptingMouseWheelEvents = true };
        ScrollBarNode.AttachNode(this);
        ScrollBarNode.SetContentNodes(ContentNode, ScrollingCollisionNode);

        ClippingContentNode.AddLinkedEvent(ScrollBarNode, 5, AtkEventType.MouseWheel);
        ScrollingCollisionNode.AddLinkedEvent(ScrollBarNode, 5, AtkEventType.MouseWheel);
        ContentNode.AddLinkedEvent(ScrollBarNode, 5, AtkEventType.MouseWheel);
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
            IsContentLimited = !float.IsFinite(value) || value > MaxContentHeight;
            ContentNode.Height = float.IsFinite(value) ? Math.Clamp(value, 0, MaxContentHeight) : 0;
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
