using System;
using System.Collections.Generic;
using AetherBags.Addons;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Items;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.UI;

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

            case "debug":
                HandleDebug(subArgs);
                break;

            case "help":
            case "?":
                PrintHelp();
                break;

            case "list":
            case "items":
                ListInventoryItems(subArgs);
                break;

            default:
                PrintChat($"Unknown command: {subCommand}. Use '/ab help' for available commands.");
                break;
        }
    }

    private void HandleDebug(string args)
    {
        if (System.Config?.General?.DebugEnabled != true)
        {
            PrintChat("Debug commands are disabled. Enable 'General.DebugEnabled' in your config to use debug commands.");
            return;
        }

        var parts = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var subArgs = parts.Length > 1 ? parts[1] : string.Empty;

        switch (cmd)
        {
            case "":
            case "help":
                PrintChat("Debug commands:\n  /ab debug saddle       - Toggle saddlebag window\n  /ab debug retainer      - Toggle retainer window\n  /ab debug test [arg]    - Test IPC source (toggle/on/off/refresh/status)\n  /ab debug help          - Show this message");
                break;

            case "saddle":
                System.AddonSaddleBagWindow.Toggle();
                break;

            case "retainer":
                System.AddonRetainerWindow.Toggle();
                break;

            case "test":
                HandleTestSource(subArgs);
                break;

            case "refresh":
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                PrintChat("Inventory refreshed.");
                break;

            default:
                PrintChat($"Unknown debug command: {cmd}. Use '/ab debug help' for debug commands.");
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

    private void ListInventoryItems(string args)
    {
        if (!System.AddonInventoryWindow.IsOpen)
        {
            PrintChat("Open your inventory first.");
            return;
        }

        var categories = System.AddonInventoryWindow.GetVisibleCategories();
        if (categories == null || categories.Count == 0)
        {
            PrintChat("No items found.");
            return;
        }

        var subCmd = args.Trim().ToLowerInvariant();

        if (subCmd is "ids" or "copy")
        {
            var ids = new List<uint>();
            foreach (var category in categories)
            {
                foreach (var item in category.Items)
                {
                    if (!ids.Contains(item.Item.ItemId))
                        ids.Add(item.Item.ItemId);
                }
            }
            var idsString = string.Join(", ", ids);
            ImGui.SetClipboardText(idsString);
            PrintChat($"Copied {ids.Count} unique item IDs to clipboard.");
            return;
        }

        if (subCmd is "full" or "all")
        {
            var lines = new List<string>();
            foreach (var category in categories)
            {
                foreach (var item in category.Items)
                {
                    lines.Add($"{item.Item.ItemId}: {item.Name} x{item.ItemCount}");
                }
            }
            var fullText = string.Join("\n", lines);
            ImGui.SetClipboardText(fullText);
            PrintChat($"Copied {lines.Count} items to clipboard.");
            return;
        }

        int limit = 20;
        if (!string.IsNullOrWhiteSpace(args) && int.TryParse(args.Trim(), out int parsed))
        {
            limit = parsed;
        }

        PrintChat($"Listing up to {limit} items (use '/ab list ids' to copy all IDs):");

        int count = 0;
        foreach (var category in categories)
        {
            foreach (var item in category.Items)
            {
                if (count >= limit) break;
                PrintChat($"  {item.Item.ItemId}: {item.Name} x{item.ItemCount}");
                count++;
            }
            if (count >= limit) break;
        }

        PrintChat($"Shown {count} items. Total categories: {categories.Count}");
    }

    private void HandleTestSource(string args)
    {
        var testSource = System.IPC?.TestSource;
        if (testSource == null)
        {
            PrintChat("Test source not available.");
            return;
        }

        var subCmd = args.Trim().ToLowerInvariant();

        switch (subCmd)
        {
            case "":
            case "toggle":
                if (testSource.IsReady)
                {
                    testSource.Disable();
                    PrintChat("Test source disabled.");
                }
                else
                {
                    testSource.Enable();
                    PrintChat("Test source enabled. Search 'test' to find items.");
                }
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "on":
            case "enable":
                testSource.Enable();
                PrintChat("Test source enabled. Search 'test' to find items.");
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "off":
            case "disable":
                testSource.Disable();
                PrintChat("Test source disabled.");
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "refresh":
                testSource.Refresh();
                PrintChat("Test source refreshed.");
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "status":
                PrintChat($"Test source is {(testSource.IsReady ? "enabled" : "disabled")}.");
                break;

            default:
                PrintChat("Usage: /ab test [toggle|on|off|refresh|status]");
                break;
        }
    }

    private void PrintHelp()
    {
        var commands = new (string Command, string Description)[]
        {
            ("/ab", "Toggle inventory window"),
            ("/ab config", "Open configuration window"),
            ("/ab show", "Open inventory window"),
            ("/ab hide", "Close inventory window"),

            ("/ab search <term>", "Open inventory and search for items"),

            ("/ab list", "List items currently visible in inventory"),
            ("/ab list <n>", "List first N items"),
            ("/ab list ids", "Copy unique item IDs to clipboard"),
            ("/ab list full", "Copy full item list to clipboard"),

            ("/ab stats", "Show inventory statistics"),

            ("/ab import", "Import config from clipboard (hold Shift)"),
            ("/ab import-sk", "Import SortaKinda clipboard config"),
            ("/ab export", "Export config to clipboard"),
            ("/ab reset", "Reset config to default"),

            ("/ab help", "Show this help message"),
        };

        var lines = new List<string> { "AetherBags Commands:" };

        foreach (var (cmd, desc) in commands)
            lines.Add($"  {cmd}  - {desc}");

        lines.Add(System.Config?.General?.DebugEnabled == true
            ? "  /ab debug [saddle|retainer|test|refresh]  - Debug commands"
            : "  /ab debug  - Debug-only commands (enable in config)");

        PrintChat(string.Join("\n", lines));
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