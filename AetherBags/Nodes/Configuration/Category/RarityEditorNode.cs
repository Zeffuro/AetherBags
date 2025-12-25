using System;
using System.Collections.Generic;
using System.Numerics;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class RarityEditorNode : VerticalListNode
{
    private static readonly string[] RarityNames = { "Common (White)", "Uncommon (Green)", "Rare (Blue)", "Relic (Purple)", "Aetherial (Pink)" };

    private List<int> _list;
    private readonly List<CheckboxNode> _checkboxes = new();
    private readonly Action? _onChanged;

    public RarityEditorNode(List<int> list, Action? onChanged = null)
    {
        _list = list;
        _onChanged = onChanged;

        FitContents = true;
        ItemSpacing = 2.0f;

        var headerLabel = new LabelTextNode
        {
            Size = new Vector2(280, 18),
            String = "Allowed Rarities:",
            TextColor = ColorHelper.GetColor(8),
        };
        AddNode(headerLabel);

        for (int i = 0; i < RarityNames.Length; i++)
        {
            var rarity = i;
            var checkbox = new CheckboxNode
            {
                Size = new Vector2(200, 20),
                String = RarityNames[i],
                IsChecked = _list.Contains(i),
                OnClick = isChecked =>
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
                    _onChanged?.Invoke();
                },
            };
            _checkboxes.Add(checkbox);
            AddNode(checkbox);
        }
    }

    public void SetList(List<int> newList)
    {
        _list = newList;
        Refresh();
    }

    public void Refresh()
    {
        for (int i = 0; i < _checkboxes.Count; i++)
        {
            _checkboxes[i].IsChecked = _list.Contains(i);
        }
    }
}