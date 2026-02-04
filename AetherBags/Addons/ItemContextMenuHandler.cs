using AetherBags.Inventory.Items;
using AetherBags.IPC.ExternalCategorySystem;
using KamiToolKit.ContextMenu;

namespace AetherBags.Addons;

public static class ItemContextMenuHandler
{
    private static ContextMenu? _itemMenu;

    public static void Initialize()
    {
        _itemMenu = new ContextMenu();
    }

    public static void Dispose()
    {
        _itemMenu?.Dispose();
        _itemMenu = null;
    }

    public static bool TryShowExternalMenu(ItemInfo item)
    {
        if (_itemMenu == null) return false;
        if (!System.Config.General.UseUnifiedExternalCategories) return false;

        var entries = ExternalCategoryManager.GetContextMenuEntries(item.Item.ItemId);
        if (entries == null || entries.Count == 0) return false;

        _itemMenu.Clear();

        var context = new ContextMenuContext(
            item.Item.ItemId,
            (int)item.Item.Container,
            item.Item.Slot
        );

        foreach (var entry in entries)
        {
            var capturedEntry = entry;
            var capturedContext = context;
            _itemMenu.AddItem(entry.Label, () => capturedEntry.OnClick(capturedContext));
        }

        _itemMenu.Open();
        return true;
    }
}
