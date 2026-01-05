using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Nodes;
using System;
using System.Numerics;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class StateFilterRowNode : HorizontalListNode
{
    private const float LabelWidth = 120f;
    private const float ButtonWidth = 100f;

    private readonly StateFilterButton _stateButton;
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

        _stateButton = new StateFilterButton
        {
            Size = new Vector2(ButtonWidth, 24),
            States = [0, 1, 2],
            SelectedState = _filter.State,
            OnStateChanged = newState =>
            {
                _filter.State = newState;
                _onChanged?.Invoke();
            }
        };
        AddNode(_stateButton);
    }

    public void SetState(StateFilter newFilter)
    {
        _filter = newFilter;
        _stateButton.SelectedState = _filter.State;
    }

    private sealed class StateFilterButton : MultiStateButtonNode<int>
    {
        private static readonly string[] StateLabels = ["Ignored", "Required", "Excluded"];

        protected override string GetStateText(int state)
            => state >= 0 && state < StateLabels.Length ?StateLabels[state] : "Unknown";
    }
}