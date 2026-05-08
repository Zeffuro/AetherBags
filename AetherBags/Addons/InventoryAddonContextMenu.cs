using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Inventory.Context;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiToolKit.ContextMenu;
using KamiToolKit.Enums;

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

    public static void OpenMain(InventoryAddonBase parent)
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