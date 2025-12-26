using System;
using System.Collections.Generic;
using System.Numerics;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class UintListEditorNode : VerticalListNode
{
    private List<uint> _list;
    private readonly NumericInputNode _addInput;
    private readonly VerticalListNode _itemsContainer;
    private readonly Action?  _onChanged;
    private readonly Func<uint, string>? _labelResolver;

    public UintListEditorNode(string label, List<uint> list, Action? onChanged = null, Func<uint, string>?  labelResolver = null)
    {
        _list = list;
        _onChanged = onChanged;
        _labelResolver = labelResolver;

        FitContents = true;
        ItemSpacing = 4.0f;

        var headerLabel = new LabelTextNode
        {
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

        _addInput = new NumericInputNode
        {
            Size = new Vector2(120, 28),
            Min = 0,
            Max = int.MaxValue,
            Value = 0,
        };
        addRow.AddNode(_addInput);

        var addButton = new TextButtonNode
        {
            Size = new Vector2(60, 28),
            String = "Add",
            OnClick = () =>
            {
                var value = (uint)_addInput.Value;
                if (! _list.Contains(value))
                {
                    _list.Add(value);
                    RefreshItems();
                    _onChanged?.Invoke();
                }
            },
        };
        addRow.AddNode(addButton);

        AddNode(addRow);

        RefreshItems();
    }

    public void SetList(List<uint> newList)
    {
        _list = newList;
        RefreshItems();
    }

    private void RefreshItems()
    {
        _itemsContainer.SyncWithListData(
            _list,
            node => node.Value,
            value => new UintListItemNode(value, _labelResolver)
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

public sealed class UintListItemNode :  HorizontalListNode
{
    public uint Value { get; }
    public Action? OnRemove { get; init; }

    public UintListItemNode(uint value, Func<uint, string>? labelResolver = null)
    {
        Value = value;
        ItemSpacing = 4.0f;

        var displayText = labelResolver != null ? $"{value} - {labelResolver(value)}" : value.ToString();
        var itemLabel = new LabelTextNode
        {
            Size = new Vector2(220, 22),
            String = displayText,
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