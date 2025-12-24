using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Interop;

// Size 0x30 (48) matches the original struct
[StructLayout(LayoutKind.Explicit, Size = 48)]
public unsafe struct AtkDragDropInterfaceFixed
{
    // Offset 0 is the Virtual Table Pointer (void**)
    [FieldOffset(0)] public void** VirtualTable;

    // Map specific fields needed for Payload logic
    [FieldOffset(36)] public DragDropType DragDropType;
    [FieldOffset(40)] public short DragDropReferenceIndex;

    // Helper to get 'this' as a pointer
    private AtkDragDropInterfaceFixed* ThisPtr => (AtkDragDropInterfaceFixed*)Unsafe.AsPointer(ref this);

    // [VirtualFunction(1)]
    public void GetScreenPosition(float* screenX, float* screenY)
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, float*, float*, void>)VirtualTable[1];
        fnPtr(ThisPtr, screenX, screenY);
    }

    // [VirtualFunction(3)]
    public AtkComponentNode* GetComponentNode()
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, AtkComponentNode*>)VirtualTable[3];
        return fnPtr(ThisPtr);
    }

    // [VirtualFunction(5)]
    public void SetComponentNode(AtkComponentNode* node)
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, AtkComponentNode*, void>)VirtualTable[5];
        fnPtr(ThisPtr, node);
    }

    // [VirtualFunction(6)]
    public AtkResNode* GetActiveNode()
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, AtkResNode*>)VirtualTable[6];
        return fnPtr(ThisPtr);
    }

    // [VirtualFunction(8)]
    public AtkComponentBase* GetComponent()
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, AtkComponentBase*>)VirtualTable[8];
        return fnPtr(ThisPtr);
    }

    // [VirtualFunction(9)]
    public bool HandleMouseUpEvent(AtkEventData.AtkMouseData* mouseData)
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, AtkEventData.AtkMouseData*, byte>)VirtualTable[9];
        return fnPtr(ThisPtr, mouseData) != 0;
    }

    // [VirtualFunction(12)]
    public AtkDragDropPayloadContainer* GetPayloadContainer()
    {
        var fnPtr = (delegate* unmanaged<AtkDragDropInterfaceFixed*, AtkDragDropPayloadContainer*>)VirtualTable[12];
        return fnPtr(ThisPtr);
    }
}