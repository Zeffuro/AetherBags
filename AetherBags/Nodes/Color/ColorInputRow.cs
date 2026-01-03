using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Addons;

namespace AetherBags.Nodes.Color;

public class ColorInputRow : HorizontalListNode
{
    private ColorPickerAddon? _colorPickerAddon;
    private readonly LabelTextNode _labelTextNode;
    private readonly ColorPreviewButtonNode _colorPreview;

    public ColorInputRow()
    {
        InitializeColorPicker();

        var initialColor = CurrentColor;

        _colorPreview = new ColorPreviewButtonNode
        {
            Size = new Vector2(28),
            Color = CurrentColor,
            OnClick = () =>
            {
                _colorPickerAddon?.InitialColor = CurrentColor;
                _colorPickerAddon?.DefaultColor = DefaultColor;
                _colorPickerAddon?.Toggle();
                _colorPickerAddon?.OnColorConfirmed = color =>
                {
                    CurrentColor = color;
                    _colorPreview?.Color = color;
                    initialColor = color;
                    OnColorConfirmed?.Invoke(color);
                };
                _colorPickerAddon?.OnColorPreviewed = color =>
                {
                    _colorPreview?.Color = color;
                    OnColorChange?.Invoke(color);
                };
                _colorPickerAddon?.OnColorCancelled = () => OnColorCanceled?.Invoke(initialColor);
            }
        };
        _colorPreview.AttachNode(this);

        _labelTextNode = new LabelTextNode
        {
            TextFlags = TextFlags.AutoAdjustNodeSize,
            Position = new Vector2(28, 0),
            Height = 24,
            String = Label ?? string.Empty,
        };
        _labelTextNode.AttachNode(this);
    }

    private void InitializeColorPicker() {
        if (_colorPickerAddon is not null) return;

        _colorPickerAddon = new ColorPickerAddon {
            InternalName = "ColorPicker_AetherBags",
            Title = "Pick a color",
        };
    }

    protected override void Dispose(bool disposing, bool isNativeDestructor) {
        base.Dispose();

        _colorPickerAddon?.Dispose();
        _colorPickerAddon = null;
    }

    public required string Label
    {
        get;
        set
        {
            field = value;
            _labelTextNode.String = value;
        }
    }

    public required Vector4 CurrentColor
    {
        get;
        set
        {
            field = value;
            _colorPreview.Color = value;
        }
    }

    public required Vector4 DefaultColor { get; set; }
    public Action<Vector4>? OnColorConfirmed { get; set; }
    public Action<Vector4>? OnColorCanceled { get; set; }
    public Action<Vector4>? OnColorChange { get; set; }
    public Action<Vector4>? OnColorPreviewed { get; set; }
}