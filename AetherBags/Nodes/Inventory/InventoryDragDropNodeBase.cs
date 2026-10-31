using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

public unsafe class InventoryDragDropNodeBase : DragDropNode
{
    private readonly AtkComponentBase.Delegates.Deinitialize iconDeinitialize;
    private readonly delegate* unmanaged<AtkComponentBase*, void> originalIconDeinitialize;

    public InventoryDragDropNodeBase()
    {
        originalIconDeinitialize = IconNode.ComponentBase->VirtualTable->Deinitialize;
        iconDeinitialize = OnIconDeinitialize;
        IconNode.ComponentBase->VirtualTable->Deinitialize =
            (delegate* unmanaged<AtkComponentBase*, void>)Marshal.GetFunctionPointerForDelegate(iconDeinitialize);
    }

    private void OnIconDeinitialize(AtkComponentBase* component)
    {
        component->VirtualTable->Deinitialize = originalIconDeinitialize;
        InventoryIconNode.ClearDisposedImageReferences(IconNode, component);
        originalIconDeinitialize(component);
    }
}
