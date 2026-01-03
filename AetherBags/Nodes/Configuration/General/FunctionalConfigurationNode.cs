using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Nodes.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

internal sealed class FunctionalConfigurationNode : TabbedVerticalListNode
{
    private readonly CheckboxNode _hideDefaultBagsCheckboxNode;
    private readonly CheckboxNode _hideSaddlebagsCheckboxNode;
    private readonly CheckboxNode _hideRetainerbagsCheckboxNode;
    private readonly LabeledDropdownNode _stackDropDown;

    public FunctionalConfigurationNode()
    {
        GeneralSettings config = System.Config.General;

        ItemVerticalSpacing = 2;

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

        var showSaddleWithGameCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Auto-open Saddlebags with game Saddlebags",
            IsChecked = config.OpenSaddleBagsWithGameInventory,
            OnClick = isChecked =>
            {
                config.OpenSaddleBagsWithGameInventory = isChecked;
                _hideSaddlebagsCheckboxNode?.IsEnabled = isChecked;
            }
        };
        AddNode(showSaddleWithGameCheckBox);

        AddTab(1);
        _hideSaddlebagsCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Hide default Saddlebags",
            IsEnabled = config.OpenSaddleBagsWithGameInventory,
            IsChecked = config.HideGameSaddleBags,
            OnClick = isChecked =>
            {
                config.HideGameSaddleBags = isChecked;
            }
        };
        AddNode(_hideSaddlebagsCheckboxNode);
        SubtractTab(1);

        var showRetainerWithGameCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Auto-open Retainer bags with game Retainer bags",
            IsChecked = config.OpenRetainerWithGameInventory,
            OnClick = isChecked =>
            {
                config.OpenRetainerWithGameInventory = isChecked;
                _hideRetainerbagsCheckboxNode?.IsEnabled = isChecked;
            }
        };
        AddNode(showRetainerWithGameCheckBox);

        AddTab(1);
        _hideRetainerbagsCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Hide default Retainer bags",
            IsEnabled = config.OpenRetainerWithGameInventory,
            IsChecked = config.HideGameRetainer,
            OnClick = isChecked =>
            {
                config.HideGameRetainer = isChecked;
            }
        };
        AddNode(_hideRetainerbagsCheckboxNode);
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

        AddNode(new ResNode
        {
            Height = 6
        });

        var searchModeDropDown = new LabeledDropdownNode
        {
            Size = new Vector2(300, 20),
            LabelText = "Search Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            Options = Enum.GetNames(typeof(SearchMode)).ToList(),
            SelectedOption = config.SearchMode.ToString(),
            OnOptionSelected = selected =>
            {
                if (Enum.TryParse<SearchMode>(selected, out var parsed))
                {
                    config.SearchMode = parsed;
                    InventoryOrchestrator.RefreshAll(updateMaps: false);
                }
            }
        };
        AddNode(searchModeDropDown);

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
                    InventoryOrchestrator.RefreshAll(updateMaps: true);
                }
            }
        };
        AddNode(_stackDropDown);
    }
}