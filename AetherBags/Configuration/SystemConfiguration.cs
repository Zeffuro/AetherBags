namespace AetherBags.Configuration;

public class SystemConfiguration
{
    public const string FileName = "AetherBags.json";

    public int Version { get; set; } = ConfigMigrator.CurrentVersion;

    private GeneralSettings _general = new();
    private CategorySettings _categories = new();
    private CurrencySettings _currency = new();
    private KeybindSettings _keybinds = new();
    private DtrSettings _dtr = new();

    public GeneralSettings General
    {
        get => _general;
        set => _general = value ?? new();
    }

    public CategorySettings Categories
    {
        get => _categories;
        set => _categories = value ?? new();
    }

    public CurrencySettings Currency
    {
        get => _currency;
        set => _currency = value ?? new();
    }

    public KeybindSettings Keybinds
    {
        get => _keybinds;
        set => _keybinds = value ?? new();
    }

    public DtrSettings Dtr
    {
        get => _dtr;
        set => _dtr = value ?? new();
    }

    /// <summary>
    /// Ensures all nested config objects are initialized. Call after deserialization.
    /// </summary>
    public void EnsureInitialized()
    {
        _general ??= new();
        _categories ??= new();
        _currency ??= new();
        _keybinds ??= new();
        _dtr ??= new();
        Version = ConfigMigrator.CurrentVersion;
        _categories.UserCategories ??= new();
        _categories.NormalizeItemSortSettings();
        _general.Layout ??= new() { UseGlobal = false };
        _general.InventoryLayout ??= new();
        _general.SaddleBagLayout ??= new();
        _general.RetainerLayout ??= new();
        _general.Layout.Normalize();
        _general.InventoryLayout.Normalize();
        _general.SaddleBagLayout.Normalize();
        _general.RetainerLayout.Normalize();
        _general.InventoryWindowSizing ??= InventoryWindowSizingDefaults.Create(InventoryWindowSizingLimits.Inventory);
        _general.SaddleBagWindowSizing ??= InventoryWindowSizingDefaults.Create(InventoryWindowSizingLimits.SaddleBag);
        _general.RetainerWindowSizing ??= InventoryWindowSizingDefaults.Create(InventoryWindowSizingLimits.Retainer);
        InventoryWindowSizingDefaults.Normalize(_general.InventoryWindowSizing, InventoryWindowSizingLimits.Inventory);
        InventoryWindowSizingDefaults.Normalize(_general.SaddleBagWindowSizing, InventoryWindowSizingLimits.SaddleBag);
        InventoryWindowSizingDefaults.Normalize(_general.RetainerWindowSizing, InventoryWindowSizingLimits.Retainer);
    }
}
