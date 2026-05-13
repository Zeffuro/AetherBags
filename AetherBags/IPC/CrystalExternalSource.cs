using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using AetherBags.IPC.ExternalCategorySystem;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.IPC;

public sealed unsafe class CrystalExternalSource : IExternalItemSource, IInventoryTypeProvidingSource, IDisposable
{
    private const uint CrystalCategoryKey = 0xFFFE_0001;
    private const uint CrystalUiCategoryId = 59;

    private static IReadOnlyDictionary<uint, ExternalCategoryAssignment>? s_assignments;

    private static readonly InventoryType[] InventoryTypesArray =
        { InventoryType.Crystals, InventoryType.RetainerCrystals };
    private static readonly InventoryType[] PlayerCrystalsArray = { InventoryType.Crystals };
    private static readonly InventoryType[] RetainerCrystalsArray = { InventoryType.RetainerCrystals };

    private int _version;
    private bool _isEnabled;

    public string SourceName => "CrystalSource";
    public string DisplayName => "Crystals";
    public int Priority => 60;
    public bool IsReady => _isEnabled;
    public int Version => _version;
    public int DefaultDisplayOrder => 35;
    public bool IsBuiltIn => true;
    public IReadOnlyList<uint> OverriddenGameCategoryIds { get; } = new uint[] { CrystalUiCategoryId };
    public bool LocksDragOut => false;
    public bool LocksDragIn => false;
    public event Action? OnDataChanged;

    public SourceCapabilities Capabilities => SourceCapabilities.Categories;
    public ConflictBehavior ConflictBehavior => ConflictBehavior.Defer;

    public IReadOnlyList<InventoryType> AdditionalInventoryTypes => InventoryTypesArray;

    public DragDropType GetDragDropTypeFor(InventoryType container) => DragDropType.Crystal;

    public bool AutoRoutesDeposits(InventoryType container) => container == InventoryType.RetainerCrystals;

    public IReadOnlyList<InventoryType> AdditionalInventoryTypesFor(InventorySourceType sourceType) => sourceType switch
    {
        InventorySourceType.MainBags => PlayerCrystalsArray,
        InventorySourceType.Retainer => RetainerCrystalsArray,
        _ => Array.Empty<InventoryType>(),
    };

    public void Enable()
    {
        if (_isEnabled) return;
        _isEnabled = true;
        _version++;
        ExternalCategoryManager.RegisterSource(this);
        OnDataChanged?.Invoke();
        Services.Logger.Information("[CrystalSource] Enabled");
    }

    public void Disable()
    {
        if (!_isEnabled) return;
        _isEnabled = false;
        ExternalCategoryManager.UnregisterSource(SourceName);
        Services.Logger.Information("[CrystalSource] Disabled");
    }

    public IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments()
    {
        if (!_isEnabled) return null;
        return s_assignments ??= BuildAssignments();
    }

    private static Dictionary<uint, ExternalCategoryAssignment> BuildAssignments()
    {
        var assignment = new ExternalCategoryAssignment(
            CategoryKey: CrystalCategoryKey,
            CategoryName: "Crystals",
            CategoryDescription: "Elemental shards, crystals, and clusters",
            CategoryColor: new Vector4(0.6f, 0.85f, 1.0f, 1.0f),
            ItemOverlayColor: null,
            SubPriority: 10,
            IsPinned: true
        );

        var result = new Dictionary<uint, ExternalCategoryAssignment>();
        foreach (var row in ItemInfo.ItemSheet)
        {
            if (row.ItemUICategory.RowId == CrystalUiCategoryId)
                result[row.RowId] = assignment;
        }
        return result;
    }

    public IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations() => null;
    public IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId) => null;
    public IReadOnlyDictionary<uint, string[]>? GetSearchTags() => null;
    public IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId) => null;

    public void Dispose() => Disable();
}
