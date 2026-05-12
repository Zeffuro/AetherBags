using System.Collections.Generic;
using System.Linq;

namespace AetherBags.Tags;

public static class DtrTagDefinitions
{
    public const string DefaultDropdownLabel = "Insert Tag...";

    public static readonly IReadOnlyDictionary<string, string> Templates = new Dictionary<string, string>
    {
        [DefaultDropdownLabel] = string.Empty,
        ["Main: Used Slots"] = "[used]",
        ["Main: Total Slots"] = "[total]",
        ["Main: Free Slots"] = "[free]",
        ["Main: Usage Percent"] = "[percent:0]%",
        ["Main: Item Stacks"] = "[items]",
        ["Main: Item Quantity"] = "[quantity]",
        ["Main: Categories"] = "[categories]",
        ["Saddlebag: Used Slots"] = "[saddle.used]",
        ["Saddlebag: Total Slots"] = "[saddle.total]",
        ["Saddlebag: Free Slots"] = "[saddle.free]",
        ["Saddlebag: Usage Percent"] = "[saddle.percent:0]%",
    };

    public static List<string> GetLabels() => Templates.Keys.ToList();
}
