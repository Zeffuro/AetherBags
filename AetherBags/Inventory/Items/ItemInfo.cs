using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AetherBags.Helpers;
using AetherBags.Inventory.Context;
using AetherBags.IPC.ExternalCategorySystem;
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
    private static ExcelSheet<EventItem>? s_eventItemSheet;
    private static ExcelSheet<Item> ItemSheet => s_itemSheet ??= Services.DataManager.GetExcelSheet<Item>();
    private static ExcelSheet<EventItem> EventItemSheet => s_eventItemSheet ??= Services.DataManager.GetExcelSheet<EventItem>();

    public static bool IsAggregatable(uint itemId)
        => ItemSheet.TryGetRow(itemId, out var row) && row.StackSize > 1;

    private bool _rowLoaded;
    private bool _rowFound;
    private Item _row;

    private bool _eventRowLoaded;
    private bool _eventRowFound;
    private EventItem _eventRow;

    private string? _name;
    private string? _description;
    private string? _levelString;
    private string? _itemLevelString;

    private int _cachedHighlightVersion = -1;
    private float _cachedVisualAlpha;
    private Vector3 _cachedHighlightColor;
    private bool _cachedIsRelationshipHighlighted;

    // Default Lumina row's internal pool ref is null; accessors must guard before touching _row.
    private bool HasRow
    {
        get
        {
            if (!_rowLoaded)
            {
                _rowFound = ItemSheet.TryGetRow(Item.ItemId, out _row);
                _rowLoaded = true;
            }
            return _rowFound;
        }
    }

    private bool HasEventRow
    {
        get
        {
            if (!_eventRowLoaded)
            {
                _eventRowFound = EventItemSheet.TryGetRow(Item.ItemId, out _eventRow);
                _eventRowLoaded = true;
            }
            return _eventRowFound;
        }
    }

    public Vector4 RarityColor => HasRow ? _row.RarityColor : Vector4.One;

    public uint IconId
    {
        get
        {
            if (HasRow) return _row.Icon;
            if (HasEventRow) return _eventRow.Icon;
            return 0u;
        }
    }

    public string Name
    {
        get
        {
            if (_name != null) return _name;
            if (HasRow) return _name = _row.Name.ToString();
            if (HasEventRow) return _name = _eventRow.Name.ToString();
            return _name = string.Empty;
        }
    }

    public int Level => HasRow ? _row.LevelEquip : 0;
    public int ItemLevel => HasRow ? (int)_row.LevelItem.RowId : 0;
    private string LevelString => _levelString ??= Level.ToString();
    private string ItemLevelString => _itemLevelString ??= ItemLevel.ToString();
    public int Rarity => HasRow ? _row.Rarity : 0;
    public uint VendorPrice => HasRow ? _row.PriceLow : 0u;
    public uint StackSize => HasRow ? _row.StackSize : 0u;

    public RowRef<ItemUICategory> UiCategory => HasRow ? _row.ItemUICategory : default;

    public bool IsUntradable => HasRow && _row.IsUntradable;
    public bool IsUnique => HasRow && _row.IsUnique;
    public bool IsCollectable => HasRow && _row.IsCollectable;
    public bool IsDyeable => HasRow && _row.DyeCount > 0;
    public bool IsRepairable => HasRow && _row.ItemRepair.RowId != 0;

    public bool IsHq => Item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
    public bool IsDesynthesizable => HasRow && _row.Desynth > 0;
    public bool IsCraftable => HasRow && (_row.ItemAction.RowId != 0 || _row.CanBeHq);
    public bool IsGlamourable => HasRow && _row.IsGlamorous;
    public bool IsSpiritbonded => Item.SpiritbondOrCollectability >= 10000; // 100% = 10000

    private string Description => _description ??= HasRow ? _row.Description.ToString() : string.Empty;

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

    public bool IsRelationshipHighlighted
    {
        get
        {
            EnsureVisualStateCached();
            return _cachedIsRelationshipHighlighted;
        }
    }

    private void EnsureVisualStateCached()
    {
        int currentVersion = HighlightState.Version;
        if (_cachedHighlightVersion == currentVersion)
            return;

        _cachedVisualAlpha = IsEligibleForContext ? 1.0f : 0.4f;
        _cachedHighlightColor = HighlightState.GetLabelColor(Item.ItemId) ?? Vector3.Zero;

        var entry = HighlightState.GetHighlightEntry(Item.ItemId);
        _cachedIsRelationshipHighlighted = entry != null;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRegexMatch(string searchTerms)
    {
        if (string.IsNullOrEmpty(searchTerms))
            return true;

        var re = RegexCache.GetOrCreate(searchTerms, compiled: false);
        if (re == null)
            return false;

        if (ExternalCategoryManager.MatchesSearchTag(Item.ItemId, searchTerms)) return true;
        return IsRegexMatch(re);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRegexMatch(Regex re)
    {
        if (re.IsMatch(Name)) return true;
        if (re.IsMatch(LevelString)) return true;
        if (re.IsMatch(ItemLevelString)) return true;
        if (re.IsMatch(Description)) return true;
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
