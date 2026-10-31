using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
using AetherBags.Inventory.Scanning;

namespace AetherBags.Inventory;

internal static unsafe class Experimental
{
    [StructLayout(LayoutKind.Explicit, Size = 324 * 8)]
    internal struct InventoryStringArray
    {
        public static InventoryStringArray* Instance()
            => (InventoryStringArray*)GetStringArray((int)StringArrayType.Inventory, 324);

        [FieldOffset(308 * 8)] public CStringPointer UsedSlotsText;
    }

    [StructLayout(LayoutKind.Explicit, Size = 142 * 8)]
    internal struct InventoryBuddyStringArray
    {
        public static InventoryBuddyStringArray* Instance()
            => (InventoryBuddyStringArray*)GetStringArray(87, 142);

        [FieldOffset(0)] public CStringPointer UsedSlotsText;
    }

    [StructLayout(LayoutKind.Explicit, Size = 167 * 8)]
    internal struct InventoryRetainerStringArray
    {
        public static InventoryRetainerStringArray* Instance()
            => (InventoryRetainerStringArray*)GetStringArray((int)StringArrayType.InventoryRetainer, 167);

        [FieldOffset(158 * 8)] public CStringPointer UsedSlotsText;
    }

    //TODO: Replace with FFXIVClientStructs version when released in mainline
    public static byte* GetUsedSlotsText(InventorySourceType source)
    {
        switch (source)
        {
            case InventorySourceType.MainBags:
                var inventory = InventoryStringArray.Instance();
                return inventory == null ? null : inventory->UsedSlotsText.Value;
            case InventorySourceType.SaddleBag:
            case InventorySourceType.AllSaddleBags:
                var buddy = InventoryBuddyStringArray.Instance();
                return buddy == null ? null : buddy->UsedSlotsText.Value;
            case InventorySourceType.Retainer:
                var retainer = InventoryRetainerStringArray.Instance();
                return retainer == null ? null : retainer->UsedSlotsText.Value;
            default:
                return null;
        }
    }

    private static CStringPointer* GetStringArray(int arrayIndex, int size)
    {
        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkArrayDataHolder == null) return null;
        var holder = stage->AtkArrayDataHolder;
        if (holder->StringArrays == null || arrayIndex >= holder->StringArrayCount) return null;
        var array = holder->StringArrays[arrayIndex];
        return array == null || array->Size < size ? null : array->StringArray;
    }

    //TODO: Use AtkDragDropInterface.CreateDragDropPreview when available in mainline.
    public static AtkComponentDragDrop* CreateDragDropPreview(AtkDragDropInterface* dragDropInterface)
    {
        var virtualTable = *(nint**)dragDropInterface;
        var createPreview = (delegate* unmanaged<AtkDragDropInterface*, AtkComponentDragDrop*>)virtualTable[15];
        return createPreview(dragDropInterface);
    }
}
