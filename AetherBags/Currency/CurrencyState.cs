using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherBags.Currency;

/// <summary>
/// Manages currency lookups, caching, and retrieval from the game.
/// </summary>
public static unsafe class CurrencyState
{
    private const uint CurrencyIdLimitedTomestone = 0xFFFF_FFFE;
    private const uint CurrencyIdNonLimitedTomestone = 0xFFFF_FFFD;

    private static readonly Dictionary<uint, CurrencyItem> CurrencyItemByCurrencyIdCache = new(capacity:  32);
    private static readonly Dictionary<uint, CurrencyStaticInfo> CurrencyStaticByItemIdCache = new(capacity: 64);

    private static uint? _cachedLimitedTomestoneItemId;
    private static uint? _cachedNonLimitedTomestoneItemId;

    public static void InvalidateCaches()
    {
        CurrencyItemByCurrencyIdCache.Clear();
        CurrencyStaticByItemIdCache.Clear();
        _cachedLimitedTomestoneItemId = null;
        _cachedNonLimitedTomestoneItemId = null;
    }

    public static IReadOnlyList<CurrencyInfo> GetCurrencyInfoList(uint[] currencyIds)
    {
        if (currencyIds.Length == 0)
            return Array.Empty<CurrencyInfo>();

        InventoryManager* inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return Array.Empty<CurrencyInfo>();

        List<CurrencyInfo> currencyInfoList = new List<CurrencyInfo>(currencyIds.Length);

        for (int i = 0; i < currencyIds.Length; i++)
        {
            CurrencyItem currencyItem = ResolveCurrencyItemIdCached(currencyIds[i]);
            if (currencyItem.ItemId == 0)
                continue;

            CurrencyStaticInfo staticInfo = GetCurrencyStaticInfoCached(currencyItem.ItemId);

            uint amount = (uint)inventoryManager->GetInventoryItemCount(currencyItem.ItemId);

            bool isCapped = false;
            if (currencyItem.IsLimited)
            {
                int weeklyLimit = InventoryManager.GetLimitedTomestoneWeeklyLimit();
                int weeklyAcquired = inventoryManager->GetWeeklyAcquiredTomestoneCount();
                isCapped = weeklyAcquired >= weeklyLimit;
            }

            currencyInfoList.Add(new CurrencyInfo
            {
                Amount = amount,
                MaxAmount = staticInfo.MaxAmount,
                ItemId = staticInfo.ItemId,
                IconId = staticInfo.IconId,
                LimitReached = amount >= staticInfo.MaxAmount,
                IsCapped = isCapped
            });
        }

        return currencyInfoList;
    }

    private static uint?  GetLimitedTomestoneItemIdCached()
    {
        if (_cachedLimitedTomestoneItemId.HasValue)
            return _cachedLimitedTomestoneItemId.Value;

        uint?  itemId = Services.DataManager.GetExcelSheet<TomestonesItem>()
            .FirstOrDefault(t => t.Tomestones.RowId == 3)
            .Item.RowId;

        _cachedLimitedTomestoneItemId = itemId;
        return itemId;
    }

    private static uint?  GetNonLimitedTomestoneItemIdCached()
    {
        if (_cachedNonLimitedTomestoneItemId.HasValue)
            return _cachedNonLimitedTomestoneItemId.Value;

        uint? itemId = Services.DataManager.GetExcelSheet<TomestonesItem>()
            .FirstOrDefault(t => t.Tomestones.RowId == 2)
            .Item.RowId;

        _cachedNonLimitedTomestoneItemId = itemId;
        return itemId;
    }

    private static CurrencyItem ResolveCurrencyItemIdCached(uint currencyId)
    {
        if (CurrencyItemByCurrencyIdCache.TryGetValue(currencyId, out var cached))
            return cached;

        uint itemId = currencyId;
        bool isLimited = false;

        if (currencyId == CurrencyIdLimitedTomestone)
        {
            itemId = GetLimitedTomestoneItemIdCached() ?? 0;
            isLimited = true;
        }
        else if (currencyId == CurrencyIdNonLimitedTomestone)
        {
            itemId = GetNonLimitedTomestoneItemIdCached() ?? 0;
        }

        var resolved = new CurrencyItem(itemId, isLimited);
        CurrencyItemByCurrencyIdCache[currencyId] = resolved;
        return resolved;
    }

    private static CurrencyStaticInfo GetCurrencyStaticInfoCached(uint itemId)
    {
        if (CurrencyStaticByItemIdCache.TryGetValue(itemId, out CurrencyStaticInfo cached))
            return cached;

        var item = Services.DataManager.GetExcelSheet<Item>().GetRow(itemId);

        var info = new CurrencyStaticInfo
        {
            ItemId = itemId,
            IconId = item.Icon,
            MaxAmount = item.StackSize,
        };

        CurrencyStaticByItemIdCache[itemId] = info;
        return info;
    }

    private struct CurrencyStaticInfo
    {
        public uint ItemId;
        public uint IconId;
        public uint MaxAmount;
    }

    private record CurrencyItem(uint ItemId, bool IsLimited);
}