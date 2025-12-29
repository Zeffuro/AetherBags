using AetherBags.Configuration;
using System;
using System.Text.RegularExpressions;

namespace AetherBags.Inventory;

internal static class UserCategoryMatcher
{
    public static bool Matches(ItemInfo item, UserCategoryDefinition userCategory)
    {
        var rules = userCategory.Rules;

        bool hasIdentificationFilters = rules.AllowedItemIds.Count > 0 || rules.AllowedItemNamePatterns.Count > 0;

        if (hasIdentificationFilters)
        {
            bool matchesAnyIdentification = false;

            if (rules.AllowedItemIds.Count > 0 && rules.AllowedItemIds.Contains(item.Item.ItemId))
            {
                matchesAnyIdentification = true;
            }

            if (!matchesAnyIdentification && rules.AllowedItemNamePatterns.Count > 0)
            {
                for (int i = 0; i < rules.AllowedItemNamePatterns.Count; i++)
                {
                    string pattern = rules.AllowedItemNamePatterns[i];
                    if (string.IsNullOrWhiteSpace(pattern))
                        continue;

                    try
                    {
                        if (Regex.IsMatch(item.Name, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                        {
                            matchesAnyIdentification = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Invalid regex:  ignore it.
                    }
                }
            }

            if (!matchesAnyIdentification)
                return false;
        }

        if (rules.AllowedUiCategoryIds.Count > 0)
        {
            uint uiCategoryId = item.UiCategory.RowId;
            if (!rules.AllowedUiCategoryIds.Contains(uiCategoryId))
                return false;
        }

        if (rules.AllowedRarities.Count > 0 && !rules.AllowedRarities.Contains(item.Rarity))
            return false;

        if (rules.Level.Enabled && !InRange(item.Level, rules.Level.Min, rules.Level.Max))
            return false;

        if (rules.ItemLevel.Enabled && !InRange(item.ItemLevel, rules.ItemLevel.Min, rules.ItemLevel.Max))
            return false;

        if (rules.VendorPrice.Enabled && !InRange(item.VendorPrice, rules.VendorPrice.Min, rules.VendorPrice.Max))
            return false;

        if (!MatchesToggle(rules.Untradable, item.IsUntradable)) return false;
        if (!MatchesToggle(rules.Unique, item.IsUnique)) return false;
        if (!MatchesToggle(rules.Collectable, item.IsCollectable)) return false;
        if (!MatchesToggle(rules.Dyeable, item.IsDyeable)) return false;
        if (!MatchesToggle(rules.Repairable, item.IsRepairable)) return false;
        if (!MatchesToggle(rules.HighQuality, item.IsHq)) return false;
        if (!MatchesToggle(rules.Desynthesizable, item.IsDesynthesizable)) return false;
        if (!MatchesToggle(rules.Glamourable, item.IsGlamourable)) return false;
        if (!MatchesToggle(rules.FullySpiritbonded, item.IsSpiritbonded)) return false;

        return true;
    }

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

    private static bool MatchesToggle(StateFilter filter, bool itemHasProperty)
        => filter.ToggleState switch
        {
            ToggleFilterState.Ignored => true,
            ToggleFilterState.Allow => itemHasProperty,
            ToggleFilterState.Disallow => !itemHasProperty,
            _ => true
        };
}