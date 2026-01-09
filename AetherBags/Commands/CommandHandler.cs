using System;
using System.Collections.Generic;
using AetherBags.Addons;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Items;
using Dalamud.Game.Command;

namespace AetherBags.Commands;

public class CommandHandler : IDisposable
{
    private const string MainCommand = "/aetherbags";
    private const string ShortCommand = "/ab";
    private const string HelpDescription = "Opens your inventory. Use '/ab help' for more options.";

    public CommandHandler()
    {
        Services.CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            DisplayOrder = 1,
            ShowInHelp = true,
            HelpMessage = HelpDescription
        });

        Services.CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            DisplayOrder = 2,
            ShowInHelp = true,
            HelpMessage = HelpDescription
        });
    }

    private void OnCommand(string command, string args)
    {
        var argsParts = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subCommand = argsParts.Length > 0 ? argsParts[0].ToLowerInvariant() : string.Empty;
        var subArgs = argsParts.Length > 1 ? argsParts[1] : string.Empty;

        switch (subCommand)
        {
            case "":
            case "toggle":
                System.AddonInventoryWindow.Toggle();
                break;

            case "config":
            case "settings":
                System.AddonConfigurationWindow.Toggle();
                break;

            case "show":
            case "open":
                System.AddonInventoryWindow.Open();
                break;

            case "hide":
            case "close":
                System.AddonInventoryWindow.Close();
                break;

            case "refresh":
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                PrintChat("Inventory refreshed.");
                break;

            case "search":
                HandleSearch(subArgs);
                break;

            case "import-sk":
                ImportExportResetHelper.TryImportSortaKindaFromClipboard(true);
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "export":
                HandleExport();
                break;

            case "import":
                ImportExportResetHelper.TryImportConfigFromClipboard();
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "reset":
                ImportExportResetHelper.TryResetConfig();
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "count":
            case "stats":
                PrintInventoryStats();
                break;

            case "saddle":
                System.AddonSaddleBagWindow.Toggle();
                break;

            case "retainer":
                System.AddonRetainerWindow.Toggle();
                break;

            case "help":
            case "?":
                PrintHelp();
                break;

            default:
                PrintChat($"Unknown command: {subCommand}. Use '/ab help' for available commands.");
                break;
        }
    }

    private void PrintInventoryStats()
    {
        var openWindows = new List<(string Name, IInventoryWindow Window)>();

        if (System.AddonInventoryWindow.IsOpen)
            openWindows.Add(("Main", System.AddonInventoryWindow));
        if (System.AddonSaddleBagWindow.IsOpen)
            openWindows.Add(("Saddle", System.AddonSaddleBagWindow));
        if (System.AddonRetainerWindow.IsOpen)
            openWindows.Add(("Retainer", System.AddonRetainerWindow));

        if (openWindows.Count == 0)
        {
            PrintChat("No inventory windows are open. Open an inventory to see stats.");
            return;
        }

        foreach (var (name, window) in openWindows)
        {
            var stats = window.GetStats();
            PrintChat($"[{name}] {stats.UsedSlots}/{stats.TotalSlots} slots ({stats.UsagePercent:F0}%) | {stats.TotalItems} items | {stats.CategoryCount} categories");
        }

        if (openWindows.Count > 1)
        {
            var combined = new InventoryStats();
            foreach (var (_, window) in openWindows)
            {
                combined += window.GetStats();
            }
            PrintChat($"[Total] {combined.UsedSlots}/{combined.TotalSlots} slots ({combined.UsagePercent:F0}%) | {combined.TotalItems} items | {combined.CategoryCount} categories");
        }
    }

    private void HandleSearch(string searchTerm)
    {
        if (!System.AddonInventoryWindow.IsOpen)
        {
            System.AddonInventoryWindow.Open();
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            System.AddonInventoryWindow.SetSearchText(searchTerm);
        }

        PrintChat($"Searching for: {searchTerm}");
    }

    private void HandleExport()
    {
        ImportExportResetHelper.TryExportConfigToClipboard(System.Config);
    }

    private void PrintHelp()
    {
        var helpText = @"AetherBags Commands:
  /ab              - Toggle inventory window
  /ab config       - Toggle configuration window
  /ab show         - Open inventory window
  /ab hide         - Close inventory window
  /ab refresh      - Force refresh inventory
  /ab search <term> - Open and search for items
  /ab import       - Import config from clipboard (hold Shift)
  /ab import-sk    - Import from SortaKinda clipboard
  /ab export       - Export config to clipboard
  /ab reset        - Reset config to default
  /ab help         - Show this help message";

        PrintChat(helpText);
    }

    private static void PrintChat(string message)
    {
        Services.ChatGui.Print(message, "AetherBags");
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(MainCommand);
        Services.CommandManager.RemoveHandler(ShortCommand);
    }
}