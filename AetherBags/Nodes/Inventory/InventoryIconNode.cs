using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

public unsafe class InventoryIconNode : IconNode
{
    protected override void OnDeinitialize(AtkComponentBase* component)
    {
        ClearDisposedImageReferences(this, component);
        base.OnDeinitialize(component);
    }

    internal static void ClearDisposedImageReferences(IconNode icon, AtkComponentBase* component)
    {
        if (icon.IconImage.Node is not null) return;

        // KTK's managed teardown already released these; native-first teardown still owns them.
        var nativeIcon = (AtkComponentIcon*)component;
        nativeIcon->Texture = null;
        nativeIcon->IconImage = null;
    }
}
