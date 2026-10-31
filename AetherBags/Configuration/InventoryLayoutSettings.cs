using System;
using System.ComponentModel;

namespace AetherBags.Configuration;

public sealed class InventoryLayoutSettings
{
    internal InventoryLayoutOptions Options => new(Updates, Retention, Contexts, SettleDelayMilliseconds, ShowEmptySlots, SeparateCategoryRows);
    public bool UseGlobal { get; set; } = true;
    public InventoryLayoutUpdates Updates { get; set; }
    public InventoryLayoutRetention Retention { get; set; } = InventoryLayoutRetention.Items;
    public InventoryLayoutContexts Contexts { get; set; } = InventoryLayoutContexts.Shops;
    public int SettleDelayMilliseconds { get; set; } = 1500;
    public bool ShowEmptySlots { get; set; }
    public bool SeparateCategoryRows { get; set; }

    public InventoryLayoutSettings Copy() => new()
    {
        UseGlobal = UseGlobal,
        Updates = Updates,
        Retention = Retention,
        Contexts = Contexts,
        SettleDelayMilliseconds = SettleDelayMilliseconds,
        ShowEmptySlots = ShowEmptySlots,
        SeparateCategoryRows = SeparateCategoryRows,
    };

    public void Normalize()
    {
        if (!Enum.IsDefined(Updates)) Updates = InventoryLayoutUpdates.Live;
        if (!Enum.IsDefined(Retention)) Retention = InventoryLayoutRetention.Items;
        Contexts &= InventoryLayoutContexts.All;
        SettleDelayMilliseconds = Math.Clamp(SettleDelayMilliseconds, 250, 10000);
    }
}

internal readonly record struct InventoryLayoutOptions(InventoryLayoutUpdates Updates, InventoryLayoutRetention Retention,
    InventoryLayoutContexts Contexts, int Delay, bool ShowEmptySlots, bool SeparateRows);

public enum InventoryLayoutUpdates : byte
{
    [Description("Live")]
    Live,
    [Description("After idle")]
    AfterIdle,
    [Description("When reopened")]
    WhenReopened,
}

public enum InventoryLayoutRetention : byte
{
    [Description("Keep category space")]
    Categories,
    [Description("Keep item positions")]
    Items,
}

[Flags]
public enum InventoryLayoutContexts : byte
{
    None = 0,
    Shops = 1,
    TradeAndMail = 2,
    Retainers = 4,
    Saddlebags = 8,
    Other = 16,
    All = Shops | TradeAndMail | Retainers | Saddlebags | Other,
}
