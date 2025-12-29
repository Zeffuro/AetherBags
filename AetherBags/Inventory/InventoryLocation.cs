using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory;

public readonly record struct InventoryLocation(InventoryType Container, ushort Slot)
{
    public static readonly InventoryLocation Invalid = new((InventoryType)uint.MaxValue, ushort.MaxValue);

    public bool IsValid => Container.IsMainInventory ||
                           Container.IsSaddleBag ||
                           Container.IsArmory ||
                           Container.IsRetainer ||
                           Container == InventoryType.EquippedItems;

    public override string ToString() => $"{Container}@{Slot}";
}

public readonly record struct InventoryMappedLocation(int Container, int Slot)
{
    public static readonly InventoryMappedLocation Invalid = new(0, 0);

    public bool IsValid => Container != 0;

    public override string ToString() => $"{Container}@{Slot}";
}