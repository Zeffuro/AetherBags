using System.Numerics;
using AetherBags.Addons;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Items;
using AetherBags.IPC.ExternalCategorySystem;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Timelines;

namespace AetherBags.Nodes.Inventory;

public class InventoryDragDropNode : DragDropNode
{
    private readonly TextNode _quantityTextNode;
    private IconNode? _badgeNode;
    private ImageNode? _borderNode;
    private ItemDecoration? _currentDecoration;

    public unsafe InventoryDragDropNode()
    {
        _quantityTextNode = new TextNode {
            Size = new Vector2(40.0f, 12.0f),
            Position = new Vector2(4.0f, 34.0f),
            NodeFlags = NodeFlags.Enabled | NodeFlags.EmitsEvents,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(51),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Right,
        };
        _quantityTextNode.AttachNode(this);
        CollisionNode.AddEvent(AtkEventType.MouseDown, OnItemMouseDown);
        CollisionNode.AddEvent(AtkEventType.MouseClick, OnItemClicked);
        CollisionNode.AddEvent(AtkEventType.MouseOver, OnItemHover);
        CollisionNode.AddEvent(AtkEventType.MouseOut, OnItemUnhover);
    }

    public required ItemInfo ItemInfo
    {
        get;
        set
        {
            field = value;
            _quantityTextNode.String = value.ItemCount.ToString();
            ApplyDecoration(ExternalCategoryManager.GetDecoration(value.Item.ItemId));
        }
    }

    public void ApplyDecoration(ItemDecoration? decoration)
    {
        if (_currentDecoration.Equals(decoration)) return;
        _currentDecoration = decoration;

        if (decoration == null)
        {
            ClearDecoration();
            return;
        }

        if (decoration.Value.Badge.HasValue)
        {
            ApplyBadge(decoration.Value.Badge.Value);
        }
        else
        {
            ClearBadge();
        }

        if (decoration.Value.Border != BorderStyle.None)
        {
            ApplyBorder(decoration.Value.Border);
        }
        else
        {
            ClearBorder();
        }
    }

    private void ApplyBadge(BadgeInfo badge)
    {
        if (_badgeNode == null)
        {
            _badgeNode = new IconNode
            {
                Size = new Vector2(16, 16),
                NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
            };
            _badgeNode.AttachNode(this);
        }

        _badgeNode.IconId = badge.IconId;
        _badgeNode.IsVisible = true;

        if (badge.TintColor.HasValue)
        {
            _badgeNode.AddColor = new Vector3(badge.TintColor.Value.X, badge.TintColor.Value.Y, badge.TintColor.Value.Z);
        }

        _badgeNode.Position = badge.Position switch
        {
            BadgePosition.TopLeft => new Vector2(0, 0),
            BadgePosition.TopRight => new Vector2(26, 0),
            BadgePosition.BottomLeft => new Vector2(0, 30),
            BadgePosition.BottomRight => new Vector2(26, 30),
            _ => new Vector2(26, 0)
        };
    }

    private void ClearBadge()
    {
        if (_badgeNode != null)
        {
            _badgeNode.IsVisible = false;
        }
    }

    private BorderStyle _currentBorderStyle = BorderStyle.None;

    private void ApplyBorder(BorderStyle style)
    {
        if (_borderNode == null)
        {
            _borderNode = new SimpleImageNode
            {
                Size = new Vector2(42, 46),
                Position = new Vector2(0, 0),
                NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
                TexturePath = "ui/uld/IconA_Frame.tex",
                TextureCoordinates = new Vector2(0, 0),
                TextureSize = new Vector2(48, 48),
            };
            _borderNode.AttachNode(this);
        }

        _borderNode.IsVisible = true;

        if (_currentBorderStyle != style)
        {
            _currentBorderStyle = style;
            BuildBorderTimeline(style);
        }

        if (style == BorderStyle.Pulse)
        {
            _borderNode.Timeline?.PlayAnimation(1);
        }
        else if (style == BorderStyle.Glow)
        {
            _borderNode.Timeline?.PlayAnimation(1);
        }
    }

    private void BuildBorderTimeline(BorderStyle style)
    {
        if (_borderNode == null) return;

        switch (style)
        {
            case BorderStyle.Solid:
                _borderNode.AddColor = new Vector3(1.0f, 1.0f, 1.0f);
                break;

            case BorderStyle.Glow:
                _borderNode.AddTimeline(new TimelineBuilder()
                    .BeginFrameSet(10, 50)
                    .AddLabel(10, 1, AtkTimelineJumpBehavior.LoopForever, 10)
                    .AddFrame(10, addColor: new Vector3(0.6f, 0.8f, 1.0f), alpha: 255)
                    .AddFrame(30, addColor: new Vector3(0.9f, 1.0f, 1.2f), alpha: 255)
                    .AddFrame(50, addColor: new Vector3(0.6f, 0.8f, 1.0f), alpha: 255)
                    .EndFrameSet()
                    .Build());
                break;

            case BorderStyle.Pulse:
                _borderNode.AddTimeline(new TimelineBuilder()
                    .BeginFrameSet(1, 40)
                    .AddLabel(1, 1, AtkTimelineJumpBehavior.LoopForever, 1)
                    .AddFrame(1, addColor: new Vector3(1.0f, 0.6f, 0.0f), alpha: 180)
                    .AddFrame(20, addColor: new Vector3(1.2f, 0.9f, 0.3f), alpha: 255)
                    .AddFrame(40, addColor: new Vector3(1.0f, 0.6f, 0.0f), alpha: 180)
                    .EndFrameSet()
                    .Build());
                break;
        }
    }

    private void ClearBorder()
    {
        if (_borderNode != null)
        {
            _borderNode.Timeline?.StopAnimation();
            _borderNode.IsVisible = false;
            _currentBorderStyle = BorderStyle.None;
        }
    }

    private void ClearDecoration()
    {
        ClearBadge();
        ClearBorder();
    }

    private unsafe void OnItemMouseDown(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData) {
        InventoryItem item = ItemInfo.Item;
        if (Services.KeyState[VirtualKey.SHIFT] && atkEventData->IsLeftClick && System.Config.General.LinkItemEnabled)
        {
            AgentChatLog.Instance()->LinkItem(item.ItemId);
            return;
        }

        if (!atkEventData->IsRightClick) return;

        if (Services.KeyState[VirtualKey.CONTROL] && ItemContextMenuHandler.TryShowExternalMenu(ItemInfo))
        {
            return;
        }

        AgentInventoryContext* context = AgentInventoryContext.Instance();
        context->OpenForItemSlot(item.Container, item.Slot, 0, context->AddonId);
    }

    private unsafe void OnItemClicked(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (Services.KeyState[VirtualKey.SHIFT] && System.Config.General.LinkItemEnabled) return;
        InventoryItem item = ItemInfo.Item;
        if (!atkEventData->IsLeftClick) return;

        System.AetherBagsAPI?.API.RaiseItemClicked(item.ItemId);
        item.UseItem();
    }

    private unsafe void OnItemHover(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        uint itemId = ItemInfo.Item.ItemId;
        System.AetherBagsAPI?.API.RaiseItemHovered(itemId);

        if (System.Config.General.UseUnifiedExternalCategories)
        {
            var relatedItems = ExternalCategoryManager.GetRelatedItemIds(itemId, RelationshipType.SameSet);
            if (relatedItems != null && relatedItems.Count > 0)
            {
                var relationships = ExternalCategoryManager.GetItemRelationships(itemId);
                Vector3? highlightColor = null;
                if (relationships != null)
                {
                    foreach (var rel in relationships)
                    {
                        if (rel.Type == RelationshipType.SameSet && rel.HighlightColor.HasValue)
                        {
                            highlightColor = rel.HighlightColor;
                            break;
                        }
                    }
                }
                HighlightState.SetRelationshipHighlight(relatedItems, highlightColor);
            }
        }
    }

    private unsafe void OnItemUnhover(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        System.AetherBagsAPI?.API.RaiseItemUnhovered(ItemInfo.Item.ItemId);

        if (System.Config.General.UseUnifiedExternalCategories)
        {
            HighlightState.SetRelationshipHighlight(null, null);
        }
    }

    public void ResetForReuse()
    {
        ClearDecoration();
        _quantityTextNode.String = string.Empty;
        Alpha = 1.0f;
        AddColor = Vector3.Zero;
        IsDraggable = true;
        IconNode.IconExtras.AntsNode.IsVisible = false;
    }
}