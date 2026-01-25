using AetherBags.Configuration;
using KamiToolKit.Nodes;
using System.Numerics;
using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Timelines;

namespace AetherBags.Nodes.Configuration.Layout;

internal sealed class CompactLookaheadNode : SimpleComponentNode
{
    public readonly LabelTextNode TitleNode;
    public readonly NumericInputNode CompactLookahead = null!;

    public CompactLookaheadNode()
    {
        GeneralSettings config = System.Config.General;

        TitleNode = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Height = 24,
            String = "Compact Lookahead",
        };
        TitleNode.AttachNode(this);

        CompactLookahead = new NumericInputNode
        {
            Position = Position with { X = 240 },
            Size = Size with { X = 88 },
            IsVisible = true,
            IsEnabled = config.CompactPackingEnabled,
            Value = config.CompactLookahead,
            OnValueUpdate = value =>
            {
                config.CompactLookahead = value;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        CompactLookahead.AttachNode(this);

        TitleNode.AddTimeline(new TimelineBuilder()
            .AddFrameSetWithFrame(1, 10, 1, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(11, 20, 11, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(21, 30, 21, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(31, 40, 31, alpha: 102, multiplyColor: new Vector3(80.0f))
            .AddFrameSetWithFrame(41, 50, 41, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(51, 60, 51, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(61, 70, 61, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(71, 80, 71, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(81, 90, 81, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(91, 100, 91, alpha: 102, multiplyColor: new Vector3(80.0f))
            .AddFrameSetWithFrame(101, 110, 101, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(111, 115, 111, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(116, 135, 116, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(126, 135, 126, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(136, 145, 136, alpha: 255, multiplyColor: new Vector3(100.0f))
            .AddFrameSetWithFrame(146, 155, 146, alpha: 255, multiplyColor: new Vector3(100.0f))
            .Build());
    }
}