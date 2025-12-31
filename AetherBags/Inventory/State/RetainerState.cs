using AetherBags. Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AetherBags. Inventory.State;

public class RetainerState :  InventoryStateBase
{
    public override InventorySourceType SourceType => InventorySourceType.Retainer;
    public override InventoryType[] Inventories => InventorySourceDefinitions.Retainer;


    public static unsafe ulong CurrentRetainerId
    {
        get
        {
            var retainerManager = RetainerManager.Instance();
            if (retainerManager == null) return 0;

            return retainerManager->LastSelectedRetainerId;
        }
    }

    public static unsafe string CurrentRetainerName
    {
        get
        {
            var retainerManager = RetainerManager.Instance();
            if (retainerManager == null) return string.Empty;

            var retainer = retainerManager->GetActiveRetainer();
            if (retainer == null) return string.Empty;

            return retainer->NameString;
        }
    }

    public static unsafe bool IsRetainerActive
    {
        get
        {
            if (! Services.ClientState.IsLoggedIn) return false;

            var retainerManager = RetainerManager. Instance();
            if (retainerManager == null) return false;

            return retainerManager->LastSelectedRetainerId != 0;
        }
    }

    public static unsafe bool AreContainersLoaded
    {
        get
        {
            if (!IsRetainerActive) return false;

            var inventoryManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
            if (inventoryManager == null) return false;

            var container = inventoryManager->GetInventoryContainer(InventoryType.RetainerPage1);
            return container != null && container->Size > 0;
        }
    }

    public static bool CanMoveItems => AreContainersLoaded;
}