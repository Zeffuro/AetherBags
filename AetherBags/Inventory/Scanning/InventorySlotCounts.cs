using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using InteropGenerator.Runtime;

namespace AetherBags.Inventory.Scanning;

public static unsafe class InventorySlotCounts
{
    private static readonly Dictionary<(InventorySourceType Source, ulong Retainer), (int Total, int Empty, bool Live)> LastCounts = [];
    private static ulong _characterId;

    public static void Clear()
    {
        LastCounts.Clear();
        _characterId = 0;
    }

    public static bool TryGet(InventorySourceType source, out int total, out int empty)
    {
        total = empty = 0;
        if (!TryGetKey(source, out var key)) return false;

        if (InventoryScanner.TryGetSlotCounts(source, out total, out empty))
        {
            LastCounts[key] = (total, empty, true);
            return true;
        }

        if (!LastCounts.TryGetValue(key, out var counts)) return false;
        (total, empty) = (counts.Total, counts.Empty);
        return true;
    }

    public static void CaptureSaddleBagUpdate()
    {
        var player = PlayerState.Instance();
        CaptureRequestedUpdate(player != null && player->HasPremiumSaddlebag ? InventorySourceType.AllSaddleBags : InventorySourceType.SaddleBag);
    }

    public static void CaptureRequestedUpdate(InventorySourceType source)
    {
        if (!TryGetKey(source, out var key)) return;
        if (InventoryScanner.TryGetSlotCounts(source, out var total, out var empty))
        {
            LastCounts[key] = (total, empty, true);
            return;
        }
        if (LastCounts.TryGetValue(key, out var previous) && previous.Live) return;

        var text = Experimental.GetUsedSlotsText(source);
        if (TryParse(text, InventorySourceDefinitions.GetTotalSlots(source), out total, out empty))
            LastCounts[key] = (total, empty, false);
    }

    private static bool TryGetKey(InventorySourceType source, out (InventorySourceType Source, ulong Retainer) key)
    {
        key = (source, 0);
        var player = PlayerState.Instance();
        ulong currentCharacter = IClientState.Get().IsLoggedIn && player != null ? player->ContentId : 0;
        if (_characterId != currentCharacter)
        {
            Clear();
            _characterId = currentCharacter;
        }
        if (currentCharacter == 0) return false;
        if (source != InventorySourceType.Retainer) return true;

        var retainers = RetainerManager.Instance();
        var order = ItemOrderModule.Instance();
        if (retainers == null || !retainers->IsReady || order == null || order->ActiveRetainerId == 0
            || order->ActiveRetainerId != retainers->LastSelectedRetainerId) return false;
        key = (source, order->ActiveRetainerId);
        return true;
    }

    private static bool TryParse(byte* text, int expectedTotal, out int total, out int empty)
    {
        total = empty = 0;
        if (text == null) return false;

        var value = new CStringPointer(text).AsSpan();
        int separator = value.IndexOf((byte)'/');
        if (separator < 0
            || !int.TryParse(value[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out int used)
            || !int.TryParse(value[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int capacity)
            || capacity != expectedTotal || used > capacity) return false;

        total = capacity;
        empty = capacity - used;
        return true;
    }
}
