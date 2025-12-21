using System.Numerics;
using KamiToolKit.Classes;

namespace AetherBags.Configuration;

public class SystemConfiguration
{
    public const string FileName = "AetherBags.json";

    public CurrencySettings Currency { get; set; } = new();
}