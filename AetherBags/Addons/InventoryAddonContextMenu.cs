using System;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.Classes.ContextMenu;

namespace AetherBags.Addons;

public static class InventoryAddonContextMenu
{
    private static ContextMenuItem Separator => new()
    {
        Name = "---------------------------",
        IsEnabled = false,
        OnClick = () => { }
    };

    public static void OpenMain(InventoryAddonBase parent)
    {
        if (parent?.ContextMenu == null || System.Config == null) return;

        var menu = parent.ContextMenu;
        menu.Clear();

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

                menu.AddItem(subMenu);
            }
        }

        menu.Open();
    }

    public static unsafe void Close()
    {
        var agent = AgentContext.Instance();
        if (agent != null)
        {
            agent->ClearMenu();
        }
    }
}