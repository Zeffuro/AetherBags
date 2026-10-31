using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public class CategoryListItemNode : ListItemWithFocusNav<CategoryWrapper>, IListItemNode
{
    private readonly TextNode _label;
    private readonly TextNode _subLabel;

    public static float ItemHeight => 48;

    public CategoryListItemNode()
    {
        _label = new TextNode
        {
            FontSize = 14,
            TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
            TextColor = ColorHelper.GetColor(8),
            TextOutlineColor = ColorHelper.GetColor(7),
            AlignmentType = AlignmentType.BottomLeft,
        };
        _label.AttachNode(this);

        _subLabel = new TextNode
        {
            FontSize = 12,
            TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
            TextColor = ColorHelper.GetColor(3),
            TextOutlineColor = ColorHelper.GetColor(7),
            AlignmentType = AlignmentType.TopLeft,
        };
        _subLabel.AttachNode(this);
    }

    protected override void SetNodeData(CategoryWrapper itemData)
    {
        _label.String = itemData.GetLabel();
        _subLabel.String = itemData.GetSubLabel();
    }

    public void RefreshDisplay()
    {
        if (ItemData is { } item) SetNodeData(item);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _label.Position = new Vector2(4, 0);
        _label.Size = new Vector2(Width - 8, Height / 2);
        _subLabel.Position = new Vector2(4, Height / 2);
        _subLabel.Size = _label.Size;
    }
}
