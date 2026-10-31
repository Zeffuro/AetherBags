using System.Collections.Generic;
using System.Linq;
using AetherBags.Currency;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Currency;

public class CurrencyListNode : HorizontalListNode
{
    public void SyncCurrencies(IReadOnlyList<CurrencyInfo> currencies)
    {
        var existing = Nodes.OfType<CurrencyNode>().ToDictionary(node => node.Currency.ItemId);
        var order = new Dictionary<uint, int>();
        foreach (var currency in currencies)
        {
            if (order.ContainsKey(currency.ItemId)) continue;
            order.Add(currency.ItemId, order.Count);
            if (existing.Remove(currency.ItemId, out var node))
                node.Currency = currency;
            else
                AddNode(new CurrencyNode { Currency = currency });
        }

        RemoveNode(existing.Values);
        ReorderNodes((left, right) => order[((CurrencyNode)left).Currency.ItemId]
            .CompareTo(order[((CurrencyNode)right).Currency.ItemId]));
    }
}
