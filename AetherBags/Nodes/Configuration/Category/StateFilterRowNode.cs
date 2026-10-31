using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using System;
using System.Numerics;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class StateFilterRowNode : HorizontalListNode
{
    private const float LabelWidth = 120f;
    private const float ButtonWidth = 100f;

    private readonly MultiStateButtonNode<FilterState> _stateButton;
    private readonly Action? _onChanged;
    private StateFilter _filter;

    public StateFilterRowNode(string label, StateFilter filter, Action?onChanged = null)
    {
        _filter = filter;
        _onChanged = onChanged;
        Size = new Vector2(LabelWidth + ButtonWidth + 8f, 24);
        ItemSpacing = 8.0f;

        var labelNode = new LabelTextNode
        {
            Size = new Vector2(LabelWidth, 24),
            String = $"{label}:",
            TextColor = ColorHelper.GetColor(8),
            AlignmentType = AlignmentType.Right,
        };
        AddNode(labelNode);

        _stateButton = new MultiStateButtonNode<FilterState>
        {
            Size = new Vector2(ButtonWidth, 24),
            States = [FilterState.Ignored, FilterState.Required, FilterState.Excluded],
            SelectedState = (FilterState)_filter.State,
            OnStateChanged = newState =>
            {
                _filter.State = (int)newState;
                _onChanged?.Invoke();
            }
        };
        AddNode(_stateButton);
    }

    public void SetState(StateFilter newFilter)
    {
        _filter = newFilter;
        _stateButton.SelectedState = (FilterState)_filter.State;
    }

    private enum FilterState
    {
        Ignored,
        Required,
        Excluded,
    }
}
