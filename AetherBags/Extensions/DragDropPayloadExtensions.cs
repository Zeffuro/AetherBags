using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;

namespace AetherBags.Extensions;

public static class DragDropPayloadExtensions
{
    extension(DragDropPayload payload)
    {
        public bool IsValidInventoryPayload =>
            payload.Type is DragDropType.Inventory_Item
                or DragDropType.Inventory_Crystal
                or DragDropType.RemoteInventory_Item
                or DragDropType.Item
                or DragDropType.Crystal
                or DragDropType.EventItem;

        public bool IsSameBaseContainer(DragDropPayload otherPayload) {
            if (payload.InventoryLocation.Container.IsSameContainerGroup(otherPayload.InventoryLocation.Container))
            {
                return true;
            }

            return false;
        }

        public InventoryLocation InventoryLocation
        {
            get
            {
                if (!payload.IsValidInventoryPayload || payload.Int1 < 0 || payload.Int2 < 0 || payload.Int2 > ushort.MaxValue)
                    return InventoryLocation.Invalid;

                if (payload.Type == DragDropType.Inventory_Item)
                {
                    return new InventoryLocation((InventoryType)payload.Int1, (ushort)payload.Int2);
                }

                int containerId = payload.Int1;
                int uiSlot = payload.Int2;

                InventoryType sourceContainer = InventoryType.GetInventoryTypeFromContainerId(containerId);

                if (sourceContainer == InventoryType.Invalid)
                    return new InventoryLocation((InventoryType)containerId, (ushort)uiSlot);

                var (container, slot) = sourceContainer.GetRealItemLocation(uiSlot);
                return new InventoryLocation(container, slot);
            }
        }
    }

}
