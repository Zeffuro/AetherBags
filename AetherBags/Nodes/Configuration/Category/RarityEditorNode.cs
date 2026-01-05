using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class RarityEditorNode :VerticalListNode
{
    private const float LabelWidth = 120f;
    private const float CheckboxWidth = 150f;

    private static readonly string[] RarityNames =
    [
        "Common (White)",
        "Uncommon (Green)",
        "Rare (Blue)",
        "Relic (Purple)",
        "Aetherial (Pink)"
    ];

    public Action? OnChanged { get; set; }

    private List<int> _list = [];
    private readonly List<CheckboxNode> _checkboxes = [];

    public RarityEditorNode()
    {
        FitContents = true;
        ItemSpacing = 2.0f;

        var headerLabel = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Size = new Vector2(280, 18),
            String = "Allowed Rarities:",
            TextColor = ColorHelper.GetColor(8),
        };
        AddNode(headerLabel);

        for (var i = 0; i < RarityNames.Length; i++)
        {
            var rarity = i;
            var checkbox = new CheckboxNode
            {
                Size = new Vector2(LabelWidth + CheckboxWidth, 22),
                String = RarityNames[i],
                OnClick = isChecked => ToggleRarity(rarity, isChecked),
            };
            _checkboxes.Add(checkbox);
            AddNode(checkbox);
        }
    }

    private void ToggleRarity(int rarity, bool isChecked)
    {
        if (isChecked && !_list.Contains(rarity))
        {
            _list.Add(rarity);
            _list.Sort();
        }
        else if (!isChecked && _list.Contains(rarity))
        {
            _list.Remove(rarity);
        }

        OnChanged?.Invoke();
    }

    public void SetList(List<int> newList)
    {
        _list = newList;
        Refresh();
    }

    public void Refresh()
    {
        for (var i = 0; i < _checkboxes.Count; i++)
        {
            _checkboxes[i].IsChecked = _list.Contains(i);
        }
    }
}