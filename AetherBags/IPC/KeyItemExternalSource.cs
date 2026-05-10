using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.IPC.ExternalCategorySystem;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.IPC;

public sealed unsafe class KeyItemExternalSource : IExternalItemSource, IInventoryTypeProvidingSource, IDisposable
{
    private const uint KeyItemCategoryKey = 0xFFFE_0002;

    private static readonly InventoryType[] InventoryTypesArray = { InventoryType.KeyItems };

    private int _version;
    private bool _isEnabled;

    public string SourceName => "KeyItemSource";
    public string DisplayName => "Key Items";
    public int Priority => 60;
    public bool IsReady => _isEnabled;
    public int Version => _version;
    public int DefaultDisplayOrder => 36;
    // KeyItems only contribute to MainBags, so per-slot AcceptedType=EventItem keeps other windows from accepting these payloads.
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
        Services.GameInventory.InventoryChangedRaw += OnInventoryChanged;
        ExternalCategoryManager.RegisterSource(this);
        OnDataChanged?.Invoke();
        Services.Logger.Information("[KeyItemSource] Enabled");
    }

    public void Disable()
    {
        if (!_isEnabled) return;
        _isEnabled = false;
        Services.GameInventory.InventoryChangedRaw -= OnInventoryChanged;
        ExternalCategoryManager.UnregisterSource(SourceName);
        Services.Logger.Information("[KeyItemSource] Disabled");
    }

    private void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        foreach (var ev in events)
        {
            if (ev.Item.ContainerType == (Dalamud.Game.Inventory.GameInventoryType)InventoryType.KeyItems)
            {
                _version++;
                OnDataChanged?.Invoke();
                return;
            }
        }
    }

    public IReadOnlyDictionary<uint, ExternalCategoryAssignment>? GetCategoryAssignments()
    {
        if (!_isEnabled) return null;

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager is null) return null;

        var container = inventoryManager->GetInventoryContainer(InventoryType.KeyItems);
        if (container is null) return null;

        var assignment = new ExternalCategoryAssignment(
            CategoryKey: KeyItemCategoryKey,
            CategoryName: "Key Items",
            CategoryDescription: "Quest and event-tied key items",
            CategoryColor: new Vector4(1.0f, 0.85f, 0.4f, 1.0f),
            ItemOverlayColor: null,
            SubPriority: 20
        );

        var result = new Dictionary<uint, ExternalCategoryAssignment>(container->Size);
        for (int i = 0; i < container->Size; i++)
        {
            uint id = container->Items[i].ItemId;
            if (id == 0) continue;
            result[id] = assignment;
        }
        return result;
    }

    public IReadOnlyDictionary<uint, ItemDecoration>? GetItemDecorations() => null;
    public IReadOnlyList<ContextMenuEntry>? GetContextMenuEntries(uint itemId) => null;
    public IReadOnlyDictionary<uint, string[]>? GetSearchTags() => null;
    public IReadOnlyList<ItemRelationship>? GetItemRelationships(uint itemId) => null;

    public void Dispose() => Disable();
}
