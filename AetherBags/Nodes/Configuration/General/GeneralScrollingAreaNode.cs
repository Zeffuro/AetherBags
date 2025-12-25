using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Nodes.Configuration.Layout;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

public sealed class GeneralScrollingAreaNode : ScrollingAreaNode<VerticalListNode>
{
    private readonly CheckboxNode _debugCheckboxNode = null!;
    private readonly LabeledDropdownNode _stackDropDown = null!;

    public GeneralScrollingAreaNode()
    {
        GeneralSettings config = System.Config.General;

        ContentNode.ItemSpacing = 32;

        _stackDropDown = new LabeledDropdownNode
        {
            Size = new Vector2(300, 20),
            IsEnabled = true,
            LabelText = "Stack Mode",
            Options = Enum.GetNames(typeof(InventoryStackMode)).ToList(),
            SelectedOption = config.StackMode.ToString(),
            OnOptionSelected = selected =>
            {
                if (Enum.TryParse<InventoryStackMode>(selected, out var parsed))
                {
                    config.StackMode = parsed;
                    System.AddonInventoryWindow.ManualInventoryRefresh();
                }
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

    private void RefreshInventory() => System.AddonInventoryWindow.ManualInventoryRefresh();
}