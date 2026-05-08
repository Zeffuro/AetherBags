using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Extensions;


public static unsafe class AtkStageExtensions
{
    extension(ref AtkStage stage)
    {
        public void ShowInventoryItemTooltip(AtkResNode* node, InventoryType container, short slot)
        {
            var tooltipArgs = stackalloc AtkTooltipManager.AtkTooltipArgs[1];
            tooltipArgs->Ctor();
            tooltipArgs->ItemArgs.Kind = DetailKind.InventoryItem;
            tooltipArgs->ItemArgs.InventoryType = container;
            tooltipArgs->ItemArgs.Slot = slot;
            tooltipArgs->ItemArgs.BuyQuantity = -1;
            tooltipArgs->ItemArgs.Flag1 = 0;

            var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(node);
            if (addon is null) return;

            stage.TooltipManager.ShowTooltip(
                AtkTooltipType.Item,
                addon->Id,
                node,
                tooltipArgs
            );
        }
    }
}