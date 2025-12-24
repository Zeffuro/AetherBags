using System.Collections.Generic;
using System.Numerics;
using AetherBags.Currency;
using AetherBags.Inventory;
using AetherBags.Nodes.Currency;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Inventory;

public sealed class InventoryFooterNode : SimpleComponentNode
{
    private readonly TextNode _slotAmountTextNode;
    private readonly CurrencyListNode _currencyListNode;

    public InventoryFooterNode()
    {
        _slotAmountTextNode = new TextNode
        {
            Position = new Vector2(Size.X - 10, 0),
            Size = new Vector2(82, 20),
            AlignmentType = AlignmentType.Right,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(32) // Could also be Color 65
        };
        _slotAmountTextNode.AttachNode(this);

        _currencyListNode = new CurrencyListNode
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(120, 28),
            IsVisible = System.Config.Currency.Enabled
        };
        _currencyListNode.AttachNode(this);

        RefreshCurrencies();
    }

    public void RefreshCurrencies()
    {
        _currencyListNode.IsVisible = System.Config.Currency.Enabled;

        IReadOnlyList<CurrencyInfo> currencyInfoList = InventoryState.GetCurrencyInfoList([1, 28, 0xFFFF_FFFE, 0xFFFF_FFFD]);
        _currencyListNode.SyncWithListDataByKey<CurrencyInfo, CurrencyNode, uint>(
            dataList: currencyInfoList,
            getKeyFromData: c => c.ItemId,
            getKeyFromNode: n => n.Currency.ItemId,
            updateNode: (node, data) =>
            {
                node.Currency = data;
            },
            createNodeMethod: data => new CurrencyNode
            {
                Size = new Vector2(120, 28),
                Currency = data
            });
    }

    public string SlotAmountText
    {
        get => _slotAmountTextNode.String;
        set => _slotAmountTextNode.String = value;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        _slotAmountTextNode.Position = new Vector2(Size.X - _slotAmountTextNode.Size.X - 10, 0);
    }
}