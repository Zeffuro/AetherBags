using System.Collections.Generic;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.ContextMenu;

namespace AetherBags.Addons;

public static class InventoryAddonContextMenu
{
    private static ContextMenuItem Separator => new()
    {
        Name = "---------------------------",
        IsEnabled = false,
        OnClick = () => { }
    };

    // Temporary method, since OwnerAddon is never set for Overlays we need to set this or else the contextmenu will close immediately
    // TODO: Figure out how to do this in the ContextMenu itself or have Kami come up with a solution
    // The better solution is to use agentContextMenu->OpenContextMenu(false, true); in KTK when it's an overlay so it doesn't bind to the addon
    private static unsafe void SetAgentOwnerAddon(int addonId)
    {
        var agentContext = AgentContext.Instance();
        agentContext->OwnerAddon = (uint) addonId;
    }

    public static void OpenMain(InventoryAddonBase? parent)
    {
        if (parent?.ContextMenu == null || System.Config == null) return;

        var menu = parent.ContextMenu;
        menu.Clear();

        SetAgentOwnerAddon(parent.AddonId);

        bool hasActiveAtFilter = !string.IsNullOrEmpty(HighlightState.SelectedAllaganToolsFilterKey);
        string searchText = parent.GetSearchText();
        if (HighlightState.IsFilterActive || hasActiveAtFilter || !string.IsNullOrEmpty(searchText))
        {
            menu.AddItem("Clear All Filters", () =>
            {
                HighlightState.ClearAll();
                parent.SetSearchText(string.Empty);
                InventoryOrchestrator.RefreshAll(updateMaps: false);
            });
            menu.AddItem(Separator);
        }

        var currentMode = System.Config.General.SearchMode;
        string modeLabel = currentMode == SearchMode.Filter ? "Mode: Hide Non-Matches" : "Mode: Fade Non-Matches";
        menu.AddItem(modeLabel, () =>
        {
            System.Config.General.SearchMode = currentMode == SearchMode.Filter ? SearchMode.Highlight :  SearchMode.Filter;
            parent.ManualRefresh();
        });

        menu.AddItem(CreateUtilitiesSubMenu(parent));
        menu.AddItem(CreateVisibilitySubMenu());
        menu.AddItem(CreateStackingSubMenu());

        var allaganToolsSubMenu = CreateAllaganToolsFilterSubMenu();
        if (allaganToolsSubMenu != null)
        {
            menu.AddItem(allaganToolsSubMenu);
        }

        menu.Open();
    }

    private static ContextMenuSubItem CreateUtilitiesSubMenu(InventoryAddonBase parent)
    {
        var subMenu = new ContextMenuSubItem
        {
            Name = "Utilities",
            OnClick = () => { }
        };

        subMenu.AddItem("Refresh Inventory", () => InventoryOrchestrator.RefreshAll(updateMaps: true));
        subMenu.AddItem("Refresh External Sources", () =>
        {
            System.IPC.RefreshExternalSources();
            RefreshInventory();
        });
        subMenu.AddItem("Copy Visible Item IDs", () => CopyVisibleItemIds(parent));
        subMenu.AddItem("Copy Visible Item List", () => CopyVisibleItemList(parent));
        subMenu.AddItem("Print Inventory Stats", () => PrintInventoryStats(parent));

        return subMenu;
    }

    private static ContextMenuSubItem CreateVisibilitySubMenu()
    {
        var config = System.Config.Categories;
        var generalConfig = System.Config.General;
        var subMenu = new ContextMenuSubItem
        {
            Name = "Visibility",
            OnClick = () => { }
        };

        subMenu.AddItem(GetCheckedLabel("Categories", config.CategoriesEnabled), () =>
        {
            config.CategoriesEnabled = !config.CategoriesEnabled;
            System.IPC.RefreshExternalSources();
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("Game Categories", config.GameCategoriesEnabled), () =>
        {
            config.GameCategoriesEnabled = !config.GameCategoriesEnabled;
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("User Categories", config.UserCategoriesEnabled), () =>
        {
            config.UserCategoriesEnabled = !config.UserCategoriesEnabled;
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("Crystals", config.CrystalsEnabled), () =>
        {
            config.CrystalsEnabled = !config.CrystalsEnabled;
            System.IPC.RefreshExternalSources();
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("Key Items", config.KeyItemsEnabled), () =>
        {
            config.KeyItemsEnabled = !config.KeyItemsEnabled;
            System.IPC.RefreshExternalSources();
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("Recently Looted Section", generalConfig.ShowRecentlyLooted), () =>
        {
            generalConfig.ShowRecentlyLooted = !generalConfig.ShowRecentlyLooted;
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("Category Item Counts", generalConfig.ShowCategoryItemCount), () =>
        {
            generalConfig.ShowCategoryItemCount = !generalConfig.ShowCategoryItemCount;
            SaveConfig();
            RefreshInventory();
        });

        return subMenu;
    }

    private static ContextMenuSubItem CreateStackingSubMenu()
    {
        var config = System.Config.General;
        var subMenu = new ContextMenuSubItem
        {
            Name = "Stacking",
            OnClick = () => { }
        };

        subMenu.AddItem(GetCheckedLabel("Split Stacks (Game Default)", config.StackMode == InventoryStackMode.NaturalStacks), () =>
        {
            config.StackMode = InventoryStackMode.NaturalStacks;
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(GetCheckedLabel("Merge Stacks (By Item ID)", config.StackMode == InventoryStackMode.AggregateByItemId), () =>
        {
            config.StackMode = InventoryStackMode.AggregateByItemId;
            SaveConfig();
            RefreshInventory();
        });

        subMenu.AddItem(new ContextMenuItem
        {
            Name = GetCheckedLabel("Combine Unstackable Items", config.AggregateUnstackableItems),
            IsEnabled = config.StackMode == InventoryStackMode.AggregateByItemId,
            OnClick = () =>
            {
                config.AggregateUnstackableItems = !config.AggregateUnstackableItems;
                SaveConfig();
                RefreshInventory();
            }
        });

        return subMenu;
    }

    private static ContextMenuSubItem? CreateAllaganToolsFilterSubMenu()
    {
        if (System.IPC.AllaganTools is { IsReady: true } && System.Config.Categories.AllaganToolsCategoriesEnabled)
        {
            var atFilters = System.IPC.AllaganTools.GetSearchFilters();
            if (atFilters is { Count: > 0 })
            {
                var subMenu = new ContextMenuSubItem
                {
                    Name = "Allagan Tools Filters...",
                    OnClick = () => { }
                };

                foreach (var (key, name) in atFilters)
                {
                    var capturedKey = key;
                    bool isActive = HighlightState.SelectedAllaganToolsFilterKey == key;
                    subMenu.AddItem(isActive ?$"✓ {name}" : $" {name}", () =>
                    {
                        HighlightState.SelectedAllaganToolsFilterKey = isActive ? string.Empty : capturedKey;
                        InventoryOrchestrator.RefreshAll(updateMaps: false);
                    });
                }

                return subMenu;
            }
        }

        return null;
    }

    private static string GetCheckedLabel(string label, bool isChecked) => isChecked ? $"✓ {label}" : $" {label}";

    private static void CopyVisibleItemIds(InventoryAddonBase parent)
    {
        var categories = parent.GetVisibleCategories();
        if (categories == null || categories.Count == 0)
        {
            PrintChat("No visible items to copy.");
            return;
        }

        var seen = new HashSet<uint>();
        var ids = new List<uint>();
        foreach (var category in categories)
        {
            foreach (var item in category.Items)
            {
                if (seen.Add(item.Item.ItemId))
                    ids.Add(item.Item.ItemId);
            }
        }

        ImGui.SetClipboardText(string.Join(", ", ids));
        PrintChat($"Copied {ids.Count} unique visible item IDs to clipboard.");
    }

    private static void CopyVisibleItemList(InventoryAddonBase parent)
    {
        var categories = parent.GetVisibleCategories();
        if (categories == null || categories.Count == 0)
        {
            PrintChat("No visible items to copy.");
            return;
        }

        var lines = new List<string>();
        foreach (var category in categories)
        {
            foreach (var item in category.Items)
            {
                lines.Add($"{item.Item.ItemId}: {item.Name} x{item.ItemCount}");
            }
        }

        ImGui.SetClipboardText(string.Join("\n", lines));
        PrintChat($"Copied {lines.Count} visible items to clipboard.");
    }

    private static void PrintInventoryStats(InventoryAddonBase parent)
    {
        var stats = parent.GetStats();
        PrintChat($"{stats.UsedSlots}/{stats.TotalSlots} slots ({stats.UsagePercent:F0}%) | {stats.TotalItems} items | {stats.CategoryCount} categories");
    }

    private static void PrintChat(string message) => Services.ChatGui.Print(message, "AetherBags");

    private static void SaveConfig() => Util.SaveConfig(System.Config);

    private static void RefreshInventory() => InventoryOrchestrator.RefreshAll(updateMaps: true);

    public static unsafe void Close()
    {
        var agent = AgentContext.Instance();
        if (agent != null)
        {
            agent->ClearMenu();
        }
    }
}