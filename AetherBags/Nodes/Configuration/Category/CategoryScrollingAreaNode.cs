using System.IO;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Helpers.Import;
using AetherBags.Inventory;
using Dalamud.Game.ClientState.Keys;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryScrollingAreaNode : ScrollingListNode
{
    private AddonCategoryConfigurationWindow? _categoryConfigurationAddon;

    public CategoryScrollingAreaNode()
    {
        InitializeCategoryAddon();

        AddNode(new CategoryGeneralConfigurationNode());

        AddNode(new ExperimentalConfigurationNode());

        AddNode(new ResNode{ Height = 10 });

        var categoryButtonRow = new HorizontalListNode
        {
            Size = new Vector2(300, 28),
            ItemSpacing = 4.0f,
        };

        categoryButtonRow.AddNode(new TextButtonNode
        {
            Size = new Vector2(200, 28),
            String = "Configure Categories",
            OnClick = () => _categoryConfigurationAddon?.Toggle(),
        });

        categoryButtonRow.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Services.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\upload.png"),
            TextTooltip = "Export All Categories to Clipboard",
            OnClick = () => CategoryImportExport.ExportAllCategoriesToClipboard(System.Config.Categories.UserCategories),
        });

        categoryButtonRow.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Services.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\download.png"),
            TextTooltip = "Import All Categories from Clipboard\n(hold shift to confirm)",
            OnClick = HandleBulkImport,
        });

        AddNode(categoryButtonRow);
    }

    private void HandleBulkImport()
    {
        if (!Services.KeyState[VirtualKey.SHIFT]) return;
        CategoryImportExport.ImportAllCategoriesFromClipboard(System.Config, true);
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void InitializeCategoryAddon() {
        if (_categoryConfigurationAddon is not null) return;

        _categoryConfigurationAddon = new AddonCategoryConfigurationWindow {
            Size = new Vector2(700.0f, 500.0f),
            InternalName = "AetherBags_CategoryConfig",
            Title = "Category Configuration Window",
        };
    }

    protected override void Dispose(bool disposing, bool isNativeDestructor)
    {
        if (disposing)
        {
            if (_categoryConfigurationAddon != null)
            {
                _categoryConfigurationAddon.Close();
                _categoryConfigurationAddon = null;
            }
        }

        base.Dispose(disposing, isNativeDestructor);
    }
}