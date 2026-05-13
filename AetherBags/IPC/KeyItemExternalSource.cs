using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Inventory.Items;
using AetherBags.IPC.ExternalCategorySystem;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.IPC;

public sealed unsafe class KeyItemExternalSource : IExternalItemSource, IInventoryTypeProvidingSource, IDisposable
{
    private const uint KeyItemCategoryKey = 0xFFFE_0002;

    private static readonly InventoryType[] InventoryTypesArray = { InventoryType.KeyItems };

    private static IReadOnlyDictionary<uint, ExternalCategoryAssignment>? s_assignments;

    private int _version;
    private bool _isEnabled;

    public string SourceName => "KeyItemSource";
    public string DisplayName => "Key Items";
    public int Priority => 60;
    public bool IsReady => _isEnabled;
    public int Version => _version;
    public int DefaultDisplayOrder => 36;
    public bool IsBuiltIn => true;
    public bool LocksDragOut => false;
    public bool LocksDragIn => false;
    public event Action? OnDataChanged;

    public SourceCapabilities Capabilities => SourceCapabilities.Categories;
    public ConflictBehavior ConflictBehavior => ConflictBehavior.Defer;

    public IReadOnlyList<InventoryType> AdditionalInventoryTypes => InventoryTypesArray;

    public DragDropType GetDragDropTypeFor(InventoryType container) => DragDropType.EventItem;

    public void Enable()
    {
        if (_isEnabled) return;
        _isEnabled = true;
        _version++;
        ExternalCategoryManager.RegisterSource(this);
        OnDataChanged?.Invoke();
        Services.Logger.Information("[KeyItemSource] Enabled");
    }

    public void Disable()
    {
        if (!_isEnabled) return;
        _isEnabled = false;
        ExternalCategoryManager.UnregisterSource(SourceName);
        Services.Logger.Information("[KeyItemSource] Disabled");
    }

    public IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments()
    {
        if (!_isEnabled) return null;
        return s_assignments ??= BuildAssignments();
    }

    private static Dictionary<uint, ExternalCategoryAssignment> BuildAssignments()
    {
        var assignment = new ExternalCategoryAssignment(
            CategoryKey: KeyItemCategoryKey,
            CategoryName: "Key Items",
            CategoryDescription: "Quest and event-tied key items",
            CategoryColor: new Vector4(1.0f, 0.85f, 0.4f, 1.0f),
            ItemOverlayColor: null,
            SubPriority: 20,
            IsPinned: true
        );

        var result = new Dictionary<uint, ExternalCategoryAssignment>();
        foreach (var row in ItemInfo.EventItemSheet)
        {
            if (row.RowId == 0) continue;
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
