using AetherBags.Addons;
using AetherBags.Configuration;

namespace AetherBags;

public static class System
{
    public static AddonInventoryWindow AddonInventoryWindow { get; set; } = null!;
    public static AddonSaddleBagWindow AddonSaddleBagWindow { get; set; } = null!;
    public static AddonRetainerWindow AddonRetainerWindow { get; set; } = null!;
    public static AddonConfigurationWindow AddonConfigurationWindow { get; set; } = null!;
    public static SystemConfiguration Config { get; set; } = null!;
}