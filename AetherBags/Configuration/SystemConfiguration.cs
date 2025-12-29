namespace AetherBags.Configuration;

public class SystemConfiguration
{
    public const string FileName = "AetherBags.json";


    public GeneralSettings General { get; set; } = new();
    public CategorySettings Categories { get; set; } = new();
    public CurrencySettings Currency { get; set; } = new();
}