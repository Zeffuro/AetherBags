using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using KamiToolKit.Classes;

namespace AetherBags.Configuration;

public class CurrencySettings
{
    [JsonIgnore]
    public const uint LimitedTomestoneId = 0xFFFF_FFFE;

    [JsonIgnore]
    public const uint NonLimitedTomestoneId = 0xFFFF_FFFD;

    public bool Enabled { get; set; } = true;
    public List<uint> DisplayedCurrencies { get; set; } = new() { 1, LimitedTomestoneId, NonLimitedTomestoneId };
    public bool ColorWhenCapped { get; set; } = true;
    public bool ColorWhenLimited { get; set; } = true;
    public Vector4 DefaultColor { get; set; } = ColorHelper.GetColor(8);
    public Vector4 CappedColor  { get; set; } = ColorHelper.GetColor(43);
    public Vector4 LimitColor   { get; set; } = ColorHelper.GetColor(17);
}