using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes.Configuration.Category;

public class UICategoryListItemNode : ListItemNode<ItemUICategory> {
    public override float ItemHeight => 30.0f;
    protected readonly TextNode LabelTextNode;

    public UICategoryListItemNode() {
        LabelTextNode = new TextNode {
            FontSize = 14,
            AlignmentType = AlignmentType.Left,
            TextColor = ColorHelper.GetColor(8),
        };
        LabelTextNode.AttachNode(this);
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();
        LabelTextNode.Size = Size with { X = Width - 10 };
        LabelTextNode.Position = new Vector2(5, 0);
    }

    protected override void SetNodeData(ItemUICategory data) {
        LabelTextNode.String = data.Name.ToString();
    }
}