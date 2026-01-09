namespace AetherBags.Inventory.Items;

public readonly struct InventoryStats
{
    public int TotalItems { get; init; }
    public int TotalQuantity { get; init; }
    public int EmptySlots { get; init; }
    public int TotalSlots { get; init; }
    public int CategoryCount { get; init; }
    public int UsedSlots => TotalSlots - EmptySlots;
    public float UsagePercent => TotalSlots > 0 ? (float)UsedSlots / TotalSlots * 100f : 0f;

    public static InventoryStats operator +(InventoryStats a, InventoryStats b) => new()
    {
        TotalItems = a.TotalItems + b.TotalItems,
        TotalQuantity = a.TotalQuantity + b.TotalQuantity,
        EmptySlots = a.EmptySlots + b.EmptySlots,
        TotalSlots = a.TotalSlots + b.TotalSlots,
        CategoryCount = a.CategoryCount + b.CategoryCount,
    };
}