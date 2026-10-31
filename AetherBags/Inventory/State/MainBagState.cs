using AetherBags.Inventory.Context;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.State;

public class MainBagState :  InventoryStateBase
{
    public override InventorySourceType SourceType => InventorySourceType.MainBags;
    public override InventoryType[] Inventories => InventorySourceDefinitions.MainBags;

    protected override void OnPostScan()
    {
        InventoryContextState.RefreshMaps();
    }
}