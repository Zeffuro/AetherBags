using System.Numerics;
using AetherBags.Currency;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;

namespace AetherBags.Nodes;

public class CurrencyNode : SimpleComponentNode
{
    private IconImageNode iconImageNode;
    private TextNode countNode;

    public CurrencyNode()
    {
        iconImageNode = new IconImageNode
        {
            FitTexture = true
        };
        iconImageNode.AttachNode(this);


        countNode = new TextNode
        {
            TextFlags = TextFlags.Emboss,
            TextColor = ColorHelper.GetColor(8),
            TextOutlineColor = ColorHelper.GetColor(7),
            FontSize = 14,
        };
        countNode.AttachNode(this);
    }

    public required CurrencyInfo Currency {
        get;
        set {
            field = value;
            iconImageNode.IconId = value.IconId;
            countNode.String = value.Amount.ToString("N0");

            countNode.Size = new Vector2(120.0f, 28.0f);
            countNode.Origin = countNode.Size / 2.0f;
            countNode.Position = new Vector2(26.0f, 0.0f);
            iconImageNode.Size = new Vector2(24f);
        }
    }
}