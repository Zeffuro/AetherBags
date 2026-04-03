using System;
using System.Numerics;
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

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _addButton.Position = new Vector2(Width - 55, (Height - 24) / 2);
        LabelTextNode.Size = new Vector2(Width - Height - 2.0f - 65.0f, Height / 2.0f);
    }
}

