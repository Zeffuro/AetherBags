using System.Collections.Generic;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace AetherBags.Inventory;

public class InventoryNotificationState
{
    private readonly Dictionary<InventoryNotificationType, InventoryNotificationInfo> notificationCache;

    public InventoryNotificationState()
    {
        var addonSheet = Services.DataManager.GetExcelSheet<Addon>();
        notificationCache = new Dictionary<InventoryNotificationType, InventoryNotificationInfo>
        {
            { InventoryNotificationType.Sell, new InventoryNotificationInfo(addonSheet.GetRow(530).Text, addonSheet.GetRow(3576).Text) },
            { InventoryNotificationType.Trade, new InventoryNotificationInfo(addonSheet.GetRow(531).Text, addonSheet.GetRow(3572).Text) },
            { InventoryNotificationType.Letters, new InventoryNotificationInfo(addonSheet.GetRow(549).Text, addonSheet.GetRow(3575).Text) },
            { InventoryNotificationType.Retainer, new InventoryNotificationInfo(addonSheet.GetRow(532).Text, addonSheet.GetRow(3573).Text) },
            { InventoryNotificationType.RetainerEquip, new InventoryNotificationInfo(addonSheet.GetRow(778).Text, addonSheet.GetRow(3585).Text) },
            { InventoryNotificationType.Equip, new InventoryNotificationInfo(addonSheet.GetRow(538).Text, addonSheet.GetRow(3577).Text) },
            { InventoryNotificationType.Armory, new InventoryNotificationInfo(addonSheet.GetRow(775).Text, addonSheet.GetRow(3578).Text) },
            { InventoryNotificationType.Markets, new InventoryNotificationInfo(addonSheet.GetRow(548).Text, addonSheet.GetRow(3574).Text) },
            { InventoryNotificationType.Trade2, new InventoryNotificationInfo(addonSheet.GetRow(531).Text, addonSheet.GetRow(3572).Text) },
            { InventoryNotificationType.CompanyChest, new InventoryNotificationInfo(addonSheet.GetRow(776).Text, addonSheet.GetRow(3579).Text) },
            { InventoryNotificationType.Exterior, new InventoryNotificationInfo(addonSheet.GetRow(3583).Text, addonSheet.GetRow(3581).Text) },
            { InventoryNotificationType.Interior, new InventoryNotificationInfo(addonSheet.GetRow(3584).Text, addonSheet.GetRow(3582).Text) },
            { InventoryNotificationType.Layout, new InventoryNotificationInfo(addonSheet.GetRow(6237).Text, addonSheet.GetRow(3580).Text) },
            { InventoryNotificationType.Plant, new InventoryNotificationInfo(addonSheet.GetRow(6416).Text, addonSheet.GetRow(6418).Text) },
            { InventoryNotificationType.Fertilize, new InventoryNotificationInfo(addonSheet.GetRow(6417).Text, addonSheet.GetRow(6419).Text) },
            { InventoryNotificationType.Transmutation, new InventoryNotificationInfo(addonSheet.GetRow(3911).Text, addonSheet.GetRow(3901).Text) },
            { InventoryNotificationType.Reward, new InventoryNotificationInfo(addonSheet.GetRow(6503).Text, addonSheet.GetRow(6502).Text) },
            { InventoryNotificationType.Feed, new InventoryNotificationInfo(addonSheet.GetRow(6519).Text, addonSheet.GetRow(6518).Text) },
            { InventoryNotificationType.Charge, new InventoryNotificationInfo(addonSheet.GetRow(8638).Text, addonSheet.GetRow(8637).Text) },
            { InventoryNotificationType.Convert, new InventoryNotificationInfo(addonSheet.GetRow(8647).Text, addonSheet.GetRow(8646).Text) },
            { InventoryNotificationType.Covering, new InventoryNotificationInfo(addonSheet.GetRow(9029).Text, addonSheet.GetRow(9028).Text) },
            { InventoryNotificationType.Feed2, new InventoryNotificationInfo(addonSheet.GetRow(9041).Text, addonSheet.GetRow(9040).Text) },
            { InventoryNotificationType.Manual, new InventoryNotificationInfo(addonSheet.GetRow(9044).Text, addonSheet.GetRow(9043).Text) },
            { InventoryNotificationType.Chocobo, new InventoryNotificationInfo(addonSheet.GetRow(9073).Text, addonSheet.GetRow(9072).Text) },
            { InventoryNotificationType.Outfit, new InventoryNotificationInfo(addonSheet.GetRow(6578).Text, addonSheet.GetRow(6579).Text) },
            { InventoryNotificationType.Outfit2, new InventoryNotificationInfo(addonSheet.GetRow(6578).Text, addonSheet.GetRow(6579).Text) },
            { InventoryNotificationType.Plant2, new InventoryNotificationInfo(addonSheet.GetRow(6416).Text, addonSheet.GetRow(6418).Text) },
            { InventoryNotificationType.Aquarium, new InventoryNotificationInfo(addonSheet.GetRow(6808).Text, addonSheet.GetRow(6807).Text) },
            { InventoryNotificationType.SaddleBag, new InventoryNotificationInfo(addonSheet.GetRow(891).Text, addonSheet.GetRow(892).Text) },
            { InventoryNotificationType.Donate, new InventoryNotificationInfo(addonSheet.GetRow(11595).Text, addonSheet.GetRow(11596).Text) },
            { InventoryNotificationType.Trade3, new InventoryNotificationInfo(addonSheet.GetRow(531).Text, addonSheet.GetRow(3572).Text) },
            { InventoryNotificationType.Trade4, new InventoryNotificationInfo(addonSheet.GetRow(531).Text, addonSheet.GetRow(3572).Text) },
            { InventoryNotificationType.Exterior2, new InventoryNotificationInfo(addonSheet.GetRow(3583).Text, addonSheet.GetRow(3581).Text) },
            { InventoryNotificationType.Interior2, new InventoryNotificationInfo(addonSheet.GetRow(6237).Text, addonSheet.GetRow(3580).Text) },
        };
    }

    public InventoryNotificationInfo? GetNotificationInfo(uint openTitleId)
    {
        return notificationCache.GetValueOrDefault((InventoryNotificationType)openTitleId);
    }

}
public record InventoryNotificationInfo(ReadOnlySeString Title, ReadOnlySeString Message);

public enum InventoryNotificationType : uint
{
    None = 0,
    Sell = 1,
    Trade = 2,
    Letters = 3,
    Retainer = 4,
    RetainerEquip = 5,
    Equip = 6,
    Armory = 7,
    Markets = 8,
    Trade2 = 9,
    CompanyChest = 10,
    Exterior = 11,
    Interior = 12,
    Layout = 13,
    Plant = 14,
    Fertilize = 15,
    Transmutation = 16,
    Reward = 17,
    Feed = 18,
    Charge = 19,
    Convert = 20,
    Covering = 21,
    Feed2 = 22,
    Manual = 23,
    Chocobo = 24,
    Outfit = 25,
    Outfit2 = 26,
    Plant2 = 27,
    Aquarium = 28,
    SaddleBag = 29,
    Donate = 30,
    Trade3 = 31,
    Trade4 = 32,
    Exterior2 = 33,
    Interior2 = 34
}