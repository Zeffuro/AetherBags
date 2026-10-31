using System.Collections.Generic;
using AetherBags.Inventory.Categories;

namespace AetherBags.Inventory.Layout;

public sealed record InventoryLayoutBlock(
    ulong Key,
    CategorizedInventory Inventory,
    int Columns,
    int SlotCount,
    IReadOnlyDictionary<ulong, int>? ItemSlots);
