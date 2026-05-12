using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.ListItem;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes.Configuration.Category;

public class ItemListItemWithAddNode : ItemListItemNode
{
    private readonly TextButtonNode _addButton;

    public static Action<Item>? OnAddClicked { get; set; }

    public ItemListItemWithAddNode()
    {
        _addButton = new TextButtonNode
        {
            String = "Add",
            Size = new Vector2(50, 24),
            OnClick = () =>
            {
                if (ItemData is { } item)
                    OnAddClicked?.Invoke(item);
            }
        };
        _addButton.AttachNode(this);
    }

    protected override void SetNodeData(Item itemData)
    {
        base.SetNodeData(itemData);

        bool hasSubLabel = !SubLabelTextNode.String.IsEmpty;
        SubLabelTextNode.IsVisible = hasSubLabel;
        LabelTextNode.AlignmentType = hasSubLabel ? AlignmentType.BottomLeft : AlignmentType.Left;
        ApplyLabelLayout();
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _addButton.Position = new Vector2(Width - 55, (Height - 24) / 2);
        ApplyLabelLayout();
    }

    private void ApplyLabelLayout()
    {
        if (Width <= 0 || Height <= 0) return;

        float labelWidth = Width - Height - 2.0f - 65.0f;
        float labelHeight = SubLabelTextNode.IsVisible ? Height / 2.0f : Height;
        LabelTextNode.Size = new Vector2(labelWidth, labelHeight);

        if (SubLabelTextNode.IsVisible)
            SubLabelTextNode.Size = new Vector2(labelWidth - 10.0f, Height / 2.0f);
    }
}

