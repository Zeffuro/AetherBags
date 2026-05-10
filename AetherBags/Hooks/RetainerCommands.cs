using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags.Hooks;

// Taken and adapted from AutoRetainer: https://github.com/PunishXIV/AutoRetainer/blob/26ce5ae794eaf64bd015050489f05aa369627e41/AutoRetainer/Internal/InventoryManagement/RetainerItemCommand.cs
public enum RetainerContextEvent : long
{
    Retrieve = 0,
    Entrust = 1,
    MarketBoardSell = 2,
    RetrieveQuantity = 3,
    EntrustQuantity = 4,
    HaveRetainerSellItem = 5,
}

public static unsafe class RetainerCommands
{
    private const int InventoryContextEventOffset = 0x28;

    public static bool TryEntrust(InventoryType playerContainer, uint playerSlot)
        => Dispatch(playerContainer, playerSlot, RetainerContextEvent.Entrust);

    public static bool TryRetrieve(InventoryType retainerContainer, uint retainerSlot)
        => Dispatch(retainerContainer, retainerSlot, RetainerContextEvent.Retrieve);

    public static bool TryEntrustQuantity(InventoryType playerContainer, uint playerSlot)
        => Dispatch(playerContainer, playerSlot, RetainerContextEvent.EntrustQuantity);

    public static bool TryRetrieveQuantity(InventoryType retainerContainer, uint retainerSlot)
        => Dispatch(retainerContainer, retainerSlot, RetainerContextEvent.RetrieveQuantity);

    public static bool TryMarketBoardSell(InventoryType retainerContainer, uint retainerSlot)
        => Dispatch(retainerContainer, retainerSlot, RetainerContextEvent.MarketBoardSell);

    private static bool Dispatch(InventoryType container, uint slot, RetainerContextEvent command)
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        if (agent == null || !agent->IsAgentActive()) return false;

        void* contextEventPtr = (byte*)agent + InventoryContextEventOffset;

        void** vtable = *(void***)contextEventPtr;

        var sendCommand = (delegate* unmanaged<void*, uint, InventoryType, ulong, RetainerContextEvent, void>)vtable[0];

        Services.Logger.DebugOnly($"Dispatching {container}@{slot} via ({command})");
        sendCommand(contextEventPtr, slot, container, 0, command);

        return true;
    }
}
