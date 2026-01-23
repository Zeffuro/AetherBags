using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class UintListEditorNode : VerticalListNode
{
    private const float LabelWidth = 300f;
    private const float RowHeight = 28f;

    private List<uint> _list = [];

    public List<uint> GetList() => _list.ToList();

    private readonly LabelTextNode _headerLabel;
    private readonly VerticalListNode _itemsContainer;
    private readonly NumericInputNode _addInput;

    public Action? OnSearchButtonClicked { get; init; }

    public Func<uint, string>? LabelResolver { get; init; }
    public Action? OnChanged { get; set; }

    public required ReadOnlySeString Label
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

        var addRow = new HorizontalListNode
        {
            Size = new Vector2(LabelWidth + 40f, RowHeight),
            ItemSpacing = 4.0f,
        };

        var searchButton = new CircleButtonNode
        {
            Size = new Vector2(28),
            Icon = ButtonIcon.MagnifyingGlass,
            OnClick = () => OnSearchButtonClicked?.Invoke(),
            TextTooltip = "Search the game database..."
        };
        addRow.AddNode(searchButton);

        _addInput = new NumericInputNode
        {
            Size = new Vector2(120, RowHeight),
            Min = 0,
            Max = int.MaxValue,
            Value = 0,
        };
        addRow.AddNode(_addInput);

        var addButton = new TextButtonNode
        {
            Size = new Vector2(60, RowHeight),
            String = "Add",
            OnClick = AddCurrentValue,
        };
        addRow.AddNode(addButton);
        addRow.RecalculateLayout();
        AddNode(addRow);
        RecalculateLayout();
    }

    public void SetList(List<uint> newList)
    {
        _list = newList;
        RefreshItems();
    }

    public void AddValue(uint value)
    {
        if (!_list.Contains(value))
        {
            _list.Add(value);
            RefreshItems();
            OnChanged?.Invoke();
        }
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
        OnChanged?.Invoke();
    }

    private UintListItemNode CreateItemNode(uint value) => new(value, LabelResolver)
    {
        Size = new Vector2(LabelWidth + 40f, RowHeight),
        OnRemove = () => RemoveValue(value),
    };

    private void RemoveValue(uint value)
    {
        _list.Remove(value);
        Services.Framework.RunOnTick(() => {
            RefreshItems();
            OnChanged?.Invoke();
        });
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

        string idDisplay = value switch {
            0xFFFF_FFFE => "[Weekly]",
            0xFFFF_FFFD => "[Tome]",
            _ => value.ToString()
        };
        
        var displayText = labelResolver is not null
            ? $"{idDisplay} - {labelResolver(value)}"
            : idDisplay;

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