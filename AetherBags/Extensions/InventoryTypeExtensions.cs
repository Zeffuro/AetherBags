using System;
using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace AetherBags.Extensions;

public static unsafe class InventoryTypeExtensions
{
    extension(InventoryType inventoryType)
    {
        public uint AgentItemContainerId =>
            inventoryType switch
            {
                InventoryType.EquippedItems => 4,
                InventoryType.KeyItems => 7,
                InventoryType.Inventory1 => 48,
                InventoryType.Inventory2 => 49,
                InventoryType.Inventory3 => 50,
                InventoryType.Inventory4 => 51,
                // It's possible that these are actually UI IDs
                InventoryType.RetainerPage1 => 52,
                InventoryType.RetainerPage2 => 53,
                InventoryType.RetainerPage3 => 54,
                InventoryType.RetainerPage4 => 55,
                InventoryType.RetainerPage5 => 56,
                InventoryType.ArmoryMainHand => 57,
                InventoryType.ArmoryHead => 58,
                InventoryType.ArmoryBody => 59,
                InventoryType.ArmoryHands => 60,
                InventoryType.ArmoryLegs => 61,
                InventoryType.ArmoryFeets => 62,
                InventoryType.ArmoryOffHand => 63,
                InventoryType.ArmoryEar => 64,
                InventoryType.ArmoryNeck => 65,
                InventoryType.ArmoryWrist => 66,
                InventoryType.ArmoryRings => 67,
                InventoryType.ArmorySoulCrystal => 68,
                InventoryType.SaddleBag1 => 69,
                InventoryType.SaddleBag2 => 70,
                InventoryType.PremiumSaddleBag1 => 71,
                InventoryType.PremiumSaddleBag2 => 72,
                _ => 0
            };

        public static InventoryType GetInventoryTypeFromContainerId(int id) =>
            id switch
            {
                4 => InventoryType.EquippedItems,
                7 => InventoryType.KeyItems,
                48 => InventoryType.Inventory1,
                49 => InventoryType.Inventory2,
                50 => InventoryType.Inventory3,
                51 => InventoryType.Inventory4,
                52 => InventoryType.RetainerPage1,
                53 => InventoryType.RetainerPage2,
                54 => InventoryType.RetainerPage3,
                55 => InventoryType.RetainerPage4,
                56 => InventoryType.RetainerPage5,
                57 => InventoryType.ArmoryMainHand,
                58 => InventoryType.ArmoryHead,
                59 => InventoryType.ArmoryBody,
                60 => InventoryType.ArmoryHands,
                61 => InventoryType.ArmoryLegs,
                62 => InventoryType.ArmoryFeets,
                63 => InventoryType.ArmoryOffHand,
                64 => InventoryType.ArmoryEar,
                65 => InventoryType.ArmoryNeck,
                66 => InventoryType.ArmoryWrist,
                67 => InventoryType.ArmoryRings,
                68 => InventoryType.ArmorySoulCrystal,
                69 => InventoryType.SaddleBag1,
                70 => InventoryType.SaddleBag2,
                71 => InventoryType.PremiumSaddleBag1,
                72 => InventoryType.PremiumSaddleBag2,
                _ => (InventoryType)0
            };

        public ItemOrderModuleSorter* GetInventorySorter => inventoryType switch {
            InventoryType.Inventory1 => ItemOrderModule.Instance()->InventorySorter,
            InventoryType.Inventory2 => ItemOrderModule.Instance()->InventorySorter,
            InventoryType.Inventory3 => ItemOrderModule.Instance()->InventorySorter,
            InventoryType.Inventory4 => ItemOrderModule.Instance()->InventorySorter,
            InventoryType.ArmoryMainHand => ItemOrderModule.Instance()->ArmouryMainHandSorter,
            InventoryType.ArmoryOffHand => ItemOrderModule.Instance()->ArmouryOffHandSorter,
            InventoryType.ArmoryHead => ItemOrderModule.Instance()->ArmouryHeadSorter,
            InventoryType.ArmoryBody => ItemOrderModule.Instance()->ArmouryBodySorter,
            InventoryType.ArmoryHands => ItemOrderModule.Instance()->ArmouryHandsSorter,
            InventoryType.ArmoryLegs => ItemOrderModule.Instance()->ArmouryLegsSorter,
            InventoryType.ArmoryFeets => ItemOrderModule.Instance()->ArmouryFeetSorter,
            InventoryType.ArmoryEar => ItemOrderModule.Instance()->ArmouryEarsSorter,
            InventoryType.ArmoryNeck => ItemOrderModule.Instance()->ArmouryNeckSorter,
            InventoryType.ArmoryWrist => ItemOrderModule.Instance()->ArmouryWristsSorter,
            InventoryType.ArmoryRings => ItemOrderModule.Instance()->ArmouryRingsSorter,
            InventoryType.ArmorySoulCrystal => ItemOrderModule.Instance()->ArmourySoulCrystalSorter,
            InventoryType.SaddleBag1 => ItemOrderModule.Instance()->SaddleBagSorter,
            InventoryType.SaddleBag2 => ItemOrderModule.Instance()->SaddleBagSorter,
            InventoryType.PremiumSaddleBag1 => ItemOrderModule.Instance()->PremiumSaddleBagSorter,
            InventoryType.PremiumSaddleBag2 => ItemOrderModule.Instance()->PremiumSaddleBagSorter,
            InventoryType.RetainerPage1 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            InventoryType.RetainerPage2 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            InventoryType.RetainerPage3 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            InventoryType.RetainerPage4 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            InventoryType.RetainerPage5 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            InventoryType.RetainerPage6 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            InventoryType.RetainerPage7 => ItemOrderModule.Instance()->GetActiveRetainerSorter(),
            _ => null,
        };

        public int GetInventoryStartIndex => inventoryType switch {
            InventoryType.Inventory2 => inventoryType.GetInventorySorter->ItemsPerPage,
            InventoryType.Inventory3 => inventoryType.GetInventorySorter->ItemsPerPage * 2,
            InventoryType.Inventory4 => inventoryType.GetInventorySorter->ItemsPerPage * 3,
            InventoryType.SaddleBag2 => inventoryType.GetInventorySorter->ItemsPerPage,
            InventoryType.PremiumSaddleBag2 => inventoryType.GetInventorySorter->ItemsPerPage,
            InventoryType.RetainerPage2 => inventoryType.GetInventorySorter->ItemsPerPage,
            InventoryType.RetainerPage3 => inventoryType.GetInventorySorter->ItemsPerPage * 2,
            InventoryType.RetainerPage4 => inventoryType.GetInventorySorter->ItemsPerPage * 3,
            InventoryType.RetainerPage5 => inventoryType.GetInventorySorter->ItemsPerPage * 4,
            InventoryType.RetainerPage6 => inventoryType.GetInventorySorter->ItemsPerPage * 5,
            InventoryType.RetainerPage7 => inventoryType.GetInventorySorter->ItemsPerPage * 6,
            _ => 0,
        };

        public bool IsMainInventory => inventoryType is
            InventoryType.Inventory1 or
            InventoryType.Inventory2 or
            InventoryType.Inventory3 or
            InventoryType.Inventory4;

        public bool IsSaddleBag => inventoryType is
            InventoryType.SaddleBag1 or
            InventoryType.SaddleBag2 or
            InventoryType.PremiumSaddleBag1 or
            InventoryType.PremiumSaddleBag2;

        public bool IsArmory => inventoryType is
            InventoryType.ArmoryMainHand or
            InventoryType.ArmoryHead or
            InventoryType.ArmoryBody or
            InventoryType.ArmoryHands or
            InventoryType.ArmoryLegs or
            InventoryType.ArmoryFeets or
            InventoryType.ArmoryOffHand or
            InventoryType.ArmoryEar or
            InventoryType.ArmoryNeck or
            InventoryType.ArmoryWrist or
            InventoryType.ArmoryRings or
            InventoryType.ArmorySoulCrystal;

        public bool IsRetainer => inventoryType is
            InventoryType.RetainerPage1 or
            InventoryType.RetainerPage2 or
            InventoryType.RetainerPage3 or
            InventoryType.RetainerPage4 or
            InventoryType.RetainerPage5 or
            InventoryType.RetainerPage6 or
            InventoryType.RetainerPage7;

        public int ContainerGroup => inventoryType switch
        {
            _ when inventoryType.IsMainInventory => 1,
            _ when inventoryType.IsSaddleBag => 2,
            _ when inventoryType.IsArmory => 3,
            _ when inventoryType.IsRetainer => 4,
            _ => 0,
        };

        public bool IsSameContainerGroup(InventoryType other)
            => inventoryType.ContainerGroup == other.ContainerGroup;

        /// <summary>
        /// Resolves the real container and slot for this inventory type using ItemOrderModule.
        /// For sorted inventories, the visual slot differs from the actual storage slot.
        /// </summary>
        public InventoryLocation GetRealItemLocation(int visualSlot)
        {
            var sorter = inventoryType.GetInventorySorter;
            if (sorter == null)
                return new InventoryLocation(inventoryType, (ushort)visualSlot);

            int startIndex = inventoryType.GetInventoryStartIndex;
            int sorterIndex = startIndex + visualSlot;

            if (sorterIndex < 0 || sorterIndex >= sorter->Items.LongCount)
                return new InventoryLocation(inventoryType, (ushort)visualSlot);

            var entry = sorter->Items[sorterIndex].Value;
            if (entry == null)
                return new InventoryLocation(inventoryType, (ushort)visualSlot);

            InventoryType baseType = inventoryType switch
            {
                _ when inventoryType.IsMainInventory => InventoryType.Inventory1,
                _ when inventoryType.IsSaddleBag => inventoryType is InventoryType.SaddleBag1 or InventoryType.SaddleBag2
                    ? InventoryType.SaddleBag1
                    : InventoryType.PremiumSaddleBag1,
                _ when inventoryType.IsRetainer => InventoryType.RetainerPage1,
                _ => inventoryType,
            };

            InventoryType realContainer = baseType + entry->Page;
            ushort realSlot = entry->Slot;

            return new InventoryLocation(realContainer, realSlot);
        }
    }
}