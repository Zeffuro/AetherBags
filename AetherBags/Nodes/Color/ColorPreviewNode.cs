using System.Drawing;
using System.Numerics;
using Dalamud.Interface;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Color;

public class ColorPreviewNode : ResNode
{
    private readonly ColorImageNode _colorBackground;
    private readonly AlphaImageNode _alphaLayer;
    private readonly ColorImageNode _colorForeground;

    public ColorPreviewNode()
    {
        base.Size = new Vector2(64, 64);

        _colorBackground = new ColorImageNode
        {
            IsVisible = true,
            Color = KnownColor.Black.Vector(),
            FitTexture = true,
        };
        _colorBackground.AttachNode(this);

        _alphaLayer = new AlphaImageNode
        {
            IsVisible = true,
            WrapMode = WrapMode.Stretch,
        };
        _alphaLayer.AttachNode(this);

        _colorForeground = new ColorImageNode
        {
            IsVisible = true,
            Color = KnownColor.White.Vector(),
            FitTexture = true,
        };
        _colorForeground.AttachNode(this);

        UpdateLayout();
    }

    public override Vector4 Color
    {
        get => _colorForeground.Color;
        set => _colorForeground.Color = value;
    }

    public override Vector2 Size
    {
        get => base.Size;
        set
        {
            base.Size = value;
            UpdateLayout();
        }
    }

    public ColorImageNode BackgroundNode => _colorBackground;
    public ColorImageNode ForegroundNode => _colorForeground;

    private void UpdateLayout()
    {
        const float backgroundPadding = 6f;
        const float alphaPadding = 8f;
        const float foregroundPadding = 8f;

        var bgSize = base.Size - new Vector2(backgroundPadding * 2f);
        var alphaSize = base.Size - new Vector2(alphaPadding * 2f);
        var fgSize = base.Size - new Vector2(foregroundPadding * 2f);

        _colorBackground.Size = bgSize;
        _colorBackground.Position = new Vector2(backgroundPadding, backgroundPadding);

        _alphaLayer.Size = alphaSize;
        _alphaLayer.Position = new Vector2(alphaPadding, alphaPadding);

        _colorForeground.Size = fgSize;
        _colorForeground.Position = new Vector2(foregroundPadding, foregroundPadding);
    }

}
