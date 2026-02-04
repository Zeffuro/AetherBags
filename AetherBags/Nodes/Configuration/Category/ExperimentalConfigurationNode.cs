using AetherBags.Configuration;
using AetherBags.Inventory;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

internal class ExperimentalConfigurationNode : TabbedVerticalListNode
{
    public ExperimentalConfigurationNode()
    {
        GeneralSettings config = System.Config.General;

        var titleNode = new CategoryTextNode
        {
            Height = 18,
            String = "Experimental",
        };
        AddNode(titleNode);

        AddTab(1);

        var externalCategoryCheckbox = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = "External Category Support",
            IsChecked = config.UseUnifiedExternalCategories,
            TextTooltip = "EXPERIMENTAL - Use at your own risk. This feature is not fully tested.\n\n" +
                          "Enables enhanced integration with external plugins like " +
                          "Allagan Tools and BisBuddy.\n\n" +
                          "Features:\n" +
                          "- Search by plugin tags (e.g. search 'bis' to find BiS items)\n" +
                          "- Relationship highlighting: hover an item to see related items\n" +
                          "  (same gear set, upgrades, crafting materials)\n" +
                          "- Item badges showing plugin status icons\n" +
                          "- Custom borders and visual effects (glow, pulse)\n" +
                          "- Additional right-click menu options from plugins\n" +
                          "- Extra tooltip information from plugins\n\n" +
                          "When disabled, external plugins still provide categories and " +
                          "basic highlighting, but without these enhanced features.",
            OnClick = isChecked =>
            {
                config.UseUnifiedExternalCategories = isChecked;
                System.IPC?.UpdateUnifiedCategorySupport(isChecked);
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(externalCategoryCheckbox);
    }
}
