namespace AetherBags.Configuration;

public class SystemConfiguration
{
    public const string FileName = "AetherBags.json";

    private GeneralSettings _general = new();
    private CategorySettings _categories = new();
    private CurrencySettings _currency = new();

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

    /// <summary>
    /// Ensures all nested config objects are initialized. Call after deserialization.
    /// </summary>
    public void EnsureInitialized()
    {
        _general ??= new();
        _categories ??= new();
        _currency ??= new();
        _categories.UserCategories ??= new();
    }
}