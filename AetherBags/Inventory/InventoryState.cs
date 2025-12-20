using System.Collections.Generic;
using System.Linq;
using AetherBags.Currency;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace AetherBags.Inventory;

public static unsafe class InventoryState
{
    public static List<InventoryType> StandardInventories => [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.EquippedItems,
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryWaist,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.Currency,
        InventoryType.Crystals,
        InventoryType.ArmorySoulCrystal,
    ];

    public static bool Contains(this List<InventoryType> inventoryTypes, GameInventoryType type)
        => inventoryTypes.Contains((InventoryType)type);

    public static List<CategorizedInventory> GetInventoryItemCategories(string filterString = "", bool invert = false)
    {
        var items = string.IsNullOrEmpty(filterString)
            ? GetInventoryItems()
            : GetInventoryItems(filterString, invert);

        return items
            .GroupBy(GetItemUiCategoryKey)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var category = GetCategoryInfoForKey(g.Key, g.FirstOrDefault());
                var list = g.OrderByDescending(i => i.ItemCount).ToList();
                return new CategorizedInventory(category, list);
            })
            .ToList();
    }

    public static List<ItemInfo> GetInventoryItems() {
        List<InventoryType> inventories = [ InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 ];
        List<InventoryItem> items = [];

        foreach (var inventory in inventories) {
            var container = InventoryManager.Instance()->GetInventoryContainer(inventory);

            for (var index = 0; index < container->Size; ++index) {
                ref var item = ref container->Items[index];
                if (item.ItemId is 0) continue;

                items.Add(item);
            }
        }

        List<ItemInfo> itemInfos = [];
        itemInfos.AddRange(from itemGroups in items.GroupBy(item => item.ItemId)
            where itemGroups.Key is not 0
            let item = itemGroups.First()
            let itemCount = itemGroups.Sum(duplicateItem => duplicateItem.Quantity)
            select new ItemInfo {
                Item = item, ItemCount = itemCount,
            });

        return itemInfos;
    }

    public static List<ItemInfo> GetInventoryItems(string filterString, bool invert = false)
        => GetInventoryItems().Where(item => item.IsRegexMatch(filterString) != invert).ToList();

    private static uint GetItemUiCategoryKey(ItemInfo info)
        => info.UiCategory.RowId;

    private static CategoryInfo GetCategoryInfoForKey(uint key, ItemInfo? sample)
    {
        if (key == 0)
        {
            return new CategoryInfo
            {
                Name = "Misc",
                Description = "Uncategorized items",
            };
        }

        var uiCat = sample?.UiCategory.Value;
        var name = uiCat?.Name.ToString();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Category\\ {key}";

        return new CategoryInfo
        {
            Name = name,
        };
    }

    private static uint GetEmptyItemSlots() => InventoryManager.Instance()->GetEmptySlotsInBag();

    private static uint GetUsedItemSlots() => 140 - GetEmptyItemSlots();

    public static string GetEmptyItemSlotsString() => $"{GetUsedItemSlots()}/140";

    public static CurrencyInfo GetCurrencyInfo(uint itemId)
    {
        return new CurrencyInfo
        {
            Amount = InventoryManager.Instance()->GetInventoryItemCount(1),
            ItemId = itemId,
            IconId = Services.DataManager.GetExcelSheet<Item>().GetRow(itemId).Icon
        };
    }
}