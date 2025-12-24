using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;

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

    private readonly Hook<MoveItemSlotDelegate>?  _moveItemSlotHook;

    public InventoryHooks()
    {
        try
        {
            _moveItemSlotHook = Services.GameInteropProvider.HookFromSignature<MoveItemSlotDelegate>(
                "E8 ?? ?? ?? ??  48 8B 03 66 FF C5",
                MoveItemSlotDetour);
            _moveItemSlotHook.Enable();

            Services.Logger.Debug("MoveItemSlot hooked successfully.");
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, "Failed to hook MoveItemSlot");
        }
    }

    private int MoveItemSlotDetour(
        InventoryManager* manager,
        InventoryType srcType,
        ushort srcSlot,
        InventoryType dstType,
        ushort dstSlot,
        bool unk)
    {
        InventoryItem* sourceItem = InventoryManager.Instance()->GetInventorySlot(srcType, srcSlot);
        InventoryItem* destItem = InventoryManager.Instance()->GetInventorySlot(dstType, dstSlot);

        Services.Logger.Info(
            $"[MoveItemSlot] Moving {srcType}@{srcSlot} ID:{sourceItem->ItemId} -> {dstType}@{dstSlot} ID:{destItem->ItemId} Unk:  {unk}");

        return _moveItemSlotHook!.Original(manager, srcType, srcSlot, dstType, dstSlot, unk);
    }

    public void Dispose()
    {
        _moveItemSlotHook?.Dispose();
    }
}