using System;
using System.Numerics;
using AetherBags.Configuration;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class StateFilterRowNode : HorizontalListNode
{
    private readonly LabelTextNode _labelNode;
    private readonly TextButtonNode _stateButton;
    private readonly Action? _onChanged;
    private StateFilter _filter;

    private static readonly string[] StateLabels = { "Ignored", "Allow", "Disallow" };

    public StateFilterRowNode(string label, StateFilter filter, Action? onChanged = null)
    {
        _filter = filter;
        _onChanged = onChanged;
        Size = new Vector2(280, 24);
        ItemSpacing = 8.0f;

        _labelNode = new LabelTextNode
        {
            Size = new Vector2(100, 24),
            String = $"{label}:",
            TextColor = ColorHelper.GetColor(8),
        };
        AddNode(_labelNode);

        _stateButton = new TextButtonNode
        {
            Size = new Vector2(100, 24),
            String = StateLabels[_filter.State],
            OnClick = CycleState,
        };
        AddNode(_stateButton);
    }

    private void CycleState()
    {
        _filter.State = (_filter.State + 1) % 3;
        _stateButton.String = StateLabels[_filter.State];
        _onChanged?.Invoke();
    }

    public void SetState(StateFilter newFilter)
    {
        _filter = newFilter;
        _stateButton.String = StateLabels[_filter.State];
    }
}