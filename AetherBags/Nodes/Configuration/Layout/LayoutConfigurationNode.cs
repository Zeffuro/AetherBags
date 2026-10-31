using System;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using KamiToolKit.Nodes;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Layout;

namespace AetherBags.Nodes.Configuration.Layout;

internal class LayoutConfigurationNode : TabbedVerticalListNode
{
    private readonly CompactLookaheadNode _compactLookaheadNode = null!;
    private readonly CheckboxNode _preferLargestFitCheckboxNode = null!;
    private readonly CheckboxNode _useStableInsertCheckboxNode = null!;

    private readonly Action? _onLayoutChanged;
    private readonly InventoryLayoutConfigurationNode _stability;
    private readonly WindowSizingConfigurationNode _windowSizing;
    private readonly TabbedVerticalListNode _sharedSettings;

    public LayoutConfigurationNode(Action? onLayoutChanged = null)
    {
        _onLayoutChanged = onLayoutChanged;
        Width = 560;
        FitWidth = true;

        GeneralSettings config = System.Config.General;

        _stability = new InventoryLayoutConfigurationNode(RefreshLayout);
        AddNode(_stability);

        AddNode(new ResNode { Height = 6 });

        _windowSizing = new WindowSizingConfigurationNode(RefreshLayout);
        AddNode(_windowSizing);

        _sharedSettings = new TabbedVerticalListNode { FitWidth = true };
        AddNode(_sharedSettings);
        _sharedSettings.AddNode(new ResNode
        {
            Height = 6
        });

        var showRecentlyLootedCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Show Recently Looted Section",
            IsChecked = config.ShowRecentlyLooted,
            OnClick = isChecked =>
            {
                config.ShowRecentlyLooted = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        _sharedSettings.AddNode(showRecentlyLootedCheckboxNode);

        var recentlyLootedColorHandler = new Action<Vector4>(newColor =>
        {
            config.RecentlyLootedHighlightColor = newColor;
            InventoryOrchestrator.RefreshAll(updateMaps: true);
        });

        var recentlyLootedColorNode = new ColorInputRow
        {
            Label = "Highlight Color",
            Size = new Vector2(300, 24),
            CurrentColor = config.RecentlyLootedHighlightColor,
            DefaultColor = new GeneralSettings().RecentlyLootedHighlightColor,
            OnColorConfirmed = recentlyLootedColorHandler,
            OnColorChange = recentlyLootedColorHandler,
            OnColorCanceled = recentlyLootedColorHandler,
            OnColorPreviewed = recentlyLootedColorHandler,
            IsEnabled = config.HighlightRecentlyLootedItems
        };

        var highlightRecentlyLootedCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Highlight Recently Looted Items",
            IsChecked = config.HighlightRecentlyLootedItems,
            OnClick = isChecked =>
            {
                config.HighlightRecentlyLootedItems = isChecked;
                recentlyLootedColorNode.IsEnabled = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        _sharedSettings.AddNode(highlightRecentlyLootedCheckboxNode);

        _sharedSettings.AddTab(1);
        _sharedSettings.AddNode(recentlyLootedColorNode);
        _sharedSettings.SubtractTab(1);

        var showCategoryItemAmountCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Show Category Item Amount",
            IsChecked = config.ShowCategoryItemCount,
            OnClick = isChecked =>
            {
                config.ShowCategoryItemCount = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        _sharedSettings.AddNode(showCategoryItemAmountCheckboxNode);

        var compactPackingCheckboxNode = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = "Use Compact Packing",
            TextTooltip = "Reorders categories to fill empty space at the end of rows.\nDisable to keep your configured order exactly.",
            IsChecked = config.CompactPackingEnabled,
            OnClick = isChecked =>
            {
                config.CompactPackingEnabled = isChecked;
                _preferLargestFitCheckboxNode.IsEnabled = isChecked;
                _useStableInsertCheckboxNode.IsEnabled = isChecked;
                _compactLookaheadNode.CompactLookahead.IsEnabled = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        _sharedSettings.AddNode(compactPackingCheckboxNode);

        _sharedSettings.AddTab(1);
        _preferLargestFitCheckboxNode = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = "Prefer Largest Fit",
            TextTooltip = "Fills row gaps with the widest category that fits, for the tightest layout.\nDisable to fill with the next available category and stay closer to your configured order.",
            IsEnabled = config.CompactPackingEnabled,
            IsChecked = config.CompactPreferLargestFit,
            OnClick = isChecked =>
            {
                config.CompactPreferLargestFit = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        _sharedSettings.AddNode(_preferLargestFitCheckboxNode);

        _useStableInsertCheckboxNode = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = "Use Stable Insert",
            TextTooltip = "Keeps category positions consistent when packing reshuffles them.\nDisable only if you don't mind nearby categories shifting between refreshes.",
            IsEnabled = config.CompactPackingEnabled,
            IsChecked = config.CompactStableInsert,
            OnClick = isChecked =>
            {
                config.CompactStableInsert = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        _sharedSettings.AddNode(_useStableInsertCheckboxNode);

        _compactLookaheadNode = new CompactLookaheadNode
        {
            Size = new Vector2(320, 28)
        };
        _sharedSettings.AddNode(_compactLookaheadNode);
    }

    internal void SelectSource(int source)
    {
        _stability.SelectSource(source);
        _windowSizing.SelectSource(source);
        _sharedSettings.IsVisible = source == 0;
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        using var visibility = new LayoutVisibilityScope(this);
        RecalculateLayout();
        _onLayoutChanged?.Invoke();
    }
}
