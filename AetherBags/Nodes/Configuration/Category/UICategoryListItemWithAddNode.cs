using System;
using System.Numerics;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes.Configuration.Category;

public class UICategoryListItemWithAddNode : UICategoryListItemNode
{
    private readonly TextButtonNode _addButton;

    public static Action<ItemUICategory>? OnAddClicked { get; set; }

    public UICategoryListItemWithAddNode()
    {
        _addButton = new TextButtonNode
        {
            String = "Add",
            Size = new Vector2(50, 24),
            OnClick = () =>
            {
                if (ItemData is { } cat)
                    OnAddClicked?.Invoke(cat);
            }
        };
        _addButton.AttachNode(this);
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _addButton.Position = new Vector2(Width - 55, (Height - 24) / 2);
        LabelTextNode.Size = new Vector2(Width - 65, Height);
    }
}

