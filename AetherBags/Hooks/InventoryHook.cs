using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Hooks;

/// <summary>
/// Manages hooks related to inventory operations.
/// </summary>
public sealed unsafe class InventoryHooks :  IDisposable
{
    private delegate int MoveItemSlotDelegate(
        InventoryManager* inventoryManager,
        InventoryType srcContainer,
        ushort srcSlot,
        InventoryType dstContainer,
        ushort dstSlot,
        bool unk);

    private delegate void HandleInventoryEventDelegate(AgentInterface* eventInterface, AtkValue* atkValue, int valueCount);

    private readonly Hook<MoveItemSlotDelegate>? _moveItemSlotHook;
    /*
    private readonly Hook<UIModule.Delegates.OpenInventory>? _openInventoryHook;
    private readonly Hook<HandleInventoryEventDelegate>? _handleInventoryEventHook;
    private readonly Hook<RaptureAtkModule.Delegates.OpenAddon>? _openAddonHook;
    */

    public InventoryHooks()
    {
        try
        {
            _moveItemSlotHook = Services.GameInteropProvider.HookFromSignature<MoveItemSlotDelegate>(
                "E8 ?? ?? ?? ?? 48 8B 03 66 FF C5",
                MoveItemSlotDetour);
            _moveItemSlotHook.Enable();

            Services.Logger.Debug("MoveItemSlot hooked successfully.");
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, "Failed to hook MoveItemSlot");
        }
        /*
        try
        {
            _openInventoryHook = Services.GameInteropProvider.HookFromAddress<UIModule.Delegates.OpenInventory>(
                UIModule.Instance()->VirtualTable->OpenInventory,
                OpenInventoryDetour);
            _openInventoryHook.Enable();

            Services.Logger.Debug("OpenInventory hooked successfully.");
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, "Failed to hook OpenInventory");
        }
        try
        {
            _handleInventoryEventHook = Services.GameInteropProvider.HookFromSignature<HandleInventoryEventDelegate>(
                "E8 ?? ?? ?? ?? 48 8B 74 24 ?? 33 C0 ?? ?? 89 43",
                HandleInventoryEventDetour);
            _handleInventoryEventHook.Enable();

            Services.Logger.Debug("HandleInventoryEvent hooked successfully.");
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, "Failed to hook HandleInventoryEvent");
        }
        try
        {
            _openAddonHook = Services.GameInteropProvider.HookFromAddress<RaptureAtkModule.Delegates.OpenAddon>(
                RaptureAtkModule.MemberFunctionPointers.OpenAddon,
                OpenAddonDetour);
            _openAddonHook.Enable();

            Services.Logger.Debug("OpenAddon hooked successfully.");
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, "Failed to hook MoveItemSlot");
        }
        */
    }

    private int MoveItemSlotDetour(InventoryManager* manager,
        InventoryType srcType,
        ushort srcSlot,
        InventoryType dstType,
        ushort dstSlot,
        bool unk)
    {
        InventoryItem* sourceItem = InventoryManager.Instance()->GetInventorySlot(srcType, srcSlot);
        InventoryItem* destItem = InventoryManager.Instance()->GetInventorySlot(dstType, dstSlot);

        Services.Logger.Debug($"[MoveItemSlot Hook] Moving {srcType}@{srcSlot} ID:{sourceItem->ItemId} -> {dstType}@{dstSlot} ID:{destItem->ItemId} Unk: {unk}");

        return _moveItemSlotHook!.Original(manager, srcType, srcSlot, dstType, dstSlot, unk);
    }

    /*
    private void OpenInventoryDetour(UIModule* uiModule, byte type)
    {
        Services.Logger.Debug($"[OpenInventory Hook] Opening inventory of type {type}");
        _openInventoryHook?.Original(uiModule, type);
    }

    private void HandleInventoryEventDetour(AgentInterface* eventInterface, AtkValue* atkValue, int valueCount)
    {
        for(int i = 0; i < valueCount; i++)
        {
            Services.Logger.Debug($"[HandleInventoryEvent Hook] AtkValue[{i}]: Type={atkValue[i].Type}, ToString: {atkValue[i].ToString()} ");
        }
        _handleInventoryEventHook?.Original(eventInterface, atkValue, valueCount);
    }

    private ushort OpenAddonDetour(RaptureAtkModule* thisPtr, uint addonNameId, uint valueCount, AtkValue* values, AtkModuleInterface.AtkEventInterface* eventInterface, ulong eventKind, ushort parentAddonId, int depthLayer)
    {
        for(int i = 0; i < valueCount; i++)
        {
            Services.Logger.Debug($"[OpenAddon Hook] AtkValue[{i}]: ToString: {values[i].ToString()} ");
        }
        return _openAddonHook!.Original(thisPtr, addonNameId, valueCount, values, eventInterface, eventKind, parentAddonId, depthLayer);
    }
*/

    public void Dispose()
    {
        _moveItemSlotHook?.Dispose();
        /*
        _openInventoryHook?.Dispose();
        _handleInventoryEventHook?.Dispose();
        _openAddonHook?.Dispose();
        */
    }
}