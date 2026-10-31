using Dalamud.Plugin.Services;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AetherBags.Inventory.State;

public class SaddleBagState : InventoryStateBase
{
    public override InventorySourceType SourceType => HasPremiumSaddlebag
        ? InventorySourceType.AllSaddleBags
        :  InventorySourceType.SaddleBag;

    public override InventoryType[] Inventories => HasPremiumSaddlebag
        ? InventorySourceDefinitions.AllSaddleBags
        : InventorySourceDefinitions.SaddleBag;

    private static unsafe bool HasPremiumSaddlebag
    {
        get
        {
            if (!IClientState.Get().IsLoggedIn) return false;

            var playerState = PlayerState.Instance();
            return playerState != null && playerState->HasPremiumSaddlebag;
        }
    }
}