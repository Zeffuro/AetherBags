using Dalamud.Plugin.Services;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using AetherBags.Addons;
using AetherBags.Helpers.Import;
using AetherBags.Inventory;
using Dalamud.Game.ClientState.Keys;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryScrollingAreaNode : ConfigurationScrollingNode
{

    public CategoryScrollingAreaNode()
    {
        ContentNode.AddNode(new CategoryGeneralConfigurationNode
        {
            OnLayoutChanged = RefreshLayoutAfterChildChange,
        });

        ContentNode.AddNode(new ResNode{ Height = 10 });

        var categoryButtonRow = new HorizontalListNode
        {
            Size = new Vector2(300, 28),
            ItemSpacing = 4.0f,
        };

        categoryButtonRow.AddNode(new TextButtonNode
        {
            Size = new Vector2(200, 28),
            String = "Configure Categories",
            OnClick = () =>
            {
                if (!Plugin.IsUnloading) System.CategoryConfigurationWindow.Toggle();
            },
        });

        categoryButtonRow.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\upload.png"),
            TextTooltip = "Export All Categories to Clipboard",
            OnClick = () => CategoryImportExport.ExportAllCategoriesToClipboard(System.Config.Categories.UserCategories),
        });

        categoryButtonRow.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\download.png"),
            TextTooltip = "Import All Categories from Clipboard\n(hold shift to confirm)",
            OnClick = HandleBulkImport,
        });

        var resetButton = new HoldButtonNode
        {
            Y = -4,
            Width = 100,
            Height = 28,
            String = "Reset",
            TextTooltip = "Resets All Categories\n(hold button to confirm)",
            TextNode = { TextColor = ColorHelper.GetColor(50) },
        };

        resetButton.OnClick = () =>
        {
            CheckAndCloseCategoryAddon();

            CategoryImportExport.ResetAllCategories(System.Config);
            InventoryOrchestrator.RefreshAll(updateMaps: true);
            resetButton.Reset();
        };

        categoryButtonRow.AddNode(resetButton);

        ContentNode.AddNode(categoryButtonRow);
    }

    private void RefreshLayoutAfterChildChange()
    {
        RefreshLayout();
        IFramework.Get().RunOnTick(() => { if (!IsDisposed) RefreshLayout(); }, delayTicks: 2);
    }

    private void HandleBulkImport()
    {
        CheckAndCloseCategoryAddon();

        if (!IKeyState.Get()[VirtualKey.SHIFT]) return;
        CategoryImportExport.ImportAllCategoriesFromClipboard(System.Config, true);
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private static void CheckAndCloseCategoryAddon() => System.CategoryConfigurationWindow.Close();

    protected override void Dispose(bool isNativeDestructor)
    {
        System.CategoryConfigurationWindow?.Close();
        base.Dispose(isNativeDestructor);
    }
}
