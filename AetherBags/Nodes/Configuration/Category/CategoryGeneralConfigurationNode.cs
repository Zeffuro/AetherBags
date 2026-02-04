using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using AetherBags.Nodes.Color;
using AetherBags.Nodes.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryGeneralConfigurationNode : TabbedVerticalListNode
{
    private readonly CheckboxNode _allaganToolsCheckbox;
    public CategoryGeneralConfigurationNode()
    {
        CategorySettings config = System.Config.Categories;

        ItemVerticalSpacing = 2;

        LabelTextNode titleNode = new LabelTextNode
        {
            Size = Size with { Y = 18 },
            String = "Category Configuration",
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(0),
        };
        AddNode(titleNode);

        AddTab(1);

        CheckboxNode categoriesEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Categories Enabled",
            IsChecked = config.CategoriesEnabled,
            OnClick = isChecked =>
            {
                config.CategoriesEnabled = isChecked;
                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };
        AddNode(categoriesEnabled);

        AddTab(1);

        CheckboxNode gameCategoriesEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "Game Categories",
            IsChecked = config.GameCategoriesEnabled,
            TextTooltip = "Use the game's built-in item categories (e.g., Arms, Tools, Armor).",
            OnClick = isChecked =>
            {
                config.GameCategoriesEnabled = isChecked;
                RefreshInventory();
            }
        };
        AddNode(gameCategoriesEnabled);

        CheckboxNode userCategoriesEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = "User Categories",
            IsChecked = config.UserCategoriesEnabled,
            TextTooltip = "Use your custom-defined categories.",
            OnClick = isChecked =>
            {
                config.UserCategoriesEnabled = isChecked;
                RefreshInventory();
            }
        };
        AddNode(userCategoriesEnabled);

        bool bisBuddyReady = System.IPC.BisBuddy?.IsReady ?? false;

        LabeledEnumDropdownNode<PluginFilterMode>? bbModeDropdown = new LabeledEnumDropdownNode<PluginFilterMode>
        {
            Size = new Vector2(500, 20),
            LabelText = "Filter Display Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            IsEnabled = config.BisBuddyEnabled && bisBuddyReady,
            Options = Enum.GetValues<PluginFilterMode>().ToList(),
            SelectedOption = config.BisBuddyMode,
            OnOptionSelected = selected =>
            {
                config.BisBuddyMode = selected;
                if (selected == PluginFilterMode.Categorize)
                    HighlightState.ClearFilter(HighlightSource.BiSBuddy);

                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };

        CheckboxNode bisBuddyEnabled = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = bisBuddyReady ? "BISBuddy" : "BISBuddy (Not Available)",
            IsChecked = config.BisBuddyEnabled,
            TextTooltip = "Allow BISBuddy to highlight items.",
            OnClick = isChecked =>
            {
                config.BisBuddyEnabled = isChecked;
                if (bbModeDropdown != null) bbModeDropdown.IsEnabled = isChecked;
                if (isChecked)
                    System.IPC.BisBuddy?.RefreshItems();
                else
                    HighlightState.ClearLabel(HighlightSource.BiSBuddy);
                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };
        AddNode(bisBuddyEnabled);
        AddNode(1, bbModeDropdown);

        bool allaganReady = System.IPC.AllaganTools?.IsReady ?? false;

        LabeledEnumDropdownNode<PluginFilterMode>? atModeDropdown = new LabeledEnumDropdownNode<PluginFilterMode>
        {
            Size = new Vector2(500, 20),
            LabelText = "Filter Display Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            IsEnabled = config.AllaganToolsCategoriesEnabled && allaganReady,
            Options = Enum.GetValues<PluginFilterMode>().ToList(),
            SelectedOption = config.AllaganToolsFilterMode,
            OnOptionSelected = selected =>
            {
                config.AllaganToolsFilterMode = selected;
                if (selected == PluginFilterMode.Categorize)
                {
                    HighlightState.ClearFilter(HighlightSource.AllaganTools);
                }

                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };

        _allaganToolsCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = allaganReady ?  "Allagan Tools Filters" : "Allagan Tools Filters (Not Available)",
            IsChecked = config.AllaganToolsCategoriesEnabled,
            IsEnabled = allaganReady,
            TextTooltip = allaganReady
                ? "Use search filters from Allagan Tools as categories. Items matching a filter will be grouped together."
                : "Allagan Tools is not installed or not initialized.",
            OnClick = isChecked =>
            {
                config.AllaganToolsCategoriesEnabled = isChecked;
                if (atModeDropdown != null) atModeDropdown.IsEnabled = isChecked;
                if (isChecked)
                    System.IPC?.AllaganTools?.RefreshFilters();
                else
                    HighlightState.ClearLabel(HighlightSource.AllaganTools);
                System.IPC?.RefreshExternalSources();
                RefreshInventory();
            }
        };
        AddNode(_allaganToolsCheckbox);

        AddNode(1, atModeDropdown);
        SubtractTab(1);
    }

    private void RefreshInventory() => InventoryOrchestrator.RefreshAll(updateMaps: true);
}