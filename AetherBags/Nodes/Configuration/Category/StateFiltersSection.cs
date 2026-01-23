using System;
using System.Collections.Generic;
using AetherBags.Configuration;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class StateFiltersSection(Func<UserCategoryDefinition> getCategoryDefinition)
    : ConfigurationSection(getCategoryDefinition)
{
    private readonly List<(StateFilterRowNode Node, Func<UserCategoryDefinition, StateFilter> GetFilter)> _filters = [];
    private bool _initialized;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        AddFilter("Untradable", def => def.Rules.Untradable);
        AddFilter("Unique", def => def.Rules.Unique);
        AddFilter("Collectable", def => def.Rules.Collectable);
        AddFilter("Dyeable", def => def.Rules.Dyeable);
        AddFilter("Repairable", def => def.Rules.Repairable);
        AddFilter("High Quality", def => def.Rules.HighQuality);
        AddFilter("Desynthesizable", def => def.Rules.Desynthesizable);
        AddFilter("Glamourable", def => def.Rules.Glamourable);
        AddFilter("Spiritbonded", def => def.Rules.FullySpiritbonded);

        RecalculateLayout();
    }

    private void AddFilter(string label, Func<UserCategoryDefinition, StateFilter> getFilter)
    {
        var node = new StateFilterRowNode(label, new StateFilter(), () => OnValueChanged?.Invoke());
        _filters.Add((node, getFilter));
        AddNode(node);
    }

    public override void Refresh()
    {
        EnsureInitialized();

        foreach (var (node, getFilter) in _filters)
        {
            node.SetState(getFilter(CategoryDefinition));
        }

        RecalculateLayout();
    }
}