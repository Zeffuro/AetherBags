using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class StringListEditorNode : VerticalListNode
{
    private List<string> _list;
    private readonly TextInputNode _addInput;
    private readonly VerticalListNode _itemsContainer;
    private readonly Action? _onChanged;

    public StringListEditorNode(string label, List<string> list, Action? onChanged = null)
    {
        _list = list;
        _onChanged = onChanged;

        FitContents = true;
        ItemSpacing = 4.0f;

        var headerLabel = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(280, 18),
            String = label,
            TextColor = ColorHelper.GetColor(8),
        };
        AddNode(headerLabel);

        _itemsContainer = new VerticalListNode
        {
            FitContents = true,
            ItemSpacing = 2.0f,
        };
        AddNode(_itemsContainer);

        var addRow = new HorizontalListNode { Size = new Vector2(300, 28), ItemSpacing = 4.0f };

        _addInput = new TextInputNode
        {
            Size = new Vector2(200, 28),
            PlaceholderString = "Add new...",
            OnInputComplete = text =>
            {
                var value = text.ExtractText();
                if (!string.IsNullOrWhiteSpace(value) && ! _list.Contains(value))
                {
                    _list.Add(value);
                    _addInput?.String = "";
                    RefreshItems();
                    _onChanged?.Invoke();
                }
            },
        };
        addRow.AddNode(_addInput);

        var addButton = new TextButtonNode
        {
            Size = new Vector2(60, 28),
            String = "Add",
            OnClick = () =>
            {
                var value = _addInput.String;
                if (!string.IsNullOrWhiteSpace(value) && !_list.Contains(value))
                {
                    _list.Add(value);
                    _addInput.String = "";
                    RefreshItems();
                    _onChanged?.Invoke();
                }
            },
        };
        addRow.AddNode(addButton);

        AddNode(addRow);

        RefreshItems();
    }

    public void SetList(List<string> newList)
    {
        _list = newList;
        RefreshItems();
    }

    private void RefreshItems()
    {
        _itemsContainer.SyncWithListData(
            _list,
            node => node.Value,
            value => new StringListItemNode(value)
            {
                Size = new Vector2(280, 22),
                OnRemove = () =>
                {
                    _list.Remove(value);
                    RefreshItems();
                    _onChanged?.Invoke();
                },
            }
        );

        _itemsContainer.RecalculateLayout();
        RecalculateLayout();
    }

    public void Refresh()
    {
        RefreshItems();
    }
}

public sealed class StringListItemNode : HorizontalListNode
{
    public string Value { get; }
    public Action? OnRemove { get; init; }

    public StringListItemNode(string value)
    {
        Value = value;
        ItemSpacing = 4.0f;

        var itemLabel = new LabelTextNode
        {
            Size = new Vector2(220, 22),
            String = value,
            TextColor = ColorHelper.GetColor(3),
        };
        AddNode(itemLabel);

        var removeButton = new TextButtonNode
        {
            Size = new Vector2(50, 22),
            String = "X",
            OnClick = () => OnRemove?.Invoke(),
        };
        AddNode(removeButton);
    }
}