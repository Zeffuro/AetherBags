using AetherBags.Configuration;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AetherBags.Inventory;

internal static class UserCategoryMatcher
{
    public static bool Matches(ItemInfo item, UserCategoryDefinition userCategory)
    {
        var rules = userCategory.Rules;

        if (rules.AllowedUiCategoryIds.Count > 0)
        {
            uint uiCategoryId = item.UiCategory.RowId;
            if (!rules.AllowedUiCategoryIds.Contains(uiCategoryId))
                return false;
        }

        if (rules.AllowedItemIds.Count > 0 && !rules.AllowedItemIds.Contains(item.Item.ItemId))
            return false;

        if (rules.AllowedRarities.Count > 0 && !rules.AllowedRarities.Contains(item.Rarity))
            return false;

        if (rules.ItemLevel.Enabled && !InRange(item.ItemLevel, rules.ItemLevel.Min, rules.ItemLevel.Max))
            return false;

        if (rules.VendorPrice.Enabled && !InRange(item.VendorPrice, rules.VendorPrice.Min, rules.VendorPrice.Max))
            return false;

        if (rules.AllowedItemNamePatterns.Count > 0)
        {
            bool any = false;
            for (int i = 0; i < rules.AllowedItemNamePatterns.Count; i++)
            {
                string pattern = rules.AllowedItemNamePatterns[i];
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                // Treat patterns as regex for now.
                try
                {
                    if (Regex.IsMatch(item.Name, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                    {
                        any = true;
                        break;
                    }
                }
                catch
                {
                    // Invalid regex: ignore it.
                }
            }

            if (!any)
                return false;
        }

        return true;
    }

    private static bool InRange<T>(T value, T min, T max) where T : struct, IComparable<T>
        => value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
}