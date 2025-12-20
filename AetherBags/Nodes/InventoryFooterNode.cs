using System.Numerics;
using AetherBags.Currency;
using AetherBags.Inventory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes;

public sealed class InventoryFooterNode : SimpleComponentNode
{
    private readonly TextNode _slotAmountTextNode;
    private readonly CurrencyNode _currencyNode;

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

        _currencyNode = new CurrencyNode
        {
            Size = new Vector2(120, 28),
            Currency = InventoryState.GetCurrencyInfo(1)
        };
        _currencyNode.AttachNode(this);
    }

    public string SlotAmountText
    {
        get => _slotAmountTextNode.String;
        set => _slotAmountTextNode.String = value;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        _slotAmountTextNode.Position = new Vector2(Size.X - _slotAmountTextNode.Size.X - 10, 0);
        _currencyNode.Position = new Vector2(0, 0);
    }
}