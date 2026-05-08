using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using KamiToolKit.Classes;

namespace AetherBags. Helpers;

public static unsafe class InventoryMoveHelper
{
    /* Unused method, here for reference in case we ever need to swap back to the other method
    public static void MoveItem(InventoryType sourceContainer, ushort sourceSlot, InventoryType destContainer, ushort destSlot)
    {
        Services.Logger.DebugOnly($"[MoveItem] {sourceContainer}@{sourceSlot} -> {destContainer}@{destSlot}");
        InventoryManager.Instance()->MoveItemSlot(sourceContainer, sourceSlot, destContainer, destSlot, true);
        Services.Framework.DelayTicks(3);
        Services.Framework.RunOnFrameworkThread(System.AddonInventoryWindow.ManualRefresh);
    }
    */

    public static void HandleItemMovePayload(DragDropPayload source, DragDropPayload target)
    {
        uint srcContainer = (uint)source.Int1;
        uint dstContainer = (uint)target.Int1;

        uint srcSlot = (uint)source.Int2;
        uint dstSlot = (uint)target.Int2;

        short srcRi = source.ReferenceIndex;
        short dstRi = target.ReferenceIndex;

        if (srcContainer == 0 || dstContainer == 0) return;

        Services.Logger.DebugOnly($"[MoveItemViaAgent] {srcContainer}:{srcSlot}:{srcRi} -> {dstContainer}:{dstSlot}:{dstRi}");

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