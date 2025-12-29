using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

internal sealed class FunctionalConfigurationNode : TabbedVerticalListNode
{
    private readonly CheckboxNode _hideDefaultBagsCheckboxNode;
    private readonly LabeledDropdownNode _stackDropDown;

    public FunctionalConfigurationNode()
    {
        GeneralSettings config = System.Config.General;

        var titleNode = new CategoryTextNode
        {
            Height = 18,
            String = "Functional Configuration",
        };
        AddNode(titleNode);

        AddTab(1);

        var showWithGameCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Auto-open with game inventory",
            IsChecked = config.OpenWithGameInventory,
            OnClick = isChecked =>
            {
                config.OpenWithGameInventory = isChecked;
                _hideDefaultBagsCheckboxNode?.IsEnabled = isChecked;
            }
        };
        AddNode(showWithGameCheckBox);

        AddTab(1);
        _hideDefaultBagsCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Hide default inventory bags",
            IsEnabled = config.OpenWithGameInventory,
            IsChecked = config.HideGameInventory,
            OnClick = isChecked =>
            {
                config.HideGameInventory = isChecked;
            }
        };
        AddNode(_hideDefaultBagsCheckboxNode);
        SubtractTab(1);

        var linkItemCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Allow item linking with Shift+Click",
            IsChecked = config.LinkItemEnabled,
            OnClick = isChecked =>
            {
                config.LinkItemEnabled = isChecked;
            }
        };
        AddNode(linkItemCheckBox);

        _stackDropDown = new LabeledDropdownNode
        {
            Size = new Vector2(300, 20),
            IsEnabled = true,
            LabelText = "Stack Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
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
        AddNode(_stackDropDown);
    }
}