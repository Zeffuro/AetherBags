using System.Numerics;
using AetherBags.Configuration;
using KamiToolKit.Nodes;
using KamiToolKit.Classes;

namespace AetherBags.Nodes.Configuration.Layout;

internal class LayoutConfigurationNode : TabbedVerticalListNode
{
    private readonly CompactLookaheadNode _compactLookaheadNode = null!;
    private readonly CheckboxNode _preferLargestFitCheckboxNode = null!;
    private readonly CheckboxNode _useStableInsertCheckboxNode = null!;

    public LayoutConfigurationNode()
    {
        GeneralSettings config = System.Config.General;

        var titleNode = new CategoryTextNode
        {
            Height = 18,
            String = "Layout Configuration",
        };
        AddNode(titleNode);

        AddTab(1);

        var showCategoryItemAmountCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Show Category Item Amount",
            IsChecked = config.ShowCategoryItemCount,
            OnClick = isChecked =>
            {
                config.ShowCategoryItemCount = isChecked;
                System.AddonInventoryWindow.ManualInventoryRefresh();
            }
        };
        AddNode(showCategoryItemAmountCheckboxNode);

        var compactPackingCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Use Compact Packing",
            IsChecked = config.CompactPackingEnabled,
            OnClick = isChecked =>
            {
                config.CompactPackingEnabled = isChecked;
                _preferLargestFitCheckboxNode.IsEnabled = isChecked;
                _useStableInsertCheckboxNode.IsEnabled = isChecked;
                _compactLookaheadNode.CompactLookahead.IsEnabled = isChecked;
                System.AddonInventoryWindow.ManualInventoryRefresh();
            }
        };
        AddNode(compactPackingCheckboxNode);

        AddTab(1);
        _preferLargestFitCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Prefer Largest Fit",
            IsEnabled = config.CompactPackingEnabled,
            IsChecked = config.CompactPreferLargestFit,
            OnClick = isChecked =>
            {
                config.CompactPreferLargestFit = isChecked;
                System.AddonInventoryWindow.ManualInventoryRefresh();
            }
        };
        AddNode(_preferLargestFitCheckboxNode);

        _useStableInsertCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Use Stable Insert",
            IsEnabled = config.CompactPackingEnabled,
            IsChecked = config.CompactStableInsert,
            OnClick = isChecked =>
            {
                config.CompactStableInsert = isChecked;
                System.AddonInventoryWindow.ManualInventoryRefresh();
            }
        };
        AddNode(_useStableInsertCheckboxNode);

        _compactLookaheadNode = new CompactLookaheadNode
        {
            Size = new Vector2(320, 20)
        };
        AddNode(_compactLookaheadNode);
    }
}