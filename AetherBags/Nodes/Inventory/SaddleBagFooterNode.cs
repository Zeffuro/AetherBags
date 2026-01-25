using System. Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Inventory;

public class SaddleBagFooterNode : SimpleComponentNode
{
    private readonly TextNode _slotCounterNode;

    private const float Padding = 8f;

    public SaddleBagFooterNode()
    {
        _slotCounterNode = new TextNode
        {
            Position = new Vector2(Padding, 4f),
            Size = new Vector2(100, 20),
            AlignmentType = AlignmentType.Left,
            TextColor = new Vector4(1f, 1f, 1f, 1f),
            FontSize = 14,
        };
        _slotCounterNode.AttachNode(this);
    }

    public ReadOnlySeString SlotAmountText
    {
        get => _slotCounterNode.String;
        set => _slotCounterNode.String = $"Slots: {value}";
    }
}