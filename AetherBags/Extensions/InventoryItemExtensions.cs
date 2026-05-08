using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace AetherBags.Extensions;

public static unsafe class InventoryItemExtensions {
    extension(ref InventoryItem item) {
        public uint IconId => item.GetIconId();
        public ReadOnlySeString Name => item.GetItemName();

        private uint GetIconId() {
            uint iconId = 0;

            if (item.GetEventItem() is { } eventItem) {
                iconId = eventItem.Icon;
            }
            else if (item.GetItem() is { } regularItem) {
                iconId = regularItem.Icon;

                if (item.IsHighQuality()) {
                    iconId += 1_000_000;
                }
            }

            return iconId;
        }

        private ReadOnlySeString GetItemName() {
            var itemId = item.GetItemId();
            var itemName = ItemUtil.GetItemName(itemId);

            return new Lumina.Text.SeStringBuilder()
                .PushColorType(ItemUtil.GetItemRarityColorType(itemId))
                .Append(itemName)
                .PopColorType()
                .ToReadOnlySeString();
        }

        private Item? GetItem() {
            var baseItemId = item.GetBaseItemId();

            if (ItemUtil.IsNormalItem(baseItemId) &&
                Services.DataManager.GetExcelSheet<Item>().TryGetRow(baseItemId, out var baseItem)) {
                return baseItem;
            }

            return null;
        }

        private EventItem? GetEventItem() {
            var baseItemId = item.GetBaseItemId();

            if (ItemUtil.IsEventItem(baseItemId) &&
                Services.DataManager.GetExcelSheet<EventItem>().TryGetRow(baseItemId, out var eventItem)) {
                return eventItem;
            }

            return null;
        }

        public void UseItem()
        {
            uint itemId = item.ItemId;
            InventoryType type = item.GetInventoryType() == InventoryType.KeyItems
                ? InventoryType.KeyItems
                : InventoryType.Invalid;

            if (InventoryManager.Instance()->GetInventoryItemCount(itemId, true) > 0)
                itemId += 1_000_000;

            if (!item.Container.IsMainInventory)
                return;

            AgentInventoryContext.Instance()->UseItem(itemId, type);
        }
    }
}