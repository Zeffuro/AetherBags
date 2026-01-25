using System;
using AetherBags.Configuration;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class RangeFiltersSection(Func<UserCategoryDefinition> getCategoryDefinition) : ConfigurationSection(getCategoryDefinition)
{
    private RangeFilterRow? _levelFilter;
    private RangeFilterRow? _itemLevelFilter;
    private RangeFilterRowUint? _vendorPriceFilter;

    private bool _initialized;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _levelFilter = new RangeFilterRow
        {
            Label = "Level",
            MinBound = 0,
            MaxBound = 200,
            OnFilterChanged = (enabled, min, max) =>
            {
                CategoryDefinition.Rules.Level.Enabled = enabled;
                CategoryDefinition.Rules.Level.Min = min;
                CategoryDefinition.Rules.Level.Max = max;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_levelFilter);

        _itemLevelFilter = new RangeFilterRow
        {
            Label = "Item Level",
            MinBound = 0,
            MaxBound = 2000,
            OnFilterChanged = (enabled, min, max) =>
            {
                CategoryDefinition.Rules.ItemLevel.Enabled = enabled;
                CategoryDefinition.Rules.ItemLevel.Min = min;
                CategoryDefinition.Rules.ItemLevel.Max = max;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_itemLevelFilter);

        _vendorPriceFilter = new RangeFilterRowUint
        {
            Label = "Vendor Price",
            MinBound = 0,
            MaxBound = 9_999_999,
            OnFilterChanged = (enabled, min, max) =>
            {
                CategoryDefinition.Rules.VendorPrice.Enabled = enabled;
                CategoryDefinition.Rules.VendorPrice.Min = min;
                CategoryDefinition.Rules.VendorPrice.Max = max;
                OnValueChanged?.Invoke();
            },
        };
        AddNode(_vendorPriceFilter);

        RecalculateLayout();
    }

    public override void Refresh()
    {
        EnsureInitialized();

        _levelFilter!.SetFilter(CategoryDefinition.Rules.Level);
        _itemLevelFilter!.SetFilter(CategoryDefinition.Rules.ItemLevel);
        _vendorPriceFilter!.SetFilter(CategoryDefinition.Rules.VendorPrice);

        RecalculateLayout();
    }
}