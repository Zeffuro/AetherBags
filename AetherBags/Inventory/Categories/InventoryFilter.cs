using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AetherBags.Helpers;
using AetherBags.Inventory.Items;

namespace AetherBags.Inventory.Categories;

public static class InventoryFilter
{
    public static IReadOnlyList<CategorizedInventory> FilterCategories(
        IReadOnlyList<CategorizedInventory> allCategories,
        Dictionary<uint, CategoryBucket> bucketsByKey,
        List<CategorizedInventory> filteredCategories,
        string filterString,
        bool invert = false)
    {
        if (string.IsNullOrEmpty(filterString))
            return allCategories;

        Regex? re = RegexCache.GetOrCreate(filterString);
        bool regexValid = re != null;

        filteredCategories.Clear();

        for (int i = 0; i < allCategories.Count; i++)
        {
            CategorizedInventory cat = allCategories[i];
            CategoryBucket bucket = bucketsByKey[cat.Key];

            var filtered = bucket.FilteredItems;
            filtered.Clear();

            var src = bucket.Items;
            for (int j = 0; j < src.Count; j++)
            {
                ItemInfo info = src[j];

                bool isMatch;
                if (regexValid)
                {
                    isMatch = info.IsRegexMatch(re!);
                }
                else
                {
                    isMatch = info.Name.Contains(filterString, StringComparison.OrdinalIgnoreCase) || info.DescriptionContains(filterString);
                }

                if (isMatch != invert)
                    filtered.Add(info);
            }

            if (filtered.Count != 0)
                filteredCategories.Add(new CategorizedInventory(bucket.Key, bucket.Category, filtered));
        }

        return filteredCategories;
    }
}