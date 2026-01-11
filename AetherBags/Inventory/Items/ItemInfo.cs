using System;
using System.Numerics;
using System.Text.RegularExpressions;
using AetherBags.Helpers;
using AetherBags.Inventory.Context;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AetherBags.Inventory.Items;

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
    private string? _levelString;
    private string? _itemLevelString;

    private int _cachedHighlightVersion = -1;
    private float _cachedVisualAlpha;
    private Vector3 _cachedHighlightColor;

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
    private string LevelString => _levelString ??= Level.ToString();
    private string ItemLevelString => _itemLevelString ??= ItemLevel.ToString();
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
    public bool IsCraftable => Row.ItemAction.RowId != 0 || Row.CanBeHq;
    public bool IsGlamourable => Row.IsGlamorous;
    public bool IsSpiritbonded => Item.SpiritbondOrCollectability >= 10000; // 100% = 10000

    private string Description => _description ??= Row.Description.ToString();

    public InventoryMappedLocation VisualLocation => InventoryContextState.GetVisualLocation(Item.Container, Item.Slot);


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
            if (IsSlotBlocked) return false;
            if (!CheckNativeContextEligibility()) return false;
            if (!HighlightState.IsInActiveFilters(Item.ItemId)) return false;

            return true;
        }
    }

    public float VisualAlpha
    {
        get
        {
            EnsureVisualStateCached();
            return _cachedVisualAlpha;
        }
    }

    public Vector3 HighlightOverlayColor
    {
        get
        {
            EnsureVisualStateCached();
            return _cachedHighlightColor;
        }
    }

    private void EnsureVisualStateCached()
    {
        int currentVersion = HighlightState.Version;
        if (_cachedHighlightVersion == currentVersion)
            return;

        _cachedVisualAlpha = IsEligibleForContext ? 1.0f : 0.4f;
        _cachedHighlightColor = System.Config.Categories.BisBuddyEnabled
            ? HighlightState.GetLabelColor(Item.ItemId) ?? Vector3.Zero
            : Vector3.Zero;
        _cachedHighlightVersion = currentVersion;
    }

    private bool CheckNativeContextEligibility()
    {
        uint contextId = InventoryContextState.ActiveContextId;
        if (contextId == 0) return true;

        bool isRetainerContext = contextId == 4;
        bool isSaddlebagContext = contextId == 29;
        bool isMainContext = !isRetainerContext && isSaddlebagContext == false;

        if (IsMainInventory)
        {
            if (!isMainContext) return true;
            return InventoryContextState.IsEligible(InventoryPage, Item.Slot);
        }

        if (Item.Container.IsRetainer)
        {
            if (!isRetainerContext) return true;
        }

        if (Item.Container.IsSaddleBag)
        {
            if (!isSaddlebagContext) return true;
        }

        return true;
    }

    public bool IsMainInventory => InventoryPage >= 0;

    public bool IsRegexMatch(string searchTerms)
    {
        if (string.IsNullOrEmpty(searchTerms))
            return true;

        var re = RegexCache.GetOrCreate(searchTerms);
        if (re == null)
            return false;

        if (re.IsMatch(Name)) return true;

        if (re.IsMatch(Description)) return true;

        if (re.IsMatch(LevelString)) return true;
        if (re.IsMatch(ItemLevelString)) return true;

        return false;
    }

    public bool IsRegexMatch(Regex re)
    {
        if (re.IsMatch(Name)) return true;
        if (re.IsMatch(Description)) return true;

        if (re.IsMatch(LevelString)) return true;
        if (re.IsMatch(ItemLevelString)) return true;

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
