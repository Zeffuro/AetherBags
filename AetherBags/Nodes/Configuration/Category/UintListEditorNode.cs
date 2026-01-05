using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class UintListEditorNode : VerticalListNode
{
    private const float LabelWidth = 300f;
    private const float RowHeight = 28f;

    private List<uint> _list = [];

    private readonly LabelTextNode _headerLabel;
    private readonly VerticalListNode _itemsContainer;
    private readonly HorizontalListNode _addRow;
    private readonly NumericInputNode _addInput;

    public Func<uint, string>? LabelResolver { get; init; }
    public Action? OnChanged { get; set; }

    public required string Label
    {
        get => _headerLabel.String;
        init => _headerLabel.String = value;
    }

    public UintListEditorNode()
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

        _addRow = new HorizontalListNode
        {
            Size = new Vector2(LabelWidth + 40f, RowHeight),
            ItemSpacing = 4.0f,
        };

        _addInput = new NumericInputNode
        {
            Size = new Vector2(120, RowHeight),
            Min = 0,
            Max = int.MaxValue,
            Value = 0,
        };
        _addRow.AddNode(_addInput);

        var addButton = new TextButtonNode
        {
            Size = new Vector2(60, RowHeight),
            String = "Add",
            OnClick = AddCurrentValue,
        };
        _addRow.AddNode(addButton);

        AddNode(_addRow);
    }

    public void SetList(List<uint> newList)
    {
        _list = newList;
        RefreshItems();
    }

    private void AddCurrentValue()
    {
        var value = (uint)_addInput.Value;
        if (!_list.Contains(value))
        {
            _list.Add(value);
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

    private UintListItemNode CreateItemNode(uint value) => new(value, LabelResolver)
    {
        Size = new Vector2(LabelWidth + 40f, RowHeight),
        OnRemove = () => RemoveValue(value),
    };

    private void RemoveValue(uint value)
    {
        _list.Remove(value);
        RefreshItems();
        OnChanged?.Invoke();
    }
}

public sealed class UintListItemNode : HorizontalListNode
{
    private const float LabelWidth = 300f;

    public uint Value { get; }
    public Action? OnRemove { get; init; }

    public UintListItemNode(uint value, Func<uint, string>? labelResolver = null)
    {
        Value = value;
        ItemSpacing = 4.0f;

        var displayText = labelResolver is not null
            ? $"{value} - {labelResolver(value)}"
            : value.ToString();

        AddNode(new LabelTextNode
        {
            Size = new Vector2(LabelWidth, 24),
            String = displayText,
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