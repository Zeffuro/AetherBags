using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.Classes.Timelines;
using KamiToolKit.Nodes;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Inventory;

public sealed class InventoryNotificationNode : SimpleComponentNode
{
    private readonly SimpleNineGridNode glowNode;
    private readonly TextNode titleTextNode;
    private readonly TextNode messageTextNode;

    private static readonly InventoryNotificationState NotificationState = new();

    public InventoryNotificationNode()
    {
        AddTimeline(ParentLabels);

        glowNode = new SimpleNineGridNode {
            TexturePath = "ui/uld/Inventory.tex",
            TextureSize = new Vector2(56.0f, 56.0f),
            TextureCoordinates = new Vector2(88.0f, 0.0f),
            TopOffset = 10,
            BottomOffset = 10,
            LeftOffset = 26,
            RightOffset = 26,
        };
        glowNode.AttachNode(this);
        glowNode.AddTimeline(GlowKeyFrames);

        titleTextNode = new TextNode
        {
            Position = new Vector2(0, 10f),
            FontType = FontType.MiedingerMed,
            FontSize = 18,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(37),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Center,
        };
        titleTextNode.AttachNode(this);
        titleTextNode.AddTimeline(TextKeyFrames);

        messageTextNode = new TextNode
        {
            Position = new Vector2(0, -10f),
            FontType = FontType.Axis,
            FontSize = 14,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(37),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Center,
        };
        messageTextNode.AttachNode(this);
        messageTextNode.AddTimeline(TextKeyFrames);

        Timeline?.PlayAnimation(17);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        glowNode.Size = Size with { Y = 40 };
        titleTextNode.Size = Size with { Y = 20 };
        messageTextNode.Size = Size with { Y = 16 };
    }

    public InventoryNotificationType NotificationType
    {
        get;
        set
        {
            field = value;
            if (value == InventoryNotificationType.None)
            {
                titleTextNode.String = string.Empty;
                messageTextNode.String = string.Empty;
                Timeline?.PlayAnimation(17); // Hide
            }
            else
            {
                var info = NotificationState.GetNotificationInfo((uint)value);
                if (info != null)
                {
                    titleTextNode.SeString = info.Title;
                    messageTextNode.SeString = info.Message;
                    Timeline?.PlayAnimation(101); // Show
                }
            }
        }
    } = InventoryNotificationType.None;

    // Future Zeff, this always goes on a parent
    private Timeline ParentLabels => new TimelineBuilder()
        .BeginFrameSet(1, 59)
        .AddLabel(1, 17, AtkTimelineJumpBehavior.PlayOnce, 0)
        .AddLabel(10, 101, AtkTimelineJumpBehavior.Start, 0)
        .AddLabel(25, 102, AtkTimelineJumpBehavior.Start, 0)
        .AddLabel(59, 0, AtkTimelineJumpBehavior.LoopForever, 102)
        .EndFrameSet()
        .Build();

    // Future Zeff, this always goes on a child
    private Timeline GlowKeyFrames => new TimelineBuilder().BeginFrameSet(15, 59)
        .AddFrame(10, scale: new Vector2(1.4f, 1.0f), alpha: 0, addColor: new Vector3(128, 128, 128))
        .AddFrame(15, scale: new Vector2(1.0f, 1.0f), alpha: 255, addColor: new Vector3(128, 128, 128))
        .AddFrame(21, scale: new Vector2(1.0f, 1.0f), alpha: 255, addColor: new Vector3(0, 0, 0))
        .AddFrame(40, scale: new Vector2(1.0f, 1.0f), alpha: 255, addColor: new Vector3(0, 0, 0))
        .AddFrame(46, scale: new Vector2(1.0f, 1.0f), alpha: 255, addColor: new Vector3(10, 10, 10))
        .AddFrame(59, scale: new Vector2(1.0f, 1.0f), alpha: 255, addColor: new Vector3(0, 0, 0))
        .EndFrameSet()
        .Build();

    // Future Zeff, this always goes on a child
    private Timeline TextKeyFrames => new TimelineBuilder().BeginFrameSet(15, 59)
        .AddFrame(15, alpha: 0, addColor: new Vector3(128, 128, 128))
        .AddFrame(18, alpha: 255, addColor: new Vector3(64, 64, 64))
        .AddFrame(25, alpha: 255, addColor: new Vector3(0, 0, 0))
        .AddFrame(40, alpha: 255, addColor: new Vector3(0, 0, 0))
        .AddFrame(46, alpha: 255, addColor: new Vector3(64, 64, 64))
        .AddFrame(59, alpha: 255, addColor: new Vector3(0, 0, 0))
        .EndFrameSet()
        .Build();
}