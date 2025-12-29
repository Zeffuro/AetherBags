using System.Linq;
using AetherBags.Configuration;
using AetherBags.Helpers.Import;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.ImGuiNotification;

namespace AetherBags.Helpers;

public abstract class ImportExportResetHelper {
    public static void TryImportConfigFromClipboard()
    {
        var clipboard = ImGui.GetClipboardText();
        var notification = new Notification { Content = "Configuration imported from clipboard.", Type = NotificationType.Success };

        if (!string.IsNullOrWhiteSpace(clipboard))
        {
            var imported = Util.DeserializeConfig(clipboard);
            if (imported != null)
            {
                System.Config = imported;
                Util.SaveConfig(System.Config);
                Services.Logger.Info("Configuration imported from clipboard.");
            }
            else
            {
                notification.Content = "Clipboard data was invalid or could not be imported.";
                notification.Type = NotificationType.Error;
                Services.Logger.Warning("Clipboard data was invalid or could not be imported.");
            }
        }
        else
        {
            notification.Content = "Clipboard is empty or invalid for import.";
            notification.Type = NotificationType.Warning;
            Services.Logger.Warning("Clipboard is empty or invalid for import.");
        }

        Services.NotificationManager.AddNotification(notification);
    }

    public static void TryExportConfigToClipboard(
        SystemConfiguration config)
    {
        var exportString = Util.SerializeConfig(config);
        ImGui.SetClipboardText(exportString);
        Services.NotificationManager.AddNotification(
            new Notification { Content = "Configuration exported to clipboard.", Type = NotificationType.Success }
        );
        Services.Logger.Info("Configuration exported to clipboard.");
    }

    public static void TryResetConfig()
    {
        System.Config = Util.ResetConfig();
        Util.SaveConfig(System.Config);

        Services.NotificationManager.AddNotification(
            new Notification { Content = "Configuration reset to default.", Type = NotificationType.Success }
        );
        Services.Logger.Info("Configuration reset to default.");
    }

    public static void TryImportSortaKindaFromClipboard(bool replaceExisting)
    {
        var notification = new Notification { Content = "SortaKinda categories imported.", Type = NotificationType.Success };

        if (!SortaKindaImportExport.TryImportFromClipboard(System.Config, replaceExisting, out var error))
        {
            notification.Content = error;
            notification.Type = NotificationType.Error;
            Services.Logger.Warning(error);
        }
        else
        {
            Util.SaveConfig(System.Config);
            Services.Logger.Info("SortaKinda categories imported from clipboard.");
        }

        Services.NotificationManager.AddNotification(notification);
    }

    public static void TryExportSortaKindaToClipboard()
    {
        SortaKindaImportExport.ExportToClipboard(System.Config);
        Services.NotificationManager.AddNotification(
            new Notification { Content = "SortaKinda JSON exported to clipboard.", Type = NotificationType.Success }
        );
        Services.Logger.Info("SortaKinda JSON exported to clipboard.");
    }
}
