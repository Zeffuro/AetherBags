using AetherBags.Configuration;
using AetherBags.Nodes.Configuration.Layout;
using KamiToolKit.Nodes;
using System;
using System.Linq;
using System.Numerics;

namespace AetherBags.Nodes.Configuration;

public sealed class GeneralScrollingAreaNode : ScrollingAreaNode<VerticalListNode>
{
    private readonly CheckboxNode _debugCheckboxNode = null!;
    private readonly LabeledDropdownNode _stackDropDown = null!;

    public unsafe GeneralScrollingAreaNode()
    {
        GeneralSettings config = System.Config.General;

        ContentNode.ItemSpacing = 32;

        _stackDropDown = new LabeledDropdownNode
        {
            Size = new Vector2(300, 20),
            LabelText = "Stack Mode",
            Options = Enum.GetNames(typeof(InventoryStackMode)).ToList(),
            SelectedOption = config.StackMode.ToString(),
            OnOptionSelected = selected =>
            {
                if (Enum.TryParse<InventoryStackMode>(selected, out var parsed))
                    config.StackMode = parsed;
                RefreshInventory();
            }
        };
        ContentNode.AddNode(_stackDropDown);

        ContentNode.AddNode(new LayoutConfigurationNode());

        _debugCheckboxNode = new CheckboxNode
        {
            Size = new Vector2(300, 20),
            IsVisible = true,
            String = "Debug Mode",
            IsChecked = config.DebugEnabled,
            OnClick = isChecked => { config.DebugEnabled = isChecked; }
        };
        ContentNode.AddNode(_debugCheckboxNode);
    }

    private void RefreshInventory() => System.AddonInventoryWindow.ManualRefresh();
}