using System;
using System.Numerics;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace AetherBags.Nodes.Input;

public class TextInputWithButtonNode : SimpleComponentNode {
    private readonly TextInputNode _textInputNode;
    private readonly CircleButtonNode _contextButton;

    public Action? OnButtonClicked {
        get => _contextButton.OnClick;
        set => _contextButton.OnClick = value;
    }

    public TextInputWithButtonNode() {
        _textInputNode = new TextInputNode {
            PlaceholderString = "Search . . .",
        };
        _textInputNode.AttachNode(this);

        _contextButton = new CircleButtonNode {
            Icon = ButtonIcon.Filter,
            Size = new Vector2(28f),
        };
        _contextButton.AttachNode(this);
    }

    public Vector3 HintAddColor {
        get => _contextButton.AddColor;
        set => _contextButton.AddColor = value;
    }

    public required Action<ReadOnlySeString>? OnInputReceived {
        get => _textInputNode.OnInputReceived;
        set => _textInputNode.OnInputReceived = value;
    }

    protected override void OnSizeChanged() {
        base.OnSizeChanged();

        _contextButton.Size = new Vector2(Height, Height);
        _contextButton.Position = new Vector2(Width - _contextButton.Width, 0.0f);

        _textInputNode.Size = new Vector2(Width - _contextButton.Width - 5.0f, Height);
        _textInputNode.Position = new Vector2(0.0f, 0.0f);
    }

    public ReadOnlySeString SearchString {
        get => _textInputNode.String;
        set => _textInputNode.String = value;
    }
}