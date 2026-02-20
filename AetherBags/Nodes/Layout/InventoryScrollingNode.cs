using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Layout;

/// <summary>
/// A copy of KamiToolKit's ScrollingAreaNode with ContentAreaClipNode changed from
/// SimpleComponentNode to ResNode, to prevent the native AtkDragDropManager from
/// treating the clip boundary as a component node (which blocks drag-to-hotbar).
/// According to Kami it is possible that this may or may not leak.
/// We will eventually change this later.
/// </summary>
public unsafe class InventoryScrollingAreaNode<T> : SimpleComponentNode where T : NodeBase, new() {

    public readonly ResNode ContentAreaClipNode;
    public readonly T ContentAreaNode;
    public readonly ScrollBarNode ScrollBarNode;
    public readonly CollisionNode ScrollingCollisionNode;

    public InventoryScrollingAreaNode() {
        ScrollingCollisionNode = new CollisionNode();
        ScrollingCollisionNode.AttachNode(this);

        ContentAreaClipNode = new ResNode {
            NodeFlags = NodeFlags.Clip | NodeFlags.EmitsEvents | NodeFlags.Visible,
        };
        ContentAreaClipNode.AttachNode(this);

        ContentAreaNode = new T();
        ContentAreaNode.AttachNode(ContentAreaClipNode);

        ScrollBarNode = new ScrollBarNode {
            ContentNode = ContentAreaNode,
            ContentCollisionNode = ScrollingCollisionNode,
            HideWhenDisabled = true,
        };
        ScrollBarNode.AttachNode(this);

        AtkResNode* clipNode = ContentAreaClipNode;
        AtkResNode* contentNode = ContentAreaNode;

        clipNode->AtkEventManager.RegisterEvent(
            AtkEventType.MouseWheel,
            5,
            null,
            ScrollingCollisionNode,
            ScrollBarNode,
            false);

        ScrollingCollisionNode.Node->AtkEventManager.RegisterEvent(
            AtkEventType.MouseWheel,
            5,
            null,
            ScrollingCollisionNode,
            ScrollBarNode,
            false);

        contentNode->AtkEventManager.RegisterEvent(
            AtkEventType.MouseWheel,
            5,
            null,
            ScrollingCollisionNode,
            ScrollBarNode,
            false);
    }

    public virtual T ContentNode => ContentAreaNode;

    public int ScrollPosition {
        get => ScrollBarNode.ScrollPosition;
        set => ScrollBarNode.ScrollPosition = value;
    }

    public int ScrollSpeed {
        get => ScrollBarNode.ScrollSpeed;
        set => ScrollBarNode.ScrollSpeed = value;
    }

    public required float ContentHeight {
        get => ContentAreaNode.Height;
        set {
            ContentAreaNode.Height = value;
            ScrollBarNode.UpdateScrollParams();
        }
    }

    public bool AutoHideScrollBar {
        get => ScrollBarNode.HideWhenDisabled;
        set => ScrollBarNode.HideWhenDisabled = value;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        ContentAreaNode.Width = Width - 16.0f;
        ScrollingCollisionNode.Size = new Vector2(Width - 16.0f, Height);
        ContentAreaClipNode.Size = new Vector2(Width - 16.0f, Height);
        ScrollBarNode.Size = new Vector2(8.0f, Height);
        ScrollBarNode.UpdateScrollParams();

        ScrollBarNode.X = Width - 8.0f;
    }

    public void FitToContentHeight() {
        if (ContentNode is LayoutListNode layoutNode) {
            ContentHeight = layoutNode.Nodes.Sum(node => node.IsVisible ? node.Height + layoutNode.ItemSpacing : 0.0f) + layoutNode.FirstItemSpacing;
        }
    }
}