using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory;

public readonly record struct InventoryLocation(InventoryType Container, ushort Slot)
{
    public static readonly InventoryLocation Invalid = new(0, 0);

    public bool IsValid => Container != 0;

    public override string ToString() => $"{Container}@{Slot}";
}

public readonly record struct InventoryMappedLocation(int Container, int Slot)
{
    public static readonly InventoryMappedLocation Invalid = new(0, 0);

    public bool IsValid => Container != 0;

    public override string ToString() => $"{Container}@{Slot}";
}