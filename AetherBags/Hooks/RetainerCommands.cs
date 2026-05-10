using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags.Hooks;

// Taken and adapted from AutoRetainer: https://github.com/PunishXIV/AutoRetainer/blob/26ce5ae794eaf64bd015050489f05aa369627e41/AutoRetainer/Internal/InventoryManagement/RetainerItemCommand.cs
public enum RetainerItemCommand : long
{
    RetrieveFromRetainer = 0,
    EntrustToRetainer = 1,
    EntrustQuantity = 4,
    HaveRetainerSellItem = 5,
}

public static unsafe class RetainerCommands
{
    private const string CommandSignature =
        "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 48 8B 5C 24 ?? 41 8B F0";

    // Re-derive if a game patch shifts AgentRetainer's layout.
    private const int AgentRetainerItemCommandModuleOffset = 0x28;

    private delegate void CommandDelegate(nint commandModule, uint slot, InventoryType inventoryType, uint a4, RetainerItemCommand command);
    private static CommandDelegate? _command;
    private static bool _resolved;

    private static CommandDelegate? GetCommand()
    {
        if (_resolved) return _command;
        _resolved = true;
        try
        {
            var ptr = Services.SigScanner.ScanText(CommandSignature);
            _command = Marshal.GetDelegateForFunctionPointer<CommandDelegate>(ptr);
            Services.Logger.Information($"[RetainerCommands] Resolved at 0x{ptr:X16}");
        }
        catch (Exception ex)
        {
            Services.Logger.Warning($"[RetainerCommands] Failed to resolve signature: {ex.Message}");
            _command = null;
        }
        return _command;
    }

    private static nint GetAgentItemCommandModule()
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer);
        if (agent is null || !agent->IsAgentActive()) return 0;
        return (nint)agent + AgentRetainerItemCommandModuleOffset;
    }

    public static bool TryEntrust(InventoryType playerContainer, uint playerSlot)
        => Dispatch("Entrust", playerContainer, playerSlot, RetainerItemCommand.EntrustToRetainer);

    public static bool TryRetrieve(InventoryType retainerContainer, uint retainerSlot)
        => Dispatch("Retrieve", retainerContainer, retainerSlot, RetainerItemCommand.RetrieveFromRetainer);

    private static bool Dispatch(string tag, InventoryType container, uint slot, RetainerItemCommand command)
    {
        var fn = GetCommand();
        if (fn is null)
        {
            Services.Logger.Warning($"[{tag}] sig unresolved, cannot dispatch.");
            return false;
        }
        var commandModule = GetAgentItemCommandModule();
        if (commandModule == 0)
        {
            Services.Logger.Warning($"[{tag}] AgentRetainer not active, cannot dispatch.");
            return false;
        }
        Services.Logger.Information($"[{tag}] {container}@{slot} via 0x{commandModule:X16} ({command})");
        fn(commandModule, slot, container, 0, command);
        return true;
    }
}
