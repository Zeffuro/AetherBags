using System.Collections.Generic;

namespace AetherBags.Inventory;

public readonly record struct CategorizedInventory(uint Key, CategoryInfo Category, List<ItemInfo> Items);