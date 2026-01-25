using System;
using System.Numerics;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class RangeFilterRow : VerticalListNode
{
    private readonly CheckboxNode _enabledCheckbox;
    private readonly NumericInputNode _minNode;
    private readonly NumericInputNode _maxNode;

    public Action<bool, int, int>? OnFilterChanged { get; set; }

    public required ReadOnlySeString Label
    {
        get => _enabledCheckbox.String.ExtractText().Replace(" Filter", "");
        init => _enabledCheckbox.String = $"{value} Filter";
    }

    public int MinBound
    {
        get => _minNode.Min;
        init
        {
            _minNode.Min = value;
            _maxNode.Min = value;
        }
    }

    public int MaxBound
    {
        get => _minNode.Max;
        init
        {
            _minNode.Max = value;
            _maxNode.Max = value;
        }
    }

    public RangeFilterRow()
    {
        FitContents = true;
        ItemSpacing = 2.0f;

        _enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(200, 20),
            OnClick = isChecked =>
            {
                if (_minNode == null || _maxNode == null) return;
                _minNode.IsEnabled = isChecked;
                _maxNode.IsEnabled = isChecked;
                OnFilterChanged?.Invoke(isChecked, _minNode.Value, _maxNode.Value);
            },
        };
        AddNode(_enabledCheckbox);

        var rangeRow = new HorizontalListNode { Size = new Vector2(300, 28), ItemSpacing = 8.0f };

        rangeRow.AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(30, 28),
            String = "Min:",
        });

        _minNode = new NumericInputNode
        {
            Size = new Vector2(100, 28),
            OnValueUpdate = val =>
            {
                if (_maxNode != null) OnFilterChanged?.Invoke(_enabledCheckbox.IsChecked, val, _maxNode.Value);
            },
        };
        rangeRow.AddNode(_minNode);

        rangeRow.AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(30, 28),
            String = "Max:",
        });

        _maxNode = new NumericInputNode
        {
            Size = new Vector2(100, 28),
            OnValueUpdate = val => OnFilterChanged?.Invoke(_enabledCheckbox.IsChecked, _minNode.Value, val),
        };
        rangeRow.AddNode(_maxNode);

        AddNode(rangeRow);
    }

    public void SetFilter(RangeFilter<int> filter)
    {
        _enabledCheckbox.IsChecked = filter.Enabled;
        _minNode.Value = filter.Min;
        _maxNode.Value = filter.Max;
        _minNode.IsEnabled = filter.Enabled;
        _maxNode.IsEnabled = filter.Enabled;
    }
}

public sealed class RangeFilterRowUint : VerticalListNode
{
    private readonly CheckboxNode _enabledCheckbox;
    private readonly NumericInputNode _minNode;
    private readonly NumericInputNode _maxNode;
    private int _maxBound = int.MaxValue;

    public Action<bool, uint, uint>? OnFilterChanged { get; set; }

    public required ReadOnlySeString Label
    {
        get => _enabledCheckbox.String.ExtractText().Replace(" Filter", "");
        init => _enabledCheckbox.String = $"{value} Filter";
    }

    public int MinBound
    {
        get => _minNode.Min;
        init
        {
            _minNode.Min = value;
            _maxNode.Min = value;
        }
    }

    public int MaxBound
    {
        get => _maxBound;
        init
        {
            _maxBound = value;
            _minNode.Max = value;
            _maxNode.Max = value;
        }
    }

    public RangeFilterRowUint()
    {
        FitContents = true;
        ItemSpacing = 2.0f;

        _enabledCheckbox = new CheckboxNode
        {
            Size = new Vector2(200, 20),
            OnClick = isChecked =>
            {
                if (_minNode == null || _maxNode == null) return;
                _minNode.IsEnabled = isChecked;
                _maxNode.IsEnabled = isChecked;
                OnFilterChanged?.Invoke(isChecked, (uint)_minNode.Value, (uint)_maxNode.Value);
            },
        };
        AddNode(_enabledCheckbox);

        var rangeRow = new HorizontalListNode { Size = new Vector2(300, 28), ItemSpacing = 8.0f };

        rangeRow.AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(30, 28),
            String = "Min:",
        });

        _minNode = new NumericInputNode
        {
            Size = new Vector2(100, 28),
            OnValueUpdate = val =>
            {
                if (_maxNode != null)
                    OnFilterChanged?.Invoke(_enabledCheckbox.IsChecked, (uint)val, (uint)_maxNode.Value);
            },
        };
        rangeRow.AddNode(_minNode);

        rangeRow.AddNode(new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(30, 28),
            String = "Max:",
        });

        _maxNode = new NumericInputNode
        {
            Size = new Vector2(100, 28),
            OnValueUpdate = val => OnFilterChanged?.Invoke(_enabledCheckbox.IsChecked, (uint)_minNode.Value, (uint)val),
        };
        rangeRow.AddNode(_maxNode);

        AddNode(rangeRow);
    }

    public void SetFilter(RangeFilter<uint> filter)
    {
        _enabledCheckbox.IsChecked = filter.Enabled;
        _minNode.Value = (int)filter.Min;
        _maxNode.Value = (int)Math.Min(filter.Max, _maxBound);
        _minNode.IsEnabled = filter.Enabled;
        _maxNode.IsEnabled = filter.Enabled;
    }
}