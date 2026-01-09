using AetherBags.Addons;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.IPC;

namespace AetherBags;

public static class System
{
    public static AddonInventoryWindow AddonInventoryWindow { get; set; } = null!;
    public static AddonSaddleBagWindow AddonSaddleBagWindow { get; set; } = null!;
    public static AddonRetainerWindow AddonRetainerWindow { get; set; } = null!;
    public static AddonConfigurationWindow AddonConfigurationWindow { get; set; } = null!;
    public static IPCService IPC { get; set; } = null!;
    public static SystemConfiguration Config { get; set; } = null!;
    public static LootedItemsTracker LootedItemsTracker { get; set; } = null!;
}