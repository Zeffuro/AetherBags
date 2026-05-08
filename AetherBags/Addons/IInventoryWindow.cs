using AetherBags.Inventory.Items;

namespace AetherBags.Addons;

public interface IInventoryWindow
{
    bool IsOpen { get; }
    void Toggle();
    void Close();
    void ManualRefresh();
    void ItemRefresh();
    void SetSearchText(string searchText);
    void FocusSearch();
    bool TryFocusSearch();
    InventoryStats GetStats();
}