using System.Globalization;
using System.Numerics;
using AetherBags.Currency;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Currency;

public class CurrencyNode : SimpleComponentNode
{
    private readonly IconImageNode _iconImageNode;
    private readonly TextNode _countNode;

    public CurrencyNode()
    {
        _iconImageNode = new IconImageNode
        {
            FitTexture = true,
            Size = new Vector2(24f)
        };
        _iconImageNode.AttachNode(this);

        _countNode = new TextNode
        {
            TextFlags = TextFlags.Emboss,
            TextColor = ColorHelper.GetColor(8),
            TextOutlineColor = ColorHelper.GetColor(7),
            AlignmentType = AlignmentType.Left,
            FontSize = 14,
            Size = new Vector2(120.0f, 28.0f)
        };
        _countNode.AttachNode(this);
    }

    public required CurrencyInfo Currency {
        get;
        set {
            field = value;
            _iconImageNode.IconId = value.IconId;
            _iconImageNode.Position = new Vector2(0f, 2f);

            _countNode.String = value.Amount.ToString("N0", CultureInfo.InvariantCulture);
            _countNode.Position = new Vector2(_iconImageNode.Bounds.Right + 2f, 0f);

            // Limit > Capped > Normal
            var config = System.Config.Currency;

            var isLimited = config.ColorWhenLimited && value.LimitReached;
            var isCapped  = config.ColorWhenCapped && value.IsCapped;

            _countNode.TextColor =
                isLimited ? config.LimitColor :
                isCapped  ? config.CappedColor :
                config.DefaultColor;
        }
    }
}