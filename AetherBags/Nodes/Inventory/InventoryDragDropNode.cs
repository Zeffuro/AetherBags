using System.Numerics;
using AetherBags.Addons;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Items;
using AetherBags.IPC.ExternalCategorySystem;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Timelines;

namespace AetherBags.Nodes.Inventory;

public class InventoryDragDropNode : DragDropNode
{
    private const int Disable = 0;
    private const int Glow = 1;
    private const int GlowHover = 2;
    private const int Pulse = 3;
    private const int PulseHover = 4;
    private const int Solid = 5;
    private const int SolidHover = 6;

    private readonly TextNode _quantityTextNode;
    private IconImageNode? _badgeNode;
    private ResNode? _borderContainerNode;
    private ImageNode? _borderNode;
    private BorderStyle _currentBorderStyle;
    private bool _isBorderHovered;
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
            var decoration = ExternalCategoryManager.GetDecoration(value.Item.ItemId);
            Services.Logger.DebugOnly($"[ItemInfo.set] Item {value.Item.ItemId}: Decoration={decoration.HasValue}, Badge={decoration?.Badge.HasValue ?? false}");
            ApplyDecoration(decoration);
        }
    }

    public void ApplyDecoration(ItemDecoration? decoration)
    {
        _currentDecoration = decoration;

        if (!decoration.HasValue)
        {
            ClearDecoration();
            return;
        }

        var dec = decoration.Value;
        Services.Logger.DebugOnly($"[ApplyDecoration] Item has decoration: Badge={dec.Badge.HasValue}, Border={dec.Border}, Overlay={dec.OverlayColor}");

        if (dec.Badge.HasValue)
        {
            Services.Logger.DebugOnly($"[ApplyDecoration] Applying badge: IconId={dec.Badge.Value.IconId}, Position={dec.Badge.Value.Position}");
            ApplyBadge(dec.Badge.Value);
        }
        else
        {
            ClearBadge();
        }

        if (dec.Border != BorderStyle.None)
        {
            Services.Logger.DebugOnly($"[ApplyDecoration] Applying border: {dec.Border}");
            ApplyBorder(dec.Border);
        }
        else
        {
            ClearBorder();
        }
    }

    private void ApplyBadge(BadgeInfo badge)
    {
        bool wasNull = _badgeNode == null;
        if (_badgeNode == null)
        {
            _badgeNode = new IconImageNode
            {
                Size = new Vector2(16, 16),
                NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
                WrapMode = WrapMode.Stretch,
            };

            _badgeNode.AttachNode(IconNode);
        }

        Services.Logger.DebugOnly($"[ApplyBadge] Badge node was {(wasNull ? "created" : "reused")}, setting IconId={badge.IconId}");

        _badgeNode.IconId = badge.IconId;
        _badgeNode.IsVisible = true;

        if (badge.TintColor.HasValue)
        {
            _badgeNode.AddColor = new Vector3(badge.TintColor.Value.X, badge.TintColor.Value.Y, badge.TintColor.Value.Z);
        }
        else
        {
            _badgeNode.AddColor = Vector3.Zero;
        }

        _badgeNode.Position = badge.Position switch
        {
            BadgePosition.TopLeft => new Vector2(2, 3),
            BadgePosition.TopRight => new Vector2(26, 3),
            BadgePosition.BottomLeft => new Vector2(2, 27),
            BadgePosition.BottomRight => new Vector2(26, 27),
            _ => new Vector2(26, 3)
        };
    }

    private void ClearBadge()
    {
        if (_badgeNode != null)
        {
            _badgeNode.IsVisible = false;
        }
    }

    private void ApplyBorder(BorderStyle style)
    {
        if (_borderContainerNode == null)
        {
            _borderContainerNode = new ResNode
            {
                Size = new Vector2(72.0f, 72.0f),
                Position = new Vector2(-12.0f, -12.0f),
                NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
            };
            _borderContainerNode.AttachNode(this);

            _borderNode = new ImageNode
            {
                Size = new Vector2(72.0f, 72.0f),
                NodeFlags = NodeFlags.Visible | NodeFlags.Enabled,
                PartId = 16,
                WrapMode = WrapMode.Tile,
            };
            IconNodeTextureHelper.LoadIconAFrameTexture(_borderNode);
            _borderNode.AttachNode(_borderContainerNode);

            _borderContainerNode.AddTimeline(BuildBorderParentTimeline());
            _borderNode.AddTimeline(BuildBorderChildTimeline());
        }

        IconNode.IconExtras.HoveredBorderImageNode.IsVisible = false;
        _borderContainerNode.IsVisible = true;
        _currentBorderStyle = style;
        _isBorderHovered = false;

        var label = GetBorderLabel(style, false);
        _borderContainerNode.Timeline?.PlayAnimation(label);
    }

    private static Timeline BuildBorderParentTimeline()
    {
        return new TimelineBuilder()
            .BeginFrameSet(1, 361)
            // Disable
            .AddLabel(1, Disable, AtkTimelineJumpBehavior.PlayOnce, 0)
            // Glow: frames 2-61
            .AddLabel(2, Glow, AtkTimelineJumpBehavior.Start, 0)
            .AddLabel(61, 0, AtkTimelineJumpBehavior.LoopForever, Glow)
            // Glow hovered: frames 62-121
            .AddLabel(62, GlowHover, AtkTimelineJumpBehavior.Start, 0)
            .AddLabel(121, 0, AtkTimelineJumpBehavior.LoopForever, GlowHover)
            // Pulse: frames 122-181
            .AddLabel(122, Pulse, AtkTimelineJumpBehavior.Start, 0)
            .AddLabel(181, 0, AtkTimelineJumpBehavior.LoopForever, Pulse)
            // Pulse hovered: frames 182-241
            .AddLabel(182, PulseHover, AtkTimelineJumpBehavior.Start, 0)
            .AddLabel(241, 0, AtkTimelineJumpBehavior.LoopForever, PulseHover)
            // Solid: frames 242-301
            .AddLabel(242, Solid, AtkTimelineJumpBehavior.Start, 0)
            .AddLabel(301, 0, AtkTimelineJumpBehavior.LoopForever, Solid)
            // Solid hovered: frames 302-361
            .AddLabel(302, SolidHover, AtkTimelineJumpBehavior.Start, 0)
            .AddLabel(361, 0, AtkTimelineJumpBehavior.LoopForever, SolidHover)
            .EndFrameSet()
            .Build();
    }

    private static Timeline BuildBorderChildTimeline()
    {
        return new TimelineBuilder()
            // Disable (frame 1)
            .AddFrameSetWithFrame(1, 1, 1, addColor: Vector3.Zero, alpha: 255)
            // Glow (2-61): blue cycle 1s
            .BeginFrameSet(2, 61)
            .AddFrame(2, addColor: new Vector3(0.3f, 0.5f, 0.8f), alpha: 200)
            .AddFrame(31, addColor: new Vector3(0.5f, 0.7f, 1.0f), alpha: 255)
            .AddFrame(61, addColor: new Vector3(0.3f, 0.5f, 0.8f), alpha: 200)
            .EndFrameSet()
            // Glow hovered (62-121): brighter blue cycle
            .BeginFrameSet(62, 121)
            .AddFrame(62, addColor: new Vector3(0.5f, 0.7f, 1.0f), alpha: 230)
            .AddFrame(91, addColor: new Vector3(0.7f, 0.9f, 1.2f), alpha: 255)
            .AddFrame(121, addColor: new Vector3(0.5f, 0.7f, 1.0f), alpha: 230)
            .EndFrameSet()
            // Pulse (122-181): orange cycle 1s
            .BeginFrameSet(122, 181)
            .AddFrame(122, addColor: new Vector3(0.8f, 0.4f, 0.0f), alpha: 180)
            .AddFrame(151, addColor: new Vector3(1.0f, 0.6f, 0.2f), alpha: 255)
            .AddFrame(181, addColor: new Vector3(0.8f, 0.4f, 0.0f), alpha: 180)
            .EndFrameSet()
            // Pulse hovered (182-241): brighter orange cycle
            .BeginFrameSet(182, 241)
            .AddFrame(182, addColor: new Vector3(1.0f, 0.6f, 0.2f), alpha: 220)
            .AddFrame(211, addColor: new Vector3(1.2f, 0.8f, 0.4f), alpha: 255)
            .AddFrame(241, addColor: new Vector3(1.0f, 0.6f, 0.2f), alpha: 220)
            .EndFrameSet()
            // Solid (242-301): static gray
            .AddFrameSetWithFrame(242, 301, 242, addColor: new Vector3(0.3f, 0.3f, 0.3f), alpha: 200)
            // Solid hovered (302-361): brighter gray
            .AddFrameSetWithFrame(302, 361, 302, addColor: new Vector3(0.5f, 0.5f, 0.5f), alpha: 255)
            .Build();
    }

    private static int GetBorderLabel(BorderStyle style, bool hovered) => (style, hovered) switch
    {
        (BorderStyle.Glow, false) => Glow,
        (BorderStyle.Glow, true) => GlowHover,
        (BorderStyle.Pulse, false) => Pulse,
        (BorderStyle.Pulse, true) => PulseHover,
        (BorderStyle.Solid, false) => Solid,
        (BorderStyle.Solid, true) => SolidHover,
        _ => Disable,
    };

    private void ClearBorder()
    {
        if (_borderContainerNode != null)
        {
            _borderContainerNode.Timeline?.PlayAnimation(Disable);
            _borderContainerNode.IsVisible = false;
            IconNode.IconExtras.HoveredBorderImageNode.IsVisible = true;
            _currentBorderStyle = BorderStyle.None;
            _isBorderHovered = false;
        }
    }

    private void ClearDecoration()
    {
        ClearBadge();
        ClearBorder();
        // Note: AddColor is managed by ApplyItemDataToNode, not here
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

        if (_currentBorderStyle != BorderStyle.None && !_isBorderHovered)
        {
            _isBorderHovered = true;
            var label = GetBorderLabel(_currentBorderStyle, true);
            _borderContainerNode?.Timeline?.PlayAnimation(label);
        }

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
                InventoryOrchestrator.RefreshHighlights();
            }
        }
    }

    private unsafe void OnItemUnhover(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        System.AetherBagsAPI?.API.RaiseItemUnhovered(ItemInfo.Item.ItemId);

        if (_currentBorderStyle != BorderStyle.None && _isBorderHovered)
        {
            _isBorderHovered = false;
            var label = GetBorderLabel(_currentBorderStyle, false);
            _borderContainerNode?.Timeline?.PlayAnimation(label);
        }

        if (System.Config.General.UseUnifiedExternalCategories)
        {
            HighlightState.SetRelationshipHighlight(null, null);
            InventoryOrchestrator.RefreshHighlights();
        }
    }

    public void ResetForReuse()
    {
        ClearDecoration();
        _currentDecoration = null; // Reset so next ApplyDecoration runs fully
        _currentBorderStyle = BorderStyle.None;
        _isBorderHovered = false;
        _quantityTextNode.String = string.Empty;
        Alpha = 1.0f;
        IconNode.AddColor = Vector3.Zero;
        IsDraggable = true;
        IconNode.IconExtras.AntsNode.IsVisible = false;
    }
}