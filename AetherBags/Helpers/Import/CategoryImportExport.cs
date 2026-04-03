using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

namespace AetherBags.Helpers.Import;

public static class CategoryImportExport
{
    public static void ExportCategoryToClipboard(UserCategoryDefinition category)
    {
        var export = new CategoryExportData
        {
            Version = 1,
            Categories = [category],
        };

        var compressed = Util.SerializeCompressed(export);
        ImGui.SetClipboardText(compressed);

        Services.NotificationManager.AddNotification(
            new Notification { Content = $"Category '{category.Name}' exported to clipboard.", Type = NotificationType.Success }
        );
    }

    public static void ExportAllCategoriesToClipboard(List<UserCategoryDefinition> categories)
    {
        if (categories.Count == 0)
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = "No categories to export.", Type = NotificationType.Warning }
            );
            return;
        }

        var export = new CategoryExportData
        {
            Version = 1,
            Categories = categories.ToList(),
        };

        var compressed = Util.SerializeCompressed(export);
        ImGui.SetClipboardText(compressed);

        Services.NotificationManager.AddNotification(
            new Notification { Content = $"Exported {categories.Count} categories to clipboard.", Type = NotificationType.Success }
        );
    }

    public static CategoryExportData? TryParseClipboard()
    {
        string clipboard;
        try
        {
            clipboard = ImGui.GetClipboardText();
        }
        catch
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = "Failed to read clipboard.", Type = NotificationType.Error }
            );
            return null;
        }

        if (string.IsNullOrWhiteSpace(clipboard))
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = "Clipboard is empty.", Type = NotificationType.Warning }
            );
            return null;
        }

        var data = Util.DeserializeCompressed<CategoryExportData>(clipboard);
        if (data?.Categories is not { Count: > 0 })
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = "Clipboard data could not be parsed as category data.", Type = NotificationType.Error }
            );
            return null;
        }

        return data;
    }

    public static UserCategoryDefinition? ImportCategoryFromClipboard()
    {
        var data = TryParseClipboard();
        if (data is null) return null;

        if (data.Categories.Count != 1)
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = $"Expected 1 category, found {data.Categories.Count}. Use 'Import All' instead.", Type = NotificationType.Warning }
            );
            return null;
        }

        Services.NotificationManager.AddNotification(
            new Notification { Content = "Category imported from clipboard.", Type = NotificationType.Success }
        );

        return data.Categories[0];
    }

    public static bool ImportAllCategoriesFromClipboard(SystemConfiguration config, bool replaceExisting)
    {
        var data = TryParseClipboard();
        if (data is null) return false;

        var dest = config.Categories.UserCategories;

        if (replaceExisting)
        {
            dest.Clear();
            dest.AddRange(data.Categories);
        }
        else
        {
            var byId = dest
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var incoming in data.Categories)
            {
                if (!string.IsNullOrWhiteSpace(incoming.Id) && byId.TryGetValue(incoming.Id, out var existing))
                {
                    existing.Name = incoming.Name;
                    existing.Description = incoming.Description;
                    existing.Order = incoming.Order;
                    existing.Priority = incoming.Priority;
                    existing.Color = incoming.Color;
                    existing.Enabled = incoming.Enabled;
                    existing.Pinned = incoming.Pinned;
                    existing.Rules = incoming.Rules;
                }
                else
                {
                    dest.Add(incoming);
                    if (!string.IsNullOrWhiteSpace(incoming.Id))
                        byId[incoming.Id] = incoming;
                }
            }
        }

        config.Categories.UserCategoriesEnabled = true;
        Util.SaveConfig(config);

        Services.NotificationManager.AddNotification(
            new Notification { Content = $"Imported {data.Categories.Count} categories from clipboard.", Type = NotificationType.Success }
        );

        return true;
    }
}

public class CategoryExportData
{
    public string Format { get; set; } = "AetherBags_Category";
    public int Version { get; set; } = 1;
    public List<UserCategoryDefinition> Categories { get; set; } = new();
}

