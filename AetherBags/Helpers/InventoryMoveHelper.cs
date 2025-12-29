using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags. Helpers;

public static unsafe class InventoryMoveHelper
{
    // Requires the visual UI slots instead of actual slots.
    public static void MoveItem(InventoryType sourceContainer, ushort sourceSlot, InventoryType destContainer, ushort destSlot)
    {
        Services.Logger.Debug($"[MoveItem] {sourceContainer}@{sourceSlot} -> {destContainer}@{destSlot}");
        InventoryManager.Instance()->MoveItemSlot(sourceContainer, sourceSlot, destContainer, destSlot, true);
        Services.Framework.DelayTicks(2);
        Services.Framework.RunOnFrameworkThread(System.AddonInventoryWindow.ManualInventoryRefresh);
    }

    /*
    private static void MoveItemViaAgent(InventoryType sourceInventory, ushort sourceSlot, InventoryType destInventory, ushort destSlot)
    {
        uint sourceContainerId = sourceInventory.AgentItemContainerId;
        uint destContainerId = destInventory.AgentItemContainerId;

        if (sourceContainerId == 0 || destContainerId == 0)
        {
            Services.Logger.Warning($"[MoveItemViaAgent] Invalid container IDs: src={sourceContainerId}, dst={destContainerId}");
            return;
        }

        Services.Logger.Debug($"[MoveItemViaAgent] {sourceContainerId}:{sourceSlot} -> {destContainerId}:{destSlot}");

        var atkValues = stackalloc AtkValue[4];
        for (var i = 0; i < 4; i++)
            atkValues[i]. Type = ValueType.UInt;

        atkValues[0].SetUInt(sourceContainerId);
        atkValues[1].SetUInt(sourceSlot);
        atkValues[2].SetUInt(destContainerId);
        atkValues[3].SetUInt(destSlot);

        var retVal = stackalloc AtkValue[1];

        RaptureAtkModule* atkModule = RaptureAtkModule.Instance();
        atkModule->HandleItemMove(retVal, atkValues, 4);
    }
    */
}