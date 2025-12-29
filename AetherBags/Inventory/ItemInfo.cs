using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;
using System.Text.RegularExpressions;

namespace AetherBags.Inventory;

public sealed class ItemInfo : IEquatable<ItemInfo>
{
    public required ulong Key { get; set; }

    public required InventoryItem Item { get; set; }
    public required int ItemCount { get; set; }

    private static ExcelSheet<Item>? s_itemSheet;
    private static ExcelSheet<Item> ItemSheet => s_itemSheet ??= Services.DataManager.GetExcelSheet<Item>();

    private bool _rowLoaded;
    private Item _row;

    private string? _name;
    private string? _description;

    private ref readonly Item Row
    {
        get
        {
            if (!_rowLoaded)
            {
                _row = ItemSheet.GetRow(Item.ItemId);
                _rowLoaded = true;
            }
            return ref _row;
        }
    }

    public Vector4 RarityColor => Row.RarityColor;
    public uint IconId => Row.Icon;

    public string Name => _name ??= Row.Name.ToString();

    public int Level => Row.LevelEquip;
    public int ItemLevel => (int)Row.LevelItem.RowId;
    public int Rarity => Row.Rarity;
    public uint VendorPrice => Row.PriceLow;
    public uint StackSize => Row.StackSize;

    public RowRef<ItemUICategory> UiCategory => Row.ItemUICategory;

    public bool IsUntradable => Row.IsUntradable;
    public bool IsUnique => Row.IsUnique;
    public bool IsCollectable => Row.IsCollectable;
    public bool IsDyeable => Row.DyeCount > 0;
    public bool IsRepairable => Row.ItemRepair.RowId != 0;

    public bool IsHq => Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
    public bool IsDesynthesizable => Row.Desynth > 0;
    public bool IsCraftable => Row.ItemAction.RowId != 0 || Row.CanBeHq; // Simplified check
    public bool IsGlamourable => Row.IsGlamorous;
    public bool IsSpiritbonded => Item.SpiritbondOrCollectability >= 10000; // 100% = 10000

    private string Description => _description ??= Row.Description.ToString();

    public InventoryMappedLocation VisualLocation =>
        IsMainInventory ? InventoryContextState.GetVisualLocation(InventoryPage, Item.Slot)
            : new InventoryMappedLocation((int)Item.Container.AgentItemContainerId, Item. Slot);


    public int InventoryPage => Item.Container switch
    {
        InventoryType.Inventory1 => 0,
        InventoryType.Inventory2 => 1,
        InventoryType.Inventory3 => 2,
        InventoryType.Inventory4 => 3,
        _ => -1
    };

    public bool IsSlotBlocked => InventoryContextState.IsSlotBlocked(Item.Container, Item.Slot);

    public bool IsEligibleForContext
    {
        get
        {
            if (!InventoryContextState.HasActiveContext)
                return true;

            return IsMainInventory && InventoryContextState.IsEligible(InventoryPage, Item.Slot);
        }
    }

    public bool IsMainInventory => InventoryPage >= 0;

    public bool IsRegexMatch(string searchTerms)
    {
        if (string.IsNullOrEmpty(searchTerms))
            return true;

        var re = new Regex(searchTerms, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (re.IsMatch(Name)) return true;

        if (re.IsMatch(Description)) return true;

        if (re.IsMatch(Level.ToString())) return true;
        if (re.IsMatch(ItemLevel.ToString())) return true;

        return false;
    }

    public bool IsRegexMatch(Regex re)
    {
        if (re.IsMatch(Name)) return true;
        if (re.IsMatch(Description)) return true;

        if (re.IsMatch(Level.ToString())) return true;
        if (re.IsMatch(ItemLevel.ToString())) return true;

        return false;
    }

    public bool DescriptionContains(string value)
        => Description.Contains(value, StringComparison.OrdinalIgnoreCase);

    public bool Equals(ItemInfo? other)
        => other is not null && Key == other.Key;

    public override bool Equals(object? obj)
        => obj is ItemInfo other && Equals(other);

    public override int GetHashCode()
        => Key.GetHashCode();
}
