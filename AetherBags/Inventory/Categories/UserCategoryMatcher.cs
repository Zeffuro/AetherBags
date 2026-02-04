using System;
using System.Runtime.CompilerServices;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory.Items;

namespace AetherBags.Inventory.Categories;

internal static class UserCategoryMatcher
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Matches(ItemInfo item, UserCategoryDefinition userCategory)
    {
        var rules = userCategory.Rules;

        if (!MatchesToggle(rules.Untradable, item.IsUntradable)) return false;
        if (!MatchesToggle(rules.Unique, item.IsUnique)) return false;
        if (!MatchesToggle(rules.Collectable, item.IsCollectable)) return false;
        if (!MatchesToggle(rules.Dyeable, item.IsDyeable)) return false;
        if (!MatchesToggle(rules.HighQuality, item.IsHq)) return false;
        if (!MatchesToggle(rules.Repairable, item.IsRepairable)) return false;
        if (!MatchesToggle(rules.Desynthesizable, item.IsDesynthesizable)) return false;
        if (!MatchesToggle(rules.Glamourable, item.IsGlamourable)) return false;
        if (!MatchesToggle(rules.FullySpiritbonded, item.IsSpiritbonded)) return false;

        if (rules.Level.Enabled && !InRange(item.Level, rules.Level.Min, rules.Level.Max))
            return false;

        if (rules.ItemLevel.Enabled && !InRange(item.ItemLevel, rules.ItemLevel.Min, rules.ItemLevel.Max))
            return false;

        if (rules.VendorPrice.Enabled && !InRange(item.VendorPrice, rules.VendorPrice.Min, rules.VendorPrice.Max))
            return false;

        if (rules.AllowedRarities.Count > 0 && !rules.AllowedRarities.Contains(item.Rarity))
            return false;

        if (rules.AllowedUiCategoryIds.Count > 0 && !rules.AllowedUiCategoryIds.Contains(item.UiCategory.RowId))
            return false;

        bool hasIdentificationFilters = rules.AllowedItemIds.Count > 0 || rules.AllowedItemNamePatterns.Count > 0;

        if (hasIdentificationFilters)
        {
            if (rules.AllowedItemIds.Count > 0 && rules.AllowedItemIds.Contains(item.Item.ItemId))
                return true;

            if (rules.AllowedItemNamePatterns.Count > 0)
            {
                for (int i = 0; i < rules.AllowedItemNamePatterns.Count; i++)
                {
                    string pattern = rules.AllowedItemNamePatterns[i];
                    if (string.IsNullOrWhiteSpace(pattern))
                        continue;

                    var regex = RegexCache.GetOrCreate(pattern);
                    if (regex != null && regex.IsMatch(item.Name))
                        return true;
                }
            }

            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InRange<T>(T value, T min, T max) where T : struct, IComparable<T>
        => value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;

    public static bool IsCatchAll(UserCategoryDefinition userCategory)
    {
        var rules = userCategory.Rules;

        if (rules.AllowedItemIds.Count > 0)
            return false;
        if (rules.AllowedItemNamePatterns.Count > 0)
            return false;
        if (rules.AllowedUiCategoryIds.Count > 0)
            return false;
        if (rules.AllowedRarities.Count > 0)
            return false;

        if (rules.Level.Enabled)
            return false;
        if (rules.ItemLevel.Enabled)
            return false;
        if (rules.VendorPrice.Enabled)
            return false;

        if (rules.Untradable.ToggleState != ToggleFilterState.Ignored)
            return false;
        if (rules.Unique.ToggleState != ToggleFilterState.Ignored)
            return false;
        if (rules.Collectable.ToggleState != ToggleFilterState.Ignored)
            return false;
        if (rules.Dyeable.ToggleState != ToggleFilterState.Ignored)
            return false;
        if (rules.Repairable.ToggleState != ToggleFilterState.Ignored)
            return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesToggle(StateFilter filter, bool itemHasProperty)
    {
        var state = filter.ToggleState;
        if (state == ToggleFilterState.Ignored) return true;
        if (state == ToggleFilterState.Allow) return itemHasProperty;
        if (state == ToggleFilterState.Disallow) return !itemHasProperty;
        return true;
    }
}