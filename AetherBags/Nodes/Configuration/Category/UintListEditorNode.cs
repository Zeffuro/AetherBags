using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class UintListEditorNode : VerticalListNode
{
    private const float RowHeight = 28f;
    private const float ButtonsAreaWidth = 100f; // 3 buttons (28) + 3 spacings (4) + slack

    private List<uint> _list = [];

    public List<uint> GetList() => _list.ToList();

    private readonly LabelTextNode _headerLabel;
    private readonly VerticalListNode _itemsContainer;
    private readonly NumericInputNode _addInput;

    public Action? OnSearchButtonClicked { get; init; }

    public Func<uint, string>? LabelResolver { get; init; }
    public Action? OnChanged { get; set; }

    public uint MaxValue { get; init; } = int.MaxValue;

    public float LabelWidth { get; init; } = 300f;
    private float RowWidth => LabelWidth + ButtonsAreaWidth;

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
            Size = new Vector2(RowWidth, 0),
            ItemSpacing = 2.0f,
            FitContents = true,
            FirstItemSpacing = 2,
        };
        AddNode(_itemsContainer);

        var addRow = new HorizontalListNode
        {
            Size = new Vector2(RowWidth, RowHeight),
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
            Size = new Vector2(LabelWidth - 60, RowHeight),
            Min = 0,
            Max = MaxValue > int.MaxValue ? int.MaxValue : (int)MaxValue,
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

    private unsafe void RefreshItems()
    {
        _itemsContainer.Clear();

        for (int i = 0; i < _list.Count; i++)
        {
            _itemsContainer.AddNode(CreateItemNode(_list[i], isFirst: i == 0, isLast: i == _list.Count - 1));
        }

        if (_list.Count == 0)
        {
            _itemsContainer.Height = 0;
        }

        _itemsContainer.RecalculateLayout();
        RecalculateLayout();

        // Drop disposed nodes from the addon's collision list to avoid use-after-free in input handling.
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
        if (addon is not null)
            addon->UpdateCollisionNodeList(false);

        OnChanged?.Invoke();
    }

    private UintListItemNode CreateItemNode(uint value, bool isFirst, bool isLast) => new(value, isFirst, isLast, LabelWidth, LabelResolver)
    {
        Size = new Vector2(RowWidth, RowHeight),
        OnRemove = () => RemoveValue(value),
        OnMoveUp = () => MoveValue(value, -1),
        OnMoveDown = () => MoveValue(value, +1),
    };

    private void RemoveValue(uint value)
    {
        _list.Remove(value);
        // Defer past the tick that handled the click so its hover/event state is fully unwound before disposal.
        Services.Framework.RunOnTick(() => {
            RefreshItems();
            OnChanged?.Invoke();
        }, delayTicks: 2);
    }

    private void MoveValue(uint value, int delta)
    {
        int index = _list.IndexOf(value);
        if (index < 0) return;
        int target = index + delta;
        if (target < 0 || target >= _list.Count) return;

        (_list[index], _list[target]) = (_list[target], _list[index]);
        Services.Framework.RunOnTick(() => {
            RefreshItems();
            OnChanged?.Invoke();
        }, delayTicks: 2);
    }
}

public sealed class UintListItemNode : HorizontalListNode
{
    public uint Value { get; }
    public Action? OnRemove { get; init; }
    public Action? OnMoveUp { get; init; }
    public Action? OnMoveDown { get; init; }

    public UintListItemNode(uint value, bool isFirst, bool isLast, float labelWidth, Func<uint, string>? labelResolver = null)
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
            Size = new Vector2(labelWidth, 24),
            String = displayText,
            TextColor = ColorHelper.GetColor(3),
            TextFlags = TextFlags.OverflowHidden | TextFlags.Ellipsis,
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.UpArrow,
            TextTooltip = "Move up",
            IsEnabled = !isFirst,
            OnClick = () => OnMoveUp?.Invoke(),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.ArrowDown,
            TextTooltip = "Move down",
            IsEnabled = !isLast,
            OnClick = () => OnMoveDown?.Invoke(),
        });

        AddNode(new CircleButtonNode
        {
            Size = new Vector2(28, 28),
            Icon = ButtonIcon.Cross,
            TextTooltip = "Remove",
            OnClick = () => OnRemove?.Invoke(),
        });
    }
}