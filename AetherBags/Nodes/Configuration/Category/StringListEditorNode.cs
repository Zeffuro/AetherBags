using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class StringListEditorNode : VerticalListNode
{
    private const float LabelWidth = 300f;
    private const float RowHeight = 28f;

    private List<string> _list = [];

    private readonly LabelTextNode _headerLabel;
    private readonly VerticalListNode _itemsContainer;
    private readonly TextInputNode _addInput;

    public Action? OnChanged { get; set; }

    public required ReadOnlySeString Label
    {
        get => _headerLabel.String;
        init => _headerLabel.String = value;
    }

    public StringListEditorNode()
    {
        FitContents = true;
        ItemSpacing = 4.0f;

        _headerLabel = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(280, 18),
            TextColor = ColorHelper.GetColor(8),
        };
        AddNode(_headerLabel);

        _itemsContainer = new VerticalListNode
        {
            Size = new Vector2(LabelWidth + 40f, 0),
            ItemSpacing = 2.0f,
            FitContents = true,
            FirstItemSpacing = 2,
        };
        AddNode(_itemsContainer);

        var addRow = new HorizontalListNode
        {
            Size = new Vector2(LabelWidth + 40f, RowHeight),
            ItemSpacing = 4.0f,
        };

        _addInput = new TextInputNode
        {
            Size = new Vector2(200, RowHeight),
            PlaceholderString = "Add new...",
            OnInputComplete = _ => AddCurrentValue(),
        };
        addRow.AddNode(_addInput);

        var addButton = new TextButtonNode
        {
            Size = new Vector2(60, RowHeight),
            String = "Add",
            OnClick = AddCurrentValue,
        };
        addRow.AddNode(addButton);

        AddNode(addRow);
    }

    public void SetList(List<string> newList)
    {
        _list = newList;
        RefreshItems();
    }

    private void AddCurrentValue()
    {
        var value = _addInput.String.ExtractText();
        if (!string.IsNullOrWhiteSpace(value) && !_list.Contains(value))
        {
            _list.Add(value);
            _addInput.String = "";
            RefreshItems();
            OnChanged?.Invoke();
        }
    }

    private void RefreshItems()
    {
        _itemsContainer.Clear();

        foreach (var value in _list)
        {
            _itemsContainer.AddNode(CreateItemNode(value));
        }

        if (_list.Count == 0)
        {
            _itemsContainer.Height = 0;
        }

        _itemsContainer.RecalculateLayout();
        RecalculateLayout();
    }

    private StringListItemNode CreateItemNode(string value) => new(value)
    {
        Size = new Vector2(LabelWidth + 40f, RowHeight),
        OnRemove = () => RemoveValue(value),
    };

    private void RemoveValue(string value)
    {
        _list.Remove(value);
        RefreshItems();
        OnChanged?.Invoke();
    }
}

public sealed class StringListItemNode : HorizontalListNode
{
    private const float LabelWidth = 300f;

    public string Value { get; }
    public Action? OnRemove { get; init; }

    public StringListItemNode(string value)
    {
        Value = value;
        ItemSpacing = 4.0f;

        AddNode(new LabelTextNode
        {
            Size = new Vector2(LabelWidth, 24),
            String = value,
            TextColor = ColorHelper.GetColor(3),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.Cross,
            OnClick = () => OnRemove?.Invoke(),
        });
    }
}