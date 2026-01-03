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
                System.IPC.BisBuddy?.RequestUpdate();
                RefreshInventory();
            }
        };
        AddNode(bisBuddyEnabled);

        bool allaganReady = System.IPC.AllaganTools?.IsReady ?? false;

        LabeledDropdownNode? atModeDropdown = new LabeledDropdownNode
        {
            Size = new Vector2(300, 20),
            LabelText = "Filter Display Mode",
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            IsEnabled = config.AllaganToolsCategoriesEnabled && allaganReady,
            Options = Enum.GetNames(typeof(AllaganToolsFilterMode)).ToList(),
            SelectedOption = config.AllaganToolsMode.ToString(),
            OnOptionSelected = selected =>
            {
                if (Enum.TryParse<AllaganToolsFilterMode>(selected, out var parsed))
                {
                    config.AllaganToolsMode = parsed;
                    if (parsed == AllaganToolsFilterMode.Categorize)
                        HighlightState.ClearFilter(HighlightSource.AllaganTools);

                    RefreshInventory();
                }
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
                if (isChecked) System.IPC?.AllaganTools?.RefreshFilters();
                RefreshInventory();
            }
        };
        AddNode(_allaganToolsCheckbox);

        AddTab(1);
        AddNode(atModeDropdown);
        SubtractTab(1);
    }

    private void RefreshInventory() => InventoryOrchestrator.RefreshAll(updateMaps: true);
}