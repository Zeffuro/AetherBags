using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using KamiToolKit.Classes;

namespace AetherBags.Helpers;

public static unsafe class InventoryMoveHelper
{
    public static void MoveItemDirect(InventoryType sourceContainer, ushort sourceSlot, InventoryType destContainer, ushort destSlot)
    {
        IPluginLog.Get().DebugOnly($"[MoveItemDirect] {sourceContainer}@{sourceSlot} -> {destContainer}@{destSlot}");
        InventoryManager.Instance()->MoveItemSlot(sourceContainer, sourceSlot, destContainer, destSlot, true);
    }

    public static void HandleItemMovePayload(DragDropPayload source, DragDropPayload target)
    {
        if (source.Type == DragDropType.Inventory_Item || target.Type == DragDropType.Inventory_Item)
        {
            var sourceLocation = source.InventoryLocation;
            var targetLocation = target.InventoryLocation;
            if (!sourceLocation.IsValid || !targetLocation.IsValid || sourceLocation == targetLocation) return;
            MoveItemDirect(sourceLocation.Container, sourceLocation.Slot, targetLocation.Container, targetLocation.Slot);
            return;
        }

        uint srcContainer = (uint)source.Int1;
        uint dstContainer = (uint)target.Int1;

        uint srcSlot = (uint)source.Int2;
        uint dstSlot = (uint)target.Int2;

        short srcRi = source.ReferenceIndex;
        short dstRi = target.ReferenceIndex;

        if (srcContainer == 0 || dstContainer == 0) return;

        IPluginLog.Get().DebugOnly($"[MoveItemViaAgent] {srcContainer}:{srcSlot}:{srcRi} -> {dstContainer}:{dstSlot}:{dstRi}");

        using var atkValues = new RentedAtkValues(4);
        for (var i = 0; i < 4; i++)
        {
            atkValues[i].Type = AtkValueType.UInt;
        }

        atkValues[0].SetUInt(srcContainer);
        atkValues[1].SetUInt(srcSlot);
        atkValues[2].SetUInt(dstContainer);
        atkValues[3].SetUInt(dstSlot);

        var retVal = stackalloc AtkValue[1];

        RaptureAtkModule* atkModule = RaptureAtkModule.Instance();

        // This is a networked call, treat with greate care!
        atkModule->HandleItemMove(retVal, atkValues, 4);
    }
}
