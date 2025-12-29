using System.Numerics;
using KamiToolKit.Classes;

namespace AetherBags.Configuration;

public class CurrencySettings
{
    public bool Enabled { get; set; } = true;
    public bool ColorWhenCapped { get; set; } = true;
    public bool ColorWhenLimited { get; set; } = true;
    public Vector4 DefaultColor { get; set; } = ColorHelper.GetColor(8);
    public Vector4 CappedColor  { get; set; } = ColorHelper.GetColor(43);
    public Vector4 LimitColor   { get; set; } = ColorHelper.GetColor(17);
}