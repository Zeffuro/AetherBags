using AetherBags.Interop;
using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using Lumina.Text.ReadOnly;
using Lumina.Text;

namespace AetherBags.Extensions;

// TODO: Remove FixedInterface when CS is merged into Dalamud.
public static unsafe class DragDropPayloadExtensions
{
    public static DragDropPayload FromFixedInterface(AtkDragDropInterface* dragDropInterface)
    {
        // Cast to our manual fixed struct
        var fixedInterface = (AtkDragDropInterfaceFixed*)dragDropInterface;

        // Calls Index 12
        var payloadContainer = fixedInterface->GetPayloadContainer();

        return new DragDropPayload
        {
            Type = fixedInterface->DragDropType,
            ReferenceIndex = fixedInterface->DragDropReferenceIndex,
            Int1 = payloadContainer->Int1,
            Int2 = payloadContainer->Int2,
            Text = new ReadOnlySeString(payloadContainer->Text),
        };
    }

    public static void ToFixedInterface(this DragDropPayload payload, AtkDragDropInterface* dragDropInterface, bool writeToPayloadContainer = true)
    {
        var fixedInterface = (AtkDragDropInterfaceFixed*)dragDropInterface;

        fixedInterface->DragDropType = payload.Type;
        fixedInterface->DragDropReferenceIndex = payload.ReferenceIndex;

        if (writeToPayloadContainer)
        {
            // Calls Index 12
            var payloadContainer = fixedInterface->GetPayloadContainer();

            payloadContainer->Clear();
            payloadContainer->Int1 = payload.Int1;
            payloadContainer->Int2 = payload.Int2;

            if (payload.Text.IsEmpty)
            {
                payloadContainer->Text.Clear();
            }
            else
            {
                var stringBuilder = new SeStringBuilder().Append(payload.Text);
                payloadContainer->Text.SetString(stringBuilder.GetViewAsSpan());
            }
        }
    }

    extension(DragDropPayload payload)
    {
        public bool IsValidInventoryPayload =>
            payload.Type is DragDropType.Inventory_Item
                or DragDropType.Inventory_Crystal
                or DragDropType.RemoteInventory_Item
                or DragDropType.Item;

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

                // Retainers have special handling:  UI has 5 tabs × 35 slots, data has 7 pages × 25 slots
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