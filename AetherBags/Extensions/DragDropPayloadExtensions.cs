
using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using Lumina.Text.ReadOnly;
using Lumina.Text;

namespace AetherBags.Extensions;

public static unsafe class DragDropPayloadExtensions
{
    extension(DragDropPayload payload)
    {
        public bool IsValidInventoryPayload =>
            payload.Type is DragDropType.Inventory_Item
                or DragDropType.Inventory_Crystal
                or DragDropType.RemoteInventory_Item
                or DragDropType.Item;

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
                if (!payload.IsValidInventoryPayload) return default;

                if (payload.Type == DragDropType.Inventory_Item)
                {
                    return new InventoryLocation((InventoryType)payload.Int1, (ushort)payload.Int2);
                }

                int containerId = payload.Int1;
                int uiSlot = payload.Int2;

                InventoryType sourceContainer = InventoryType.GetInventoryTypeFromContainerId(containerId);

                if (sourceContainer == 0)
                    return new InventoryLocation(0, 0);

                // Retainers have special handling: UI has 5 tabs × 35 slots, data has 7 pages × 25 slots
                if (sourceContainer.IsRetainer)
                {
                    // Container IDs 52-56 = UI tabs 0-4
                    int uiTabIndex = containerId - 52;

                    // Convert to global data index
                    int globalDataIndex = (uiTabIndex * 35) + uiSlot;

                    // Calculate data page and slot
                    int dataPage = globalDataIndex / 25;
                    int dataSlot = globalDataIndex % 25;

                    InventoryType dataContainer = InventoryType.RetainerPage1 + (uint)dataPage;

                    // Now resolve through sorter for the actual storage location
                    var (realContainer, realSlot) = dataContainer.GetRealItemLocation(dataSlot);
                    return new InventoryLocation(realContainer, realSlot);
                }

                // For non-retainers, use the standard resolution
                var (container, slot) = sourceContainer.GetRealItemLocation(uiSlot);
                return new InventoryLocation(container, slot);
            }
        }
    }

}