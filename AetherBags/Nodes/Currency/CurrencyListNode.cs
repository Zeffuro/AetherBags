using System.Collections.Generic;
using AetherBags.Currency;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Currency;

public class CurrencyListNode : HorizontalListNode
{
    public List<CurrencyInfo>? CurrencyInfoList { get; set; }
}